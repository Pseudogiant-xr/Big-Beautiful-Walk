# Public release readiness

The current candidate is private. Technical acceptance of the existing rendering build is separate from permission to distribute the complete integration. This audit does not clear a public release or authorize vendor contact.

## Rights and attribution

| Area | Verified treatment | Remaining release condition |
| --- | --- | --- |
| Original code | Apache-2.0 grant, scoped NOTICE and historical MIT rights are preserved. | Keep third-party exceptions explicit; do not label the entire binary/dependency stack Apache-only. |
| Unity adaptation | UCL header, licence and upstream notices accompany `StockPostProcessing.cs`. | No qualifying Engine entitlement is established. Verify an applicable licence/context, obtain permission, or independently replace/remove the adaptation. Unity Personal eligibility alone does not establish coverage for this mod. |
| NVIDIA DLSS/NGX | Incorporated SDK material is excluded from the Apache grant; source-code attribution and SDK notices are present. | Resolve commercial-game plug-in notification applicability, concrete application credit/Mark treatment and protective SDK distribution terms. Notification is distinct from general release approval. |
| NVIDIA Reflex | Its separate licence and feature attribution are present; the pinned NVIDIA FG provider requests low-latency mode. | Verify actual initialization, marker/sleep integration and applicable UI guidelines. The generic F10 Low Latency page describes a different path; a DLL or mode request is insufficient proof. |
| OptiScaler GPL | Exact upstream version, source-archive link and library notices are recorded. | Establish the combined-work/separate-work basis and any corresponding-source obligations. Verify preferred source/build material for prebuilt libraries; a source-archive label is insufficient. |
| Intel SDK | Original binary hashes, licence and third-party notices are preserved. | Assess the actual XeSS/XeLL API, loader/lifetime and module-handle interception against the operation-alteration restriction. No single INI flag disables all those paths. |
| AMD binaries | Binary exceptions and notices remain separate from the older MIT source grant. | Preserve the applicable restrictions and assess any GPL combination; no broader open-source grant is claimed. |
| LGPL loader libraries | BepInEx/Il2CppInterop are external shared references, with notices and replacement/debugging freedoms preserved. | Keep that mechanism and source-delivery obligations if a future package conveys their binaries. |
| Native/prebuilt provenance | The loader inventory identifies the observed packages and preserves known notices. | Exact native Dobby provenance and prebuilt-library source correspondence remain unestablished; do not turn direct downloads into an exhaustive source/rights claim. |
| Additional credits | Detours, JSON, Anti-Lag 2 and LatencyFleX notices supplement the existing inventory. | Recheck the actual frozen ZIP and installed notice payload after changes. Attribution does not cure a missing permission. |
| NR model | Excluded from the repository, release ZIP and automatic downloads. | Do not add a model or download URL without an authorized source and applicable terms. |

The [licensing assessment](LICENSING.md) distinguishes binding clauses, interpretations and options. It does not treat every factual vendor reference as a marketing approval request, or a mod DLL as automatically prohibited standalone SDK distribution.

## First-time user checks

The previously accepted runtime passed in-world checks on the recorded RTX 4090/DX11 system. Preset cycling, distant shadows and Game defaults restoration were confirmed. The audit's installer and UI-copy changes need their own frozen-build checks; historical observations do not validate a new binary.

Before public release:

- Rehearse a genuinely clean BepInEx installation and first-launch interop generation in an authorized test session, preserving the working installation and profiles. Confirm private-world Home status, Quality/Preset L, F8 comparison and ordinary removal/recovery.
- Check Setup and Home at the intended Windows DPI, resolution and input method. Verify the first-use guide, licences, queued restart state, error text and keyboard/cursor behavior.
- Compare NVIDIA FG and NR with a baseline in the same moving view; keep AMD/Intel performance limits explicit. Verify Reflex separately instead of inferring it from FG presents.
- Run installer selection/recovery tests, the complete pinned-dependency fixture and package/installed-notice audits on the frozen candidate. Keep exact binary/source identities and CI results distinct.

No public repository visibility change, release upload, merge or vendor submission follows automatically from these checks. Those decisions remain with the publisher.
