# Release checklist

This checklist records the alpha's delivered licence/attribution handling, validation coverage and historical observations. Checkmarks describe the named work, not a blanket legal-clearance statement. Known SDK and upstream-source conditions remain in the assessment; no Unity or NVIDIA outreach is planned. Historical runtime observations do not validate a new binary.

## License review

- [x] Owner-requested Apache-2.0 grant applied to independently authored material; full canonical `LICENSE`, scoped `NOTICE`, historical 0.6 MIT copy and third-party exceptions are preserved. This is a mixed-license project; `StockPostProcessing.cs` remains UCL and NVIDIA SDK portions retain NVIDIA terms. See [the licensing assessment](LICENSING.md).
- [x] Preserve the Unity adaptation's UCL header, licence and upstream notices, outside the Apache grant. [UCL v1.4 section 1](https://unity.com/legal/licenses/unity-companion-license) coverage remains unverified; this checked item records notice preservation, not satisfaction of that condition. No Unity outreach or licence-activation workflow is planned.
- [x] Retain NVIDIA SDK terms, attribution and the [SDK-specific distribution notice](licenses/NVIDIA-DISTRIBUTION-NOTICE.md). [The assessment](LICENSING.md#nvidia-sdk-incorporation-and-attribution) records the actual notification/Mark conditions; no notification or approval is represented as complete, and no vendor-contact workflow is pending.
- [ ] Recheck the actual public-release ZIP and repository for game assemblies, generated interop assemblies, game assets, copied game source, proprietary runtime DLLs, personal paths, configuration dumps and private logs.
- [x] Add the verified principal BepInEx/Il2CppInterop/HarmonyX, OptiScaler/RenoDX, Streamline, Intel, AMD, FreeType/font and transitive-library notices. License origins and saved-file hashes are in [SOURCES.md](licenses/SOURCES.md); loader binary provenance limits remain explicit in [the inventory](licenses/BepInEx-DEPENDENCIES.md).
- [ ] Recheck the final public-release ZIP and installed notice payload for `LICENSE`, `NOTICE`, `THIRD_PARTY_NOTICES.md` and all linked `docs/licenses` files, plus the applicable downloaded SDK notices. Source-tree presence alone does not prove packaging preserved them.
- [x] Keep OptiScaler an unchanged upstream download with GPL text, source directions, credits and replacement rights. Document the independent core and source-correspondence limits without relicensing GPL material or claiming downloading alone settles the combined-work boundary.
- [x] Preserve Intel/AMD binary terms and document the actual Intel export-detour limitation, separately from ordinary API/loading behavior; no SDK exception or custom fork is claimed.
- [ ] Complete NVIDIA Reflex integration/UI verification, including successful enablement and marker/sleep operation with FG; a DLL, credit or saved request is insufficient.
- [x] Keep NR binaries out of the repository/ZIP. Make the RHI community model a separate unchecked download, pinned to the previously tested bytes; preserve [source, signature and licence limits](licenses/RHI-Neural-Rendering-NOTICE.md). Bridge-only installation/repair must not opt into it.

## Private 0.7 validation

- [x] CPU installer fixtures: 34 guarded cases and 12 memory-only feature-selection cases under Windows PowerShell 5.1 and PowerShell 7, including separate NR opt-in/repair/collision behavior. These do not exercise graphics runtimes or first-launch game behavior.
- [ ] Recheck the public-release ZIP and fixture-installed notices after any final licensing edits; confirm linked license files, installer ownership/removal behavior and absence of bundled runtime/game/model binaries.
- [x] Record the previously accepted rendering pair and build evidence in the release's compatibility documentation. The later UI audit build has separate package checksums and still needs visual/first-bootstrap acceptance; it is not the previously accepted DLL pair.
- [x] Fresh independent reviews cover the 0.7 implementation and vegetation binding corrections. Findings, reviewed heads and regression results are recorded in the private pull request's review table; the historical 0.6 review is separate.
- [x] Final restoration acceptance in the authorized real-game test: NVIDIA FG plus Default NR received user confirmation; AMD FG operated with poorer results and Intel interpolation reduced FPS on the tested RTX 4090. The user confirmed stable preset cycling, Extreme distant tree shadows and Game defaults nearby foliage restoration after the final vegetation correction.
- [ ] Validate a fresh BepInEx installation and first-launch interop generation; the historical existing-loader tests do not satisfy this check.
- [ ] Visually check the revised Setup/Home wording at the intended DPI and resolution. The hosted Setup preview was not targetable through the permitted UI interface, and the new managed UI has not been installed in the live game during this audit.

## Historical private 0.6 observations

These checks describe the earlier private 0.6 line through `ccef38a`, not a public release or validation of the new 0.7 head. Preserve the original observation scope and known limitations.

- [x] Core dependency URLs retrieved and SHA-256 computed; NVIDIA SR signature verified during preparation. Exact pins are in `packaging/dependencies.lock.json`.
- [x] Installer fixture checks complete for install, repair, interrupted recovery, conflicts and uninstall; a real GitHub download and a complete packaged install/remove with all locked dependencies also passed.
- [x] Official SR 310.9.1 sustained DLSS evaluation in the game world with the existing BepInEx installation; Home menu and Quality / Game defaults choices visually confirmed on the observed setup.
- [ ] A completely fresh BepInEx installation and first-launch interop generation complete in the real game. The existing-loader test does not satisfy this check.
- [ ] All graphics presets, live save and restoration exercised on that candidate.
- [x] Expanded quality modes and preset choices visually confirmed; FSR 3.1 and XeSS each initialized, sustained reconstruction and received visual checks. Official FG runtime recorded generated-frame presents. FSR 4 remains unavailable on the RTX 4090. XeSS flicker and FG shimmer remain known limitations.
- [ ] Historical validation records exact mod binary hashes together with game build, Unity version, GPU, driver and graphics API.
- [x] Documentation describes the optional upscaler/FG pack, manual NR prerequisites, VR exclusion and known visual/performance limits.
- [x] Independent review completed; three installer findings received fixes and regression coverage, with 27 cases passing under Windows PowerShell 5.1 and PowerShell 7.

## Release scope

The alpha core is DLSS Super Resolution plus the graphics panel. Its optional bridge downloads pinned OptiScaler with built-in native FSR 3.1, official NVIDIA Streamline/DLSS FG, Intel XeSS SR/FG/XeLL and selected official AMD loader/FG files. The separate NR option downloads the RHI community model and includes the bridge it needs. Fresh FG/NR defaults remain off. Model distribution authorization is unverified; its source is not presented as an official NVIDIA release. A fresh BepInEx first launch, broader hardware coverage and exact GPU allocation of the experimental 16384 atlas remain unverified.
