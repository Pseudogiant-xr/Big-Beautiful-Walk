# Dependency sources and policy

The installer consumes `packaging/dependencies.lock.json` with `SchemaVersion: 1`. It accepts only the pinned HTTPS URL and SHA-256 for each dependency; a hash verifies the selected bytes, not legal permission or compatibility. `Files` explicitly maps archive members to paths relative to the game folder. The release ZIP carries none of these runtime downloads.

## Core downloads

| ID | Version / source | SHA-256 |
| --- | --- | --- |
| `bepinex` | BepInEx Unity IL2CPP Windows x64, 6.0.0-be.755+3fab71a; official build service | `3616D6A67F5F595973EC4AA7BD7EDAF7F799D5BB9926F7146A6DCC7B4ABF478F` |
| `nvngx_dlss` | NVIDIA official SDK 310.9.1; immutable commit `374959484e79a640feaba44c93ac8cfb0a03f5b5` | `3975567B8943C53ACCE397F2B72380092F84F162D00B0D2C7D08A1025C563983` |

The [lock file](../packaging/dependencies.lock.json) contains the exact download URLs, source and license links. The SR file's reported version was `310,9,1,0`, and Windows signature verification reported a valid NVIDIA Corporation signature during preparation. The downloaded BepInEx archive was inspected to produce its 227 explicit file mappings.

## Optional FG and NR

Setup's optional upscaler/FG checkbox downloads the pinned OptiScaler proxy, production Streamline/FG files, AMD FidelityFX loader/FG and Intel XeSS SR/FG plus XeLL runtimes. The direct core install remains DLSS-only. The optional fresh configuration leaves FG and NR off, selects DLSS as the initial renderer and lets Home provide model-preset requests. Version 0.7 adds saved Home FG/NR profiles; individual RTX 4090 observations and performance limitations are in [the FG / NR guide](FRAME-GENERATION.md). They do not validate every backend combination or a fresh loader bootstrap.

The 0.6 official NVIDIA optional FG set produced generated-frame presents in-world on the tested setup. FSR 3.1 initialized, sustained reconstruction and was visually confirmed. Neural Rendering still requires a separately obtained model; setup supplies its forwarder from the pinned OptiScaler archive, without a model. Existing conflicting injector stacks may require their own removal before this install can proceed.

The known prototype used Skynizz OptiScaler-DLSS5 v1.2.1, Streamline 2.14.16, FG 310.9.16 and an NR 310.8.SF-v2 model. These version numbers identify that experiment; they are not a recommendation to download arbitrary copies. The public NVIDIA Streamline release found during preparation was 2.14.1, and the official public DLSS SDK was 310.9.1. Those are different bytes from the cached experiment.

Skynizz's [official v1.2.1 release](https://github.com/Skynizz/optiscaler-dlss5/releases/tag/v1.2.1) provides its own binary/source archives and checksums. Its binary ZIP SHA-256 is `0D5B24887EA2C622754DA68EFC76EF55AE42B5D3BE350A8D43CAFBABB6747EC6`; the contained `OptiScaler.dll` SHA-256 is `AD143C5BD6E5FAB4D1ABD8B048C424E9DDCEABF1A864E0E0695C01A0DC82B4C9`. Upstream explicitly excludes NVIDIA's NR model.

Only supply additional proprietary runtimes when you have obtained them under terms permitting your intended use. No verified public authorized source for the prototype NR model was established. This project therefore provides no NR model download, mirror, extraction instructions or redistribution claim. Retain existing files and settings rather than overwriting another mod's setup. In the prototype configuration, **F10** opened OptiScaler; a separately configured installation can use a different shortcut.

## Updating a lock

A maintainer should retrieve the exact upstream version, compute its SHA-256, inspect archive member paths and licenses, then test installation/removal in a disposable fixture and rendering in the real game. Pin immutable commits where available. Updating a hash or passing a fixture test does not establish real-game compatibility. Do not substitute cached user files, game assets or unlicensed models for a verified upstream download.

## Additional upscaler sources

The optional XeSS candidate is Intel SDK 3.0.2, exactly the submodule commit referenced by OptiScaler 1.2.1: `8fe81bdbbaf00b3c1b733fd0d830c333dc84e6f0`. Its [official release ZIP](https://github.com/intel/xess/releases/download/v3.0.2/XeSS_SDK_3.0.2.zip) has SHA-256 `88B8A373F30E33F3558A77A93E634F11B8132FC3047EA1A8EDEEAD32B8471990`. The required SR binary is `bin/libxess.dll`, mapped to `OptiScaler/libxess.dll`; its SHA-256 is `251659DD84A3E84DE67C886A4186E01F3ECA49B00641906FE38BB6B807E5D5B7`. SDK 3.0.2's SR DLL reports version 2.0.2.68, so those version labels should not be substituted for each other. The preparation check found a valid Intel signature. Include the SDK's `LICENSE.txt` and `third-party-programs.txt` alongside an installation.

The same archive provides `bin/libxess_fg.dll` (1.3.1.78) and `bin/libxell.dll` (1.3.2.10), mapped into `OptiScaler/`. AMD FG uses the SDK commit below's signed `amd_fidelityfx_framegeneration_dx12.dll` (4.0.1.2740, SHA-256 `02297BEEDD285E822D3A64F314CF00FAF378DCEC0EDC47FF0C4DD71B3A8C2F18`) with its loader. The optional lock includes AMD's licence and third-party notice downloads. This FG runtime is separate from the AMD upscaler DLL and does not enable FSR 4 on unsupported hardware.

Setup also installs this project's `LICENSE`, `NOTICE`, full `THIRD_PARTY_NOTICES.md` and the `docs/licenses` collection under `BepInEx/BigWalkDLSS/notices`. Notices are retained if another mod still needs the shared BepInEx loader after this mod is removed.

FSR 3.1.2's native DX11 implementation is statically included in OptiScaler. The FSR API bridge is separate: the [official AMD SDK commit](https://github.com/GPUOpen-LibrariesAndSDKs/FidelityFX-SDK/tree/60f4ea81909200d8542eca14dccb2628b763a9a3) contains `Kits/FidelityFX/signedbin/amd_fidelityfx_loader_dx12.dll` (2.3.0.2740, SHA-256 `E2D85AA05A9BD9ED8B38935FDF5199372CCA6F74C12015143BB6F945EE1608AA`) and `amd_fidelityfx_upscaler_dx12.dll` (4.1.1.2740, SHA-256 `D0DCCCC74A43C44BA435B7A369B456E0970D8A4464E4BD683119B374F2C9FB46`). Both signatures were valid during preparation. FSR 4 is unsupported on the observed RTX 4090; these files are researched candidates, not a working claim or an automatic download promise. The shipped dependency lock is the authoritative list of what Setup downloads.
