# Release checklist

This checklist records public-alpha validation and unresolved dependency terms. It is not a legal-clearance statement. The presence of a license copy, a checksum or an upstream download does not prove every distribution condition has been satisfied.

## License review

- [ ] Confirm the intended source-license grant for independently authored code. `StockPostProcessing.cs` and Unity-derived portions remain outside that grant under the Unity Companion License.
- [ ] Confirm the Unity Companion Use condition is met for the adapted file. [UCL v1.4 section 1](https://unity.com/legal/licenses/unity-companion-license) requires a valid Engine License for the described exercise of its grant; section 3.2 addresses ownership of derivative works, and section 5 requires license/copyright notices. Copies are in `docs/licenses`.
- [ ] Resolve NVIDIA SDK incorporation and attribution requirements for the proposed public mod release. [NVIDIA RTX SDK license](https://github.com/NVIDIA/DLSS/blob/374959484e79a640feaba44c93ac8cfb0a03f5b5/LICENSE.txt), section 1(c), permits object-code incorporation subject to its conditions; section 2 sets distribution requirements; section 4(b) restricts standalone SDK distribution; section 4(e) limits imposing open-source obligations on the SDK. The supplement's sections 7.1(a)-(c) and 7.2(c) cover attribution, marks and approval requirements. Avoiding bundled runtimes and vendor logos does not by itself settle these obligations.
- [ ] Check the actual outgoing ZIP and repository for game assemblies, generated interop assemblies, game assets, copied game source, proprietary runtime DLLs, personal paths, configuration dumps and private logs.
- [x] Preserve upstream notices and license exceptions with the candidate; no blanket MIT claim is made.
- [ ] Keep optional OptiScaler's GPL source/notice obligations separate from NVIDIA's proprietary runtime terms. Do not infer compatibility between those terms or permission for the cached NR model. [Skynizz's release](https://github.com/Skynizz/optiscaler-dlss5/releases/tag/v1.2.1) explicitly excludes the NR model.

## Validation

- [x] Core dependency URLs retrieved and SHA-256 computed; NVIDIA SR signature verified during preparation. Exact pins are in `packaging/dependencies.lock.json`.
- [x] Installer fixture checks complete for install, repair, interrupted recovery, conflicts and uninstall; a real GitHub download and a complete packaged install/remove with all locked dependencies also passed.
- [x] Official SR 310.9.1 sustained DLSS evaluation in the game world with the existing BepInEx installation; Home menu and Quality / Game defaults choices visually confirmed on the observed setup.
- [ ] A completely fresh BepInEx installation and first-launch interop generation complete in the real game. The existing-loader test does not satisfy this check.
- [ ] All graphics presets, live save and restoration exercised on the final release build.
- [x] Expanded quality modes and preset choices visually confirmed; FSR 3.1 and XeSS each initialized, sustained reconstruction and received visual checks. Official FG runtime recorded generated-frame presents. FSR 4 remains unavailable on the RTX 4090. XeSS flicker and FG shimmer remain known limitations.
- [ ] Final validation records the exact mod binary hashes together with game build, Unity version, GPU, driver and graphics API.
- [x] Documentation describes the optional upscaler/FG pack, manual NR prerequisites, VR exclusion and known visual/performance limits.
- [x] Independent review completed; three installer findings received fixes and regression coverage, with 27 cases passing under Windows PowerShell 5.1 and PowerShell 7.

## Release scope

The core is DLSS Super Resolution plus the graphics panel. The optional checkbox downloads pinned FSR 3.1/XeSS/FG bridge dependencies; fresh FG defaults remain off. NR requires a separately obtained model and is not installed automatically. A complete fresh BepInEx first launch, broader hardware coverage and exact GPU allocation of the experimental 16384 shadow atlas remain unverified.
