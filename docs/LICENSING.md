# Licensing assessment

Big Beautiful Walk uses mixed licence terms. Independently authored project material is licensed under the Apache License 2.0, as chosen by the project owner. The Unity adaptation, SDK material and dependencies keep their own licences.

This assessment separates three things: what the licence texts say, how this project reads them, and permissions that are not verified. It reflects the position on 2026-10-16. It is not blanket legal clearance.

## Apache-2.0 scope and history

[LICENSE](../LICENSE) is the complete, unmodified Apache License 2.0 from the Apache Software Foundation. [NOTICE](../NOTICE) holds the project attribution.

The Apache licence covers independently authored managed and native source, scripts, build files and documentation. It does not apply where an existing third-party notice says otherwise, or in these cases:

- `StockPostProcessing.cs` and its Unity-derived portions stay under the Unity Companion License, including in the managed DLL.
- NVIDIA NGX headers, inline helpers and linked SDK code stay under NVIDIA's licence. Incorporated Streamline MIT material stays under MIT. Our own portions of `Native.cpp` can be Apache-2.0, but the compiled DLL also includes material under other terms.
- The project LICENSE does not relicense dependencies, game materials, or the copied third-party licence and notice texts.

The earlier 0.6 candidate at `ccef38a` licensed independently authored material under MIT, with Unity and third-party exceptions. Its [historical licence](licenses/Big-Beautiful-Walk-0.6-MIT.txt) is kept. The Apache grant does not withdraw MIT rights already granted. Do not describe the whole repository or the binaries as licensed only under Apache.

## Unity adaptation

