# Third-party notices

Big Beautiful Walk is an unofficial mod for Big Walk by House House. House House, Unity, NVIDIA, Intel and AMD do not sponsor or endorse it. Independently authored project files are under [Apache-2.0](LICENSE); the game and third-party material retain their own licenses. A compiled plugin can contain material under several licenses. See [the licensing assessment](docs/LICENSING.md) for scope, conditions and unresolved questions.

## Unity Graphics adaptation

`src/managed/StockPostProcessing.cs` adapts post-processing sequencing from Unity's Universal Render Pipeline, including [PostProcessPassRenderGraph.cs](https://github.com/Unity-Technologies/Graphics/blob/6000.3/staging/Packages/com.unity.render-pipelines.universal/Runtime/Passes/PostProcessPassRenderGraph.cs). This file and its Unity-derived portions are covered by the Unity Companion License, outside the project's Apache-2.0 grant. Its existing copyright/license header is retained; no clean-room rewrite is claimed.

Copyright © 2020 Unity Technologies ApS.

The upstream [package notice](docs/licenses/Unity-Graphics-NOTICE.md), [URP third-party notices](docs/licenses/Unity-URP-THIRD-PARTY.md) and [Unity Companion License](docs/licenses/Unity-Companion-License.txt) accompany this source and its compiled form. Its Engine License condition is not established merely by owning the game. The original adaptation commit was not recorded: the referenced mutable branch resolved to `2d2e78cc9d6254bc6e7c9c5552cea053508e86cb` on 2026-10-16, which is an audit reference, not a claim of the adaptation's exact source revision.

## NVIDIA DLSS / NGX

The native bridge uses NVIDIA's DLSS/NGX SDK headers, inline helper code and linked `nvsdk_ngx_d.lib` code. This software contains source code provided by NVIDIA Corporation.

Copyright and all other rights in NVIDIA's SDK components remain with NVIDIA and its suppliers. They are governed by the [NVIDIA RTX SDKs license](docs/licenses/NVIDIA-RTX-SDK.txt), including its supplement. The mod's license does not relicense these components. DLSS, NVIDIA RTX and NVIDIA GeForce RTX are NVIDIA marks.

