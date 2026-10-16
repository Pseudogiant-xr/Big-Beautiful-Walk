# Changelog

## 0.7.0 — private test candidate

- Add Home profiles for NVIDIA, AMD and Intel frame generation, with dependency checks and startup/next-launch status.
- Add Neural Rendering settings and its integration forwarder; no proprietary NR model is included.
- Stage pinned AMD FG and Intel FG / XeLL runtimes through the optional installer.
- Save coordinated provider settings as one lossless, backed-up INI edit.
- Add in-app credits and install the full attribution/licence collection.
- License original project code under Apache 2.0 while preserving Unity and vendor-specific terms and historical MIT permissions.
- Restore vegetation material values at the override write boundary instead of replaying quality-table fade guesses; preserve subsequent game changes.
- Repair previously owned optional bridge components during default Install / Repair, preserving edited settings.
- Add missing Detours/JSON notices, specific Anti-Lag 2/LatencyFleX attribution and NVIDIA Reflex Low Latency feature naming.
- Explain first-use verification, the Visual C++ prerequisite and startup recovery; link Setup to the feature guide and licences.

NVIDIA FG and Default-style NR received in-world visual checks on the tested RTX 4090. AMD FG operated with poorer results; Intel FG produced interpolated frames but reduced measured FPS on this setup. Preset cycling, distant tree shadows under Extreme and nearby foliage restoration under Game defaults were confirmed after the vegetation binding corrections. Publication remains private pending the permission questions in `docs/LICENSING.md`.

## 0.6.0 — public alpha candidate

- Rename the project Big Beautiful Walk and add an Overview tab with DLSS status and graphics presets.
- Retain the game's graphics settings on first installation; enable shadow/LOD presets only by explicit selection.
- Add a Windows setup window with Steam discovery, verified dependency downloads, diagnostics and ownership-aware removal.
- Preserve editable profiles and shared BepInEx files used by other mods.
- Disable renderer changes on untested game builds, unsupported graphics APIs and XR.
- Disable intrusive diagnostic screenshots and pixel readbacks by default.
- Keep the existing native DLSS reconstruction, stock post effects, vegetation controls and experimental 16K atlas option.

This candidate is tested on one RTX 4090 system. Optional FG/NR integration and third-party release obligations are described in the documentation.
