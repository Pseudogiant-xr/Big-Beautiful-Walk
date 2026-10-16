# Release checklist

This checklist tracks the private 0.7 test candidate, unresolved public-release terms and explicitly labeled historical 0.6 observations. It is not a legal-clearance statement. A license copy, checksum or upstream download does not prove every distribution condition has been satisfied; historical runtime observations do not validate the new candidate.

## License review

- [x] Owner-requested Apache-2.0 grant applied to independently authored material; full canonical `LICENSE`, scoped `NOTICE`, historical 0.6 MIT copy and third-party exceptions are preserved. This is a mixed-license project; `StockPostProcessing.cs` remains UCL and NVIDIA SDK portions retain NVIDIA terms. See [the licensing assessment](LICENSING.md).
- [ ] Confirm the Unity Companion Use condition is met for the adapted file. [UCL v1.4 section 1](https://unity.com/legal/licenses/unity-companion-license) requires a valid Engine License for the described exercise of its grant; section 3.2 addresses ownership of derivative works, and section 5 requires license/copyright notices. Copies are in `docs/licenses`.
- [ ] Resolve NVIDIA's actual public-mod conditions: SDK incorporation/protective distribution terms; supplement 4 notification applicability to a free plug-in for a commercial game; 7.1(b) application attribution/Mark locations; and applicable 7.1(a)/7.2(c) supplier-marketing/Mark uses. [The assessment](LICENSING.md#nvidia-sdk-incorporation-and-attribution) separates these clauses. It does not treat every factual technical citation as requiring approval; no required notification, application-credit treatment or Mark approval is recorded as complete.
- [ ] Check the actual outgoing ZIP and repository for game assemblies, generated interop assemblies, game assets, copied game source, proprietary runtime DLLs, personal paths, configuration dumps and private logs.
- [x] Add the verified principal BepInEx/Il2CppInterop/HarmonyX, OptiScaler/RenoDX, Streamline, Intel, AMD, FreeType/font and transitive-library notices. License origins and saved-file hashes are in [SOURCES.md](licenses/SOURCES.md); loader binary provenance limits remain explicit in [the inventory](licenses/BepInEx-DEPENDENCIES.md).
- [ ] Inspect the final candidate ZIP and installed notice payload for `LICENSE`, `NOTICE`, `THIRD_PARTY_NOTICES.md` and all linked `docs/licenses` files, plus the applicable downloaded SDK notices. Source-tree presence alone does not prove packaging preserved them.
- [ ] Establish a supportable GPL combined-work/separate-work basis for optional OptiScaler, and the applicable corresponding-source method if its DLL is redistributed. Apache-2.0 is compatible with GPLv3 in the direction described in [the assessment](LICENSING.md#optiscaler-and-gplv3); dynamic loading and separate downloading alone are not proof of separation, and GPL cannot override proprietary SDK/UCL terms.
- [ ] Check the actual Intel integration paths against the unchanged-binary/no-alteration-of-operation restriction; preserve Intel/AMD binary terms rather than label every SDK file open source.
- [x] Exclude the NR model from the release, dependency lock and automatic download. [Skynizz's release](https://github.com/Skynizz/optiscaler-dlss5/releases/tag/v1.2.1) also excludes it; cached-file hashes and vendor performance announcements do not establish public redistribution rights.

## Private 0.7 validation

- [x] CPU installer fixtures: 30 cases under Windows PowerShell 5.1 and PowerShell 7 in the author session. This does not exercise graphics runtimes or first-launch game behavior.
- [ ] Inspect the final packaged ZIP and fixture-installed notices after the final licensing edits; confirm linked license files, installer ownership/removal behavior and absence of bundled runtime/game/model binaries.
- [x] Record the tested 0.7 managed/native binary hashes and build evidence in [compatibility](COMPATIBILITY.md); the release preserves that pair through subsequent documentation edits.
- [x] Fresh independent reviews cover the 0.7 implementation and vegetation binding corrections. Findings, reviewed heads and regression results are recorded in the private pull request's review table; the historical 0.6 review is separate.
- [x] Final restoration acceptance in the authorized real-game test: NVIDIA FG plus Default NR received user confirmation; AMD FG operated with poorer results and Intel interpolation reduced FPS on the tested RTX 4090. The user confirmed stable preset cycling, Extreme distant tree shadows and Game defaults nearby foliage restoration after the final vegetation correction.
- [ ] Validate a fresh BepInEx installation and first-launch interop generation; the historical existing-loader tests do not satisfy this check.

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

The private 0.7 core is DLSS Super Resolution plus the graphics panel. Its optional pack includes the pinned OptiScaler bridge with built-in native FSR 3.1, official NVIDIA Streamline/DLSS FG files, Intel XeSS SR/FG/XeLL files and selected official AMD loader/FG files. New FG choices remain experimental until the selected path is exercised on the final candidate; fresh FG defaults remain off. The pack supplies an NR forwarder without the NR model: a separately supplied model is required, no model is automatically downloaded, and redistribution permission is unverified. A complete fresh BepInEx first launch, broader hardware coverage and exact GPU allocation of the experimental 16384 shadow atlas remain unverified.
