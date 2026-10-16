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

A fresh BepInEx installation downloaded the Unity 6000.3.17 base libraries, processed IL2CPP metadata version 39 and generated 158 interop assemblies. It loaded one plugin, Big Beautiful Walk 0.7.0, and sustained DLSS evaluation; a repeat launch reused the generated files successfully. The user confirmed the picture, movement and Home/F10 menus in a private world after this fresh installation. These observations do not establish frame pacing or compatibility on another machine. For individual upscaler and experimental-mode checks, see [the feature matrix](UPSCALERS.md).

The previously accepted vegetation runtime is built from `4dc8555e72b25f2a7c34fa4aa38814b61356a100`. After the binding corrections, the user confirmed stable preset cycling, distant tree shadows with Extreme active and nearby foliage restoration with Game defaults. The log recorded a 1950–2000 m material fade, then restoration of 542 changed material properties and the active Flora parameter without replaying a guessed fade range. These are observations on the setup above, not universal restoration or scene-lifecycle coverage.

Previously accepted DLL SHA-256 values:

- Managed: `0859D14B19B7DB03D231B35E6598CA40DEC8EB7557374EB7B67D2115CDE214A6`.
- Native, unchanged during the vegetation corrections: `947D6EB4634AC85E3F4ECE92115B308E34C908B4DDB086B0F70B8C23E6D01914`.

The managed Release build used .NET SDK 8.0.426 and `dotnet build src/managed/BigWalkDLSS.csproj -c Release -p:GameDir=<local game folder>`, exiting 0 with the existing MSB3246 game-reference warning. The native Release build previously exited 0; its ABI remains version 3 / 120 bytes. The hashes above identify the earlier accepted pair. The later installer/UI build uses managed SHA-256 `C4E1CD56CCFCA4772D4341A05D2C53E4E6460274D2E35D389D197DCE8D15F124` and the unchanged native DLL; that pair completed the fresh bootstrap described above. Package identities are also recorded in `SHA256SUMS.txt`.

## Limits

- VR is unsupported. Remove or disable this graphics integration before a VR session.
- Version 0.7's controls and three FG routes were exercised on the observed RTX 4090. NVIDIA FG and Default-style NR received positive visual checks; AMD FG was functional but inferior in the user's comparison, and Intel FG produced frames with a net performance regression. Other hardware, all backend/FG combinations and AMD/Intel FG plus NR remain unverified. See [FRAME-GENERATION.md](FRAME-GENERATION.md).
- DirectX 12, Vulkan, Linux/Proton and other RTX generations are unverified. This release requires a system with an NVIDIA GPU, including when selecting FSR or XeSS; AMD/Intel-only systems are outside the present build's SDK scope and supported configuration.
- Game or Unity updates may change the rendering hooks. Preserve your installer backups before updating.
- FG can shimmer on grass and foliage; generated-frame rate is different from the game's rendered-frame rate.
- XeSS produced a valid picture but slight flicker was reported on the tested display. The cause is unconfirmed; no VRR-specific fix is claimed.
- NR requires a model. Setup can optionally download RHI's unsigned community SF-v2 variant, which matches the model already tested here; it remains outside the release ZIP. Its mirror/source and absent distribution-grant evidence are recorded in [the notice](licenses/RHI-Neural-Rendering-NOTICE.md). Other/newer models remain unverified.
- Large shadow ranges and the 16384 atlas can substantially increase GPU cost. Exact GPU atlas allocation remains unverified.
- Vegetation restoration is verified for the tested preset/default cycles. Other scenes, quality changes during an override, game updates and all material variants remain unverified; compare against an unmodified launch if a visual change persists.
- No extra cascades beyond four, distance-field shadows, ray-traced shadows or separate near/far shadow renderer are implemented.

## Problem reports

Open a [GitHub issue](https://github.com/Pseudogiant-xr/Big-Beautiful-Walk/issues) with version 0.7.0, your game version/build, GPU/driver, API, whether OptiScaler is installed, the selected graphics preset, steps to reproduce and relevant log excerpts. For a crash or missing panel, run Setup's **Diagnose** first. Do not upload game assemblies, proprietary DLLs, full personal configuration dumps or unredacted logs.
