# Third-party notices

Big Beautiful Walk is an unofficial mod for Big Walk by House House. House House, Unity, NVIDIA, Intel and AMD do not sponsor or endorse it.

Independently authored project files are under [Apache-2.0](LICENSE). The game and third-party material keep their own licences. A compiled plugin can contain material under several licences. See [the licensing assessment](docs/LICENSING.md) for scope, conditions and unresolved questions.

## Unity Graphics adaptation

`src/managed/StockPostProcessing.cs` adapts post-processing sequencing from Unity's Universal Render Pipeline, including [PostProcessPassRenderGraph.cs](https://github.com/Unity-Technologies/Graphics/blob/6000.3/staging/Packages/com.unity.render-pipelines.universal/Runtime/Passes/PostProcessPassRenderGraph.cs). This file and its Unity-derived portions are under the Unity Companion License, outside the project's Apache-2.0 grant. The file keeps its existing copyright and licence header. The project does not claim a clean-room rewrite.

Copyright © 2020 Unity Technologies ApS.

The upstream [package notice](docs/licenses/Unity-Graphics-NOTICE.md), [URP third-party notices](docs/licenses/Unity-URP-THIRD-PARTY.md) and [Unity Companion License](docs/licenses/Unity-Companion-License.txt) come with this source and its compiled form. Owning the game does not by itself show that the licence's Engine License condition is met.

The Unity commit the adaptation was originally based on was not recorded. On 2026-10-16 the referenced branch, which can change, resolved to `2d2e78cc9d6254bc6e7c9c5552cea053508e86cb`. That commit is a reference point for audits. It is not a claim about the exact source revision the adaptation used.

## NVIDIA DLSS / NGX

The native bridge uses NVIDIA's DLSS/NGX SDK headers, inline helper code and linked `nvsdk_ngx_d.lib` code. This software contains source code provided by NVIDIA Corporation.

Copyright and all other rights in NVIDIA's SDK components remain with NVIDIA and its suppliers. They are governed by the [NVIDIA RTX SDKs license](docs/licenses/NVIDIA-RTX-SDK.txt), including its supplement. The mod's licence does not relicense these components. DLSS, NVIDIA RTX and NVIDIA GeForce RTX are NVIDIA marks.

The [NVIDIA distribution notice](docs/licenses/NVIDIA-DISTRIBUTION-NOTICE.md) attaches those SDK-specific terms to the incorporated NVIDIA portions, and leaves the separate Apache, GPL and LGPL grants in place. The current build contains NGX material and is intended for systems with an NVIDIA GPU, including when you select an optional AMD or Intel algorithm. The project does not claim any SDK exception for systems without an NVIDIA GPU.

