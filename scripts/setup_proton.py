#!/usr/bin/env python3
"""Experimental, non-overwriting installer for the Windows mod under Proton."""

import argparse
import contextlib
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import re
import shutil
import stat
import sys
import tempfile
import time
import urllib.parse
import urllib.request
import zipfile


STATE = ".big-beautiful-walk-proton"
CONFIGS = {"OptiScaler.ini", "BepInEx/config/local.bigwalk.native-dlss.cfg"}
ROOT_FILES = {"winhttp.dll", "doorstop_config.ini", ".doorstop_version", "nvngx_dlss.dll", "version.dll", "OptiScaler.ini"}
PREFIXES = ("BepInEx/core/", "dotnet/", "BepInEx/plugins/BigWalkDLSS/", "BepInEx/BigWalkDLSS/notices/", "OptiScaler/")


class SetupError(Exception):
    pass


def relative(value):
    if not isinstance(value, str) or not value or "\\" in value or ":" in value:
        raise SetupError("Invalid package path: " + str(value))
    path = PurePosixPath(value)
    if path.is_absolute() or any(p in ("", ".", "..") for p in value.split("/")):
        raise SetupError("Unsafe package path: " + value)
    return path


def safe_path(root, value):
    """Reject links and case aliases below the user-selected canonical root."""
    path = root
    for component in relative(value).parts:
        if path.is_dir():
            aliases = [p.name for p in path.iterdir()
                       if p.name.casefold() == component.casefold() and p.name != component]
            if aliases:
                raise SetupError("Case-conflicting path: " + str(path / component))
        path = path / component
        if path.is_symlink():
            raise SetupError("Symbolic link below selected directory: " + str(path))
        if path.exists() and not (path.is_file() or path.is_dir()):
            raise SetupError("Unsupported file type: " + str(path))
    return path


def digest(path):
    if not path.exists():
        return None
    if not path.is_file():
        raise SetupError("Expected a regular file: " + str(path))
    result = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            result.update(chunk)
    return result.hexdigest()


def sync_directory(path):
    fd = os.open(path, os.O_RDONLY | os.O_DIRECTORY)
    try:
        os.fsync(fd)
    finally:
        os.close(fd)


def save_json(path, value):
    temporary = path.with_name(path.name + ".tmp")
    if temporary.is_symlink():
        raise SetupError("Linked installer state: " + str(temporary))
    with temporary.open("w", encoding="utf-8") as stream:
        json.dump(value, stream, indent=2)
        stream.write("\n")
        stream.flush()
        os.fsync(stream.fileno())
    os.replace(temporary, path)
    sync_directory(path.parent)


def assert_closed(proc=Path("/proc")):
    # Wine process names/cmdlines vary. Refuse any visible Big Walk instance rather
    # than guessing that a Windows Z: or library alias identifies another copy.
    try:
        processes = list(proc.iterdir())
    except OSError as error:
        raise SetupError("Cannot inspect running processes; close Big Walk and retry.") from error
    for process in processes:
        if not process.name.isdigit():
            continue
        try:
            args = (process / "cmdline").read_bytes().split(b"\0")
            comm = (process / "comm").read_text().strip().casefold()
        except FileNotFoundError:
            continue  # Process exited between enumeration and inspection.
        except PermissionError as error:
            raise SetupError("Cannot inspect a process; close Big Walk and retry.") from error
        names = [arg.replace(b"\\", b"/").rsplit(b"/", 1)[-1].lower() for arg in args]
        if b"big walk.exe" in names or comm in ("big walk", "big walk.exe"):
            raise SetupError("Close Big Walk before changing installed files.")


