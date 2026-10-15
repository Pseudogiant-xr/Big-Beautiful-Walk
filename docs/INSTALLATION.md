# Installation and recovery

## Before installing

Use the Windows x64 Steam version of Big Walk on a monitor, with an NVIDIA RTX GPU and DirectX 11. Close the game. Extract the whole release archive before running `Setup.cmd`; do not run Setup inside the ZIP or copy only its launcher.

Setup searches Steam libraries and lets you choose another folder. Select the folder containing `Big Walk.exe`. Administrator access is normally unnecessary. A working internet connection is needed for the first download, and BepInEx may need internet access on its first game launch.

## Install or repair

Open `Setup.cmd` and choose **Install / Repair**. The installer stages and hashes its downloads before changing the game folder. It tracks installed files and stores original files under `.big-beautiful-walk` in the game folder. Preserve that folder for recovery and uninstall.

Setup refuses unrecognized conflicting loader or runtime files rather than replacing another mod's installation. Existing configurations are preserved. The optional bridge checkbox adds OptiScaler, production Streamline/FG files and Intel XeSS; its fresh configuration keeps FG and NR off. Enable FG explicitly in the bridge menu after verifying the picture. NR needs a separately obtained model.

Launch Big Walk normally. Press **Home** for the mod panel and DLSS status. The game continues simulating while the panel is open.

## Diagnose

With the game closed, open Setup and choose **Diagnose**. This checks installer-owned files against the recorded hashes; it does not verify rendering, GPU compatibility or frame rate. A file conflict means the recorded installation and the current file differ. Preserve the backup folder and investigate the named file before attempting repair.

Useful local logs are `BepInEx/LogOutput.log` and `BepInEx/BigWalkDLSS/native.log`. Share only relevant excerpts after removing personal paths.

## Uninstall

Close the game, run `Setup.cmd`, select the same folder and choose **Uninstall**. The installer removes files it installed and restores originals it backed up. It preserves configuration files, saves, other mods and its backup folder. If an installed file has been changed externally, Setup stops rather than deleting someone else's change.

If Setup reports interrupted recovery, keep `.big-beautiful-walk` and run Setup again after closing the game. Do not manually delete the transaction journal or original backups.

## Advanced: offline and command line

The same installer supports PowerShell:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Setup.ps1 -Headless -GamePath 'E:\SteamLibrary\steamapps\common\Big Walk' -Mode Install
```

Use `-Mode Diagnose` or `-Mode Uninstall` for the corresponding action. Add `-Features fg` for the optional upscaler/FG bridge. For offline dependencies, add `-DependencyDirectory 'E:\BBW-dependencies'`. Core requires `bepinex.zip` and `nvngx_dlss.file`; the optional bridge also requires `optiscaler_fg.zip`, `streamline_fg.zip` and `xess.zip`, with exactly the hashes in the lock. Offline mode still validates hashes. These are user-supplied upstream downloads, not part of the release.
