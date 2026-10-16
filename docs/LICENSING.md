# Licensing assessment

Independently authored Big Beautiful Walk material is offered under Apache License 2.0 at the owner's request. The project remains subject to mixed terms: the Unity adaptation, SDK material and dependencies keep their licenses. This assessment distinguishes primary text, interpretation and unverified permissions as of 2026-10-16; it is not blanket legal clearance.

## Apache-2.0 scope and history

[LICENSE](../LICENSE) is the complete, unmodified Apache License 2.0 from the Apache Software Foundation; [NOTICE](../NOTICE) carries project attribution. Independently authored managed/native source, scripts, build files and documentation are covered, except where an existing third-party notice or these exceptions say otherwise:

- `StockPostProcessing.cs` and its Unity-derived portions remain under the Unity Companion License, including in the managed DLL.
- NVIDIA NGX headers, inline helpers and linked SDK code remain under NVIDIA's license; incorporated Streamline MIT material retains MIT. Our authored portions of `Native.cpp` can be Apache-2.0 while the compiled DLL includes material under other terms.
- Dependencies, game materials and copied third-party license/notice texts are not relicensed by the project LICENSE.

The earlier 0.6 candidate at `ccef38a` used MIT for independently authored material with Unity/third-party exceptions. Its [historical license](licenses/Big-Beautiful-Walk-0.6-MIT.txt) is preserved. The Apache grant does not withdraw previously granted MIT rights. Do not describe the entire repository or binaries as exclusively Apache-licensed.

## Unity adaptation