@contextlib.contextmanager
def locked_game(game):
    import fcntl
    game = game.expanduser().resolve(strict=True)
    if not game.is_dir():
        raise SetupError("Select the folder containing Big Walk.exe.")
    for name in ("Big Walk.exe", "GameAssembly.dll", "Big Walk_Data"):
        item = safe_path(game, name)
        if not item.exists():
            raise SetupError("Not a Big Walk game folder: missing " + name)
    # Do not mix recovery ownership with the Windows installer.
    if any(safe_path(game, ".big-beautiful-walk/" + name).exists()
           for name in ("manifest.json", "journal.json")):
        raise SetupError("Windows installer state exists. Use its Uninstall before switching installers.")
    state = safe_path(game, STATE)
    state.mkdir(exist_ok=True)
    sync_directory(game)
    lock = safe_path(state, "lock")
    with lock.open("a+b") as stream:
        try:
            fcntl.flock(stream, fcntl.LOCK_EX | fcntl.LOCK_NB)
        except BlockingIOError as error:
            raise SetupError("Another Proton installer is using this game folder.") from error
        yield game, state


def check_hash(value):
    if not isinstance(value, str) or not re.fullmatch(r"[0-9a-fA-F]{64}", value):
        raise SetupError("Invalid SHA-256 in package or installer state.")
    return value.lower()


def installation_target(value):
    target = str(relative(value))
    if target not in ROOT_FILES | CONFIGS and not target.startswith(PREFIXES):
        raise SetupError("Not an installer-owned target: " + target)
    return target


def read_manifest(state):
    path = safe_path(state, "manifest.json")
    if not path.exists():
        return None
    data = json.loads(path.read_text(encoding="utf-8"))
    if data.get("schema") != 1 or data.get("gpu") not in ("nvidia", "amd", "intel"):
        raise SetupError("Unsupported Proton installer manifest.")
    seen = set()
    for entry in data["files"]:
        target = installation_target(entry["target"])
        if target.casefold() in seen or target.casefold().startswith(STATE.casefold() + "/"):
            raise SetupError("Duplicate or reserved manifest target.")
        seen.add(target.casefold())
        check_hash(entry["sha256"])
        if not isinstance(entry["owned"], bool):
            raise SetupError("Invalid manifest ownership.")
        identity = entry.get("identity")
        if entry["owned"] and (not isinstance(identity, list) or len(identity) != 2 or
                               any(type(v) is not int or v < 0 for v in identity)):
            raise SetupError("Invalid installed-file identity.")
    if data.get("status") not in ("installing", "installed", "removing"):
        raise SetupError("Invalid manifest status.")
    return data


def verify_package(package):
    checksums = safe_path(package, "SHA256SUMS.txt")
    if not checksums.is_file():
        raise SetupError("Extract the complete release ZIP; SHA256SUMS.txt is missing.")
    seen = {}
    for line in checksums.read_text(encoding="utf-8-sig").splitlines():
        expected, name = line.split("  ", 1)
        if name.casefold() in seen:
            raise SetupError("Duplicate package checksum: " + name)
        seen[name.casefold()] = check_hash(expected)
        if digest(safe_path(package, name)) != seen[name.casefold()]:
            raise SetupError("Package checksum mismatch: " + name)
    return seen


class HttpsOnly(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, request, fp, code, message, headers, newurl):
        validate_url(newurl)
        return super().redirect_request(request, fp, code, message, headers, newurl)


def validate_url(url):
    parsed = urllib.parse.urlsplit(url)
    if parsed.scheme != "https" or not parsed.hostname or parsed.username or parsed.password:
        raise SetupError("Dependencies must use HTTPS without embedded credentials.")


def download(dependency, destination, offline):
    name = dependency["Id"] + "." + dependency["Archive"]
    if offline:
        shutil.copyfile(safe_path(offline, name), destination)
    else:
        validate_url(dependency["Url"])
        opener = urllib.request.build_opener(HttpsOnly())
        request = urllib.request.Request(dependency["Url"], headers={"User-Agent": "BigBeautifulWalk-Proton-Preview"})
        started = time.monotonic()
        total = 0
        with opener.open(request, timeout=60) as response, destination.open("xb") as target:
            while True:
                chunk = response.read(1024 * 1024)
                if not chunk:
                    break
                total += len(chunk)
                if total > 1024 * 1024 * 1024 or time.monotonic() - started > 1800:
                    raise SetupError("Dependency download exceeded the size/time limit.")
                target.write(chunk)
    if digest(destination) != check_hash(dependency["SHA256"]):
        raise SetupError("Dependency SHA-256 mismatch: " + dependency["Id"])