The standard installer downloads the unmodified, NVIDIA-signed SR runtime 310.9.1 directly from an immutable [official NVIDIA SDK commit](https://github.com/NVIDIA/DLSS/tree/374959484e79a640feaba44c93ac8cfb0a03f5b5). All 23 cached NGX headers were compared byte for byte with that commit. That comparison does not prove the provenance of every historical binary.

Runtime DLLs are not in the release ZIP or the repository, but the compiled native DLL still contains SDK material. Downloading the runtimes separately does not waive NVIDIA's conditions. See [release checks](docs/RELEASE-CHECKLIST.md).

## BepInEx and its dependencies

The installer downloads [BepInEx 6 build 755](https://builds.bepinex.dev/projects/bepinex_be/755), based on [commit `3fab71a1914132a1ce3a545caf3192da603f2258`](https://github.com/BepInEx/BepInEx/tree/3fab71a1914132a1ce3a545caf3192da603f2258), directly from the BepInEx build service. Its pinned source declares LGPL-2.1-only and Copyright © 2022 BepInEx Team. The [full licence](docs/licenses/BepInEx-LICENSE.txt) is included. The BepInEx archive contains separately licensed components, which are outside our Apache-2.0 grant.

The managed plugin uses these separately installed libraries. The details below are confirmed from build-755 assembly metadata:

| Component | Authors, version and source | Terms |
| --- | --- | --- |
| Il2CppInterop | knah, BepInEx et al.; `1.5.1-ci.829`, commit [`6d9007c18cc8440830379c5e1d5714085e7ec577`](https://github.com/BepInEx/Il2CppInterop/tree/6d9007c18cc8440830379c5e1d5714085e7ec577), also confirmed by the NuGet package metadata | [LGPL-3.0-only, including GPLv3 text](docs/licenses/Il2CppInterop-LICENSE.txt) |
| HarmonyX (`0Harmony.dll`) | Andreas Pardeike, Geoffrey Horsington, ManlyMarco et al.; `2.10.2`, commit [`97b2a2e9cecd6d0c757da4dbb696abaff9163faa`](https://github.com/BepInEx/HarmonyX/tree/97b2a2e9cecd6d0c757da4dbb696abaff9163faa); license copyright (c) 2020 BepInEx | [MIT](docs/licenses/HarmonyX-LICENSE.txt) |

BepInEx, its .NET runtime and the other dependency DLLs are not in the project's release ZIP or repository. The build-755 archive itself had no licence or notice files. The project includes copies of the main verified texts instead, including [Microsoft's licence](docs/licenses/DotNet-6.0.7-LICENSE.txt) and [third-party notices](docs/licenses/DotNet-6.0.7-THIRD-PARTY-NOTICES.txt).

Dobby, by jmpews and contributors with the BepInEx fork, keeps its [Apache-2.0 licence](docs/licenses/Dobby-LICENSE.txt). The loader DLL matches the fork's v1.0.5 Windows release. [The dependency inventory](docs/licenses/BepInEx-DEPENDENCIES.md) records further credits and provenance.

Keep the notices, and keep interface-compatible library replacement possible. A direct download is not blanket permission for future repackaging. [Source access and redistribution guidance](docs/licenses/DEPENDENCY-SOURCES.md) comes with these notices.

## Optional OptiScaler, Streamline and Neural Rendering

The optional bridge uses [Skynizz's OptiScaler-DLSS5 v1.2.1](https://github.com/Skynizz/optiscaler-dlss5/releases/tag/v1.2.1), commit [`6bfc4389c25054ef6747df6e6b1b470e9541c4da`](https://github.com/Skynizz/optiscaler-dlss5/tree/6bfc4389c25054ef6747df6e6b1b470e9541c4da). It credits cdozdil and the OptiScaler team, Dagherbou for Neural Rendering integration and Skynizz for this fork's optimizations. The fork's [original README](https://github.com/Skynizz/optiscaler-dlss5/blob/6bfc4389c25054ef6747df6e6b1b470e9541c4da/README_OptiScaler.md) credits PotatoOfDoom's CyberFSR2 and other contributors.

Upstream declares [GPL-3.0](docs/licenses/OptiScaler-LICENSE.txt) and offers a [source archive including submodules](https://github.com/Skynizz/optiscaler-dlss5/releases/download/v1.2.1/OptiScaler-DLSS5-1.2.1-source.zip). Automatic GitHub source archives leave submodules out. It is not verified that the preferred source and build material for the linked precompiled libraries is complete. The archive's label does not prove that it is complete corresponding source.

This project does not bundle the DLL. Its optional installer downloads the pinned archive and selected runtime dependencies. Dynamic loading and separate downloading do not by themselves settle the GPL combined-work boundary. [The assessment](docs/LICENSING.md#optiscaler-and-gplv3) records this open question.

The fork incorporates RenoDX colour composition by clshortfuse (Carlos Lopez Jr.), Copyright (c) 2025 Carlos Lopez Jr., with [the full MIT attribution](docs/licenses/OptiScaler-RenoDX-ATTRIBUTION.txt). It also uses Dear ImGui by Omar Cornut under [MIT](docs/licenses/OptiScaler-Dear-ImGui-LICENSE.txt), FreeType 2.13.3 under the [FreeType License](docs/licenses/FreeType-2.13.3-FTL.txt), and Hack font v3.003 under [MIT/Bitstream Vera terms](docs/licenses/Hack-Font-LICENSE.md). The vendored font was compared byte for byte with upstream v3.003. More pinned library licence copies and their exact origins are in [the source inventory](docs/licenses/SOURCES.md).

Portions of this software are copyright © 2024 The FreeType Project (www.freetype.org). All rights reserved.

[NVIDIA Streamline 2.14.1](https://github.com/NVIDIA-RTX/Streamline/releases/tag/v2.14.1), tag commit `2122257e0fce486f91b385aa63b9a09b0a34b363`, is the official version pinned for the optional features, with FG runtime 310.9.1. Its [root MIT licence](docs/licenses/Streamline-LICENSE.txt), [third-party terms](docs/licenses/Streamline-THIRD-PARTY.md), [DLSS licence](docs/licenses/Streamline-DLSS-LICENSE.txt) and [Reflex licence](docs/licenses/NVIDIA-Reflex-LICENSE.txt) stay separate. The Streamline MIT grant does not cover all included binaries. The headers we use carry Copyright (c) 2022-2023 NVIDIA CORPORATION and MIT terms.

Cached Streamline 2.14.16 and FG 310.9.16 prototype files are not the official versions this installer pins.

The separate optional NR download uses RankFTW/RHI's community-hosted SF-v2 archive, and matches the model previously tested in Big Walk. It is unsigned, is not in this project's repository or ZIP, and carries no licence in its upstream archive. [The NR provenance notice](docs/licenses/RHI-Neural-Rendering-NOTICE.md) records its exact hashes and these limits. Neither RHI's GPL nor this project's Apache grant relicenses the vendor model. The project claims no NVIDIA authorisation for the model, and does not claim it is the latest model.

OptiScaler also uses Microsoft Detours headers that identify version 4.0.1, and links a prebuilt Detours library. Keep Microsoft's [MIT copyright and permission notice](docs/licenses/OptiScaler-Detours-LICENSE.md). The exact source and build that produced the linked library are not verified.

Its version-check code incorporates nlohmann JSON 3.12.0, Copyright (c) 2013-2025 Niels Lohmann, under [MIT](docs/licenses/OptiScaler-nlohmann-json-LICENSE.txt). This notice is separate from Streamline's older JSON attribution.

Its custom FSR libraries also have separately published OptiScaler backend projects. The included AMD MIT texts cover:

- [FSR SDK DX11](docs/licenses/OptiScaler-FSR-DX11-LICENSE.txt), Copyright © 2023 Advanced Micro Devices, Inc.
- [FSR 2 DX11](docs/licenses/OptiScaler-FSR2-DX11-LICENSE.txt), Copyright (c) 2022–2023 Advanced Micro Devices, Inc.
- [FSR 2.1.2](docs/licenses/OptiScaler-FSR2-212-LICENSE.txt), Copyright (c) 2022 Advanced Micro Devices, Inc.

The [source-access guide](docs/licenses/DEPENDENCY-SOURCES.md) gives concrete source and build routes. It separates candidate revisions from revisions confirmed to correspond to the pinned prebuilt libraries. These MIT grants do not cover the separate, newer AMD binary-only SDK components.

The optional NVIDIA FG route uses NVIDIA Reflex Low Latency. When it enables NVIDIA FG, the pinned bridge requests Reflex low-latency mode. The source shows this request; it does not prove that Reflex initialised in any particular launch. Keep the [Reflex terms](docs/licenses/NVIDIA-Reflex-LICENSE.txt), and assess the applicable [RTX UI guidelines](https://github.com/NVIDIA-RTX/Streamline/blob/2122257e0fce486f91b385aa63b9a09b0a34b363/docs/RTX%20UI%20Developer%20Guidelines.pdf), including latency-feature attribution and FG pairing.

The bridge also incorporates:

- AMD Anti-Lag 2 header code, Copyright (c) 2024 Advanced Micro Devices, Inc. All rights reserved., under its [MIT notice](docs/licenses/OptiScaler-AMD-AntiLag2-LICENSE.txt);
- LatencyFleX header code, Copyright 2021 Tatsuyuki Ishi, under [Apache-2.0](LICENSE), with the [retained header notice](docs/licenses/OptiScaler-LatencyFleX-NOTICE.txt).

These credits identify incorporated material. They do not claim that either latency mode is active in this mod.

## Game content

This project does not distribute Big Walk, game assemblies, generated game interop assemblies or assets. To build locally you need your own installed copy of the game and generated interop references. Do not commit these files, copied game source, or personal configuration or log dumps.

## Optional XeSS and AMD FSR runtimes

Intel's [XeSS SDK 3.0.2](https://github.com/intel/xess/tree/8fe81bdbbaf00b3c1b733fd0d830c333dc84e6f0), the version referenced by OptiScaler 1.2.1, supplies `libxess.dll` (reported SR runtime version 2.0.2.68), `libxess_fg.dll` and `libxell.dll`. Intel(R) Xe Super Sampling (XeSS) SDK: Copyright (C) 2025 Intel Corporation.

Its [Intel Simplified Software License](docs/licenses/Intel-XeSS-LICENSE.txt) allows redistribution of unmodified binaries with notices. It also contains restrictions, including on altering the software's operation. A separate download or an intact signature does not show that every integration meets those terms. Its [SDK third-party notices](docs/licenses/Intel-XeSS-THIRD-PARTY.txt) are included.

AMD's [FSR SDK 2.3.0](https://github.com/GPUOpen-LibrariesAndSDKs/FidelityFX-SDK/tree/60f4ea81909200d8542eca14dccb2628b763a9a3), also referenced by OptiScaler 1.2.1, supplies signed loader, upscaler and frame-generation binaries under its [binary redistribution terms and licence exceptions](docs/licenses/AMD-FSR-SDK-LICENSE.md), with [third-party notices](docs/licenses/AMD-FSR-SDK-THIRD-PARTY.md). These proprietary binaries are outside the project's Apache-2.0 grant. The dependency lock decides which of them are selected. An SDK credit does not show that a feature works on given hardware.

OptiScaler's native DX11 FSR 3.1.2 upscaler is compiled into its GPL-licensed DLL, so that upscaler needs no additional AMD runtime DLL. This is separate from the external FSR API, FSR 4 and frame-generation binaries. Its older FidelityFX SDK source submodule [`c6efa6bf7f2027b3ec94f28578bb5965eabb9e55`](https://github.com/GPUOpen-LibrariesAndSDKs/FidelityFX-SDK/tree/c6efa6bf7f2027b3ec94f28578bb5965eabb9e55) has [MIT terms](docs/licenses/AMD-FidelityFX-1.1.4-LICENSE.txt), Copyright (C) 2024 Advanced Micro Devices, Inc. That does not establish the source history of every precompiled library. See [the feature matrix](docs/UPSCALERS.md) for what was tested.
