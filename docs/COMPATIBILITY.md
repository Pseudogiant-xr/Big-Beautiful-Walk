# Compatibility and known issues

NR 310.9.16 can also be imported from a user-supplied, hash-verified compatibility DLL. This runtime is not bundled or downloaded automatically; its Big Walk rendering is unverified. See [local import](FRAME-GENERATION.md#nr-310916-local-import) and [provenance](licenses/RHI-Neural-Rendering-NOTICE.md#nr-310916-local-import).

## Tested setup

Big Beautiful Walk 0.7 has been tested on one PC:

| Item | Tested |
| --- | --- |
| Game | Big Walk 1.6.0, Steam build 25723723 |
| Application version | `1.6.0 2609301522` (the mod's version check matches this exact build stamp) |
| Engine | Unity 6000.3.17f1, IL2CPP, URP |
| Platform | Windows x64, DirectX 11, monitor |
| GPU / driver | GeForce RTX 4090 / 617.42 |
| Loader | BepInEx 6 bleeding-edge build 755, commit 3fab71a |
| Prototype SR | 310.9.16 from a pre-existing setup |
| Public installer SR | NVIDIA official SDK 310.9.1, pinned and signature verified |

Frame pacing and other machines have not been tested.

### What was tested

With an existing BepInEx install, the public core's official SR 310.9.1 ran DLSS steadily in the game world. The Home menu and the **Quality** and **Game defaults** choices looked right.

An earlier prototype also ran FG and NR with runtimes obtained separately. That does not prove the new public set of optional downloads works.

**Fresh install.** On a fresh BepInEx install, BepInEx downloaded the Unity 6000.3.17 base libraries, processed IL2CPP metadata version 39 and generated 158 interop assemblies. It loaded one plugin, Big Beautiful Walk 0.7.0, and DLSS ran steadily. A second launch reused the generated files without problems. The picture, movement and the Home and F10 menus were then confirmed in a private world.

For each upscaler and the experimental modes, see [the feature matrix](UPSCALERS.md).

**Vegetation.** The tested vegetation build comes from commit `4dc8555e72b25f2a7c34fa4aa38814b61356a100`. On it:

- Switching between presets was stable.
- Distant tree shadows appeared with **Extreme**.
- **Game defaults** restored nearby foliage.

The log showed a 1950–2000 m material fade. It then showed 542 changed material properties and the active Flora parameter being restored, without replaying a guessed fade range. This covers the setup above only. It does not show that restoring works everywhere, or across every way a scene loads and unloads.

### Build hashes

SHA-256 hashes of the earlier tested DLL pair:

- Managed: `0859D14B19B7DB03D231B35E6598CA40DEC8EB7557374EB7B67D2115CDE214A6`.
- Native (not changed by the vegetation fixes): `947D6EB4634AC85E3F4ECE92115B308E34C908B4DDB086B0F70B8C23E6D01914`.

The managed Release build used .NET SDK 8.0.426 and `dotnet build src/managed/BigWalkDLSS.csproj -c Release -p:GameDir=<local game folder>`. It exited 0 with the known MSB3246 game-reference warning. The native Release build also exited 0 earlier. Its ABI is still version 3 / 120 bytes.

The later installer and UI build has managed SHA-256 `C4E1CD56CCFCA4772D4341A05D2C53E4E6460274D2E35D389D197DCE8D15F124` and the same native DLL. That pair passed the fresh install test above. The package's file hashes are also listed in `SHA256SUMS.txt`.

## Limits

- VR is not supported. Remove or turn off this graphics mod before a VR session.
- In 0.7, the controls and all three FG providers were tried on the tested RTX 4090. NVIDIA FG and Default-style NR looked good. AMD FG worked but was worse in comparison. Intel FG generated frames but made the game slower overall. Not tested: other hardware, every backend and FG combination, and AMD or Intel FG together with NR. See [FRAME-GENERATION.md](FRAME-GENERATION.md).
- DirectX 12, Vulkan, Linux/Proton and other RTX generations have not been tested.
- AMD and Intel GPUs are supported experimentally through the OptiScaler bridge (FSR 3.1, XeSS, AMD and Intel frame generation). This path has not been tested on AMD or Intel hardware; the native bridge plugin was only exercised on the RTX 4090 with the AMD/Intel path forced. DLSS, NVIDIA frame generation and Neural Rendering need an NVIDIA RTX GPU.
- On a PC with an NVIDIA GPU and an AMD integrated GPU, if OptiScaler's GPU spoofing is turned on and Windows runs the game on the integrated GPU, the mod cannot tell which GPU is rendering and takes the NVIDIA path. Big Beautiful Walk ships with spoofing off (`[Spoofing] Dxgi=false`); keep it off.
- Game or Unity updates may break the rendering hooks. Keep your installer backups before you update.
- FG can make grass and foliage shimmer. The generated frame rate is not the same as the game's rendered frame rate.
- XeSS gives a valid picture, but slight flicker was reported on the tested display. The cause is unknown. This release does not claim a VRR-specific fix.
- NR needs a model. Setup can download RHI's unsigned community SF-v2 variant for you. It matches the model already tested here, and it is not part of the release ZIP. [The notice](licenses/RHI-Neural-Rendering-NOTICE.md) records its mirror and source, and that there is no evidence of a grant to redistribute it. Other and newer models have not been tested.
- Long shadow ranges and the 16384 atlas can add a lot of GPU cost. The exact atlas memory the GPU allocates has not been measured.
- Vegetation restoring works for the preset and **Game defaults** switches that were tested. Not tested: other scenes, changing the game's quality setting while an override is on, game updates, and every material variant. If a visual change sticks around, compare with a launch without the mod.
- The mod does not add more than four cascades, distance-field shadows, ray-traced shadows, or a separate near/far shadow renderer.

## Problem reports

Open a [GitHub issue](https://github.com/Pseudogiant-xr/Big-Beautiful-Walk/issues) and include:

- Big Beautiful Walk version 0.8.0
- your game version and build
- your GPU and driver
- the graphics API
- whether OptiScaler is installed
- the selected graphics preset
- steps to reproduce
- the relevant parts of your log

For a crash or a missing panel, run Setup's **Diagnose** first.

Do not upload game assemblies, proprietary DLLs, full dumps of your personal configuration, or logs you have not redacted.
