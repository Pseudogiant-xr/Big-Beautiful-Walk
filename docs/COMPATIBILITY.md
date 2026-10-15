# Compatibility and known issues

## Observed environment

| Item | Observed setup |
| --- | --- |
| Game | Big Walk 1.6.0, Steam build 25723723 |
| Application version | `1.6.0 2609301522` (the exact version guard includes this build stamp) |
| Engine | Unity 6000.3.17f1, IL2CPP, URP |
| Platform | Windows x64, DirectX 11, monitor |
| GPU / driver | GeForce RTX 4090 / 617.42 |
| Loader | BepInEx 6 bleeding-edge build 755, commit 3fab71a |
| Prototype SR | 310.9.16 from a pre-existing setup |
| Public installer SR | NVIDIA official SDK 310.9.1, pinned and signature verified |

The public core's official SR 310.9.1 sustained DLSS evaluation in the game world using an existing BepInEx installation. The Home menu and Quality / Game defaults choices were visually confirmed. The earlier prototype also demonstrated optional FG/NR integration with separately obtained runtimes; that does not validate a new public optional dependency set.

A completely fresh BepInEx installation and first-launch interop generation have not been exercised in the real game. A clean installer fixture checks file operations; it does not prove fresh bootstrap, image quality, frame pacing or compatibility on another machine. Expanded quality modes, alternative upscalers and experimental presets each need their own rendering check; see [the feature matrix](UPSCALERS.md).

## Limits

- VR is unsupported. Remove or disable this graphics integration before a VR session.
- DirectX 12, Vulkan, Linux/Proton, AMD/Intel GPUs and other RTX generations are unverified.
- Game or Unity updates may change the rendering hooks. Preserve your installer backups before updating.
- FG can shimmer on grass and foliage; generated-frame rate is different from the game's rendered-frame rate.
- XeSS produced a valid picture but slight flicker was reported on the tested display. The cause is unconfirmed; no VRR-specific fix is claimed.
- NR requires a separately obtained model. Its provenance and redistribution permissions have not been established for the prototype model, so it is absent from downloads and releases.
- Large shadow ranges and the 16384 atlas can substantially increase GPU cost. Exact GPU atlas allocation remains unverified.
- Complete vegetation-material restoration is unverified. Use a restart and compare against an unmodified launch if a visual change persists.
- No extra cascades beyond four, distance-field shadows, ray-traced shadows or separate near/far shadow renderer are implemented.

## Problem reports

Include version 0.6.0, your game version/build, GPU/driver, API, whether OptiScaler is installed, the selected graphics preset, steps to reproduce and relevant log excerpts. For a crash or missing panel, run Setup's **Diagnose** first. Do not upload game assemblies, proprietary DLLs, full personal configuration dumps or unredacted logs.
