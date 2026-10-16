# Changelog

## 0.7.0 — alpha

### Added

- Home profiles for NVIDIA, AMD and Intel frame generation. Each checks its dependencies and shows its status at startup and for the next launch.
- Neural Rendering settings and the forwarder that connects them. No proprietary Neural Rendering model is included.
- Optional download of the Neural Rendering model from the RHI community mirror, pinned to the exact SF-v2 model tested in Big Walk. It is separate from the release ZIP and off by default.
- The optional bridge now installs pinned AMD FG and Intel FG / XeLL runtimes.
- Setup.exe, a small graphical launcher that opens the installer without a terminal window. Setup.cmd stays for console diagnostics and command-line use.
- An in-game credits page. The full attribution and licence collection is now installed with the mod.
- Notices for Detours and nlohmann JSON, specific attribution for Anti-Lag 2 and LatencyFleX, and the correct feature name for NVIDIA Reflex Low Latency.
- Documentation for first-launch checks, the Visual C++ prerequisite and recovering from a failed startup. Setup now links to the feature guide and the licences.

### Changed

- Provider settings are saved together in one INI edit that keeps every other setting and backs up the previous file.
- Original project code is now licensed under Apache 2.0. Unity and vendor-specific terms still apply to their parts, and the earlier MIT permissions still stand.
- Vegetation material values are now restored at the point where the override is written, instead of being guessed back from the quality table. Later changes made by the game are kept.
- The default Install / Repair also repairs optional bridge components that were installed before, without touching edited settings.

### Fixed

- The background installer now loads PowerShell's built-in modules explicitly, so a freshly started GUI worker can hash files and read JSON without relying on module autoloading.

### Testing

Tested on one RTX 4090:

- NVIDIA FG and Neural Rendering with the Default style were checked in-world and looked right.
- AMD FG worked, with worse results.
- Intel FG produced interpolated frames but lowered the measured frame rate.
- Cycling presets, distant tree shadows on Extreme, and nearby foliage returning to normal on Game defaults all worked after the vegetation fixes.
- The final build generated BepInEx interop files from a fresh install and launched again cleanly. The picture and the Home and F10 menus worked in-world, and Setup.exe opened without an empty terminal window.

Other hardware has not been tested. Component licence terms are in `docs/LICENSING.md`.

## 0.6.0 — public alpha candidate

- Renamed the project Big Beautiful Walk and added an Overview tab with DLSS status and graphics presets.
- A first install keeps the game's own graphics settings. Shadow and LOD presets apply only when you select them.
- Added a Windows setup window with Steam discovery, verified dependency downloads, diagnostics, and removal that only touches files Setup installed.
- Editable profiles and BepInEx files shared with other mods are kept.
- Renderer changes are disabled on untested game builds, unsupported graphics APIs and XR.
- Diagnostic screenshots and pixel readbacks are off by default.
- Kept the native DLSS reconstruction, stock post effects, vegetation controls and the experimental 16K atlas option.

Tested on one RTX 4090 system. The documentation covers the optional FG / NR integration and third-party licence obligations.
