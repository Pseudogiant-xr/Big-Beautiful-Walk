# Installation and recovery

## Before installing

Use the Windows x64 Steam version of Big Walk on a monitor, with an NVIDIA RTX GPU and DirectX 11. Close the game. Extract the whole release archive before running `Setup.cmd`; do not run Setup inside the ZIP or copy only its launcher.

Install or repair the [Microsoft Visual C++ v14 Redistributable for x64](https://aka.ms/vc14/vc_redist.x64.exe) before starting the game. The native plugin needs its runtime libraries; Setup does not install this system prerequisite. [Microsoft's supported runtime guidance](https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist) explains the package.

Setup searches Steam libraries and lets you choose another folder. Select the folder containing `Big Walk.exe`. Administrator access is normally unnecessary. A working internet connection is needed for the first download, and BepInEx may need internet access on its first game launch.

## Install or repair

Open `Setup.cmd` and choose **Install / Repair**. The installer stages and hashes its downloads before changing the game folder. It tracks installed files and stores original files under `.big-beautiful-walk` in the game folder. Preserve that folder for recovery and uninstall.

Setup refuses unrecognized conflicting loader or runtime files rather than replacing another mod's installation. Existing configurations are preserved. The optional bridge adds OptiScaler and NVIDIA, AMD and Intel FG dependencies; its fresh configuration keeps FG and NR off. Already-installed optional components are included in later repairs even when the box is unchecked. Open **Home → Frame Gen**, choose a provider, enable it for the next launch, then quit normally and restart. NR needs a separately obtained model. Setup's **FG / NR guide** opens the [feature instructions](FRAME-GENERATION.md), and **Licences** opens the component notices.

Launch through Steam. The first BepInEx launch can pause while preparing interop files; `BepInEx/LogOutput.log` records progress. A completely fresh bootstrap remains an outstanding release test. Enter a private world, press **Home**, and expect **Upscaling active** after loading. **Waiting for a scene** at the title screen is normal. Start with Quality / Preset L and compare off/on with **F8**. Try graphics presets individually; **Game defaults** restores graphics overrides while Upscaling, Frame Gen and Neural retain separate controls. The game continues simulating while the panel is open.

## Diagnose

Open Setup and choose **Diagnose**; this read-only action also works while the game is running. It checks installer-owned files against recorded hashes, not rendering, GPU compatibility or frame rate. For a missing owned file, close the game and use **Install / Repair**. For a conflict, preserve the changed file and investigate it before attempting repair.

Useful local logs are `BepInEx/LogOutput.log` and `BepInEx/BigWalkDLSS/native.log`. Share only relevant excerpts after removing personal paths.

If Home reports **Upscaler unavailable**, install or repair the x64 Visual C++ Redistributable, restart the game and check those logs. If a saved optional feature prevents startup, close the game and back up `OptiScaler.ini`. In its existing `[FrameGen]` and `[DlssNr]` sections, change each existing `Enabled` value to `false`, retaining the other keys. Edit the existing sections rather than appending duplicates. Retry the baseline before enabling one feature at a time; use Uninstall if you need to remove this integration. Repair preserves settings and cannot by itself undo a failing feature request.

Report reproducible problems through [GitHub Issues](https://github.com/Pseudogiant-xr/Big-Beautiful-Walk/issues), with the mod/game version, GPU/driver, backend/provider, reproduction steps and a short redacted log excerpt.

## Uninstall

Close the game, run `Setup.cmd`, select the same folder and choose **Uninstall**. The installer removes files it installed and restores originals it backed up. It preserves configuration files, saves, other mods and its backup folder. If an installed file has been changed externally, Setup stops rather than deleting someone else's change.

If Setup reports interrupted recovery, keep `.big-beautiful-walk` and run Setup again after closing the game. Do not manually delete the transaction journal or original backups.

## Advanced: offline and command line

The same installer supports PowerShell:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Setup.ps1 -Headless -GamePath 'E:\SteamLibrary\steamapps\common\Big Walk' -Mode Install
```

Use `-Mode Diagnose` or `-Mode Uninstall` for those actions. Add `-Features fg` for a new optional upscaler/FG installation; later repairs also include previously owned optional components. For offline dependencies, add `-DependencyDirectory 'E:\BBW-dependencies'`. Core requires `bepinex.zip` and `nvngx_dlss.file`. The optional pack also requires `optiscaler_fg.zip`, `streamline_fg.zip`, `xess.zip`, `amd_fsr_loader.file`, `amd_fsr_framegeneration.file`, `amd_fsr_license.file` and `amd_fsr_notices.file`, renamed to these lock IDs and matching the lock's hashes. Offline mode still validates hashes. These are user-supplied upstream downloads, outside the release ZIP.
