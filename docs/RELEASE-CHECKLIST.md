# Release checklist

Work through this list before publishing a build. A tick means the named work is done. It is not a blanket legal clearance. Known SDK and upstream-source conditions are listed in the licensing assessment. No Unity or NVIDIA contact, and no Unity licence activation, is planned. Past runtime results do not validate a new binary.

## Licence review

- [x] Apply the owner-requested Apache-2.0 grant to independently authored material. Keep the full canonical `LICENSE`, the scoped `NOTICE`, the historical 0.6 MIT copy and the third-party exceptions. The project is mixed-licence: `StockPostProcessing.cs` stays under the UCL, and NVIDIA SDK portions keep NVIDIA terms. See [the licensing assessment](LICENSING.md).
- [x] Keep the Unity adaptation's UCL header, licence and upstream notices, outside the Apache grant. Coverage under [UCL v1.4 section 1](https://unity.com/legal/licenses/unity-companion-license) is not verified. This tick covers keeping the notices, not meeting that condition.
- [x] Keep NVIDIA SDK terms, attribution and the [SDK-specific distribution notice](licenses/NVIDIA-DISTRIBUTION-NOTICE.md). [The assessment](LICENSING.md#nvidia-sdk-incorporation-and-attribution) lists the actual notification and Mark conditions. No notification or approval is claimed as complete.
- [x] Recheck the actual public-release ZIP and the repository for game assemblies, generated interop assemblies, game assets, copied game source, proprietary runtime DLLs, personal paths, configuration dumps and private logs.
- [x] Add the verified principal notices for BepInEx/Il2CppInterop/HarmonyX, OptiScaler/RenoDX, Streamline, Intel, AMD, FreeType/font and transitive libraries. Licence origins and saved-file hashes are in [SOURCES.md](licenses/SOURCES.md). [The inventory](licenses/BepInEx-DEPENDENCIES.md) states the limits of loader binary provenance.
- [x] Recheck the final public-release ZIP and the installed notice payload for `LICENSE`, `NOTICE`, `THIRD_PARTY_NOTICES.md`, every linked `docs/licenses` file and the applicable downloaded SDK notices. A file in the source tree does not prove the package includes it.
- [x] Ship OptiScaler as an unchanged upstream download with its GPL text, source directions, credits and replacement rights. Document that the core is independent, and document the limits of source correspondence. Do not relicense GPL material or claim that downloading alone settles the combined-work boundary.
- [x] Keep Intel and AMD binary terms. Document the actual Intel export-detour limitation separately from ordinary API and loading behaviour. No SDK exception or custom fork is claimed.
- [ ] Verify NVIDIA Reflex integration and UI: Reflex must enable successfully and run markers and sleep with FG. A DLL, a credit or a saved request is not enough.
- [x] Keep NR binaries out of the repository and ZIP. Offer the RHI community model as a separate download, unchecked by default and pinned to the previously tested bytes. Keep its [source, signature and licence limits](licenses/RHI-Neural-Rendering-NOTICE.md). Bridge-only install or repair must not opt into it.

## Private 0.7 validation

- [x] Run the CPU installer fixtures: 35 guarded cases and 12 memory-only feature-selection cases under Windows PowerShell 5.1 and PowerShell 7, including separate NR opt-in, repair and collision behaviour. These do not exercise graphics runtimes or first-launch game behaviour.
- [x] Recheck the release ZIP and the fixture-installed notices. The ZIP has 128 packaged files with complete checksums, 54 notice/source files and 50 upstream snapshot hashes. The FG and NR install/repair/uninstall fixtures passed with 313 (FG) and 314 (NR) installed hashes and 148 notice links each. The only packaged binaries are the project DLL pair and Setup.exe.
- [x] Record the previously accepted rendering pair and its build records in the release's compatibility documentation. The later C4E1/947D pair has its own package checksums and completed a fresh bootstrap. With it, the maintainer confirmed the picture, movement and Home/F10 menus in-world. It is a different pair from the previously accepted 0859/947D pair.
- [x] Get fresh independent reviews of the 0.7 implementation and the vegetation binding corrections. The pull request's review table records findings, reviewed heads and regression results. The historical 0.6 review is separate.
- [x] Run final restoration acceptance in the real game. The maintainer confirmed NVIDIA FG plus Default NR. AMD FG worked with poorer results. Intel interpolation reduced FPS on the tested RTX 4090. After the final vegetation correction, the maintainer confirmed stable preset cycling, Extreme distant tree shadows and Game defaults restoring nearby foliage.
- [x] Bootstrap a fresh BepInEx 755 install. It downloaded Unity libraries, processed metadata 39, generated 158 interop assemblies, loaded Big Beautiful Walk 0.7.0 and sustained DLSS evaluation. A repeat launch reused those files successfully.
- [x] Visually check the revised Setup/Home interface on the tested desktop. The real Setup GUI completed installation, and the new managed UI is installed and evaluating. The maintainer confirmed Home/F10 and the picture in-world, and confirmed Setup.exe opens without a blank terminal. Other DPI configurations are not verified.

## Historical private 0.6 observations

These checks cover the earlier private 0.6 line up to `ccef38a`. They are not a public release and do not validate the 0.7 head. Keep their original scope and known limitations.

- [x] Retrieve the core dependency URLs and compute their SHA-256 hashes. The NVIDIA SR signature was verified when the lock was prepared. Exact pins are in `packaging/dependencies.lock.json`.
- [x] Run installer fixture checks for install, repair, interrupted recovery, conflicts and uninstall. A real GitHub download and a complete packaged install/remove with all locked dependencies also passed.
- [x] Run official SR 310.9.1 in the game world with the existing BepInEx install. DLSS evaluation was sustained. The Home menu and the Quality / Game defaults choices were visually confirmed on that setup.
- [ ] Complete a fully fresh BepInEx install and first-launch interop generation in the real game. The existing-loader test does not count.
- [ ] Exercise all graphics presets, live save and restoration on that candidate.
- [x] Visually confirm the expanded quality modes and preset choices. FSR 3.1 and XeSS each initialised, sustained reconstruction and were visually checked. The official FG runtime recorded generated-frame presents. FSR 4 is unavailable on the RTX 4090. XeSS flicker and FG shimmer are known limitations.
- [ ] Record exact mod binary hashes in the historical validation, together with game build, Unity version, GPU, driver and graphics API.
- [x] Document the optional upscaler/FG pack, manual NR prerequisites, VR exclusion and known visual and performance limits.
- [x] Complete an independent review. Three installer findings were fixed and got regression coverage. 27 cases passed under Windows PowerShell 5.1 and PowerShell 7.

## Release scope

The alpha core is DLSS Super Resolution plus the graphics panel. Its optional bridge downloads:

- pinned OptiScaler, with built-in native FSR 3.1
- official NVIDIA Streamline and DLSS FG
- Intel XeSS SR, FG and XeLL
- selected official AMD loader and FG files

The separate NR option downloads the RHI community model and includes the bridge it needs. FG and NR start off in a fresh install. Authorisation to distribute the model is not verified, and its source is not presented as an official NVIDIA release. A fresh BepInEx bootstrap worked on the recorded system. Wider hardware coverage and the exact GPU allocation of the experimental 16384 atlas are not verified.
