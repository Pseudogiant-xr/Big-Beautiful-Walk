# Dependency sources and policy

The installer reads `packaging/dependencies.lock.json` (`SchemaVersion: 1`). For each dependency it accepts only the pinned HTTPS URL and SHA-256. A matching hash confirms the downloaded bytes. It does not confirm legal permission or compatibility. `Files` maps each archive member explicitly to a path relative to the game folder. The release ZIP contains none of these runtime downloads.

NR 310.9.16 can also be imported from a user-supplied, hash-verified compatibility DLL. This runtime is not bundled or downloaded automatically; its Big Walk rendering is unverified. See [local import](FRAME-GENERATION.md#nr-310916-local-import) and [provenance](licenses/RHI-Neural-Rendering-NOTICE.md#nr-310916-local-import).

## Core downloads

| ID | Version / source | SHA-256 |
| --- | --- | --- |
| `bepinex` | BepInEx Unity IL2CPP Windows x64, 6.0.0-be.755+3fab71a; official build service | `3616D6A67F5F595973EC4AA7BD7EDAF7F799D5BB9926F7146A6DCC7B4ABF478F` |
| `nvngx_dlss` | NVIDIA official SDK 310.9.1; immutable commit `374959484e79a640feaba44c93ac8cfb0a03f5b5` | `3975567B8943C53ACCE397F2B72380092F84F162D00B0D2C7D08A1025C563983` |

The [lock file](../packaging/dependencies.lock.json) has the exact download URLs and the source and licence links. The SR file reported version `310,9,1,0`. When the lock was prepared, Windows signature verification reported a valid NVIDIA Corporation signature. The 227 explicit BepInEx file mappings come from inspecting the downloaded archive.

## Optional FG and NR

Setup's optional upscaler/FG checkbox downloads:

- the pinned OptiScaler proxy
- production Streamline/FG files
- AMD FidelityFX loader and FG
- Intel XeSS SR/FG and XeLL runtimes

The direct core install stays DLSS-only. A fresh optional configuration leaves FG and NR off, selects DLSS as the initial renderer and lets Home provide model-preset requests. Version 0.7 adds saved Home FG/NR profiles. [The FG / NR guide](FRAME-GENERATION.md) has the individual RTX 4090 observations and performance limits. Those observations do not validate every backend combination or a fresh loader bootstrap.

In 0.6, the official NVIDIA optional FG set produced generated-frame presents in-world on the tested setup. FSR 3.1 initialised, sustained reconstruction and was visually confirmed. Setup supplies the NR forwarder from the pinned OptiScaler archive. The separate NR option can also download the tested SF-v2 model from RHI's community mirror. If another injector stack conflicts, you may need to remove it before this install can proceed.

The known prototype used Skynizz OptiScaler-DLSS5 v1.2.1, Streamline 2.14.16, FG 310.9.16 and an NR 310.8.SF-v2 model. These version numbers identify that experiment. They are not a recommendation to download arbitrary copies. When the lock was prepared, the public NVIDIA Streamline release was 2.14.1 and the official public DLSS SDK was 310.9.1. Those are different bytes from the cached experiment.

Skynizz's [official v1.2.1 release](https://github.com/Skynizz/optiscaler-dlss5/releases/tag/v1.2.1) provides its own binary and source archives and checksums. Its binary ZIP SHA-256 is `0D5B24887EA2C622754DA68EFC76EF55AE42B5D3BE350A8D43CAFBABB6747EC6`. The contained `OptiScaler.dll` SHA-256 is `AD143C5BD6E5FAB4D1ABD8B048C424E9DDCEABF1A864E0E0695C01A0DC82B4C9`. Upstream explicitly excludes NVIDIA's NR model.

The separate `nr` feature uses [RankFTW/RHI's SF-v2 archive](https://github.com/RankFTW/rhi-repo/releases/tag/dlssnr-310.8.SF-v2). Selecting `nr` also selects the bridge's `fg` dependencies. Selecting `fg` does not select NR. The archive SHA-256 is `1DA35941894994EB087E017577829E492454E9BAE3A6A9397027069CEB74955C`. Its only member maps to `OptiScaler/nvngx_dlssnr.dll`, SHA-256 `6EB209E764F39872625DEBD6ABAF45E2BB6322F6F270F781F70C059AE30B3927`. This matches the model previously tested in Big Walk. It is an unsigned, community-hosted variant with no licence file in its archive. [The accompanying notice](licenses/RHI-Neural-Rendering-NOTICE.md) records that distinction and does not claim vendor authorisation. Runtime and model binaries are not in this project's release ZIP.

RHI's [pinned catalogue](https://github.com/RankFTW/RHI/blob/b0570336431bfce265e0f6dc19156ea751e404f7/dlss_manifest.json) uses its maintainer's mirrors for NVIDIA versions. Big Beautiful Walk keeps its tested official NVIDIA SR/Streamline URLs and the upstream OptiScaler archive. It uses RHI's endpoint only for the separate NR option. It does not copy RHI code or move automatically to an untested latest version. Updates need a new lock and new verification. Existing files and settings are kept. **F10** opens the supplied bridge's menu.

## Updating a lock

1. Retrieve the exact upstream version. Pin immutable commits where available.
2. Compute its SHA-256.
3. Inspect the archive member paths and licences.
4. Test installation and removal in a disposable fixture.
5. Test rendering in the real game.

A new hash or a passing fixture test does not prove real-game compatibility. Do not substitute cached user files, game assets or unlicensed models for a verified upstream download.

## Additional upscaler sources

The optional bridge's XeSS dependency (`xess` in the lock) is Intel SDK 3.0.2. This is exactly the submodule commit that OptiScaler 1.2.1 references: `8fe81bdbbaf00b3c1b733fd0d830c333dc84e6f0`. Its [official release ZIP](https://github.com/intel/xess/releases/download/v3.0.2/XeSS_SDK_3.0.2.zip) has SHA-256 `88B8A373F30E33F3558A77A93E634F11B8132FC3047EA1A8EDEEAD32B8471990`. The required SR binary is `bin/libxess.dll`, mapped to `OptiScaler/libxess.dll`, with SHA-256 `251659DD84A3E84DE67C886A4186E01F3ECA49B00641906FE38BB6B807E5D5B7`. The SR DLL in SDK 3.0.2 reports version 2.0.2.68, so do not swap one version label for the other. A valid Intel signature was found when the lock was prepared. Include the SDK's `LICENSE.txt` and `third-party-programs.txt` with any installation.

The same archive provides `bin/libxess_fg.dll` (1.3.1.78) and `bin/libxell.dll` (1.3.2.10), both mapped into `OptiScaler/`. AMD FG uses the signed `amd_fidelityfx_framegeneration_dx12.dll` from the SDK commit below (4.0.1.2740, SHA-256 `02297BEEDD285E822D3A64F314CF00FAF378DCEC0EDC47FF0C4DD71B3A8C2F18`), with its loader. The optional lock also downloads AMD's licence and third-party notice. This FG runtime is separate from the AMD upscaler DLL and does not enable FSR 4 on unsupported hardware.

Setup also installs this project's `LICENSE`, `NOTICE`, full `THIRD_PARTY_NOTICES.md`, `docs/LICENSING.md`, `docs/RELEASE-CHECKLIST.md`, `docs/UPSCALERS.md` and the `docs/licenses` collection under `BepInEx/BigWalkDLSS/notices`. If another mod still needs the shared BepInEx loader after this mod is removed, the notices stay.

OptiScaler statically includes FSR 3.1.2's native DX11 implementation. The FSR API bridge is separate. The [official AMD SDK commit](https://github.com/GPUOpen-LibrariesAndSDKs/FidelityFX-SDK/tree/60f4ea81909200d8542eca14dccb2628b763a9a3) contains `Kits/FidelityFX/signedbin/amd_fidelityfx_loader_dx12.dll` (2.3.0.2740, SHA-256 `E2D85AA05A9BD9ED8B38935FDF5199372CCA6F74C12015143BB6F945EE1608AA`) and `amd_fidelityfx_upscaler_dx12.dll` (4.1.1.2740, SHA-256 `D0DCCCC74A43C44BA435B7A369B456E0970D8A4464E4BD683119B374F2C9FB46`). Both signatures were valid when the lock was prepared. FSR 4 is not supported on the tested RTX 4090. The optional bridge downloads the loader (`amd_fsr_loader` in the lock). The upscaler DLL is a researched candidate only: Setup does not download it, and it is not claimed to work. The shipped dependency lock is the authoritative list of what Setup downloads.
