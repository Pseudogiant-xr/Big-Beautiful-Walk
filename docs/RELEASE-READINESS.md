# Public release readiness

The alpha uses the tested rendering integration and downloads its dependencies from upstream. It keeps each component's licence and credits. There is no custom OptiScaler fork and no vendor contact. Publishing is the maintainer's decision. This page is not an exhaustive legal clearance or a vendor approval.

NR 310.9.16 can also be imported from a user-supplied, hash-verified compatibility DLL. This runtime is not bundled or downloaded automatically; its Big Walk rendering is unverified. See [local import](FRAME-GENERATION.md#nr-310916-local-import) and [provenance](licenses/RHI-Neural-Rendering-NOTICE.md#nr-310916-local-import).

## Rights and attribution

| Area | What the release does | Scope and known limits |
| --- | --- | --- |
| Original code | Keeps the Apache-2.0 grant, the scoped NOTICE and the historical MIT rights. | Keep third-party exceptions explicit. Do not label the whole binary/dependency stack Apache-only. |
| Unity adaptation | `StockPostProcessing.cs` carries its UCL header, licence and upstream notices. It is excluded from the Apache grant. | Section 1 coverage is not verified. No Unity contact or licence activation is planned. Keeping the notices does not prove that coverage. |
| NVIDIA DLSS/NGX | Incorporated SDK material is excluded from Apache. Attribution, full terms and an SDK-specific downstream notice are included. SR/FG downloads come from NVIDIA's own endpoints. | The NGX-linked DLL is only installed and loaded on systems with an NVIDIA GPU; AMD and Intel systems use a separate bridge DLL without NVIDIA SDK code. No notification or Mark approval is recorded. The assessment lists the contractual conditions. No contact is planned. |
| NVIDIA Reflex | Its separate licence and feature attribution are included. The pinned NVIDIA FG provider requests low-latency mode. | Still to verify: actual initialisation, marker/sleep integration and the applicable UI guidelines. The generic F10 Low Latency page describes a different path. A DLL or a mode request does not prove Reflex works. |
| OptiScaler GPL | The unchanged upstream binary is an optional download. GPL text, authors, exact version and source/build directions come with it. | The core works on its own through NGX. Correspondence between some upstream prebuilt binaries and their source is not verified. This release does not claim to rebuild or relicense OptiScaler, or to settle every combined-work question. |
| Intel SDK | Keeps the original binary hashes, licence and third-party notices, with the tested upstream bridge. | Real export detours are different from ordinary loader/API use. The upstream detour/operation condition is documented. No custom hook-removal build or vendor exception is claimed. |
| AMD binaries | Binary exceptions and notices stay separate from the older MIT source grant. | Keep the applicable restrictions and assess any GPL combination. No broader open-source grant is claimed. |
| LGPL loader libraries | BepInEx/Il2CppInterop are external shared references. Their notices and replacement/debugging freedoms are kept. | If a future package conveys their binaries, keep that mechanism and meet the source-delivery obligations. |
| Native/prebuilt provenance | The loader's Dobby DLL matches BepInEx/Dobby v1.0.5 byte for byte. Its source pin and Apache licence are recorded. | Source correspondence for OptiScaler's linked prebuilt libraries is incomplete. Do not present direct downloads as an exhaustive source or rights claim. |
| Additional credits | Detours, JSON, Anti-Lag 2 and LatencyFleX notices add to the existing inventory. | After any change, recheck the actual frozen ZIP and the installed notice payload. Attribution does not cure a missing permission. |
| NR model | A separate opt-in downloads the hash-pinned RHI community SF-v2 model. It stays outside the repository and ZIP, and starts disabled. | It matches the previously tested model exactly. It is unsigned and its archive has no licence. It is not claimed to be an official NVIDIA download, to have a distribution grant, or to be the latest model. |

The [licensing assessment](LICENSING.md) separates binding clauses, interpretations and options. It does not treat every factual vendor reference as a request for marketing approval. It also does not treat a mod DLL as automatically prohibited standalone SDK distribution.

## Validation scope

The previously accepted runtime passed in-world checks on the recorded RTX 4090/DX11 system: preset cycling, distant shadows and Game defaults restoration. The later C4E1 managed / unchanged 947D native pair completed a fresh BepInEx bootstrap and a repeat launch with sustained DLSS evaluation. With that pair, the maintainer confirmed the picture, movement and Home/F10 menus in a private world. These results do not validate other binaries.

Coverage limits and remaining checks for the alpha:

- Fresh bootstrap worked on one system only. Unity libraries downloaded, metadata 39 was processed and 158 interop assemblies were generated.
- The real GUI install completed after its worker fix, and all 312 immutable installed hashes matched. The maintainer confirmed Home/F10 in-world and that Setup.exe opens without a blank terminal. Other DPI configurations are not verified.
- Compare NVIDIA FG and NR against a baseline in the same moving view. Keep AMD/Intel performance limits explicit. Verify Reflex on its own; FG presents do not show that Reflex works.
- On the frozen candidate, run the installer selection/recovery tests, the complete pinned-dependency fixture and the package/installed-notice audits. Keep exact binary/source identities and CI results separate.

These checks do not trigger a repository visibility change, release upload, merge or vendor submission. The publisher decides those.
