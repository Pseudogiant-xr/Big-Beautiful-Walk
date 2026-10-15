# Third-party notices

Big Beautiful Walk is an unofficial mod. House House, Unity and NVIDIA do not sponsor or endorse it. The game and each dependency remain subject to their own licenses; the project's source license does not grant rights to them.

## Unity Graphics adaptation

`src/managed/StockPostProcessing.cs` adapts post-processing sequencing from Unity's Universal Render Pipeline, including [PostProcessPassRenderGraph.cs](https://github.com/Unity-Technologies/Graphics/blob/6000.3/staging/Packages/com.unity.render-pipelines.universal/Runtime/Passes/PostProcessPassRenderGraph.cs). This file and its Unity-derived portions are covered by the Unity Companion License, not the project's MIT license.

Copyright © 2020 Unity Technologies ApS.

The upstream [notice](docs/licenses/Unity-Graphics-NOTICE.md) and [Unity Companion License](docs/licenses/Unity-Companion-License.txt) accompany this source and its compiled form. The license limits use to the circumstances described in its Unity Companion Use clause. This notice does not establish that every proposed use satisfies those conditions.

## NVIDIA DLSS / NGX

The native bridge uses NVIDIA's DLSS/NGX SDK headers, helper code and static import/library code. This software contains source code provided by NVIDIA Corporation.

Copyright and all other rights in NVIDIA's SDK components remain with NVIDIA and its suppliers. They are governed by the [NVIDIA RTX SDKs license](docs/licenses/NVIDIA-RTX-SDK.txt), including its supplement. The mod's license does not relicense these components. DLSS, NVIDIA RTX and NVIDIA GeForce RTX are NVIDIA marks.

The standard installer downloads the unmodified NVIDIA-signed SR runtime directly from an immutable [official NVIDIA SDK commit](https://github.com/NVIDIA/DLSS/tree/374959484e79a640feaba44c93ac8cfb0a03f5b5). Runtime DLLs are absent from the release ZIP and repository. Downloading separately does not waive NVIDIA's SDK conditions. See [release checks](docs/RELEASE-CHECKLIST.md) for unresolved obligations.

## BepInEx and its dependencies

The installer downloads [BepInEx 6 build 755](https://builds.bepinex.dev/projects/bepinex_be), based on [commit 3fab71a](https://github.com/BepInEx/BepInEx/tree/3fab71a), directly from its build service. BepInEx is licensed under [LGPL 2.1](docs/licenses/BepInEx-LICENSE.txt). Its archive contains separately licensed components; downloading BepInEx does not make those components part of this project's MIT license.

The managed plugin uses the loader's separately installed [Il2CppInterop](https://github.com/BepInEx/Il2CppInterop) and [HarmonyX](https://github.com/BepInEx/HarmonyX) libraries. BepInEx, its .NET runtime, interop generator, HarmonyX and their dependencies are not bundled in the release ZIP or copied into this repository. Preserve their upstream notices when redistributing them.

## Optional OptiScaler, Streamline and Neural Rendering

The prototype used [Skynizz's OptiScaler-DLSS5 v1.2.1](https://github.com/Skynizz/optiscaler-dlss5/releases/tag/v1.2.1). Upstream identifies it as GPL-3.0 and provides the corresponding full source archive, including submodules, on that release page. Its credits include OptiScaler contributors, Dagherbou's Neural Rendering integration and clshortfuse's MIT-licensed RenoDX colour composition. Big Beautiful Walk does not bundle or automatically download that stack.

[NVIDIA Streamline](https://github.com/NVIDIA-RTX/Streamline) has its own source and third-party terms; its included DLSS/NGX components retain their NVIDIA licenses. Cached Streamline 2.14.16, FG 310.9.16 and NR 310.8.SF-v2 files used during prototype testing do not establish public redistribution rights or official SDK provenance. The NR model is excluded from both the release and the dependency lock. No license or compatibility claim is made for it.

## Game content

Big Walk, game assemblies, generated game interop assemblies and assets are not distributed. Building locally requires the developer's own installed game and generated interop references. Do not commit these files, copied game source or personal configuration/log dumps.

## Optional XeSS and AMD FSR runtimes

Intel's [XeSS SDK 3.0.2](https://github.com/intel/xess/tree/8fe81bdbbaf00b3c1b733fd0d830c333dc84e6f0), the version referenced by OptiScaler 1.2.1, supplies `libxess.dll` (reported SR runtime version 2.0.2.68). Its [Intel Simplified Software License](docs/licenses/Intel-XeSS-LICENSE.txt) permits unmodified binary redistribution with notices and contains restrictions, including alteration of operation. A separate download or intact signature does not establish that every integration satisfies those terms. Preserve its SDK third-party notices when installing or redistributing it.

AMD's [FSR SDK 2.3.0](https://github.com/GPUOpen-LibrariesAndSDKs/FidelityFX-SDK/tree/60f4ea81909200d8542eca14dccb2628b763a9a3), also referenced by OptiScaler 1.2.1, supplies signed loader and upscaler binaries under its [binary redistribution terms and license exceptions](docs/licenses/AMD-FSR-SDK-LICENSE.md), with [third-party notices](docs/licenses/AMD-FSR-SDK-THIRD-PARTY.md). These proprietary binaries are not covered by this project's MIT license. Their availability does not remove AMD's hardware requirements or prove compatibility with Big Walk.

OptiScaler's native DX11 FSR 3.1.2 backend is compiled into its GPL-licensed DLL and needs no additional AMD runtime DLL. That is distinct from the separately supplied FSR API / FSR 4 binaries. See [the feature matrix](docs/UPSCALERS.md) for the tested scope.
