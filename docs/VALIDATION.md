# Validation records and scope

Observed on Windows x64, Big Walk application version `1.6.0 2609301522` (Steam build 25723723), Unity 6000.3.17f1, RTX 4090 and driver 617.42, using DirectX 11. These results do not establish compatibility on other hardware or a completely fresh BepInEx first launch.

## Rendering and interface

The accepted private 0.7 rendering runtime is managed source `4dc8555`, with the unchanged native ABI 3 / 120-byte pair and hashes recorded in [compatibility](COMPATIBILITY.md). The later pre-release audit changes installer behavior, documentation and UI wording; it has not received a fresh game or first-bootstrap check. The observations below must not be silently transferred to a newly built DLL.

- Official SR 310.9.1 sustained in-world reconstruction with the existing BepInEx loader.
- All six quality choices produced a valid picture in the user's test, including native-resolution DLAA and Ultra Quality. Preset choices were accepted by the API and visually exercised; this does not prove the effective model under driver overrides.
- The official Streamline 2.14.1 / FG 310.9.1 set returned successful generated-frame presentation samples. This is not a measured FPS multiplier.
- FSR 3.1 initialized its own context, sustained evaluations and was visually confirmed.
- XeSS initialized through the DX12 bridge, sustained reconstruction and was visually confirmed. Slight flicker was reported; its cause remains unconfirmed.
- Overview and Upscaling were visually checked with backend-specific labels; queued DLSS selection survived a normal game exit. The final restart notice is more prominent than the first tested text.
- Experimental 16384 shadow setting was accepted and improved the user's picture at a frame-rate cost. Exact GPU texture allocation remains unverified.
- On the accepted 0.7 runtime, the user confirmed NVIDIA FG plus Default-style NR, stable preset cycling, distant tree shadows with Extreme active and nearby foliage after Game defaults. Logs record a 1950–2000 m material fade and restoration of 542 changed material properties plus the active Flora parameter, without guessed fade-helper replay.
- AMD FG operated but was inferior in the user's comparison; Intel produced marked interpolated frames with reported 71 FPS off / 40 on. These are setup-specific observations, not a controlled provider benchmark. Successful Reflex operation and complete latency-marker coverage remain unverified.

## Build and recovery checks

Managed Release builds use .NET SDK 8.0.426 targeting .NET 6 and exit 0 with the existing MSB3246 game-reference warning. Do not describe them as warning-free. The native Windows x64 MSVC Release build previously exited 0; the native source has not changed during the current installer/documentation audit. Deploy the verified managed/native ABI pair.

`dotnet run --project tests/GraphicsChecks.csproj -c Release` covers compatibility, quality/preset, backend-label and cascade tests, including 20,000 randomized cascade cases. `tests/BridgeChecks.csproj` has 31 assertions for INI formatting/encoding, duplicate-key refusal, stale writes, backups, atomic replacement and Windows junction rejection. `FeatureChecks`, `FeatureMenuChecks` and `BridgeRefreshChecks` cover provider profiles, stale-menu recovery and saved bridge-state refresh.

The pre-release audit passed 31 `Setup.Tests.ps1` cases under Windows PowerShell 5.1 and PowerShell 7. The new default optional-component update regression preserves edited INI bytes; it fails against the previous module and passes after the repair-selection correction. Eight memory-only `SetupSelection.Tests.ps1` cases also pass in both environments, covering already-owned optional roles, missing-file selection, fresh opt-in and unknown-role refusal without deleting a DLL. The guarded suite covers missing core files, shared ownership, interrupted recovery, locks, path aliases/junctions and HTTPS redirects.

Production vegetation tests cover 12 ownership assertions and 16 interop assertions. Their Unity stubs do not replace real-game acceptance. The full `Dependencies.Tests.ps1` fixture verifies every pinned dependency, installed file hash, notice link, repair and removal without executing its disposable game files. Its accepted 0.7 run verified 302 installed hashes and 103 links; notice additions require a new package run and new observed counts.

An independent source review found three installer issues; each received a regression case and fix. Hosted CI covers CPU/installer checks, not rendering. The release's checksums identify the final DLLs and packaged files; no game assemblies or runtime download caches are included.

Public release remains subject to [release readiness](RELEASE-READINESS.md) and the unresolved permissions in [the release checklist](RELEASE-CHECKLIST.md). A clean BepInEx bootstrap, visual/DPI review of the revised UI and successful Reflex verification remain open. This validation is not a legal-clearance statement.