def prepare(package, stage, gpu, offline):
    checksums = verify_package(package)
    items = {}

    def add(source, target):
        target = installation_target(target)
        if target.casefold() in items or target.casefold().startswith(STATE.casefold() + "/"):
            raise SetupError("Duplicate or reserved installation target: " + target)
        frozen = stage / str(len(items))
        shutil.copyfile(source, frozen)
        with frozen.open("rb") as stream:
            os.fsync(stream.fileno())
        items[target.casefold()] = {"target": target, "sha256": digest(frozen), "source": frozen}

    def bundled(name, target):
        if name.casefold() not in checksums:
            raise SetupError("File absent from release checksums: " + name)
        add(safe_path(package, name), target)
        if items[target.casefold()]["sha256"] != checksums[name.casefold()]:
            raise SetupError("Package bytes changed during preparation: " + name)

    plugins = ["BigWalkDLSS.dll", "BigWalkBridgeNative.dll"]
    if gpu == "nvidia":
        plugins.append("BigWalkDLSSNative.dll")
    for name in plugins:
        bundled("payload/BigWalkDLSS/" + name, "BepInEx/plugins/BigWalkDLSS/" + name)
    for name in ("ui-landscape.png", "ui-border.png"):
        bundled("packaging/" + name, "BepInEx/plugins/BigWalkDLSS/" + name)
    for name in ("LICENSE", "NOTICE", "THIRD_PARTY_NOTICES.md", "docs/LICENSING.md",
                 "docs/RELEASE-CHECKLIST.md", "docs/UPSCALERS.md"):
        bundled(name, "BepInEx/BigWalkDLSS/notices/" + name)
    for name in sorted(safe_path(package, "docs/licenses").rglob("*")):
        if name.is_symlink():
            raise SetupError("Linked licence file in release.")
        if name.is_file():
            path = name.relative_to(package).as_posix()
            bundled(path, "BepInEx/BigWalkDLSS/notices/" + path)
    bundled("payload/defaults/local.bigwalk.native-dlss.cfg", "BepInEx/config/local.bigwalk.native-dlss.cfg")
    if gpu != "nvidia":
        bundled("payload/defaults/OptiScaler.bridge.ini", "OptiScaler.ini")
    lock_name = "packaging/dependencies.lock.json"
    if lock_name.casefold() not in checksums:
        raise SetupError("Dependency lock is absent from release checksums.")
    lock = json.loads(safe_path(package, lock_name).read_text(encoding="utf-8-sig"))
    if lock.get("SchemaVersion") != 1:
        raise SetupError("Unsupported dependency lock.")
    wanted = {"bepinex", "nvngx_dlss"} if gpu == "nvidia" else {"bepinex", "optiscaler_fg", "xess"}
    selected = [d for d in lock["Dependencies"] if d["Id"] in wanted]
    if len(selected) != len(wanted) or {d["Id"] for d in selected} != wanted:
        raise SetupError("Required Proton dependencies are missing or duplicated.")
    for dependency in selected:
        if dependency["Archive"] not in ("zip", "file"):
            raise SetupError("Unsupported dependency archive.")
        archive = stage / (dependency["Id"] + "." + dependency["Archive"])
        print("Verifying " + dependency["Id"], flush=True)
        download(dependency, archive, offline)
        with contextlib.ExitStack() as stack:
            zipped = stack.enter_context(zipfile.ZipFile(archive)) if dependency["Archive"] == "zip" else None
            for mapping in dependency["Files"]:
                # Only SR dependencies: no NR forwarder, FG or latency DLLs.
                if dependency["Id"] == "optiscaler_fg" and mapping["Target"] != "version.dll":
                    continue
                if dependency["Id"] == "xess" and mapping["Target"] in ("OptiScaler/libxess_fg.dll", "OptiScaler/libxell.dll"):
                    continue
                source = archive
                if zipped:
                    source_name = str(relative(mapping["Source"]))
                    matches = [i for i in zipped.infolist() if i.filename == source_name]
                    if len(matches) != 1 or matches[0].is_dir() or stat.S_ISLNK(matches[0].external_attr >> 16):
                        raise SetupError("Missing, duplicate or linked dependency member: " + source_name)
                    if matches[0].file_size > 512 * 1024 * 1024:
                        raise SetupError("Dependency member exceeds the size limit: " + source_name)
                    source = stage / "member"
                    with zipped.open(matches[0]) as content, source.open("wb") as target:
                        shutil.copyfileobj(content, target, 1024 * 1024)
                add(source, mapping["Target"])
    return list(items.values())


