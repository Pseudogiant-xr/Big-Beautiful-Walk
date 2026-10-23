# Frame generation and neural rendering

Version 0.7 lets you save Frame Generation (FG) and Neural Rendering (NR) profiles in Home. You can pick one of three FG providers.

Tested on one RTX 4090 with DX11:

- NVIDIA FG looked best.
- Neural Rendering with the Default style looked good.
- AMD FG worked, but looked and ran worse than NVIDIA.
- Intel FG produced interpolated frames, but the game ran slower with it on.

Not tested on other hardware, and the providers were not benchmarked against each other under controlled conditions. Settings and files being in place do not prove a feature is actually rendering.

## Turning it on

1. Close the game and run Setup. Tick the optional bridge, or the Neural Rendering model checkbox, which includes the bridge. Fresh installs keep FG and NR off. Setup keeps any OptiScaler settings you already have.
2. If you changed anything in **F10**, save it first. If you save F10 after queuing a Home choice, the bridge can write its older settings over your new profile.
3. In the game, open **Home → Frame Gen** or **Home → Neural** and make your choice.
4. Quit the game normally and start Big Walk again.

The panel shows what this launch started with and what is queued for the next launch. **F10** is still the advanced OptiScaler menu. Use it to see what the provider is really doing and any errors.

On AMD and Intel GPUs, NVIDIA DLSS FG and Neural Rendering are greyed out because they need an NVIDIA RTX GPU. AMD FSR FG and Intel XeSS FG stay available; they have not been tested on AMD or Intel hardware yet.

## Frame-generation providers

| Choice | Files the optional setup installs | Requirements and test results |
| --- | --- | --- |
| NVIDIA DLSS FG | Production Streamline 2.14.1 plus DLSS FG | Official direct FG needs an RTX 40 GPU or newer, Windows build 19041+, and hardware-accelerated GPU scheduling. On the RTX 4090, the 0.7 picture and controls were confirmed and this was the preferred provider. NVIDIA reported FG preset C as effective. |
| AMD FSR FG | Signed FidelityFX loader 2.3.0.2740 and frame-generation DLL 4.0.1.2740 | Works across GPU vendors. On the RTX 4090 it dispatched frames, showed generated-only motion, and passed off/on checks. Quality and performance were worse than NVIDIA. The day and night readings were not a controlled comparison. |
| Intel XeSS FG | XeSS SDK 3.0.2's FG 1.3.1.78 and XeLL 1.3.2.10 | Supports Intel hardware and non-Intel GPUs with Shader Model 6.4. Needs XeLL and a recent Visual C++ runtime. Use borderless or windowed mode. On the RTX 4090 the debug markers for interpolated frames appeared, but an off/on test gave 71 FPS off and 40 FPS on. Not recommended on that setup. |

Each profile generates one frame per rendered frame. It also turns off F10's **Force XeLL** mode, because that mode disables frame generation. Multi-frame generation is not offered until it is shown to work.

NVIDIA's FG preset C is a driver-profile setting. It is separate from the SR model preset L, and this mod does not change driver profiles.

Get a steady rendered frame rate before you turn FG on. Look closely at bars, foliage and fast movement, because interpolation can make them shimmer.

### Comparing with FG on and off

Home's **Enable Frame Generation after restart** toggle only saves a request for the next launch. To switch FG live, use F10's provider **Active** control. For NVIDIA it is labelled **DLSSG State Active**. Changing **FG Input** does something else.

Compare the same moving view with FG off and on. Check the frames actually shown on screen, and how responsive the game feels. The game's own FPS counter may only count rendered frames.

### Safety

The pinned OptiScaler can force the game to quit if you change its replacement setting and direct NVIDIA FG is not supported. Home checks the known requirements before it saves that route, but the provider has the final say. Do not bypass the checks. A DLL being present does not mean your system supports the feature. See [compatibility](COMPATIBILITY.md), and keep a profile you know works.

