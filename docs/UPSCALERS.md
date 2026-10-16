# Upscalers and presets

Big Beautiful Walk upscales with NVIDIA DLSS Super Resolution by default. The other backends need the optional OptiScaler bridge. XeSS and the FSR API also need extra runtime files from their vendors.

This release needs an NVIDIA GPU, even if you choose FSR or XeSS. Those algorithms may run on other vendors' GPUs, but this build does not support systems without an NVIDIA GPU.

A backend showing up in a menu, or its DLL loading, does not prove that backend drew the frame you see.

## Backend availability

| Backend | OptiScaler 1.2.1 DX11 setting | Extra files | Tested result and limits |
| --- | --- | --- | --- |
| DLSS | `dlss_12` for the optional bridge. Core DLSS also works without the bridge. | Core `nvngx_dlss.dll` | Official SR 310.9.1 ran steadily in the game world on the tested RTX 4090. |
| FSR 3.1 | `fsr31` | None beyond OptiScaler | Native DX11 FSR 3.1.2 started, kept upscaling and looked right on the tested setup. |
| XeSS | `xess_12` | `OptiScaler/libxess.dll` | The DX12 bridge started, kept upscaling and looked right. A slight flicker was reported. Its cause is unknown, and any link to VRR is unconfirmed. Native `xess` only works on Intel Arc. |
| FSR 4 | `ffx_12` | AMD FSR loader and upscaler DLLs | Official FSR 4.1.1 needs a supported AMD RX 7000 / RX 9000 GPU or newer. Not supported on the tested RTX 4090. No claim is made that it works. |

### Changing backend

1. Pick the backend in Home. Home saves it for the next launch and shows both the backend running now and the queued one.
2. Do not save a different choice in OptiScaler after queuing one in Home.
3. Restart the game.
4. Check which backend is actually running. OptiScaler can fall back to another backend if the one you asked for cannot load.

Keep your existing OptiScaler settings. The game itself keeps running on DX11 when the bridge uses a DX12 device in the background.

### Sources

The config values in the table come from [OptiScaler's exact v1.2.1 mapping](https://github.com/Skynizz/optiscaler-dlss5/blob/v1.2.1/OptiScaler/OptiTypes.cpp) and its [DX11 feature provider](https://github.com/Skynizz/optiscaler-dlss5/blob/v1.2.1/OptiScaler/upscalers/FeatureProvider_Dx11.cpp). Intel documents the [native DX11 hardware restriction](https://github.com/intel/xess/blob/8fe81bdbbaf00b3c1b733fd0d830c333dc84e6f0/doc/xess_sr_developer_guide_english.md#initialization-d3d11-specific). AMD documents [FSR 4's hardware limit](https://gpuopen.com/manuals/fsr_sdk/techniques/super-resolution-ml/#limitations).

## DLSS quality modes

Quality mode and render preset are two separate settings. Quality mode sets the input resolution. The render preset picks the reconstruction model and how it behaves.

| Quality mode | NGX value | Typical input scale per side | Status |
| --- | --- | --- | --- |
| DLAA | 5 | 100% | Native resolution. Experimental in this rendering hook until tested on its own. |
| Ultra Quality | 4 | 77%, a custom starting point | Experimental. The SDK listing this mode does not mean the runtime supports it or uses this ratio. |
| Quality | 2 | About 67% | The tested baseline. |
| Balanced | 1 | About 58% | Needs its own rendering test. |
| Performance | 0 | 50% | Needs its own rendering test. |
| Ultra Performance | 3 | About 33% | Meant for high output resolutions. Needs its own rendering test. |

The values come from the [pinned NVIDIA SDK header](https://github.com/NVIDIA/DLSS/blob/374959484e79a640feaba44c93ac8cfb0a03f5b5/include/nvsdk_ngx_defs.h). NVIDIA describes [the usual Quality, Balanced, Performance and Ultra Performance scales](https://developer.nvidia.com/blog/tips-getting-the-most-out-of-the-dlss-unreal-engine-4-plugin/). What the runtime supports, and the render size it actually uses, always win. The 77% for Ultra Quality is this mod's experimental choice.

## DLSS render presets

The public core pins SDK and runtime 310.9.1. Its [header](https://github.com/NVIDIA/DLSS/blob/374959484e79a640feaba44c93ac8cfb0a03f5b5/include/nvsdk_ngx_defs.h) defines:

- Default=0, J=10, K=11, L=12 and M=13.
- E=5 and F=6 as deprecated legacy choices.

A–D were removed. G, H, I, N and O are reserved or documented to fall back to the default. A legacy letter shown in the menu is not a separate supported model.

The default is L. Quality and preset changes for direct DLSS apply straight away.

With the optional bridge, OptiScaler overrides its backend's preset when `[DLSS] RenderPresetOverride=true` and `RenderPresetForAll=<value>` are set. With `false`, the preset hint from Home stays in control. The DLSS runtime or the driver can still override the preset, so check the preset actually in use after you restart with a bridge setup.

FG preset C is a different thing: an NVIDIA driver-profile setting with value 3. It is not an OptiScaler render-preset key. Setting the SR preset to C would not set FG preset C. The mod does not silently change driver profiles.