def launch_options(gpu):
    override = "winhttp=n,b" if gpu == "nvidia" else "winhttp,version=n,b"
    return 'WINEDLLOVERRIDES="' + override + '" %command%'


def remove_owned(game, state, manifest):
    # Preflight everything before deleting any file. Never remove changed binaries.
    remove = []
    plugin_root = safe_path(game, "BepInEx/plugins")
    patcher_root = safe_path(game, "BepInEx/patchers")
    ours = {"BigWalkDLSS.dll", "BigWalkDLSSNative.dll", "BigWalkBridgeNative.dll"}
    shared_loader = False
    for root in (plugin_root, patcher_root):
        if root.exists():
            for path in root.rglob("*"):
                if path.is_symlink() or (path.suffix.casefold() == ".dll" and
                                        not (path.parent == plugin_root / "BigWalkDLSS" and path.name in ours)):
                    shared_loader = True
    for entry in manifest["files"]:
        if not entry["owned"]:
            continue
        if shared_loader and (entry["target"].startswith(("BepInEx/core/", "dotnet/")) or
                              entry["target"] in ("winhttp.dll", "doorstop_config.ini", ".doorstop_version")):
            print("Keeping loader needed by other plugins: " + entry["target"])
            continue
        path = safe_path(game, entry["target"])
        actual = digest(path)
        if actual is None:
            continue
        info = path.stat()
        if actual != entry["sha256"] or [info.st_dev, info.st_ino] != entry["identity"]:
            if entry["target"] in CONFIGS:
                print("Keeping edited settings: " + entry["target"])
                continue
            raise SetupError("Installed file changed; preserved for inspection: " + entry["target"])
        remove.append(path)
    manifest["status"] = "removing"
    save_json(safe_path(state, "manifest.json"), manifest)
    for path in remove:
        # Inspect again immediately before mutation; the game must remain closed.
        assert_closed()
        entry = next(e for e in manifest["files"] if game / e["target"] == path)
        current = safe_path(game, entry["target"])
        info = current.stat()
        if digest(current) != entry["sha256"] or [info.st_dev, info.st_ino] != entry["identity"]:
            raise SetupError("File changed during removal: " + str(path))
        path.unlink()
        sync_directory(path.parent)
    safe_path(state, "manifest.json").unlink()
    sync_directory(state)


