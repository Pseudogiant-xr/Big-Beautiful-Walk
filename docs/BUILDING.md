# Building from source

## What you need

- Windows x64.
- The .NET SDK.
- Visual Studio C++ build tools and CMake.
- Your own Big Walk installation.
- BepInEx build 755.
- The interop assemblies BepInEx generates locally when you launch it with the game.

Keep the game assemblies and the generated interop files outside this repository.

## Managed plugin

Pass the folder where the game is actually installed:

```powershell
dotnet build .\src\managed\BigWalkDLSS.csproj -c Release -p:GameDir='E:\SteamLibrary\steamapps\common\Big Walk'
```

The project finds loader references in `BepInEx/core` and game interop references in `BepInEx/interop`. Neither is copied into the public source tree.

## Native bridge

Get the NVIDIA DLSS/NGX SDK and the Streamline headers under their upstream terms. You need Windows x64 and the Visual Studio 2022 C++ build tools.

- `NGX_ROOT` takes either the SDK repository layout (`include` and `lib/Windows_x86_64/x64`) or a flat `include` / `lib` layout.
- `STREAMLINE_ROOT` takes either the SDK root that contains `include/sl_dlss_g.h`, or the include folder itself.

`Build.ps1` builds the managed plugin and the native bridge. `Package.ps1` then assembles the release package:

```powershell
.\scripts\Build.ps1 -GameDir 'E:\SteamLibrary\steamapps\common\Big Walk' -NgxRoot 'E:\SDKs\DLSS' -StreamlineRoot 'E:\SDKs\Streamline'
.\scripts\Package.ps1 -Destination '.\dist\Big-Beautiful-Walk-0.8.5'
```

The managed/native packet ABI is 120 bytes, version 3. Always ship both DLLs from the same build. Release builds leave out debug-path records.

The bridge loads the runtime DLL from the game folder. Building the bridge does not give you that runtime, and does not mean it works with every runtime version. For the public core, use the versions pinned in `packaging/dependencies.lock.json`.

## Tests

All checks run on the CPU. Hosted CI (`.github/workflows/checks.yml`) runs every one of them on each push and pull request. It does not test game rendering.

- .NET 8 projects, run with `dotnet run --project <project> -c Release`: `tests/GraphicsChecks.csproj`, `tests/VegetationRestoreChecks.csproj`, `tests/VegetationInteropChecks.csproj`, `tests/BridgeChecks.csproj`, `tests/FeatureChecks.csproj` and `tests/FeatureMenuChecks.csproj`.
- `tests/Invoke-BridgeRefreshChecks.ps1` and `tests/Launcher.Tests.ps1`, on PowerShell 7.
- `tests/Setup.Tests.ps1` (installer recovery) and `tests/SetupSelection.Tests.ps1` (optional-feature selection), on both Windows PowerShell 5.1 and PowerShell 7.

`tests/Dependencies.Tests.ps1` is not part of CI: it needs a built package (`-PackagePath`) and a folder of real downloaded dependencies (`-DependencyDirectory`).

## Setup launcher

Packaging also builds the small `Setup.exe` GUI launcher from `src/setup/SetupLauncher.cs`, using Windows' x64 .NET Framework compiler. It starts the same Windows PowerShell installer without a console window, and adds no downloaded runtime dependency. `Setup.cmd` is still there as the console and command-line option.

## Licences

Keep `LICENSE`, `NOTICE`, `THIRD_PARTY_NOTICES.md` and `docs/licenses` with both the source and any compiled build you hand out. The Unity adaptation and the NVIDIA SDK code included in this project keep their own terms. Read `docs/RELEASE-CHECKLIST.md` before you publish a build.

After building with your locally generated game interop assemblies, the optional image-binding check records whether the known span-wrapper error is present and JIT-compiles the replacement loader without starting the game. It also prepares the performance and small-plant adapter methods against those assemblies:

```powershell
dotnet run --project tests/ImageInteropChecks.csproj -c Release -- $env:BIGWALK_GAME_DIR ./src/managed/bin/Release/net6.0/BigWalkDLSS.dll
```

This checks managed binding compatibility only. It does not execute Unity’s native PNG decoder or verify the image on screen. Game assemblies are not included in CI or redistributed.
