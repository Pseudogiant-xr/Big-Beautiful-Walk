# Validation of the alpha candidate

Observed on Windows x64, Big Walk application version `1.6.0 2609301522` (Steam build 25723723), Unity 6000.3.17f1, RTX 4090 and driver 617.42, using DirectX 11. These results do not establish compatibility on other hardware or a completely fresh BepInEx first launch.

## Rendering and interface

- Official SR 310.9.1 sustained in-world reconstruction with the existing BepInEx loader.
- All six quality choices produced a valid picture in the user's test, including native-resolution DLAA and Ultra Quality. Preset choices were accepted by the API and visually exercised; this does not prove the effective model under driver overrides.
- The official Streamline 2.14.1 / FG 310.9.1 set returned successful generated-frame presentation samples. This is not a measured FPS multiplier.
- FSR 3.1 initialized its own context, sustained evaluations and was visually confirmed.
- XeSS initialized through the DX12 bridge, sustained reconstruction and was visually confirmed. Slight flicker was reported; its cause remains unconfirmed.
- Overview and Upscaling were visually checked with backend-specific labels; queued DLSS selection survived a normal game exit. The final restart notice is more prominent than the first tested text.
- Experimental 16384 shadow setting was accepted and improved the user's picture at a frame-rate cost. Exact GPU texture allocation remains unverified.

## Build and recovery checks

The managed Release build completed with zero warnings/errors using .NET SDK 8.0.426 targeting .NET 6. The native Windows x64 MSVC Release build also completed successfully. Managed/native packet ABI is version 3, 120 bytes; deploy the matching pair.

`dotnet run --project tests/GraphicsChecks.csproj -c Release` passed compatibility, quality/preset, backend-label and cascade tests, including 20,000 randomized cascade cases. `dotnet run --project tests/BridgeChecks.csproj -c Release` passed 26 assertions for INI formatting/encoding, duplicate-key refusal, stale writes, backups, atomic replacement and Windows junction rejection.

`tests/Setup.Tests.ps1` passed 27 disposable integration cases under Windows PowerShell 5.1 and PowerShell 7. Coverage includes running-game refusal, independent game locks, concurrent-transaction ownership, edited configurations, missing-file repair, shared plugin/patcher dependencies, interrupted recovery, path aliases/junctions, locked files and HTTPS redirects. A separate real GitHub download matched its locked hash and installed/removed successfully in a disposable fixture. The complete packaged installer also installed, diagnosed and removed the actual five locked dependency sets in a fake game while preserving game/configuration bytes.

An independent source review found three installer issues; each received a regression case and fix. Hosted CI covers CPU/installer checks, not rendering. The release's checksums identify the final DLLs and packaged files; no game assemblies or runtime download caches are included.

Public release remains subject to the unresolved permissions in [the release checklist](RELEASE-CHECKLIST.md). This validation is not a legal-clearance statement.
