# Big Beautiful Walk 0.7.0

Alpha candidate graphics mod for **Big Walk** on Windows. It adds DLSS Super Resolution and a Home-key panel for upscalers, shadows, vegetation, textures and other graphics controls. Start with the game's own graphics settings, then opt into the Balanced, Quality or Extreme presets.

This is an unofficial mod. It is not affiliated with or endorsed by House House, Unity or NVIDIA.

This is an experimental alpha tested on one RTX 4090 system. See [compatibility](docs/COMPATIBILITY.md) for known limitations and [release notes](docs/RELEASE-READINESS.md) for validation scope.

## Install

1. Install or repair the [Microsoft Visual C++ v14 runtime for x64](https://aka.ms/vc14/vc_redist.x64.exe), then close Big Walk and extract the complete release ZIP into a separate folder. See [Microsoft's runtime guidance](https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist).
2. Open **Setup.exe**, choose the Big Walk folder containing `Big Walk.exe`, and select **Install / Repair**. The graphical launcher has no terminal window; `Setup.cmd` remains available for troubleshooting and command-line use.
3. Setup downloads the pinned BepInEx loader and DLSS runtime and verifies their SHA-256 hashes. The optional bridge adds alternative upscalers and NVIDIA/AMD/Intel frame generation. A separate **Neural Rendering runtime** option adds RHI's community-hosted SF-v2 model and the bridge it needs. Both options start unchecked; fresh FG and NR settings remain off. Component sources and terms are in [the notices](THIRD_PARTY_NOTICES.md).
4. Launch Big Walk normally on your monitor. The first BepInEx launch can take longer while it prepares local interop files.
5. Enter a private world, press **Home**, and look for **Upscaling active**. **Waiting for a scene** at the title screen is normal. Start with DLSS Quality / Preset L; **F8** compares reconstruction off/on.
6. Try graphics presets individually. **Game defaults** restores graphics overrides; upscaling, FG and NR have separate controls. Use **Home → Frame Gen** or **Neural**, then quit normally and restart to apply their saved requests.

Use **Diagnose** in Setup to check installed files, or **Uninstall** with the game closed to remove this install and restore backed-up originals. Keep the game's `.big-beautiful-walk` folder until removal is complete.

[Installation and recovery](docs/INSTALLATION.md) · [Graphics guide](docs/GRAPHICS.md) · [Upscalers and presets](docs/UPSCALERS.md) · [Compatibility and known issues](docs/COMPATIBILITY.md) · [Building from source](docs/BUILDING.md)

## What is supported

- Windows x64, DirectX 11, monitor play and an NVIDIA RTX GPU.
- DLSS quality choices from Ultra Performance through native DLAA, with selectable render presets; Quality / Preset L is the tested baseline. DLAA and Ultra Quality are experimental.
- Optional graphics overrides, including shadow cascades, vegetation shadow range and an experimental 16384-pixel main shadow atlas.
- Home offers NVIDIA, AMD and Intel **Frame Gen** profiles and **Neural** settings through the optional bridge. Changes are saved for a restart. On the tested RTX 4090, NVIDIA FG gave the preferred result; AMD FG was functional with quality/performance limitations, while Intel FG produced frames but reduced reported FPS. See the [FG / NR guide](docs/FRAME-GENERATION.md).
- Neural Rendering can use the optional RHI community download or an existing compatible model. That pinned download matches the model previously tested with NVIDIA FG and Default style. The October 16 faster model is not claimed as tested; [the NR guide](docs/FRAME-GENERATION.md#neural-rendering) identifies the supplied version and source.
- Home also offers optional FSR 3.1 and XeSS backend choices when their bridge requirements are available. FSR 4 is shown as unsupported on the tested RTX 4090. See the [feature matrix](docs/UPSCALERS.md) for prerequisites and individual test status.

The official NVIDIA SR 310.9.1 runtime sustained in-world DLSS evaluation on Big Walk 1.6.0 (Steam build 25723723), Unity 6000.3.17f1, an RTX 4090 and driver 617.42. A fresh BepInEx installation downloaded the Unity libraries, generated 158 interop assemblies, loaded the mod and evaluated DLSS; a subsequent launch reused those files successfully. Final picture and menu acceptance for this installation is pending. See the [compatibility notes](docs/COMPATIBILITY.md).

VR, distance-field shadows and ray-traced shadows are unsupported. Extreme settings can cost substantial performance and video memory.

## Dependencies and source

The release contains the mod's binaries, source, installer and notices. It does not contain game assemblies, game assets, NVIDIA runtime DLLs or a Neural Rendering model. Runtime downloads are pinned in [dependencies.lock.json](packaging/dependencies.lock.json); full license boundaries and upstream sources are in [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).

Original project code is under Apache 2.0. The Unity-adapted post-processing code and third-party components retain their own terms; this is not a blanket Apache licence for the whole dependency stack. Home's **Credits and licences** page and the installed `BepInEx/BigWalkDLSS/notices` folder provide attribution. Component terms and remaining maintainer checks are recorded in [LICENSING.md](docs/LICENSING.md) and [the release checklist](docs/RELEASE-CHECKLIST.md).

Report problems through [GitHub Issues](https://github.com/Pseudogiant-xr/Big-Beautiful-Walk/issues). Include the mod version, game version, GPU, driver, graphics API and the smallest relevant log excerpt. Remove account names and personal paths before posting logs publicly.
