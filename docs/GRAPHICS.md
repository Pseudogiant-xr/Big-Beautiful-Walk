# Graphics guide

Press **Home** to open Big Beautiful Walk. All graphics overrides start off, so the game's own settings stay in charge until you change something. Balanced, Quality and Extreme are presets you pick yourself. Try them in the same scene before you keep one.

## Controls

Each graphics row has a short explanation underneath it, including what higher or lower values do and where extra detail can cost frame rate or graphics memory. Upscaling, Frame Gen and Neural also explain their choices. Start with Balanced on Overview; try Quality or Extreme in the same view if you want more detail.

Changing a control turns on its override. Number fields take effect when you press **Enter**. Changes save a moment after you stop editing.

Untick an override to go back to the value the game had before. This works unless another system has changed that value in the meantime. Changing the game's own quality preset can still affect any setting you have not overridden.

Your settings live in two files:

- Graphics profile: `BepInEx/config/local.bigwalk.advanced-graphics.cfg`
- DLSS profile: `BepInEx/config/local.bigwalk.native-dlss.cfg`

Diagnostic shortcuts and capture probes are off by default.

## What the optional tools do

- **DLSS Super Resolution** can improve frame rate while keeping a detailed picture. The GPU draws fewer pixels, then AI uses motion and earlier frames to rebuild detail. It needs an NVIDIA RTX GPU.
- **FSR 3.1** aims for the same speed benefit across GPU brands. It combines a smaller picture, motion and earlier frames to rebuild detail without the DLSS AI model.
- **XeSS Super Resolution** uses AI and earlier frames to rebuild detail from fewer pixels. It can work on different GPU brands; compare its moving picture with DLSS or FSR.
- **Frame Generation** makes movement look smoother by adding pictures between real game frames. It does not make controls respond faster. NVIDIA and Intel use AI; the pinned AMD path uses motion between frames. Each can produce visual glitches.
- **Neural Rendering** uses AI to redraw lighting and surface detail for a more realistic look. It can change the art style and reduce frame rate. It is separate from upscaling and is not ray tracing.
- **DLAA / Native AA** smooths edges at full resolution, favouring image quality rather than an upscaling speed boost.

See the publishers' explanations: [NVIDIA DLSS](https://www.nvidia.com/en-us/geforce/technologies/dlss/), [AMD FSR](https://gpuopen.com/fidelityfx-super-resolution-3/) and [Intel XeSS](https://www.intel.com/content/www/us/en/developer/articles/technical/xess2-whitepaper.html). Hardware requirements and this mod's test results are in [the upscaler guide](UPSCALERS.md) and [FG / NR guide](FRAME-GENERATION.md).

## DLSS Super Resolution

By default DLSS renders at `0.666667` of your resolution on each side. That is DLSS Quality. The default render preset is L.

The panel's status line tells you whether DLSS is still waiting or is actually running. An FPS counter on its own does not tell you that DLSS is active.

The **Upscaling** tab in Home offers these choices:

- Quality modes: DLAA, Ultra Quality, Quality, Balanced, Performance and Ultra Performance.
- Render presets: Default, J, K, L and M, plus the legacy E and F.

Quality and preset changes for direct DLSS apply straight away. DLAA and Ultra Quality stay experimental until they are tested on their own. A working Quality run does not prove they work.

### FSR 3.1 and XeSS

The FSR 3.1 and XeSS backends run through OptiScaler. When you pick one of these, Home saves it for the next launch and you need to restart the game. The panel shows both the backend running now and the one queued for next start. FSR 4 is not available on the tested RTX 4090. The [upscaler feature matrix](UPSCALERS.md) lists the exact requirements and preset limits.

The mod keeps the game's post-processing around the DLSS pass. If you already had OptiScaler installed, it may switch the backend. If the picture differs from what direct DLSS gives you, check OptiScaler's status. Do not save different backend choices in Home and in OptiScaler before you restart.

## Shadows and vegetation

Shadow distance, atlas size, cascade count and cascade boundaries work together. The shadow map has a fixed size, so a longer distance spreads it over more of the world and each area gets less detail. The engine allows at most four cascades.

Cascade boundaries show in metres but are saved as fractions of the total distance. If you change the total distance, the boundaries you have turned on scale with it.

### The 16384 shadow atlas

The experimental 16384 main atlas only appears if your GPU reports a large enough maximum texture size. With four cascades, each cascade gets a quarter of the atlas, so up to 8192 pixels per cascade.

Going from an 8192 atlas to 16384 takes four times the atlas memory. A single 16-bit, 16384-square depth surface is about 512 MiB. The real GPU allocation and the cost per frame have not been measured. The engine accepting the setting does not tell you what the GPU actually allocates.

### Vegetation

Vegetation shadows cannot reach further than the main light's shadow distance.

The Unity Terrain controls change details and trees that belong to Unity Terrain. They may not change landscape or foliage built from ordinary meshes.

Tested on one setup:

- Extreme's 2000 m vegetation fade works.
- **Game defaults** puts back the game's shorter range, so distant tree shadows fade out again.

Not tested: other scenes, changing the game's quality setting while an override is on, and every material variant.

## Optional Frame Generation and Neural Rendering

**F10** opens the optional OptiScaler menu.

Setup's optional bridge checkbox downloads the pinned upscaler and Frame Generation files. Frame Generation (FG) starts off, so you can compare with and without it. The bridge checkbox does not download the Neural Rendering model. See [the dependency policy](DEPENDENCIES.md).

What to expect:

- FG can make foliage shimmer.
- Neural Rendering (NR) can change how the game looks. **Default** was the preferred style in prototype testing. **Natural** reduced saturation.
- Neither feature has been tested on a wide range of hardware or on a fresh install.
- NR is not ray tracing. It does not add distance-field or ray-traced shadows.

## Experimental shadow-culling optimisation

**Shadows → Reduce duplicate shadow work** exposes Unity's conservative enclosing-sphere culling. It attempts to reduce repeated shadow casting in overlapping cascade regions, while keeping the selected shadow distance, cascade splits and map resolution. It is off until you enable its override, and selecting a graphics preset clears custom overrides as usual.

This is a candidate for investigating Extreme's dawn/dusk slowdown, not a verified fix. Low-angle sunlight may increase shadow-casting work, but no controlled dawn/dusk profile has established the cause here. Compare frame rate and shadow edges in the same view with the option off/on; restore the setting if shadows change incorrectly. No time-of-day quality reductions are applied.

Unity documents the setting's behaviour and compatibility caveats in [the URP asset reference](https://docs.unity.com/en-us/engine/6000.0/manual/render-pipelines/universal-render-pipeline/urp-reference/universalrp-asset#shadows).
