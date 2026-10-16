# Dependency source access and redistribution

These notes accompany the component credits and licence texts. Big Beautiful Walk's release ZIP includes its managed/native source, build files and installer source. Setup downloads the dependencies below from their upstream publishers; it does not take them from another player's installation. Dependency binaries are not included in this project's ZIP.

## Source locations

| Component | Version used | Preferred-source location / build information |
| --- | --- | --- |
| Big Beautiful Walk | Use the source accompanying the downloaded release | `src`, `scripts`, `tests` and `packaging` are included in the release. The repository's [build guide](https://github.com/Pseudogiant-xr/Big-Beautiful-Walk/blob/6166d55a9539f6e4ae45b66ec7fdfa62d66044b6/docs/BUILDING.md) explains the local game/SDK prerequisites; game and generated interop assemblies are excluded. |
| OptiScaler-DLSS5 | v1.2.1, `6bfc4389c25054ef6747df6e6b1b470e9541c4da` | [Pinned source tree](https://github.com/Skynizz/optiscaler-dlss5/tree/6bfc4389c25054ef6747df6e6b1b470e9541c4da), [explicit source-with-submodules archive](https://github.com/Skynizz/optiscaler-dlss5/releases/download/v1.2.1/OptiScaler-DLSS5-1.2.1-source.zip). Read the completeness limits below. |
| BepInEx | 6.0.0-be.755+3fab71a | [Source and build scripts](https://github.com/BepInEx/BepInEx/tree/3fab71a1914132a1ce3a545caf3192da603f2258), including the dependency download/packaging recipe in `build/Program.cs`. |
| Il2CppInterop | 1.5.1-ci.829 | [Source, solution and build scripts](https://github.com/BepInEx/Il2CppInterop/tree/6d9007c18cc8440830379c5e1d5714085e7ec577). |
| UnityDoorstop | v4.5.0 | [Source and build files](https://github.com/NeighTools/UnityDoorstop/tree/33dab9a6733862eb81869ff08431d9478b28784b). |
| Dobby, BepInEx fork | v1.0.5 | [Source and CMake build files](https://github.com/BepInEx/Dobby/tree/888d971214900374edbca6206fad6ded8a2c1311); [binary identity comparison](BepInEx-DEPENDENCIES.md). |
| Other loader libraries | Exact observed versions in the inventory | [BepInEx dependency inventory](BepInEx-DEPENDENCIES.md), with source commits, copyright holders and retained terms. |
| Optional NR model | SF-v2, file version `310.8.SF.0` | [RHI community source/provenance notice](RHI-Neural-Rendering-NOTICE.md). A binary model is not GPL source merely because a GPL installer lists its URL. |
| NVIDIA, AMD and Intel SDKs | Pins in the dependency lock and notices | [Component notices](../../THIRD_PARTY_NOTICES.md) identify official SDK source trees, binary-only portions and licence exceptions. Those exceptions remain in force; these SDKs are not all GPL or Apache material. |

OptiScaler's direct source archive is 537,700,869 bytes, SHA-256 `1EAAD2223E679A935426E922F86A0341232AE96641579FEC4A5B34004A1CD507`. GitHub's automatically generated source archives omit the nine submodules. To inspect the Git version instead, check out the exact commit **before** initializing its submodules:

```powershell
git clone --no-checkout https://github.com/Skynizz/optiscaler-dlss5.git
cd optiscaler-dlss5
git checkout --detach 6bfc4389c25054ef6747df6e6b1b470e9541c4da
git submodule update --init --recursive
```

## OptiScaler source completeness

The explicit source archive was inspected on 2026-10-16. It contains the main project and submodule content, but also linked prebuilt libraries. Source checksums retained in those libraries improve the mapping: all 415 tracked FreeType implementation/configuration inputs match [official 2.13.3](https://github.com/freetype/freetype/tree/42608f77f20749dd6ddc9e0536788eaad70ea4b5) after CRLF conversion; generated version resources are outside that comparison. Detours identifies 4.0.1, but its recorded `disasm.cpp` checksum was not matched to the public history checked. [The official implementation/build sources](https://github.com/microsoft/Detours/tree/e4bfd6b03e50de46b47abfbd1e46b384f0c5f833) remain a source route, rather than a claim that every vendored file is unmodified.

The release also links custom FSR DX11 libraries. The included FidelityFX SDK submodule supplies DX12/Vulkan backends and does not supply those DX11 sources; included SDK and OptiScaler upscaler headers report different FSR patch versions. OptiScaler's organization publishes separate backend source projects:

| Candidate source supplement | Version / build entry point | Retained terms |
| --- | --- | --- |
| [FidelityFX-SDK-DX11, `9b04fa49`](https://github.com/optiscaler/FidelityFX-SDK-DX11/tree/9b04fa49abae5aa341c0007af13c8ba1b4ad9913) | FSR 3.1.2, `_Fsr31` DX11 exports; `sdk/BuildFidelityFXDX11.bat` | [MIT, AMD copyright 2023](OptiScaler-FSR-DX11-LICENSE.txt) |
| [FidelityFX-FSR2-DX11, `f2e3f863`](https://github.com/optiscaler/FidelityFX-FSR2-DX11/tree/f2e3f86390746eb3f0bd1b28e91ea3cbc790ee76) | FSR 2.2.1; `build/BuildLibs.bat` | [MIT, AMD copyright 2022–2023](OptiScaler-FSR2-DX11-LICENSE.txt) |
| [FidelityFX-FSR2-212, `ce46c0fa`](https://github.com/optiscaler/FidelityFX-FSR2-212/tree/ce46c0fa359a1bdc4280d9550d0df011e0e6dd49) | FSR 2.1.2, renamed `Fsr212` exports/libraries; `build/BuildLibs.bat` | [MIT, AMD copyright 2022](OptiScaler-FSR2-212-LICENSE.txt) |

The FSR 2.1.2 paired debug-library checksums match the earlier [revision `026a9fc`](https://github.com/optiscaler/FidelityFX-FSR2-212/tree/026a9fc63123cb3fe7709a8a6e912579226c6c06), including a header later changed in the current candidate. FSR 3.1's tracked host/backend inputs match `9b04fa49`; generated shader headers remain outside the verified mapping, and some release correspondence is inferred from paired debug artifacts. Nine modified FSR 2.2.1 implementation/header inputs remain unmatched. These are limits on reproducing upstream's exact binaries, not a statement that the public source routes are absent.

These are actionable source locations, not confirmed build revisions for the prebuilt libraries in this release. Establish the actual revisions, patches and required build material before representing the archive plus supplements as complete corresponding source for the DLL. A byte-identical independently reproduced build is not the test of source completeness: the relevant issue is availability of the actual source and material needed to modify and rebuild the covered work.

## Recipients' rights

OptiScaler retains [GPLv3](OptiScaler-LICENSE.txt), BepInEx and UnityDoorstop retain their LGPL terms, and Il2CppInterop retains its [LGPLv3 terms](Il2CppInterop-LICENSE.txt). The project's Apache grant does not replace these licences. Recipients may exercise the rights granted by each component's own licence, including applicable modification, redistribution and interface-compatible shared-library replacement/debugging rights. There is no project-imposed ban on modifying these libraries or reverse engineering necessary to debug such modifications.

The managed plugin uses external loader libraries rather than embedding them. Setup's refusal to overwrite or uninstall bytes it no longer owns protects user changes; it is not a licence restriction on replacing those files. Preserve backups when testing your own builds, and review the installer diagnostics before a later repair.

## If redistributing dependency binaries

The current release supplies credits, source directions and direct upstream downloads. It does not offer a three-year written source promise or claim that a link alone discharges every distributor's obligations. If a future release mirrors or bundles GPL/LGPL binaries, use a source-delivery method permitted by the applicable licence, provide the matching preferred source and build/relinking material as required, retain licence/copyright notices, and verify source access alongside that exact binary release. For network distribution, GPLv3 section 6(d) addresses equivalent source access, including clear directions when another server hosts it.

A separate download is not by itself proof of independent works. The [licensing assessment](../LICENSING.md#optiscaler-and-gplv3) records the combination question and separate SDK restrictions. Do not mark that question resolved merely because credits or this guide have been added.
