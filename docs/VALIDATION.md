# Validation records and scope

Test system: Windows x64, Big Walk application version `1.6.0 2609301522` (Steam build 25723723), Unity 6000.3.17f1, RTX 4090 with driver 617.42, DirectX 11. Not tested on other hardware.

## Rendering and interface

The accepted private 0.7 rendering runtime is managed source `4dc8555` with the unchanged native ABI 3 / 120-byte pair. [Compatibility](COMPATIBILITY.md) lists its hashes. The later pre-release audit changed installer behaviour, documentation and UI wording. Its C4E1 managed / unchanged 947D native pair then completed a fresh bootstrap and sustained DLSS evaluation. The maintainer then confirmed the picture, movement and Home/F10 menus in a private world. The older results below keep their original scope.

- Official SR 310.9.1 sustained in-world reconstruction with the existing BepInEx loader.
- All six quality choices gave a valid picture, including native-resolution DLAA and Ultra Quality. The API accepted the preset choices, and they were visually exercised. This does not prove which model is in effect under driver overrides.
- The official Streamline 2.14.1 / FG 310.9.1 set returned successful generated-frame presentation samples. This is not a measured FPS multiplier.
- FSR 3.1 initialised its own context, sustained evaluations and was visually confirmed.
- XeSS initialised through the DX12 bridge, sustained reconstruction and was visually confirmed. Slight flicker was reported. Its cause is not confirmed.
- Overview and Upscaling were visually checked with backend-specific labels. A queued DLSS selection survived a normal game exit. The final restart notice is more prominent than the first tested text.
- The experimental 16384 shadow setting was accepted and improved the picture at a frame-rate cost. Exact GPU texture allocation is not verified.
- On the accepted 0.7 runtime, the maintainer confirmed NVIDIA FG plus Default-style NR, stable preset cycling, distant tree shadows with Extreme active, and nearby foliage after Game defaults. Logs record a 1950–2000 m material fade and restoration of 542 changed material properties plus the active Flora parameter, without guessed fade-helper replay.
- AMD FG worked but looked worse in the maintainer's comparison. Intel produced marked interpolated frames, with a reported 71 FPS off and 40 on. These are observations from one setup, not a controlled provider benchmark. Successful Reflex operation and complete latency-marker coverage are not verified.

## Fresh installation and launcher

The real Setup GUI installed the optional bridge and the NR model, after a fix for a module initialisation failure in the background runspace. All 312 non-editable installed files matched their manifest hashes. The two editable settings files changed during normal use. Four protected game files kept their original hashes, and no transaction journal was left behind. The previous installation was backed up before testing.

On 16 October 2026, a new BepInEx 755 install downloaded Unity 6000.3.17 libraries, processed metadata version 39 with Cpp2IL and generated 158 interop assemblies without using the previous cache. It loaded one Big Beautiful Walk 0.7.0 plugin and sustained DLSS evaluation. The next launch reused those assemblies successfully. The maintainer confirmed the picture, movement and Home/F10 menus in a private world. The exits and restarts during the test were deliberate.

The source-included x64 GUI launcher passes `Launcher.Tests.ps1` under Windows PowerShell 5.1 and PowerShell 7 (exit 0), including with a package path that contains spaces and an ampersand. The test checks the GUI PE format and child-script execution. The maintainer confirmed that the real Setup.exe opens normally without the blank terminal window.

## Build and recovery checks

Managed Release builds use .NET SDK 8.0.426 targeting .NET 6. They exit 0 with the existing MSB3246 game-reference warning, so do not describe them as warning-free. The native Windows x64 MSVC Release build exited 0 earlier. The native source has not changed during the 0.7 installer/documentation audit. Deploy the verified managed/native ABI pair.

Code checks:

- `dotnet run --project tests/GraphicsChecks.csproj -c Release` covers compatibility, quality/preset, backend-label and cascade tests, including 20,000 randomised cascade cases.
- `tests/BridgeChecks.csproj` has 31 assertions for INI formatting and encoding, duplicate-key refusal, stale writes, backups, atomic replacement and Windows junction rejection.
- `FeatureChecks`, `FeatureMenuChecks` and `BridgeRefreshChecks` cover provider profiles, stale-menu recovery and saved bridge-state refresh.
- Production vegetation tests cover 12 ownership assertions and 16 interop assertions. Their Unity stubs do not replace real-game acceptance.

Installer tests, in the order they were added:

- **Pre-release audit.** 31 `Setup.Tests.ps1` cases passed under Windows PowerShell 5.1 and PowerShell 7. A new regression for the default optional-component update keeps edited INI bytes. It fails against the previous module and passes after the repair-selection fix. Eight memory-only `SetupSelection.Tests.ps1` cases also pass in both shells. They cover already-owned optional roles, missing-file selection, fresh opt-in, and refusing an unknown role without deleting a DLL. The guarded suite covers missing core files, shared ownership, interrupted recovery, locks, path aliases/junctions and HTTPS redirects.
- **Separate NR download.** 34 guarded installer cases and 12 memory-only feature-selection cases passed in each shell (exit 0). New cases check that NR includes its bridge, unchecked repair keeps an owned NR model, FG-only repair never selects an available model, and pre-existing or modified model files keep the existing collision and ownership protections. The downloaded RHI archive hash and model hash match the lock and the previously accepted game-root model. The model was not loaded during the installer audit. The final package's FG/NR dependency fixture is a separate check; these synthetic cases do not cover it.
- **Production GUI worker.** This regression brings the guarded installer suite to 35 cases in each shell (exit 0). It extracts the real worker body and runs install, unchecked repair, diagnose and uninstall in normal, autoload-disabled and cold Core-only runspaces. The old body reproduces the missing-command failure. Explicit engine-local Management/Utility imports fix it.

Dependency and package checks:

- The full `Dependencies.Tests.ps1` fixture checks every pinned dependency, installed file hash, notice link, repair and removal. It does not run its disposable game files.
- The final 0.7 FG-only fixture verified 313 installed hashes and 148 notice links. The NR fixture verified 314 hashes and 148 links. Both completed installation, unchecked repair and removal, exit 0.
- The release ZIP audit verified 128 files with complete checksums, 54 notice/source files and 50 upstream snapshot hashes. The only packaged binaries are the project DLL pair and Setup.exe.

An independent source review found three installer issues. Each got a fix and a regression case. Hosted CI runs the CPU/installer checks, not rendering. The release checksums identify the final DLLs and packaged files. The release contains no game assemblies or runtime download caches.

[Release readiness](RELEASE-READINESS.md) and [the checklist](RELEASE-CHECKLIST.md) list the alpha's scope and known licence/validation limits. Other DPI configurations and successful Reflex operation are not verified. This page is not a legal-clearance statement.
