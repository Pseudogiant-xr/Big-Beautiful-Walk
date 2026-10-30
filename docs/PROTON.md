# Experimental Steam/Proton installation

This is an experimental installation path for the **Windows build of Big Walk running through Steam/Proton**. It is not a native Linux or Unity Vulkan build. No Big Beautiful Walk release has yet been rendered or played through Proton, so installing successfully does not establish that an upscaler works correctly.

The installer supports three explicit GPU choices:

| `--gpu` value | Initial upscaler path | Status |
| --- | --- | --- |
| `nvidia` | The mod's D3D11 DLSS backend | Intended for the first remote test on an RTX 4080; unverified |
| `amd` | OptiScaler bridge, with FSR 3.1 selected; XeSS is also available | Unverified |
| `intel` | OptiScaler bridge, with FSR 3.1 selected; XeSS is also available | Unverified |

Frame Generation and Neural Rendering remain off; their runtime dependencies are not installed by this preview. Neither has been validated through Proton, and this guide does not claim that either is impossible.

## Requirements

- Big Walk must already start successfully through Steam/Proton.
- Use a complete release ZIP containing `Setup.sh`. Earlier Windows-only ZIPs do not contain this installer.
- Python 3.9 or newer is required. The installer uses only Python's standard library.
- Run as your normal Steam user, without `sudo`, on a filesystem supporting hard links.
- Keep Proton's normal DXVK path enabled. Do not force the game to Unity Vulkan or set `PROTON_USE_WINED3D` for this mod.
- On NVIDIA, use a current proprietary driver. DXVK-NVAPI documents D3D11 DLSS forwarding through DXVK, but that general capability is not proof that this mod works. See the [DXVK-NVAPI requirements](https://github.com/jp7677/dxvk-nvapi#requirements).

The installer does not guess which GPU a hybrid system will use. Select the GPU that actually renders the game; choosing the high-performance GPU is a Steam, desktop and driver configuration decision.

## Install

Close Big Walk. From the extracted release directory, pass the exact folder containing `Big Walk.exe`:

```bash
bash Setup.sh install --game '/path/to/steamapps/common/Big Walk' --gpu nvidia
```

Use `--gpu amd` or `--gpu intel` on those GPUs. A custom Steam library or Flatpak Steam installation is supported by supplying its exact `--game` path; automatic library discovery is not required.

Setup downloads only the dependencies pinned in `packaging/dependencies.lock.json` and verifies their SHA-256 hashes before installing them. For an offline installation, place the locked archives in a directory and add:

```bash
--dependencies '/path/to/dependency-cache'
```

The installer is deliberately collision-safe:

- It does not overwrite a different pre-existing file.
- A byte-for-byte matching dependency may be reused, but is not claimed or removed later.
- No existing file is overwritten, so there are no original files to restore. Uninstall removes unchanged files it created, preserves edited settings, and keeps the shared BepInEx loader if other plugins need it.
- It refuses a game directory managed by the Windows installer; uninstall that copy with Windows Setup before switching installers.
- If an interrupted transaction is reported, run `recover` before another install or uninstall.

This preview does not update an existing installation in place. Uninstall it before installing another version; differing configurations and injectors need to be handled before reinstalling. Keep the extracted package for diagnosis and removal. Empty directories and installer state/staging directories may remain after removal; they are not active runtime files.

After installation, add the launch option printed by Setup to the game's Steam properties. It will contain one of these DLL override fragments:

```text
WINEDLLOVERRIDES="winhttp=n,b" %command%
WINEDLLOVERRIDES="winhttp,version=n,b" %command%
```

The first is for the NVIDIA core path. The second also loads the OptiScaler proxy used by AMD and Intel. Preserve existing environment variables, game arguments and wrappers; keep one `%command%` placeholder. Merge any existing `WINEDLLOVERRIDES` entries into the same variable rather than adding a second assignment. Do not add blanket `nvapi`, `nvngx` or `dxgi` overrides. BepInEx explains why its `winhttp.dll` proxy needs explicit forwarding under Proton in its [Proton/Wine guide](https://docs.bepinex.dev/articles/advanced/proton_wine.html).

Start the game normally. The first BepInEx launch can take longer while it creates interop assemblies.

## Diagnose, recover and uninstall

Run these commands from the extracted release directory with Big Walk closed:

```bash
bash Setup.sh diagnose --game '/path/to/steamapps/common/Big Walk'
bash Setup.sh recover --game '/path/to/steamapps/common/Big Walk'
bash Setup.sh uninstall --game '/path/to/steamapps/common/Big Walk'
```

Removing the files does not edit Steam's launch options. Remove the `WINEDLLOVERRIDES` fragment manually after uninstalling if no other mod needs it.

Removal stops before deleting anything if an owned binary was edited or replaced, even by an identical copy with a different filesystem identity. Preserve that file outside the game directory for inspection, then run `recover` again. Uninstall before moving/copying an installed game to another filesystem; this preview records file identities as well as hashes to avoid removing files created by another installer.

If the plugin reports that a native DLL could not load, install or repair the Microsoft Visual C++ x64 runtime **inside this game's Proton prefix**. Do not install it into system Wine or an unrelated prefix. This project does not guess a Steam application ID or run prefix-management tools for you.

## First remote test

Use a private world and test in three stages:

1. Confirm that unmodified Big Walk still starts with the selected Proton version and GPU driver.
2. Install the mod and confirm that `BepInEx/LogOutput.log` exists and the Home menu opens.
3. Enable the initial upscaler, check the picture while moving, and compare it with reconstruction off before trying graphics presets.

For a problem report, include the mod and game versions, Proton version, GPU and driver, the selected `--gpu` value, and the relevant redacted lines from `BepInEx/LogOutput.log`. Do not upload the entire Proton prefix, game files, proprietary DLLs or an unredacted log.

CachyOS documents its current Steam and Proton-CachyOS setup in the [CachyOS gaming guide](https://wiki.cachyos.org/configuration/gaming/). Distribution-specific performance options are separate from this mod's installation and are not required by Setup.