If a saved feature stops the game from starting, the [recovery guide](INSTALLATION.md#diagnose) shows how to turn off FG and NR without opening Home. Install/Repair keeps those settings.

### NVIDIA Reflex

When you turn on FG, the pinned NVIDIA provider asks for **NVIDIA Reflex Low Latency**, as shown in the [provider source](https://github.com/Skynizz/optiscaler-dlss5/blob/6bfc4389c25054ef6747df6e6b1b470e9541c4da/OptiScaler/framegen/dlssg/DLSSG_Dx12.cpp#L300). A DLL being present, or a saved request, does not show that Reflex started or how much latency it saves. Reflex attribution and the relevant [UI guidelines](https://github.com/NVIDIA-RTX/Streamline/blob/2122257e0fce486f91b385aa63b9a09b0a34b363/docs/RTX%20UI%20Developer%20Guidelines.pdf) are still on the pre-release checklist.

F10 can show FG status. Its general Low Latency page belongs to a different part of the bridge. It can show no Reflex support even while the NVIDIA FG provider asks Streamline for Reflex directly. Confirming that Reflex works needs separate runtime telemetry or an integration test. Neither that page's value nor the lack of an error message proves it.

## Neural rendering

Setup has a separate checkbox, **Install community RHI SF-v2 Neural Rendering model**. It downloads RHI's community-hosted SF-v2 model, plus the bridge and forwarder it needs. The download is about 111 MiB and installs as `OptiScaler/nvngx_dlssnr.dll`. NR stays off until you turn it on in Home and restart. The core install and the bridge-only install do not download it.

You can use an existing compatible model instead. If Setup finds conflicting files, it refuses to continue instead of overwriting another mod's copy.

### About the model

The model is not part of this project's repository or ZIP. It is a pinned, unsigned community variant, not an official NVIDIA SDK release. [The provenance notice](licenses/RHI-Neural-Rendering-NOTICE.md) gives its source, hashes and licence limits.

The downloaded DLL is byte-for-byte the same as the model used in the earlier successful NR test. A model with a different name, or a newer one, will not necessarily work.

An NR model and forwarder already in the game's root folder are also recognised. In testing, that model ran at 3440×1440 using real 2293×960 depth and motion guides. With the Default style and NVIDIA FG, colour, motion and controls all looked right in the game world. NR with AMD or Intel FG has not been tested.

### Which upscaler NR needs

In DX11, NR needs an OptiScaler backend that uses the DX12 interop route. The supplied DLSS and XeSS options do. Native `fsr31` does not reach the pinned NR call. This is a limit of this mod's integration. It does not mean NR in general needs DLSS Super Resolution.

### Settings

Start with the **Default** style and all strengths at 1.

- **Natural** and **Cinematic** are community style names from the pinned bridge. In testing, Natural reduced saturation and Default brought back the preferred colour.
- A colour strength of 0 keeps the game's hue in the composed image.
- Model preset numbers belong to NR. They are not the same as the SR preset letters.
- F10 has experimental cache, codec and resolution controls. They can change quality and performance. This mod does not turn them on for you.

### NR 310.9.16 local import

For a new install, download and extract the separate [NR 310.9.16 pack](https://github.com/Pseudogiant-xr/Big-Beautiful-Walk/releases/download/v0.8.1-alpha/Big-Beautiful-Walk-NR-310.9.16.zip), then select its DLL with Setup. This same pack works with 0.8.2.

Setup can import your existing `nvngx_dlssnr.dll` 310.9.16 compatibility build. Click **Use my NR 310.9.16 compatibility DLL...**, select the extracted DLL and choose **Install / Repair**. Selecting it includes the bridge. The supported DLL has SHA-256 `CDFA6C1EC784867182164C7836F324F087B5C9F374D0C9FE46F54999FEABF3ED`, matching [OpenNR's reference output](https://github.com/clshortfuse/openNR/tree/57ca170cb836f549f0e64544f282d535666b0f7d). Other bytes, including the unmodified RTX 50 runtime, are rejected. The main mod ZIP does not contain the model; the separate optional NR pack supplies it. No Discord attachment URL is used.

The command-line equivalent is:

```powershell
.\Setup.cmd -Headless -GamePath 'E:\SteamLibrary\steamapps\common\Big Walk' -NeuralModelPath 'E:\Models\nvngx_dlssnr.dll'
```

With NR 310.9.16, **Home → Neural → Model preset** offers **1 · A: Standard** (the older model) and **2 · B: Lightweight FP8** (the newer model). **0 · Auto** leaves the runtime to choose; 310.9.16 defaults to B. These are NR presets, separate from DLSS SR preset L. Save, quit normally and restart after changing Home settings. The model selection is documented by [the OptiScaler consumer mapping](https://github.com/machiavelli31/DLSS-AiO-Swapper/blob/main/app/src/core/nr-model.js). No Big Walk image-quality or performance improvement is claimed yet.

Install/Repair preserves your existing FG/NR settings and retains an unchanged, installer-owned imported model, even without selecting it again. If that installed model is missing, select the DLL again to repair it. Edited or unowned conflicting models keep the normal refusal protections. Uninstall removes the owned installed copy and leaves your source DLL alone. Without a local selection, fresh NR installs still download the earlier SF-v2 model.

### The newer NVIDIA model

NVIDIA's [October 13 announcement](https://www.nvidia.com/en-us/geforce/news/dlss-5-faster-ai-model-hogwarts-legacy-rtx-spark-laptops/) says a faster NR model debuts on October 16 in supported products. Its roughly 2× figure is about the model's own speed, not double the frame rate of the whole game.

The optional SF-v2 download is the earlier, tested model, not that update. The 310.9.16 local-import option has passed installer checks, but rendering and performance with it in Big Walk remain unverified. One RTX 4090 test in another game does not mean official support, and does not show that a new model works in Big Walk. Home does not limit NR to RTX 50 GPUs.

## Sources

Exact parser values and source references are pinned to [OptiScaler v1.2.1](https://github.com/Skynizz/optiscaler-dlss5/tree/6bfc4389c25054ef6747df6e6b1b470e9541c4da). Intel's [FG guide](https://github.com/intel/xess/blob/8fe81bdbbaf00b3c1b733fd0d830c333dc84e6f0/doc/xess_fg_developer_guide_english.md) covers cross-vendor support, XeLL and presentation limits. Download URLs and hashes are in the [lock file](../packaging/dependencies.lock.json). Licences and open release questions are in [the notices](../THIRD_PARTY_NOTICES.md).
