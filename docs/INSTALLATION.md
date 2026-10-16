# Installation and recovery

## Before installing

You need:

- The Windows x64 Steam version of Big Walk, played on a monitor.
- An NVIDIA RTX GPU running DirectX 11.
- The [Microsoft Visual C++ v14 Redistributable for x64](https://aka.ms/vc14/vc_redist.x64.exe). The native plugin needs it, and Setup does not install it. [Microsoft's runtime guidance](https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist) explains the package.
- An internet connection for the first install. BepInEx may also need one the first time the game starts.

Close the game. Extract the whole release archive before you run `Setup.exe`. Don't run Setup from inside the ZIP, and don't copy the launcher out on its own.

Setup lists the Steam libraries it finds, and you can browse to another folder. Pick the folder that contains `Big Walk.exe`. You normally don't need administrator rights.

## Install or repair

Open `Setup.exe` and click **Install / Repair**. Setup.exe runs the PowerShell installer without a terminal window. If you want console output or command-line options, use `Setup.cmd` instead.

What Setup does:

- Downloads its files to a staging area and checks their hashes before it changes the game folder.
- Records every file it installs, and keeps the originals it replaces in `.big-beautiful-walk` in the game folder. Keep that folder: repair and uninstall need it.
- Stops instead of overwriting loader or runtime files it doesn't recognise, so it won't replace another mod's install.
- Keeps your existing configuration files.

### Optional components

Two checkboxes add optional components. Both start unchecked.

- **Bridge**: OptiScaler, plus the NVIDIA, AMD and Intel frame generation runtimes.
- **Neural Rendering model**: RHI's community-hosted SF-v2 model, about 111 MiB, plus the bridge it needs. This is not an official NVIDIA SDK download.

Installing only the bridge does not add a Neural Rendering model. Once an optional component is installed, later repairs include it even if its box is unchecked. Frame generation and Neural Rendering stay off until you choose them: open **Home → Frame Gen** or **Neural**, make your choice, then quit the game normally and restart it.

Setup's **FG / NR guide** button opens the [feature guide](FRAME-GENERATION.md), and **Licences** opens the component notices.

### First launch

1. Start the game through Steam. The first BepInEx launch can pause while it prepares interop files. `BepInEx/LogOutput.log` shows its progress. Let it finish.
2. Load into a private world and press **Home**. After loading you should see **Upscaling active**. On the title screen it says **Waiting for a scene**, which is normal.
3. Start with Quality and Preset L. Press **F8** to switch reconstruction off and on.
4. Try graphics presets one at a time. **Game defaults** removes the graphics overrides. Upscaling, Frame Gen and Neural have their own controls and are not affected.

The game keeps running while the panel is open.

## Diagnose

Open Setup and click **Diagnose**. It only reads files, so it also works while the game is running. It compares the files Setup installed against their recorded hashes. It does not test rendering, GPU compatibility or frame rate.

- If an installed file is missing, close the game and run **Install / Repair**.
- If a file conflicts, keep the changed file and find out what changed it before you repair.

The useful logs are `BepInEx/LogOutput.log` and `BepInEx/BigWalkDLSS/native.log`. When you share them, post only the relevant lines and remove personal paths.

### If something goes wrong

- **Home shows "Upscaler unavailable"**: install or repair the x64 Visual C++ Redistributable, restart the game and check the logs above.
- **The game won't start after you turned on an optional feature**: repair keeps your settings, so it won't undo this on its own. Instead:
  1. Close the game and back up `OptiScaler.ini` in the game folder.
  2. In its existing `[FrameGen]` and `[DlssNr]` sections, set each existing `Enabled` value to `false`. Leave the other keys alone, and edit the existing sections instead of adding new ones.
  3. Check the game starts with the baseline, then turn features back on one at a time.
  4. If you want to remove the integration completely, use **Uninstall**.

Report problems you can reproduce on [GitHub Issues](https://github.com/Pseudogiant-xr/Big-Beautiful-Walk/issues). Include the mod and game versions, GPU and driver, backend or provider, the steps to reproduce, and a short log excerpt with personal details removed.

## Uninstall

Close the game, run `Setup.exe`, select the same folder and click **Uninstall**. Setup removes the files it installed and restores the originals it backed up. It leaves your configuration files, saves, other mods and its backup folder in place. If a file it installed has since been changed by something else, Setup stops instead of deleting that change.

If Setup reports an interrupted recovery, keep `.big-beautiful-walk`, close the game and run Setup again. Don't delete the transaction journal or the original backups by hand.

## Advanced: offline and command line

You can run the installer directly from PowerShell:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Setup.ps1 -Headless -GamePath 'E:\SteamLibrary\steamapps\common\Big Walk' -Mode Install
```

- `-Mode Diagnose` and `-Mode Uninstall` run those actions.
- `-Features fg` adds the optional upscaler and frame generation bridge. `-Features nr` adds the Neural Rendering model and the bridge it needs. Later repairs include optional components that are already installed.
- `-DependencyDirectory 'E:\BBW-dependencies'` installs from local files instead of downloading. Name each file after its ID in the lock file. Setup still checks every file against the locked hash.

Files needed in the dependency directory:

| Install | Files |
| --- | --- |
| Core | `bepinex.zip`, `nvngx_dlss.file` |
| Bridge (`fg`) | Core files, plus `optiscaler_fg.zip`, `streamline_fg.zip`, `xess.zip`, `amd_fsr_loader.file`, `amd_fsr_framegeneration.file`, `amd_fsr_license.file`, `amd_fsr_notices.file` |
| Neural Rendering (`nr`) | Bridge files, plus `nvidia_nr.zip` |

None of these runtime or model downloads are included in the release ZIP.
