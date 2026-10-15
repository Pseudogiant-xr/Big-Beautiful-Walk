# Upscalers and presets

The core path uses NVIDIA DLSS Super Resolution. Alternative backends require the optional OptiScaler bridge and, for XeSS or the FSR API, additional upstream runtimes. A backend appearing in a menu or loading a DLL does not prove that it produced the displayed frame.

## Backend availability

| Backend | OptiScaler 1.2.1 DX11 setting | Additional files | Validation / limitation |
| --- | --- | --- | --- |
| DLSS | `dlss_12` for the optional bridge; core DLSS also works directly | Core `nvngx_dlss.dll` | Official SR 310.9.1 sustained evaluation in the world on the observed RTX 4090 setup. |
| FSR 3.1 | `fsr31` | None beyond OptiScaler | Native DX11 FSR 3.1.2 initialized, sustained reconstruction and was visually confirmed on the tested setup. |
| XeSS | `xess_12` | `OptiScaler/libxess.dll` | The DX12 bridge initialized, sustained reconstruction and was visually confirmed. A slight flicker was reported; its cause, including any VRR relation, remains unconfirmed. Native `xess` is limited to Intel Arc. |
| FSR 4 | `ffx_12` | AMD FSR loader and upscaler DLLs | Official FSR 4.1.1 requires supported AMD RX 7000 / RX 9000 hardware or later. Unsupported on the observed RTX 4090; no working claim is made. |

Backend changes through the mod's saved configuration require a restart. Home distinguishes the current session from the next-start choice. Preserve existing OptiScaler settings and confirm the actual backend after restarting; OptiScaler can fall back when a requested backend cannot load. Avoid saving conflicting choices in OptiScaler after queuing a change in Home. The game itself continues using DX11 when the bridge uses a background DX12 device.

The source-backed config tokens above come from [OptiScaler's exact v1.2.1 mapping](https://github.com/Skynizz/optiscaler-dlss5/blob/v1.2.1/OptiScaler/OptiTypes.cpp) and [DX11 feature provider](https://github.com/Skynizz/optiscaler-dlss5/blob/v1.2.1/OptiScaler/upscalers/FeatureProvider_Dx11.cpp). Intel documents the [native DX11 hardware restriction](https://github.com/intel/xess/blob/8fe81bdbbaf00b3c1b733fd0d830c333dc84e6f0/doc/xess_sr_developer_guide_english.md#initialization-d3d11-specific); AMD documents [FSR 4's hardware limit](https://gpuopen.com/manuals/fsr_sdk/techniques/super-resolution-ml/#limitations).

## DLSS quality modes

Quality mode controls input resolution; a render preset selects the reconstruction model/behavior. They are separate settings.

| Quality mode | NGX value | Typical input scale per dimension | Status |
| --- | --- | --- | --- |
| DLAA | 5 | 100% | Offered as native resolution; experimental in this rendering hook until separately verified. |
| Ultra Quality | 4 | 77% custom starting point | Experimental; enum availability does not guarantee runtime support or this ratio. |
| Quality | 2 | About 67% | Baseline tested mode. |
| Balanced | 1 | About 58% | Separate rendering validation required. |
| Performance | 0 | 50% | Separate rendering validation required. |
| Ultra Performance | 3 | About 33% | Intended for high output resolutions; separate rendering validation required. |

The values come from the [pinned NVIDIA SDK header](https://github.com/NVIDIA/DLSS/blob/374959484e79a640feaba44c93ac8cfb0a03f5b5/include/nvsdk_ngx_defs.h). NVIDIA describes [the usual Quality, Balanced, Performance and Ultra Performance scales](https://developer.nvidia.com/blog/tips-getting-the-most-out-of-the-dlss-unreal-engine-4-plugin/). The runtime's supported settings and actual render dimensions remain authoritative; Ultra Quality's 77% is an experimental application choice.

## DLSS render presets

The public core pins SDK/runtime 310.9.1. Its [header](https://github.com/NVIDIA/DLSS/blob/374959484e79a640feaba44c93ac8cfb0a03f5b5/include/nvsdk_ngx_defs.h) defines Default=0, J=10, K=11, L=12 and M=13. E=5 and F=6 are deprecated legacy choices. A–D were removed; G, H, I, N and O are reserved or documented to revert to the default. A displayed legacy letter must not be presented as a distinct supported model.

The baseline is L. Direct DLSS quality and render-preset changes apply live. For the optional bridge, OptiScaler uses `[DLSS] RenderPresetOverride=true` and `RenderPresetForAll=<value>` to override its backend; `false` leaves the caller's hint in control. Runtime/driver overrides can change the result, so inspect the effective setting after restarting a bridge configuration.

FG preset C is a different NVIDIA driver-profile setting, with value 3. It is not an OptiScaler render-preset key, and changing SR preset C would not configure FG C. The mod does not silently change driver profiles.