**Binding text:** section 1 of [UCL v1.4](https://unity.com/legal/licenses/unity-companion-license) makes its grant conditional on authoring or distribution connected with content under a valid Unity Engine License. Section 3.2 assigns derivative-work rights in the Work itself to Unity, or grants the fallback licence it specifies. Section 5 requires copyright and licence notices.

**Treatment:** keep the file's UCL header, upstream notice and licence, including with its compiled form. Owning Big Walk does not by itself show that the mod author holds an Engine License. The project does not claim a clean-room rewrite of this adaptation, or that the adaptation is relicensed under Apache.

**Status:** it is not established that section 1 covers this external mod. The adaptation keeps its UCL terms and is excluded from the Apache grant. No Unity support submission or licence-activation workflow is planned. This records an unresolved licence condition. It is not an approval request, and it does not claim the condition is met.

## NVIDIA SDK incorporation and attribution

**Binding text:** base section 1(c) of the pinned [DLSS licence](https://github.com/NVIDIA/DLSS/blob/374959484e79a640feaba44c93ac8cfb0a03f5b5/LICENSE.txt) allows incorporating the SDK into application object code, subject to its conditions. Section 2 requires material additional functionality, source-derived attribution, protective terms when distributing the SDK, and its other conditions. Section 4(b) prohibits redistributing the SDK on its own. Section 4(e) prohibits making SDK portions subject to the open-source obligations it describes. Section 8.2 keeps the developer's rights in its application, subject to NVIDIA's rights.

**Treatment:** the Apache grant excludes NVIDIA SDK portions. Runtime DLLs are downloaded separately, but the native DLL still links SDK code. The required source-code attribution is in NOTICE and THIRD_PARTY_NOTICES. There is also an in-app Credits menu and a page of [SDK-specific downstream terms](licenses/NVIDIA-DISTRIBUTION-NOTICE.md). Under supplement section 1, this build is intended for systems with an NVIDIA GPU, including when an optional AMD or Intel algorithm is selected. None of this proves that every required application location or Mark condition is met.

The supplement has several separate requirements:

| Clause | What the binding text covers | Interpretation and unverified points |
| --- | --- | --- |
| 4 | Notification before commercial release, expressly including a plug-in to a commercial application | Being a free mod does not clearly settle the wording about plug-ins to commercial games. Decide whether the clause applies, and notify if it does. The clause is not worded as general release approval or as mandatory submission of game content. |
| 7.1(a) | Identifying NVIDIA as SDK supplier on websites, printed collateral, trade-show displays and other retail packaging; prior written approval for such references | Assess how the release actually identifies NVIDIA as supplier or uses it in marketing. Do not read this as requiring approval for every factual source link or technical citation anywhere. Release-page wording still needs checking against the contract language. |
| 7.1(b) | DLSS/NGX attribution and NVIDIA Marks on splash screens, in an about box if present, and in game credits | A notice in the documentation does not by itself meet these location requirements. Decide how this applies to a mod that does not own the game's splash screen or credits. |
| 7.1(c) | Credit-screen or end-user-documentation treatment for SDKs other than the listed DLSS/RTX Video/NGX cases | Its fallback to documentation does not replace the different DLSS/NGX wording in 7.1(b). |
| 7.2(c) | Mark use guidelines, a sample of the proposed use at least two weeks before the desired implementation, and written approval; no reply after ten business days means not approved | Assess the NVIDIA Mark uses actually proposed. Leaving out a logo does not by itself remove textual Mark uses or mandatory attribution. |

**Release approach:**

- Keep NVIDIA's terms and attribution.
- Fetch the tested SR/FG runtimes from NVIDIA's own endpoints.
- Do not claim certification or endorsement.

No notification or Mark approval is recorded as completed, and no vendor-contact workflow is planned. This section records the outstanding contractual conditions. It does not present them as waived, and it does not turn private development or a factual list of sources into approval requests.

## OptiScaler and GPLv3

**Binding text:** the fork declares GPL-3.0. Section 5 separates combined works from aggregates of separate and independent works. Sections 5 and 6 impose whole-work and source conditions where they apply. The [Apache Software Foundation's compatibility guidance](https://www.apache.org/licenses/GPL-compatibility.html) explains that Apache-2.0 material may go into a GPLv3 work, but GPLv3 material cannot simply be relicensed as Apache-2.0.

**Packaging boundary:**

- OptiScaler is an optional, unchanged upstream download.
- Its source is not copied into, or statically linked with, our plugin.
- The core also works without OptiScaler, through NVIDIA's NGX interface. The optional replacement uses that interface plus additional camera parameters.
- OptiScaler keeps GPLv3, its authors' credits and exact source directions.

These are the concrete facts about separation. Dynamic loading or downloading alone is not a universal legal test of whether works are independent.

The release keeps each component's own grant instead of applying Apache to the whole dependency stack. If you repackage a GPL-covered combined work, satisfy GPLv3 for that work. Apache licensing of the original portions is compatible in that direction. A GPL declaration cannot override the separate SDK and UCL restrictions, or create a linking exception. The project does not claim exhaustive legal clearance.

The v1.2.1 release offers a [source archive with submodules](https://github.com/Skynizz/optiscaler-dlss5/releases/tag/v1.2.1). GitHub's automatic source archive leaves the submodules out. Direct inspection of the archive found:

- prebuilt Detours and FreeType libraries without their implementation sources;
- custom FSR DX11 libraries whose backend sources are not in the included SDK.

[The source-access guide](licenses/DEPENDENCY-SOURCES.md) records the exact archive hash, source directions and these gaps. The point is not whether someone can independently compile a bit-identical copy. It is whether the preferred source and build material needed to modify and rebuild the covered work are available. If you convey the DLL, use a GPL-compliant source-delivery method and make sure the corresponding source stays available. OptiScaler's GPL declaration does not turn proprietary SDK components into GPL material.

## BepInEx and LGPL libraries

BepInEx is LGPL-2.1-only. The Il2CppInterop package actually used is LGPL-3.0-only. HarmonyX is MIT. [The notices](../THIRD_PARTY_NOTICES.md) list the exact versions and sources found.

The managed plugin references the installed shared libraries and does not embed them. LGPL-3.0 section 4 allows combined works under terms of your choice, subject to its notice, licence, relinking and shared-library conditions. Keep interface-compatible replacement possible, along with the debugging and reverse-engineering permissions that apply. The libraries themselves do not become Apache-2.0.

Build-755's archive contains no licence or notice files. The project's accompanying notices supply the verified main licence texts and credits. The [.NET and transitive dependency inventory](licenses/BepInEx-DEPENDENCIES.md) records the assembly metadata found and the remaining limits on native-binary provenance. Anyone who later repackages these files must check every binary they actually redistribute, and supply corresponding source where required. Being able to download directly from upstream is not blanket permission to repackage later.

## Intel and AMD

**Intel binding text:** the [pinned licence](https://github.com/intel/xess/blob/8fe81bdbbaf00b3c1b733fd0d830c333dc84e6f0/LICENSE.txt) allows use and redistribution of the binaries without modification, with notice and non-endorsement conditions. It prohibits altering the software or its operation, including during execution. Separate third-party terms also apply. The same package covers the SR, FG and XeLL files.

**Integration:** the pinned [XeSS proxy](https://github.com/Skynizz/optiscaler-dlss5/blob/6bfc4389c25054ef6747df6e6b1b470e9541c4da/OptiScaler/proxies/XeSS_Proxy.h#L429) and [XeLL hook](https://github.com/Skynizz/optiscaler-dlss5/blob/6bfc4389c25054ef6747df6e6b1b470e9541c4da/OptiScaler/hooks/Xell_Hooks.cpp#L11) attach Detours to Intel exports. Detours modifies target instructions in memory. So unchanged file hashes on disk do not by themselves answer Intel's condition against altering operation. Host-side DLL selection, reference counting and ordinary API calls are different from that binary patching, and are not automatically prohibited modifications.

Intel's own [SDK maintainer directs OptiScaler users to official XeLL/XeFG binaries and invites cross-vendor issue reports](https://github.com/intel/xess/issues/55#issuecomment-4024599304). That supports ordinary API integration, but it is not a blanket waiver for every hook. This release uses the tested upstream OptiScaler binary and the original Intel downloads. It does not ship a custom fork with the hooks removed, and it does not claim an exception from Intel. The specific question about detours and altered operation is a documented limitation of that upstream integration.

**AMD binding text and treatment:** older FidelityFX source is under MIT terms. The newer SDK's [binary exceptions](licenses/AMD-FSR-SDK-LICENSE.md) separately allow redistribution of unmodified binaries, on conditions, with [notices](licenses/AMD-FSR-SDK-THIRD-PARTY.md). The signed loader, upscaler and FG binaries do not become MIT or Apache just because other SDK files are open source. Hardware and API compatibility is a separate technical matter.

## NVIDIA Reflex attribution and integration

The optional pack includes `sl.reflex.dll` and its [separate licence](licenses/NVIDIA-Reflex-LICENSE.txt). Reflex supplement section 3.1 requires feature attribution that follows the supplied UI guidelines. It does not contain the RTX supplement's two-week sample and prior-approval procedure. Do not merge these different terms into one blanket approval rule.

The pinned [NVIDIA FG provider](https://github.com/Skynizz/optiscaler-dlss5/blob/6bfc4389c25054ef6747df6e6b1b470e9541c4da/OptiScaler/framegen/dlssg/DLSSG_Dx12.cpp#L300) requests Reflex low-latency mode. Home and Credits name **NVIDIA Reflex Low Latency**, and they distinguish a request from a verified runtime state. The [RTX UI guidelines](https://github.com/NVIDIA-RTX/Streamline/blob/2122257e0fce486f91b385aa63b9a09b0a34b363/docs/RTX%20UI%20Developer%20Guidelines.pdf), including FG/Reflex pairing and feature naming, still need a concrete check against the integration and UI. Installing a DLL or adding a credit does not prove that Reflex is operating.

## Neural Rendering model

The optional OptiScaler fork does not include `nvngx_dlssnr.dll`. Setup's separate NR option downloads the pinned SF-v2 model from RankFTW/RHI's community mirror. Its bytes match the model previously tested in Big Walk. The model is not in this project's repository or ZIP, is unsigned, and has no licence file in its archive.

[The provenance notice](licenses/RHI-Neural-Rendering-NOTICE.md) records the exact source and hashes, and that no NVIDIA distribution grant is established. Community availability and RHI's GPL source licence do not relicense vendor or model assets. The project does not claim vendor approval, or compatibility with an announced newer model.

## Release records

[SOURCES.md](licenses/SOURCES.md) records where each saved licence came from and its hash. [RELEASE-CHECKLIST.md](RELEASE-CHECKLIST.md) separates unfinished release conditions from technical observations. The private 0.7 candidate is not a public release. It asserts no vendor approvals and no exhaustive legal clearance.

The [official multiplayer EULA](https://bigwalk.game/eula/) prohibits disrupting other players and infringing their rights. The official game pages reviewed did not show express permission for rendering mods, or a blanket ban on mods. This project keeps its unofficial, non-endorsement statement. It excludes game assets, game assemblies and generated interop files. The project does not read the fan-merchandise policy as adding any House House approval requirement.
