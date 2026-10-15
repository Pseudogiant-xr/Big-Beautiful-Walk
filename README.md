# Big Beautiful Walk 0.7.0

Alpha candidate graphics mod for **Big Walk** on Windows. It adds DLSS Super Resolution and a Home-key panel for upscalers, shadows, vegetation, textures and other graphics controls. Start with the game's own graphics settings, then opt into the Balanced, Quality or Extreme presets.

This is an unofficial mod. It is not affiliated with or endorsed by House House, Unity or NVIDIA.

## Install

1. Close Big Walk and extract the complete release ZIP into a separate folder.
2. Open **Setup.cmd**, choose the Big Walk folder containing `Big Walk.exe`, and select **Install / Repair**.
3. Setup downloads the pinned BepInEx loader and DLSS runtime and verifies their SHA-256 hashes. The optional bridge checkbox adds OptiScaler, NVIDIA Streamline, AMD FidelityFX FG and Intel XeSS FG / XeLL. Fresh bridge defaults leave FG and NR off. Review the [third-party terms](THIRD_PARTY_NOTICES.md) before installation.
4. Launch Big Walk normally on your monitor. The first BepInEx launch can take longer while it prepares local interop files.
5. Press **Home** to open Big Beautiful Walk. Check the DLSS status before changing graphics settings.

Use **Diagnose** in Setup to check installed files, or **Uninstall** with the game closed to remove this install and restore backed-up originals. Keep the game's `.big-beautiful-walk` folder until removal is complete.

[Installation and recovery](docs/INSTALLATION.md) · [Graphics guide](docs/GRAPHICS.md) · [Upscalers and presets](docs/UPSCALERS.md) · [Compatibility and known issues](docs/COMPATIBILITY.md) · [Building from source](docs/BUILDING.md)

## What is supported

- Windows x64, DirectX 11, monitor play and an NVIDIA RTX GPU.
- DLSS quality choices from Ultra Performance through native DLAA, with selectable render presets; Quality / Preset L is the tested baseline. DLAA and Ultra Quality are experimental.
- Optional graphics overrides, including shadow cascades, vegetation shadow range and an experimental 16384-pixel main shadow atlas.
- Home offers NVIDIA, AMD and Intel **Frame Gen** profiles and **Neural** settings through the optional bridge. Changes are saved for a restart. These new controls and provider combinations have not yet been tested in-game; the established 0.6 NVIDIA FG path was tested separately. See the [FG / NR guide](docs/FRAME-GENERATION.md).
- Neural Rendering requires a separately obtained model; the installer supplies the integration forwarder, without a model. The October 16 faster model is not bundled or claimed as tested.
- Home also offers optional FSR 3.1 and XeSS backend choices when their bridge requirements are available. FSR 4 is shown as unsupported on the tested RTX 4090. See the [feature matrix](docs/UPSCALERS.md) for prerequisites and individual test status.

The public core's official NVIDIA SR 310.9.1 runtime sustained DLSS evaluation in the game world with an existing BepInEx installation. The Home menu and Quality / Game defaults choices were visually confirmed on Big Walk 1.6.0 (Steam build 25723723), Unity 6000.3.17f1, an RTX 4090 and driver 617.42. A completely fresh BepInEx installation and first-launch interop generation have not been tested in the real game. See the [compatibility notes](docs/COMPATIBILITY.md).

VR, distance-field shadows and ray-traced shadows are unsupported. Extreme settings can cost substantial performance and video memory.

## Dependencies and source

The release contains the mod's binaries, source, installer and notices. It does not contain game assemblies, game assets, NVIDIA runtime DLLs or a Neural Rendering model. Runtime downloads are pinned in [dependencies.lock.json](packaging/dependencies.lock.json); full license boundaries and upstream sources are in [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).

Original project code is under Apache 2.0. The Unity-adapted post-processing code and third-party components retain their own terms; this is not a blanket Apache licence for the whole dependency stack. Home's **Credits and licences** page and the installed `BepInEx/BigWalkDLSS/notices` folder provide attribution. Public release remains pending the permissions listed in [LICENSING.md](docs/LICENSING.md).

When reporting a problem, include the mod version, game version, GPU, driver, graphics API and the smallest relevant log excerpt. Remove account names and personal paths before posting logs publicly.
