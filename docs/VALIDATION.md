# Validation records and scope

Observed on Windows x64, Big Walk application version `1.6.0 2609301522` (Steam build 25723723), Unity 6000.3.17f1, RTX 4090 and driver 617.42, using DirectX 11. These results do not establish compatibility on other hardware.

## Rendering and interface

The accepted private 0.7 rendering runtime is managed source `4dc8555`, with the unchanged native ABI 3 / 120-byte pair and hashes recorded in [compatibility](COMPATIBILITY.md). The later pre-release audit changes installer behavior, documentation and UI wording. Its C4E1 managed / unchanged 947D native pair subsequently completed fresh bootstrap and sustained DLSS evaluation; final picture/menu acceptance is pending. Historical observations below retain their original scope.

- Official SR 310.9.1 sustained in-world reconstruction with the existing BepInEx loader.
- All six quality choices produced a valid picture in the user's test, including native-resolution DLAA and Ultra Quality. Preset choices were accepted by the API and visually exercised; this does not prove the effective model under driver overrides.
- The official Streamline 2.14.1 / FG 310.9.1 set returned successful generated-frame presentation samples. This is not a measured FPS multiplier.
- FSR 3.1 initialized its own context, sustained evaluations and was visually confirmed.
- XeSS initialized through the DX12 bridge, sustained reconstruction and was visually confirmed. Slight flicker was reported; its cause remains unconfirmed.
- Overview and Upscaling were visually checked with backend-specific labels; queued DLSS selection survived a normal game exit. The final restart notice is more prominent than the first tested text.
- Experimental 16384 shadow setting was accepted and improved the user's picture at a frame-rate cost. Exact GPU texture allocation remains unverified.
- On the accepted 0.7 runtime, the user confirmed NVIDIA FG plus Default-style NR, stable preset cycling, distant tree shadows with Extreme active and nearby foliage after Game defaults. Logs record a 1950–2000 m material fade and restoration of 542 changed material properties plus the active Flora parameter, without guessed fade-helper replay.
- AMD FG operated but was inferior in the user's comparison; Intel produced marked interpolated frames with reported 71 FPS off / 40 on. These are setup-specific observations, not a controlled provider benchmark. Successful Reflex operation and complete latency-marker coverage remain unverified.

## Fresh installation and launcher

The real Setup GUI installed the optional bridge and NR model after correcting a background-runspace module initialization failure. All 312 noneditable installed files matched their manifest hashes; the two editable settings files changed during normal use. Four protected game files retained their original hashes, and no transaction journal remained. The previous installation was backed up before testing.

On 16 October 2026, a new BepInEx 755 installation downloaded Unity 6000.3.17 libraries, processed metadata version 39 with Cpp2IL and generated 158 interop assemblies without using the previous cache. It loaded one Big Beautiful Walk 0.7.0 plugin and sustained DLSS evaluation. A subsequent launch reused those assemblies successfully. Final private-world picture and Home/F10 acceptance remain pending.

The source-included x64 GUI launcher passes `Launcher.Tests.ps1` under Windows PowerShell 5.1 and PowerShell 7, exit 0, including a package path containing spaces and an ampersand. The test verifies GUI PE format and child-script execution. Visual confirmation that Setup.exe opens without a terminal remains pending; that is not inferred from the test.

## Build and recovery checks

Managed Release builds use .NET SDK 8.0.426 targeting .NET 6 and exit 0 with the existing MSB3246 game-reference warning. Do not describe them as warning-free. The native Windows x64 MSVC Release build previously exited 0; the native source has not changed during the current installer/documentation audit. Deploy the verified managed/native ABI pair.

`dotnet run --project tests/GraphicsChecks.csproj -c Release` covers compatibility, quality/preset, backend-label and cascade tests, including 20,000 randomized cascade cases. `tests/BridgeChecks.csproj` has 31 assertions for INI formatting/encoding, duplicate-key refusal, stale writes, backups, atomic replacement and Windows junction rejection. `FeatureChecks`, `FeatureMenuChecks` and `BridgeRefreshChecks` cover provider profiles, stale-menu recovery and saved bridge-state refresh.

The pre-release audit passed 31 `Setup.Tests.ps1` cases under Windows PowerShell 5.1 and PowerShell 7. The new default optional-component update regression preserves edited INI bytes; it fails against the previous module and passes after the repair-selection correction. Eight memory-only `SetupSelection.Tests.ps1` cases also pass in both environments, covering already-owned optional roles, missing-file selection, fresh opt-in and unknown-role refusal without deleting a DLL. The guarded suite covers missing core files, shared ownership, interrupted recovery, locks, path aliases/junctions and HTTPS redirects.

The later separate NR-download change passed 34 guarded installer cases and 12 memory-only feature-selection cases under each shell, exit 0. New cases verify that NR includes its bridge, unchecked repair retains owned NR, FG-only repair never selects an available model, and preexisting/modified model files keep the existing collision and ownership protections. The downloaded RHI archive hash and model hash match the lock and the previously accepted game-root model; it was not loaded during the installer audit. The final package's FG/NR dependency fixture is a separate check, not implied by those synthetic cases.

The production GUI-worker regression brings the guarded installer suite to 35 cases in each shell, exit 0. It extracts the real worker body and exercises install, unchecked repair, diagnose and uninstall in normal, autoload-disabled and cold Core-only runspaces. The old body reproduces the missing-command failure; explicit engine-local Management/Utility imports correct it.

Production vegetation tests cover 12 ownership assertions and 16 interop assertions. Their Unity stubs do not replace real-game acceptance. The full `Dependencies.Tests.ps1` fixture verifies every pinned dependency, installed file hash, notice link, repair and removal without executing its disposable game files. Its accepted 0.7 run verified 302 installed hashes and 103 links; notice additions require a new package run and new observed counts.

An independent source review found three installer issues; each received a regression case and fix. Hosted CI covers CPU/installer checks, not rendering. The release's checksums identify the final DLLs and packaged files; no game assemblies or runtime download caches are included.

The alpha's scope and known licence/validation limits are recorded in [release readiness](RELEASE-READINESS.md) and [the checklist](RELEASE-CHECKLIST.md). Final visual/DPI acceptance of the revised UI and successful Reflex verification remain unverified. This validation is not a legal-clearance statement.
