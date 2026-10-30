"""Disposable Linux filesystem checks; never launch Wine, Unity or a GPU."""
import argparse
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch
import zipfile

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location("setup_proton", ROOT / "scripts/setup_proton.py")
setup = importlib.util.module_from_spec(spec)
spec.loader.exec_module(setup)


def put(root, name, data=b"fixture"):
    path = root / name
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(data)
    return path


def checksums(package):
    rows = [setup.digest(p) + "  " + p.relative_to(package).as_posix()
            for p in sorted(package.rglob("*")) if p.is_file() and p.name != "SHA256SUMS.txt"]
    put(package, "SHA256SUMS.txt", ("\n".join(rows) + "\n").encode())


@unittest.skipUnless(sys.platform == "linux", "Linux filesystem/proc/locking contract")
class ProtonSetupChecks(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="bbw-proton-test-")
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.game = self.root / "Library with spaces" / "Big Walk"
        self.game.mkdir(parents=True)
        put(self.game, "Big Walk.exe", b"game-executable")
        put(self.game, "GameAssembly.dll", b"game-assembly")
        (self.game / "Big Walk_Data").mkdir()
        self.package = self.root / "package"
        self.cache = self.root / "cache"
        self.cache.mkdir()
        for name in ("BigWalkDLSS.dll", "BigWalkDLSSNative.dll", "BigWalkBridgeNative.dll"):
            put(self.package, "payload/BigWalkDLSS/" + name, name.encode())
        for name in ("LICENSE", "NOTICE", "THIRD_PARTY_NOTICES.md", "docs/LICENSING.md",
                     "docs/RELEASE-CHECKLIST.md", "docs/UPSCALERS.md", "docs/licenses/vendor.txt",
                     "packaging/ui-landscape.png", "packaging/ui-border.png",
                     "payload/defaults/local.bigwalk.native-dlss.cfg"):
            put(self.package, name)
        put(self.package, "payload/defaults/OptiScaler.bridge.ini", b"[FrameGen]\nEnabled=false\n[DlssNr]\nEnabled=false\n")
        deps = []
        for identity, members in (
                ("bepinex", {"winhttp.dll": b"loader", "core.dll": b"core"}),
                ("optiscaler_fg", {"OptiScaler.dll": b"bridge", "nr-forwarder.dll": b"not-selected"}),
                ("xess", {"libxess.dll": b"xess", "libxess_fg.dll": b"not-selected", "LICENSE.txt": b"licence"})):
            archive = self.cache / (identity + ".zip")
            with zipfile.ZipFile(archive, "w") as zipped:
                for name, data in members.items():
                    zipped.writestr(name, data)
            targets = {"core.dll": "BepInEx/core/core.dll", "OptiScaler.dll": "version.dll",
                       "nr-forwarder.dll": "OptiScaler/nvngx.dll_dlssnr.dll",
                       "libxess.dll": "OptiScaler/libxess.dll", "libxess_fg.dll": "OptiScaler/libxess_fg.dll",
                       "LICENSE.txt": "OptiScaler/licenses/Intel-XeSS-LICENSE.txt"}
            deps.append({"Id": identity, "Archive": "zip", "SHA256": setup.digest(archive),
                         "Url": "https://example.invalid/" + identity,
                         "Files": [{"Source": name, "Target": targets.get(name, name)} for name in members]})
        dlss = put(self.cache, "nvngx_dlss.file", b"dlss")
        deps.append({"Id": "nvngx_dlss", "Archive": "file", "SHA256": setup.digest(dlss),
                     "Url": "https://example.invalid/dlss", "Files": [{"Source": "dlss", "Target": "nvngx_dlss.dll"}]})
        self.lock = {"SchemaVersion": 1, "Dependencies": deps}
        self.save_lock()

    def save_lock(self):
        put(self.package, "packaging/dependencies.lock.json", json.dumps(self.lock).encode())
        checksums(self.package)

    def run_setup(self, action="install", gpu="nvidia", check=lambda: None):
        args = argparse.Namespace(action=action, game=self.game, gpu=gpu, dependencies=self.cache)
        setup.run(args, self.package, check)

    def test_all_gpu_install_diagnose_remove(self):
        for gpu in ("nvidia", "amd", "intel"):
            with self.subTest(gpu=gpu):
                self.run_setup(gpu=gpu)
                self.run_setup("diagnose")
                self.assertEqual((self.game / "nvngx_dlss.dll").exists(), gpu == "nvidia")
                self.assertEqual((self.game / "version.dll").exists(), gpu != "nvidia")
                self.assertFalse((self.game / "OptiScaler/libxess_fg.dll").exists())
                self.assertFalse((self.game / "OptiScaler/nvngx.dll_dlssnr.dll").exists())
                self.run_setup("uninstall")
                self.assertFalse((self.game / "winhttp.dll").exists())
                self.assertEqual((self.game / "Big Walk.exe").read_bytes(), b"game-executable")

    def test_matching_loader_is_reused_not_removed(self):
        loader = put(self.game, "winhttp.dll", b"loader")
        self.run_setup()
        self.run_setup("uninstall")
        self.assertEqual(loader.read_bytes(), b"loader")

    def test_old_windows_backups_are_preserved_without_blocking_install(self):
        backup = put(self.game, ".big-beautiful-walk/backups/original.bin", b"old backup")
        self.run_setup()
        self.run_setup("uninstall")
        self.assertEqual(backup.read_bytes(), b"old backup")

    def test_conflicting_loader_is_not_changed(self):
        loader = put(self.game, "winhttp.dll", b"other-loader")
        with self.assertRaisesRegex(setup.SetupError, "Existing file differs"):
            self.run_setup()
        self.assertEqual(loader.read_bytes(), b"other-loader")
        self.assertFalse((self.game / "BepInEx/plugins/BigWalkDLSS/BigWalkDLSS.dll").exists())

    def test_edited_config_survives_uninstall(self):
        self.run_setup(gpu="amd")
        config = put(self.game, "OptiScaler.ini", b"user edits")
        self.run_setup("uninstall")
        self.assertEqual(config.read_bytes(), b"user edits")

    def test_other_plugins_retain_shared_loader(self):
        self.run_setup()
        put(self.game, "BepInEx/plugins/AnotherMod.dll", b"other plugin")
        self.run_setup("uninstall")
        self.assertEqual((self.game / "winhttp.dll").read_bytes(), b"loader")
        self.assertEqual((self.game / "BepInEx/core/core.dll").read_bytes(), b"core")
        self.assertFalse((self.game / "BepInEx/plugins/BigWalkDLSS/BigWalkDLSS.dll").exists())

    def test_changed_binary_blocks_removal_before_any_deletion(self):
        self.run_setup()
        put(self.game, "BepInEx/plugins/BigWalkDLSS/BigWalkDLSSNative.dll", b"changed")
        with self.assertRaisesRegex(setup.SetupError, "Installed file changed"):
            self.run_setup("uninstall")
        self.assertTrue((self.game / "BepInEx/plugins/BigWalkDLSS/BigWalkDLSS.dll").exists())
        self.assertTrue((self.game / "winhttp.dll").exists())

    def test_interrupt_install_then_recover(self):
        link = os.link
        count = 0

        def interrupt(source, target):
            nonlocal count
            count += 1
            if count == 4:
                raise OSError("fixture interruption")
            return link(source, target)

        with patch.object(setup.os, "link", side_effect=interrupt):
            with self.assertRaisesRegex(OSError, "fixture interruption"):
                self.run_setup()
        with self.assertRaisesRegex(setup.SetupError, "Interrupted operation|Missing or changed"):
            self.run_setup("diagnose")
        self.run_setup("recover")
        self.run_setup()
        self.run_setup("uninstall")

    def test_interrupt_remove_then_recover(self):
        self.run_setup()
        unlink = Path.unlink
        count = 0

        def interrupt(path, *args, **kwargs):
            nonlocal count
            count += 1
            if count == 3:
                raise OSError("fixture remove interruption")
            return unlink(path, *args, **kwargs)

        with patch.object(Path, "unlink", interrupt):
            with self.assertRaisesRegex(OSError, "fixture remove interruption"):
                self.run_setup("uninstall")
        self.run_setup("recover")
        self.assertFalse((self.game / "winhttp.dll").exists())

    def test_no_replace_if_destination_appears_after_preflight(self):
        save = setup.save_json

        def racing_write(path, data):
            save(path, data)
            if data["status"] == "installing":
                put(self.game, data["files"][0]["target"], b"racing-file")

        with patch.object(setup, "save_json", side_effect=racing_write):
            with self.assertRaises(FileExistsError):
                self.run_setup()
        self.assertEqual((self.game / "BepInEx/plugins/BigWalkDLSS/BigWalkDLSS.dll").read_bytes(), b"racing-file")

    def test_matching_file_from_other_writer_is_not_claimed_by_recovery(self):
        save = setup.save_json

        def racing_write(path, data):
            save(path, data)
            if data["status"] == "installing":
                target = data["files"][0]["target"]
                put(self.game, target, (self.package / "payload/BigWalkDLSS/BigWalkDLSS.dll").read_bytes())

        with patch.object(setup, "save_json", side_effect=racing_write):
            with self.assertRaises(FileExistsError):
                self.run_setup()
        with self.assertRaisesRegex(setup.SetupError, "Installed file changed"):
            self.run_setup("recover")
        self.assertTrue((self.game / "BepInEx/plugins/BigWalkDLSS/BigWalkDLSS.dll").exists())

    def test_live_process_prevents_install(self):
        def running():
            raise setup.SetupError("Close Big Walk")
        with self.assertRaisesRegex(setup.SetupError, "Close Big Walk"):
            self.run_setup(check=running)
        self.assertFalse((self.game / "winhttp.dll").exists())

    def test_proc_detection_and_permission_failure(self):
        proc = self.root / "proc"
        put(proc, "123/cmdline", b"Z:\\games\\Big Walk\\Big Walk.exe\0")
        put(proc, "123/comm", b"wine64")
        with self.assertRaisesRegex(setup.SetupError, "Close Big Walk"):
            setup.assert_closed(proc)
        with patch.object(Path, "read_bytes", side_effect=PermissionError()):
            with self.assertRaisesRegex(setup.SetupError, "Cannot inspect"):
                setup.assert_closed(proc)

    def test_refuse_duplicate_installer_lock(self):
        with setup.locked_game(self.game):
            with self.assertRaisesRegex(setup.SetupError, "Another Proton installer"):
                with setup.locked_game(self.game):
                    pass

    def test_symlink_does_not_write_outside_game(self):
        outside = self.root / "outside"
        outside.mkdir()
        (self.game / "BepInEx").symlink_to(outside, target_is_directory=True)
        with self.assertRaisesRegex(setup.SetupError, "Symbolic link"):
            self.run_setup()
        self.assertFalse(list(outside.iterdir()))

    def test_case_alias_rejected(self):
        put(self.game, "bepinex/unrelated", b"preserve")
        with self.assertRaisesRegex(setup.SetupError, "Case-conflicting"):
            self.run_setup()

    def test_windows_state_and_other_proxies_rejected(self):
        for name in (".big-beautiful-walk/manifest.json", "version.dll", "dxgi.dll", "OptiScaler.ini"):
            with self.subTest(name=name):
                file = put(self.game, name)
                with self.assertRaises(setup.SetupError):
                    self.run_setup()
                file.unlink()
                if name.startswith(".big-beautiful-walk/"):
                    file.parent.rmdir()

    def test_hash_rejection_before_game_files(self):
        put(self.cache, "bepinex.zip", b"tampered download")
        with self.assertRaisesRegex(setup.SetupError, "SHA-256 mismatch"):
            self.run_setup()
        self.assertFalse((self.game / "winhttp.dll").exists())

    def test_package_tamper_rejection(self):
        put(self.package, "payload/BigWalkDLSS/BigWalkDLSS.dll", b"tampered package")
        with self.assertRaisesRegex(setup.SetupError, "checksum mismatch"):
            self.run_setup()

    def test_archive_traversal_target_rejection(self):
        self.lock["Dependencies"][0]["Files"][0]["Target"] = "../outside.dll"
        self.save_lock()
        with self.assertRaisesRegex(setup.SetupError, "Unsafe package path"):
            self.run_setup()

    def test_manifest_cannot_remove_game_files(self):
        state = self.game / setup.STATE
        state.mkdir()
        data = {"schema": 1, "gpu": "nvidia", "status": "installed", "files": [
            {"target": "GameAssembly.dll", "owned": True, "sha256": setup.digest(self.game / "GameAssembly.dll")}]}
        setup.save_json(state / "manifest.json", data)
        with self.assertRaisesRegex(setup.SetupError, "Not an installer-owned"):
            self.run_setup("uninstall")
        self.assertEqual((self.game / "GameAssembly.dll").read_bytes(), b"game-assembly")

    def test_https_only(self):
        for url in ("http://example.org/a", "file:///tmp/a", "https://user:pass@example.org/a"):
            with self.assertRaises(setup.SetupError):
                setup.validate_url(url)
        setup.validate_url("https://example.org/a")
        request = setup.urllib.request.Request("https://example.org/a")
        with self.assertRaises(setup.SetupError):
            setup.HttpsOnly().redirect_request(request, None, 302, "", {}, "http://example.org/a")

    def test_real_cli_install_and_remove(self):
        if os.geteuid() == 0:
            result = subprocess.run([sys.executable, str(ROOT / "scripts/setup_proton.py"), "install",
                                     "--game", str(self.game), "--gpu", "nvidia"], capture_output=True)
            self.assertEqual(result.returncode, 2)
            self.assertIn(b"without sudo", result.stderr)
            return
        put(self.package, "scripts/setup_proton.py", (ROOT / "scripts/setup_proton.py").read_bytes())
        put(self.package, "Setup.sh", (ROOT / "Setup.sh").read_bytes())
        checksums(self.package)
        for action in ("install", "diagnose", "uninstall"):
            command = ["bash", str(self.package / "Setup.sh"), action, "--game", str(self.game)]
            if action == "install":
                command += ["--gpu", "nvidia", "--dependencies", str(self.cache)]
            result = subprocess.run(command, capture_output=True, text=True)
            self.assertEqual(result.returncode, 0, result.stderr + result.stdout)


if __name__ == "__main__":
    unittest.main()
