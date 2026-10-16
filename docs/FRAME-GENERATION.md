# Frame generation and neural rendering

Version 0.7 adds saved Home profiles for three frame-generation providers and Neural Rendering. After offline preparation it was tested on the observed RTX 4090 / DX11 setup. NVIDIA FG and Default-style NR received positive visual checks. AMD FG was functional but judged inferior; Intel FG produced marked interpolated frames with a reported performance regression. These observations do not establish performance on other hardware or a controlled provider benchmark. Configuration and file checks alone cannot establish active rendering.

Install the optional bridge using Setup while the game is closed. Fresh defaults keep FG and NR off. Existing OptiScaler settings are preserved. Use **Home → Frame Gen** or **Home → Neural**, make a choice, then quit normally and restart Big Walk. The panel separates startup configuration from the next-launch request. **F10** remains the advanced OptiScaler menu and the place to inspect actual provider state and errors. Save any F10 changes before queuing Home choices; saving the bridge's older in-memory settings afterwards can overwrite the queued profile.

## Frame-generation providers

| Choice | Runtime files supplied by optional setup | Requirements and current evidence |
| --- | --- | --- |
| NVIDIA DLSS FG | Production Streamline 2.14.1 plus DLSS FG | Official direct FG requires RTX 40 or newer, Windows build 19041+, and hardware-accelerated GPU scheduling. User confirmed the 0.7 picture/controls and preferred this provider on the RTX 4090. NVIDIA reported FG preset C effective. |
| AMD FSR FG | Signed FidelityFX loader 2.3.0.2740 and frame-generation DLL 4.0.1.2740 | Cross-vendor route. Successful dispatches, generated-only motion and user off/on checks support operation on the RTX 4090; quality/performance was judged worse than NVIDIA. Day/night readings were not a controlled comparison. |
| Intel XeSS FG | XeSS SDK 3.0.2's FG 1.3.1.78 and XeLL 1.3.2.10 | Supports Intel hardware and non-Intel Shader Model 6.4 GPUs. Requires XeLL and a recent Visual C++ runtime. Use borderless or windowed mode. Interpolated-frame debug markers appeared on the RTX 4090, but user A/B reported 71 FPS off and 40 on: not recommended for that tested setup. |

Profiles use one generated frame per rendered frame and clear F10's conflicting Force XeLL mode, which disables frame generation. Multi-frame generation is not exposed without real capability evidence. Changing NVIDIA's FG preset C is a driver-profile operation and is separate from SR model preset L; this mod does not alter driver profiles. Prefer a stable rendered-frame rate before enabling FG. Bars, foliage and fast movement need close comparison because interpolation can introduce shimmer.

Home's **Enabled** control saves the next-launch request. For a live comparison, use F10's provider **Active** control (NVIDIA labels its state **DLSSG State Active**). Changing **FG Input** is a different operation. Compare the same moving view with FG disabled and enabled, checking actual displayed presents as well as responsiveness; an engine FPS counter may only count rendered frames.

Pinned OptiScaler can terminate the process when direct NVIDIA FG is unsupported, after changing its replacement setting. Home checks known prerequisites before saving that route, but the provider API remains authoritative. Do not bypass checks or assume a DLL's presence proves support. See [compatibility](COMPATIBILITY.md) and retain a known-good profile.

The pinned NVIDIA provider requests **NVIDIA Reflex Low Latency** when enabling FG, as shown in the [provider source](https://github.com/Skynizz/optiscaler-dlss5/blob/6bfc4389c25054ef6747df6e6b1b470e9541c4da/OptiScaler/framegen/dlssg/DLSSG_Dx12.cpp#L300). A present DLL or saved request does not establish successful Reflex initialization or measured latency. Its feature attribution and the applicable [UI guidelines](https://github.com/NVIDIA-RTX/Streamline/blob/2122257e0fce486f91b385aa63b9a09b0a34b363/docs/RTX%20UI%20Developer%20Guidelines.pdf) remain part of the pre-release checks.

F10 can report FG status. Its generic Low Latency page describes a different bridge abstraction and can show no Reflex support while the NVIDIA FG provider makes a direct Streamline Reflex request. Successful Reflex operation needs separate runtime telemetry or integration verification; neither that generic value nor absence of an error message proves it.

If a saved feature prevents startup, the [recovery guide](INSTALLATION.md#diagnose) explains how to disable FG and NR without reaching Home. Install/Repair preserves those settings.

## Neural rendering

The optional package includes OptiScaler's NR forwarder. Supply a model only if you obtained it under terms permitting your use, at `OptiScaler/nvngx_dlssnr.dll`. No model is downloaded, extracted from another game, mirrored or redistributed by this project. A correctly named file still may fail initialization or lack required exports.

The user's existing root-level NR model and forwarder layout is also recognized. That model evaluated at 3440×1440 with real 2293×960 depth/motion guides; Default style plus NVIDIA FG received a positive in-world colour, motion and controls check. AMD/Intel FG combined with NR remain unverified.

NR's DX11 integration needs an OptiScaler backend using the DX12 interoperability route: the supplied DLSS and XeSS selections use this route; native `fsr31` does not reach the pinned NR call site. This is an implementation limitation of this integration, rather than a general claim that NR requires DLSS Super Resolution.

Start with **Default** style and strengths of 1. Natural and Cinematic are community style names in the pinned bridge. The tested user's Natural style reduced saturation; Default restored the preferred colour. Colour strength 0 retains the game's hue in the composed path. Model preset numbers are NR-specific and are separate from SR preset letters. F10 exposes experimental cache, codec and resolution controls; those can change quality and performance and are not automatically enabled by this mod.

NVIDIA's [October 13 announcement](https://www.nvidia.com/en-us/geforce/news/dlss-5-faster-ai-model-hogwarts-legacy-rtx-spark-laptops/) schedules the faster NR model's debut for October 16 in supported products. Its roughly 2× claim concerns model performance, not a doubling of whole-game FPS. No official public standalone download was established in the checked NVIDIA SDK trees or Streamline 2.14.1 archive. The project therefore makes no latest-model or faster-model compatibility claim. A user's RTX 4090 test in another game is useful compatibility evidence; it does not establish official support or validate a new model in Big Walk. Home does not apply an RTX 50-only NR block.

Exact parser values and source provenance are pinned to [OptiScaler v1.2.1](https://github.com/Skynizz/optiscaler-dlss5/tree/6bfc4389c25054ef6747df6e6b1b470e9541c4da). Intel's [FG guide](https://github.com/intel/xess/blob/8fe81bdbbaf00b3c1b733fd0d830c333dc84e6f0/doc/xess_fg_developer_guide_english.md) describes cross-vendor support, XeLL and presentation restrictions. Dependency URLs and hashes are in the [lock](../packaging/dependencies.lock.json); licences and outstanding release questions are in [the notices](../THIRD_PARTY_NOTICES.md).