def run(args, package, process_check=assert_closed):
    with locked_game(args.game) as (game, state):
        manifest = read_manifest(state)
        if args.action == "diagnose":
            if not manifest:
                print("No Proton installation is recorded.")
                return
            problems = []
            for entry in manifest["files"]:
                actual = digest(safe_path(game, entry["target"]))
                if actual != entry["sha256"]:
                    problems.append(entry["target"])
            print("State: " + manifest["status"] + "; GPU path: " + manifest["gpu"])
            print("Steam launch options: " + launch_options(manifest["gpu"]))
            if problems:
                raise SetupError("Missing or changed files: " + ", ".join(problems))
            if manifest["status"] != "installed":
                raise SetupError("Interrupted operation; close Big Walk and run recover.")
            print("Recorded files match. Proton rendering has not been validated by this check.")
            return
        process_check()
        if args.action in ("uninstall", "recover"):
            if not manifest:
                print("No Proton installation to remove.")
                return
            remove_owned(game, state, manifest)
            print("Removed owned unchanged files; reused files and edited settings were preserved.")
            return
        if manifest:
            raise SetupError("An installation is already recorded. Diagnose it, or uninstall/recover before installing again.")
        # Proxy collisions can load before BepInEx and break the game even if not
        # included in this GPU path. Do not repurpose an existing injector/config.
        for name in ("dxgi.dll", "version.dll", "OptiScaler.ini", "nvngx_dlssnr.dll"):
            if safe_path(game, name).exists():
                raise SetupError("Existing injector/configuration must be handled by its installer: " + name)
        with tempfile.TemporaryDirectory(prefix="stage-", dir=state) as staging:
            items = prepare(package, Path(staging), args.gpu, args.dependencies)
            entries = []
            for item in items:
                actual = digest(safe_path(game, item["target"]))
                if actual is not None and actual != item["sha256"]:
                    raise SetupError("Existing file differs; no game files replaced: " + item["target"])
                info = item["source"].stat()
                entries.append({"target": item["target"], "sha256": item["sha256"], "owned": actual is None,
                                "identity": [info.st_dev, info.st_ino] if actual is None else None})
            manifest = {"schema": 1, "gpu": args.gpu, "status": "installing", "files": entries}
            process_check()
            save_json(safe_path(state, "manifest.json"), manifest)
            for item, entry in zip(items, entries):
                if not entry["owned"]:
                    continue
                process_check()
                target = safe_path(game, item["target"])
                parent = game
                for part in target.relative_to(game).parts[:-1]:
                    child = parent / part
                    child.mkdir(exist_ok=True)
                    sync_directory(parent)
                    parent = child
                # Same-filesystem hard link publishes a complete fsynced file and
                # refuses an existing destination. Never truncate another mod.
                os.link(item["source"], target)
                sync_directory(target.parent)
            for entry in entries:
                if digest(safe_path(game, entry["target"])) != entry["sha256"]:
                    raise SetupError("Installed/reused file changed during installation: " + entry["target"])
            manifest["status"] = "installed"
            save_json(safe_path(state, "manifest.json"), manifest)
        print("Experimental Proton installation complete. FG and NR are not installed/enabled.")
        print("Steam launch options: " + launch_options(args.gpu))
        print("Merge this DLL override with existing launch options; do not discard them.")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("action", choices=("install", "diagnose", "uninstall", "recover"))
    parser.add_argument("--game", type=Path, required=True, help="Folder containing the Windows Big Walk.exe")
    parser.add_argument("--gpu", choices=("nvidia", "amd", "intel"), help="GPU actually rendering Big Walk (required for install)")
    parser.add_argument("--dependencies", type=Path, help="Offline cache containing locked <id>.zip or <id>.file downloads")
    args = parser.parse_args()
    if sys.platform != "linux":
        parser.error("Run this installer on Linux; use Setup.exe on Windows.")
    if os.geteuid() == 0:
        parser.error("Run as your Steam user, without sudo.")
    if args.action == "install" and not args.gpu:
        parser.error("install requires --gpu nvidia, amd or intel")
    package = Path(__file__).resolve().parent.parent
    try:
        if args.dependencies:
            args.dependencies = args.dependencies.expanduser().resolve(strict=True)
        run(args, package)
    except (SetupError, OSError, ValueError, KeyError, zipfile.BadZipFile) as error:
        print("Setup stopped: " + str(error), file=sys.stderr)
        print("No existing files were overwritten. If a manifest remains, close Big Walk and run recover.", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