The standard installer downloads the unmodified NVIDIA-signed SR runtime 310.9.1 directly from an immutable [official NVIDIA SDK commit](https://github.com/NVIDIA/DLSS/tree/374959484e79a640feaba44c93ac8cfb0a03f5b5). All 23 cached NGX headers were byte-for-byte compared with that commit; this does not prove provenance of every historical binary. Runtime DLLs are absent from the release ZIP and repository, but the compiled native DLL still contains SDK material. Downloading separately does not waive NVIDIA's conditions. See [release checks](docs/RELEASE-CHECKLIST.md).

## BepInEx and its dependencies

The installer downloads [BepInEx 6 build 755](https://builds.bepinex.dev/projects/bepinex_be/755), based on [commit `3fab71a1914132a1ce3a545caf3192da603f2258`](https://github.com/BepInEx/BepInEx/tree/3fab71a1914132a1ce3a545caf3192da603f2258), directly from its build service. Its pinned source declares LGPL-2.1-only and Copyright © 2022 BepInEx Team; the [full license](docs/licenses/BepInEx-LICENSE.txt) is included. Its archive has separately licensed components, outside our Apache-2.0 grant.

The managed plugin uses these separately installed libraries, confirmed from build-755 assembly metadata:

| Component | Authors, version and source | Terms |
| --- | --- | --- |
| Il2CppInterop | knah, BepInEx et al.; `1.5.1-ci.829`, commit [`6d9007c18cc8440830379c5e1d5714085e7ec577`](https://github.com/BepInEx/Il2CppInterop/tree/6d9007c18cc8440830379c5e1d5714085e7ec577), also confirmed by the NuGet package metadata | [LGPL-3.0-only, including GPLv3 text](docs/licenses/Il2CppInterop-LICENSE.txt) |
| HarmonyX (`0Harmony.dll`) | Andreas Pardeike, Geoffrey Horsington, ManlyMarco et al.; `2.10.2`, commit [`97b2a2e9cecd6d0c757da4dbb696abaff9163faa`](https://github.com/BepInEx/HarmonyX/tree/97b2a2e9cecd6d0c757da4dbb696abaff9163faa); license copyright (c) 2020 BepInEx | [MIT](docs/licenses/HarmonyX-LICENSE.txt) |

BepInEx, its .NET runtime and other dependency DLLs are absent from the project release ZIP/repository. Build-755's archive itself had no license/notice files; accompanying project copies preserve the principal verified texts, including [Microsoft's license](docs/licenses/DotNet-6.0.7-LICENSE.txt) and [third-party notices](docs/licenses/DotNet-6.0.7-THIRD-PARTY-NOTICES.txt). Dobby, by jmpews and contributors with the BepInEx fork, retains its [Apache-2.0 licence](docs/licenses/Dobby-LICENSE.txt); the loader DLL matches the fork's v1.0.5 Windows release. [The dependency inventory](docs/licenses/BepInEx-DEPENDENCIES.md) records further credits and provenance. Preserve notices and interface-compatible library replacement; a direct download is not blanket permission for future repackaging. [Source access and redistribution guidance](docs/licenses/DEPENDENCY-SOURCES.md) accompany these notices.

## Optional OptiScaler, Streamline and Neural Rendering

The optional bridge uses [Skynizz's OptiScaler-DLSS5 v1.2.1](https://github.com/Skynizz/optiscaler-dlss5/releases/tag/v1.2.1), commit [`6bfc4389c25054ef6747df6e6b1b470e9541c4da`](https://github.com/Skynizz/optiscaler-dlss5/tree/6bfc4389c25054ef6747df6e6b1b470e9541c4da). It credits cdozdil and the OptiScaler team, Dagherbou for Neural Rendering integration and Skynizz for this fork's optimizations. Its [retained original README](https://github.com/Skynizz/optiscaler-dlss5/blob/6bfc4389c25054ef6747df6e6b1b470e9541c4da/README_OptiScaler.md) credits PotatoOfDoom's CyberFSR2 and other contributors.

Upstream declares [GPL-3.0](docs/licenses/OptiScaler-LICENSE.txt) and offers a [source archive including submodules](https://github.com/Skynizz/optiscaler-dlss5/releases/download/v1.2.1/OptiScaler-DLSS5-1.2.1-source.zip). Preferred-source/build completeness for linked precompiled libraries remains unverified; the archive's label is not proof of complete corresponding source. Automatic GitHub source archives omit submodules. This project does not bundle the DLL; its optional installer downloads the pinned archive and selected runtime dependencies. Dynamic loading and separate downloading do not by themselves settle the GPL combined-work boundary; [the assessment](docs/LICENSING.md#optiscaler-and-gplv3) records the remaining question.

The fork incorporates RenoDX colour composition by clshortfuse (Carlos Lopez Jr.), Copyright (c) 2025 Carlos Lopez Jr., with [the full MIT attribution](docs/licenses/OptiScaler-RenoDX-ATTRIBUTION.txt). It also uses Dear ImGui by Omar Cornut under [MIT](docs/licenses/OptiScaler-Dear-ImGui-LICENSE.txt), FreeType 2.13.3 under the [FreeType License](docs/licenses/FreeType-2.13.3-FTL.txt), and Hack font v3.003 under [MIT/Bitstream Vera terms](docs/licenses/Hack-Font-LICENSE.md). The vendored font was byte-for-byte compared with upstream v3.003. Additional pinned library license copies and exact origins are in [the source inventory](docs/licenses/SOURCES.md).

Portions of this software are copyright © 2024 The FreeType Project (www.freetype.org). All rights reserved.

[NVIDIA Streamline 2.14.1](https://github.com/NVIDIA-RTX/Streamline/releases/tag/v2.14.1), tag commit `2122257e0fce486f91b385aa63b9a09b0a34b363`, is the official optional pin, with FG runtime 310.9.1. Its [root MIT license](docs/licenses/Streamline-LICENSE.txt), [third-party terms](docs/licenses/Streamline-THIRD-PARTY.md), [DLSS license](docs/licenses/Streamline-DLSS-LICENSE.txt) and [Reflex license](docs/licenses/NVIDIA-Reflex-LICENSE.txt) remain distinct; the Streamline MIT grant does not cover all included binaries. Our used headers carry Copyright (c) 2022-2023 NVIDIA CORPORATION and MIT terms.

Cached Streamline 2.14.16, FG 310.9.16 and NR 310.8.SF-v2 prototype files do not establish redistribution rights or official SDK provenance. The NR model is excluded from the release, dependency lock and automatic download; no license grant or model compatibility assurance is asserted.

OptiScaler also uses Microsoft Detours headers identifying version 4.0.1 and links a prebuilt Detours library. Preserve Microsoft's [MIT copyright and permission notice](docs/licenses/OptiScaler-Detours-LICENSE.md). The linked library's exact source/build correspondence remains unverified. Its version-check code incorporates nlohmann JSON 3.12.0, Copyright (c) 2013-2025 Niels Lohmann, under [MIT](docs/licenses/OptiScaler-nlohmann-json-LICENSE.txt); this is a separate notice from Streamline's older JSON attribution.

Its custom FSR libraries also have separately published OptiScaler backend projects. Retained AMD MIT texts cover [FSR SDK DX11](docs/licenses/OptiScaler-FSR-DX11-LICENSE.txt), Copyright © 2023 Advanced Micro Devices, Inc.; [FSR 2 DX11](docs/licenses/OptiScaler-FSR2-DX11-LICENSE.txt), Copyright (c) 2022–2023 Advanced Micro Devices, Inc.; and [FSR 2.1.2](docs/licenses/OptiScaler-FSR2-212-LICENSE.txt), Copyright (c) 2022 Advanced Micro Devices, Inc. The [source-access guide](docs/licenses/DEPENDENCY-SOURCES.md) gives concrete source/build routes while distinguishing candidate revisions from confirmed correspondence to the pinned prebuilt libraries. These MIT grants do not cover the separate newer AMD binary-only SDK components.

NVIDIA Reflex Low Latency is used by the optional NVIDIA FG route. The pinned bridge requests Reflex low-latency mode when enabling NVIDIA FG; this is a source-backed request, not proof that Reflex initialized in a particular launch. Preserve the [Reflex terms](docs/licenses/NVIDIA-Reflex-LICENSE.txt) and assess the applicable [RTX UI guidelines](https://github.com/NVIDIA-RTX/Streamline/blob/2122257e0fce486f91b385aa63b9a09b0a34b363/docs/RTX%20UI%20Developer%20Guidelines.pdf), including latency-feature attribution and FG pairing.

The bridge also incorporates AMD Anti-Lag 2 header code, Copyright (c) 2024 Advanced Micro Devices, Inc. All rights reserved., under its [MIT notice](docs/licenses/OptiScaler-AMD-AntiLag2-LICENSE.txt), and LatencyFleX header code, Copyright 2021 Tatsuyuki Ishi, under [Apache-2.0](LICENSE), with the [retained header notice](docs/licenses/OptiScaler-LatencyFleX-NOTICE.txt). These credits identify incorporated material; they do not assert that either latency mode is active in this mod.

## Game content

Big Walk, game assemblies, generated game interop assemblies and assets are not distributed. Building locally requires the developer's own installed game and generated interop references. Do not commit these files, copied game source or personal configuration/log dumps.

## Optional XeSS and AMD FSR runtimes

Intel's [XeSS SDK 3.0.2](https://github.com/intel/xess/tree/8fe81bdbbaf00b3c1b733fd0d830c333dc84e6f0), the version referenced by OptiScaler 1.2.1, supplies `libxess.dll` (reported SR runtime version 2.0.2.68), `libxess_fg.dll` and `libxell.dll`. Intel(R) Xe Super Sampling (XeSS) SDK: Copyright (C) 2025 Intel Corporation. Its [Intel Simplified Software License](docs/licenses/Intel-XeSS-LICENSE.txt) permits unmodified binary redistribution with notices and contains restrictions, including alteration of operation. A separate download or intact signature does not establish that every integration satisfies those terms. Its [SDK third-party notices](docs/licenses/Intel-XeSS-THIRD-PARTY.txt) are preserved.

AMD's [FSR SDK 2.3.0](https://github.com/GPUOpen-LibrariesAndSDKs/FidelityFX-SDK/tree/60f4ea81909200d8542eca14dccb2628b763a9a3), also referenced by OptiScaler 1.2.1, supplies signed loader, upscaler and frame-generation binaries under its [binary redistribution terms and license exceptions](docs/licenses/AMD-FSR-SDK-LICENSE.md), with [third-party notices](docs/licenses/AMD-FSR-SDK-THIRD-PARTY.md). These proprietary binaries are outside the project's Apache-2.0 grant. The dependency lock determines which are selected; an SDK credit does not establish feature/hardware compatibility.

OptiScaler's native DX11 FSR 3.1.2 upscaler is compiled into its GPL-licensed DLL and needs no additional AMD runtime DLL for that upscaler. This is distinct from external FSR API / FSR 4 / frame-generation binaries. Its older FidelityFX SDK source submodule [`c6efa6bf7f2027b3ec94f28578bb5965eabb9e55`](https://github.com/GPUOpen-LibrariesAndSDKs/FidelityFX-SDK/tree/c6efa6bf7f2027b3ec94f28578bb5965eabb9e55) has [MIT terms](docs/licenses/AMD-FidelityFX-1.1.4-LICENSE.txt), Copyright (C) 2024 Advanced Micro Devices, Inc.; this does not establish every precompiled library's source history. See [the feature matrix](docs/UPSCALERS.md) for tested scope.
