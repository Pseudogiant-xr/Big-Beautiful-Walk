# Public release readiness

The alpha uses the tested rendering integration and upstream dependency downloads. It preserves component licences and credits, without a custom OptiScaler fork or a vendor-contact workflow. Publication remains a maintainer decision; this document does not claim exhaustive legal clearance or vendor approval.

## Rights and attribution

| Area | Delivered treatment | Scope / known limitation |
| --- | --- | --- |
| Original code | Apache-2.0 grant, scoped NOTICE and historical MIT rights are preserved. | Keep third-party exceptions explicit; do not label the entire binary/dependency stack Apache-only. |
| Unity adaptation | UCL header, licence and upstream notices accompany `StockPostProcessing.cs`; it is excluded from the Apache grant. | Section 1 coverage remains unverified. No Unity outreach or licence-activation workflow is planned; retaining notices does not establish that coverage. |
| NVIDIA DLSS/NGX | Incorporated SDK material is excluded from Apache; attribution, full terms and an SDK-specific downstream notice are present. SR/FG downloads use NVIDIA's own endpoints. | The current build requires a system with an NVIDIA GPU. No notification or Mark approval is recorded; the contractual conditions remain in the assessment, with no outreach planned. |
| NVIDIA Reflex | Its separate licence and feature attribution are present; the pinned NVIDIA FG provider requests low-latency mode. | Verify actual initialization, marker/sleep integration and applicable UI guidelines. The generic F10 Low Latency page describes a different path; a DLL or mode request is insufficient proof. |
| OptiScaler GPL | The unchanged upstream binary is an optional download; GPL text, authors, exact version and source/build directions accompany it. | The core works independently through NGX. Some upstream prebuilt-source correspondence remains unverified; this release does not claim to rebuild or relicense OptiScaler or settle every combined-work question. |
| Intel SDK | Original binary hashes, licence and third-party notices are preserved, with the tested upstream bridge. | Real export detours are distinct from ordinary loader/API use. The upstream detour/operation condition is documented; no custom hook-removal build or vendor exception is claimed. |
| AMD binaries | Binary exceptions and notices remain separate from the older MIT source grant. | Preserve the applicable restrictions and assess any GPL combination; no broader open-source grant is claimed. |
| LGPL loader libraries | BepInEx/Il2CppInterop are external shared references, with notices and replacement/debugging freedoms preserved. | Keep that mechanism and source-delivery obligations if a future package conveys their binaries. |
| Native/prebuilt provenance | The loader's Dobby DLL matches BepInEx/Dobby v1.0.5 byte for byte; its source pin and Apache licence are recorded. | OptiScaler's linked prebuilt-library source correspondence remains incomplete; do not turn direct downloads into an exhaustive source/rights claim. |
| Additional credits | Detours, JSON, Anti-Lag 2 and LatencyFleX notices supplement the existing inventory. | Recheck the actual frozen ZIP and installed notice payload after changes. Attribution does not cure a missing permission. |
| NR model | A separate opt-in downloads the hash-pinned RHI community SF-v2 model; it remains outside the repository/ZIP and starts disabled. | It exactly matches the previously tested model, is unsigned and has no licence in its archive. No official NVIDIA download, distribution grant or latest-model claim is asserted. |

The [licensing assessment](LICENSING.md) distinguishes binding clauses, interpretations and options. It does not treat every factual vendor reference as a marketing approval request, or a mod DLL as automatically prohibited standalone SDK distribution.

## Validation scope

The previously accepted runtime passed in-world checks on the recorded RTX 4090/DX11 system. Preset cycling, distant shadows and Game defaults restoration were confirmed. The later C4E1 managed / unchanged 947D native pair completed a fresh BepInEx bootstrap and repeat launch with sustained DLSS evaluation. The user confirmed the picture, movement and Home/F10 menus in a private world; historical observations do not validate other binaries.

Keep the following coverage limits visible for the alpha:

- Fresh bootstrap succeeded on the recorded system: Unity libraries downloaded, metadata 39 processed and 158 interop assemblies generated. This remains a one-system check.
- The real GUI install completed after its worker fix, with 312 immutable installed hashes verified. The user confirmed Home/F10 in-world. The user separately confirmed Setup.exe opens without a blank terminal; other DPI configurations are unverified.
- Compare NVIDIA FG and NR with a baseline in the same moving view; keep AMD/Intel performance limits explicit. Verify Reflex separately instead of inferring it from FG presents.
- Run installer selection/recovery tests, the complete pinned-dependency fixture and package/installed-notice audits on the frozen candidate. Keep exact binary/source identities and CI results distinct.

No public repository visibility change, release upload, merge or vendor submission follows automatically from these checks. Those decisions remain with the publisher.