**Binding text:** [UCL v1.4](https://unity.com/legal/licenses/unity-companion-license) section 1 conditions its grant on authoring/distribution connected with content under a valid Unity Engine License. Section 3.2 assigns derivative-work rights in the Work itself to Unity or grants the specified fallback license; section 5 requires copyright/license notices.

**Treatment:** retain the file's UCL header, upstream notice and license, including alongside its compiled form. Owning Big Walk alone does not demonstrate the mod author's Engine License entitlement. No clean-room rewrite or Apache relicensing of this adaptation is claimed.

**Status:** coverage of this external mod under section 1 has not been established. The adaptation retains its UCL terms and is excluded from the Apache grant. No Unity support submission or licence-activation workflow is planned. This records an unresolved licence condition, rather than an approval request or a claim that the condition has been satisfied.

## NVIDIA SDK incorporation and attribution

**Binding text:** the pinned [DLSS license](https://github.com/NVIDIA/DLSS/blob/374959484e79a640feaba44c93ac8cfb0a03f5b5/LICENSE.txt), base section 1(c), allows SDK incorporation in application object code subject to its conditions. Section 2 requires material additional functionality, source-derived attribution, protective terms for SDK distribution and its other conditions. Section 4(b) prohibits standalone SDK redistribution; 4(e) prohibits making SDK portions subject to the described open-source obligations. Section 8.2 preserves developer application rights subject to NVIDIA's rights.

**Treatment:** the Apache grant excludes NVIDIA SDK portions. The native DLL still links SDK code even though runtime DLLs are downloaded separately. The required source-code attribution is in NOTICE and THIRD_PARTY_NOTICES, with an in-app Credits menu and [SDK-specific downstream terms](licenses/NVIDIA-DISTRIBUTION-NOTICE.md). This build is intended for systems with an NVIDIA GPU under supplement section 1, including when an optional AMD/Intel algorithm is selected. This is not proof of every required application location or Mark condition.

The supplement has several distinct requirements:

| Clause | Scope of binding text | Interpretation / unverified point |
| --- | --- | --- |
| 4 | Notification before commercial release, expressly including a plug-in to a commercial application | A free mod does not conclusively settle the commercial-game plug-in wording. Determine applicability; if applicable, notify. This is not phrased as general release approval or mandatory submission of game content. |
| 7.1(a) | Identifying NVIDIA as SDK supplier on websites, printed collateral, trade-show displays and other retail packaging; prior written approval for such references | Assess the actual supplier-identification/marketing use. Do not expand this into a requirement to approve every factual source link or technical citation anywhere. Release-page wording still needs assessment against the contractual language. |
| 7.1(b) | DLSS/NGX attribution and NVIDIA Marks on splash screens, in an about box if present, and in game credits | A documentation notice alone does not establish satisfaction of these locations. Determine treatment for a mod that does not own the game's splash/credits. |
| 7.1(c) | Credit-screen or end-user-documentation treatment for SDKs other than the listed DLSS/RTX Video/NGX cases | Its documentation fallback does not replace the different DLSS/NGX wording in 7.1(b). |
| 7.2(c) | Mark use guidelines, proposed-use sample at least two weeks before desired implementation, written approval; silence after ten business days is unapproved | Assess actual proposed NVIDIA Mark uses. Omitting a logo does not automatically remove textual Mark uses or mandatory attribution. |

**Release approach:** preserve NVIDIA's terms and attribution, fetch the tested SR/FG runtimes from NVIDIA's own endpoints, and avoid claiming certification or endorsement. No notification or Mark approval is recorded as completed; no vendor-contact workflow is planned. This documents the outstanding contractual conditions without representing them as waived, or turning private development and factual source inventory into approval requests.

## OptiScaler and GPLv3

**Binding text:** the fork declares GPL-3.0; section 5 distinguishes combined works from aggregates of separate and independent works, and sections 5/6 impose whole-work/source conditions where applicable. The [Apache Software Foundation's compatibility guidance](https://www.apache.org/licenses/GPL-compatibility.html) explains that Apache-2.0 material may enter a GPLv3 work, but GPLv3 material cannot simply be relicensed as Apache-2.0.

**Packaging boundary:** OptiScaler is an optional, unchanged upstream download. Its source is not copied into or statically linked with our authored plugin. The core also works without OptiScaler, through NVIDIA's NGX interface; the optional replacement uses that interface and additional camera parameters. OptiScaler retains GPLv3, its authors' credits and exact source directions. These are the concrete separation facts; dynamic loading or downloading alone is not a universal legal test of independent works.

The release preserves each component's grant rather than applying Apache to the entire dependency stack. If repackaging a GPL-covered combined work, satisfy GPLv3 for that work; Apache licensing of original portions is compatible in that direction. A GPL declaration cannot override the separate SDK/UCL restrictions or manufacture a linking exception. No claim of exhaustive legal clearance is made.

The exact release offers a [source archive with submodules](https://github.com/Skynizz/optiscaler-dlss5/releases/tag/v1.2.1); GitHub's automatic source archive omits them. Direct inspection found prebuilt Detours/FreeType libraries without implementation sources, and custom FSR DX11 libraries without their backend sources in the included SDK. [The source-access guide](licenses/DEPENDENCY-SOURCES.md) records the exact archive hash, source directions and concrete gaps. This is not a demand for bit-identical independent compilation: the issue is the preferred source and build material needed to modify/rebuild the covered work. If conveying the DLL, use a GPL-compliant source-delivery method and ensure the corresponding source remains available. Its GPL declaration does not convert proprietary SDK components to GPL.

## BepInEx and LGPL libraries

BepInEx is LGPL-2.1-only; the actual Il2CppInterop package is LGPL-3.0-only; HarmonyX is MIT. Exact observed versions and sources are in [the notices](../THIRD_PARTY_NOTICES.md). The managed plugin references installed shared libraries and does not embed them. LGPL-3.0 section 4 permits combined works under chosen terms subject to notice/license and relinking/shared-library conditions. Preserve interface-compatible replacement and applicable debugging/reverse-engineering permissions; the libraries themselves do not become Apache-2.0.

Build-755's archive contains no license/notice files. Accompanying project notices now supply the verified principal texts and credits. Its [.NET and transitive inventory](licenses/BepInEx-DEPENDENCIES.md) records observed assembly metadata and remaining native provenance limits. A future repackager must check every binary actually redistributed and supply corresponding source where required; direct upstream downloading is not blanket permission for later repackaging.

## Intel and AMD

**Intel binding text:** the [pinned license](https://github.com/intel/xess/blob/8fe81bdbbaf00b3c1b733fd0d830c333dc84e6f0/LICENSE.txt) allows binary use/redistribution without modification, with notices and non-endorsement conditions, and prohibits alteration of the software or its operation, including during execution. Separate third-party terms also apply. The same package covers SR, FG and XeLL files.

**Observed integration:** the pinned [XeSS proxy](https://github.com/Skynizz/optiscaler-dlss5/blob/6bfc4389c25054ef6747df6e6b1b470e9541c4da/OptiScaler/proxies/XeSS_Proxy.h#L429) and [XeLL hook](https://github.com/Skynizz/optiscaler-dlss5/blob/6bfc4389c25054ef6747df6e6b1b470e9541c4da/OptiScaler/hooks/Xell_Hooks.cpp#L11) attach Detours to Intel exports. Detours modifies target instructions in memory, so unchanged disk hashes alone do not answer Intel's operation-alteration condition. Host-side DLL selection, reference counting and ordinary API calls are distinct from that binary patching; they are not automatically prohibited modifications.

Intel's own [SDK maintainer directs OptiScaler users to official XeLL/XeFG binaries and invites cross-vendor issue reports](https://github.com/intel/xess/issues/55#issuecomment-4024599304). That supports ordinary API integration, without constituting a blanket waiver for every hook. This release keeps the tested upstream OptiScaler binary and original Intel downloads; it does not ship a custom hook-removal fork or claim an Intel exception. The specific detour/operation question remains a documented limitation of that upstream integration.

**AMD binding text/treatment:** older FidelityFX source has MIT terms. Newer SDK [binary exceptions](licenses/AMD-FSR-SDK-LICENSE.md) separately grant conditional unmodified-binary redistribution with [notices](licenses/AMD-FSR-SDK-THIRD-PARTY.md). Signed loader/upscaler/FG binaries do not become MIT or Apache merely because other SDK files are open source. Hardware/API compatibility is a separate technical matter.

## NVIDIA Reflex attribution and integration

The optional pack includes `sl.reflex.dll` and its [separate licence](licenses/NVIDIA-Reflex-LICENSE.txt). Reflex supplement section 3.1 requires feature attribution following the supplied UI guidelines; it does not contain the RTX supplement's two-week sample/prior-approval procedure. Those distinct terms must not be collapsed into a blanket approval rule.

The pinned [NVIDIA FG provider](https://github.com/Skynizz/optiscaler-dlss5/blob/6bfc4389c25054ef6747df6e6b1b470e9541c4da/OptiScaler/framegen/dlssg/DLSSG_Dx12.cpp#L300) requests Reflex low-latency mode. Home and Credits name **NVIDIA Reflex Low Latency**, while distinguishing a request from verified runtime state. The [RTX UI guidelines](https://github.com/NVIDIA-RTX/Streamline/blob/2122257e0fce486f91b385aa63b9a09b0a34b363/docs/RTX%20UI%20Developer%20Guidelines.pdf), including FG/Reflex pairing and feature naming, still need a concrete integration/UI check; installing a DLL or adding a credit is not proof of successful Reflex operation.

## Neural Rendering model

The optional OptiScaler fork excludes `nvngx_dlssnr.dll`. Setup's separate NR option retrieves the pinned SF-v2 model from RankFTW/RHI's community mirror; its bytes match the model previously tested in Big Walk. The model remains outside this project's repository/ZIP, is unsigned and has no licence file in its archive. [The provenance notice](licenses/RHI-Neural-Rendering-NOTICE.md) records the exact source/hashes and the absence of an established NVIDIA distribution grant. Community availability and RHI's GPL source licence do not relicense vendor/model assets. No vendor approval or compatibility with an announced newer model is claimed.

## Release records

[SOURCES.md](licenses/SOURCES.md) records saved license origins/hashes. [RELEASE-CHECKLIST.md](RELEASE-CHECKLIST.md) separates unfinished release conditions from technical observations. The private 0.7 candidate is not a public release and has no asserted vendor approvals or exhaustive legal clearance.

The [official multiplayer EULA](https://bigwalk.game/eula/) prohibits disrupting other players and infringing their rights. The reviewed official game pages did not establish express rendering-mod permission or a blanket mod ban. This project retains its unofficial/non-endorsement statement and excludes game assets, game assemblies and generated interop files; no additional House House approval requirement is invented from the fan-merchandise policy.
