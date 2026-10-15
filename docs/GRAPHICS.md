# Graphics guide

Press **Home** to open Big Beautiful Walk. Graphics overrides start disabled so the game's own settings remain the baseline. Balanced, Quality and Extreme are explicit choices; compare them in the same scene before keeping one.

## Controls

Changing a control enables its override. Numeric fields commit with **Enter**; changes save after an editing pause. Unticking an override returns the captured value when another system has not changed it. A change to the game's quality preset can affect settings without overrides.

The graphics profile is `BepInEx/config/local.bigwalk.advanced-graphics.cfg`. The DLSS profile is `BepInEx/config/local.bigwalk.native-dlss.cfg`. Diagnostic shortcuts and capture probes are disabled by default.

## DLSS Super Resolution

The default scale is `0.666667` on each dimension, corresponding to DLSS Quality, with Preset L. The panel's status distinguishes waiting for a frame from active evaluation. An FPS counter alone does not prove DLSS is active.

Home's **Upscaling** controls offer DLAA, Ultra Quality, Quality, Balanced, Performance and Ultra Performance, and Default / J / K / L / M render presets plus legacy E / F. Direct DLSS quality and preset changes apply live. DLAA and Ultra Quality remain experimental until their rendering path is separately verified; a successful Quality run does not validate them.

Alternative FSR 3.1 and XeSS backends use OptiScaler. Selecting a bridge backend writes the next-start choice and requires a restart; the panel distinguishes the current session from the next-start configuration. FSR 4 remains unavailable on the observed RTX 4090. Read the [upscaler feature matrix](UPSCALERS.md) for the exact requirements and preset limits.

The mod retains the game's post-processing around the DLSS pass. An existing OptiScaler installation may change the selected backend; inspect its status if the result differs from the direct SR path. Avoid saving competing backend choices in both menus before restarting.

## Shadows and vegetation

Shadow distance, atlas size, cascade count and cascade boundaries work together. Large distances spread a finite shadow map over more world space. Four cascades remain the engine limit. Cascade boundaries display metres but are stored as normalized fractions; changing the total distance scales the enabled boundaries proportionally.

The experimental 16384 main atlas is offered only when the GPU reports sufficient maximum texture size. With four cascades, its quarter-atlas tiles can provide up to 8192 pixels per cascade. Doubling an 8192 atlas to 16384 uses four times the atlas storage. A single 16-bit 16384-square depth surface is approximately 512 MiB; actual allocation and frame cost have not been verified. Engine acceptance of the setting does not prove the exact GPU allocation.

Vegetation shadow range is also limited by the main light's shadow distance. Unity Terrain controls affect Terrain-owned details and trees; they do not necessarily affect mesh-based landscape or foliage. Restoring vegetation settings disables the override, but complete visual restoration of every vegetation material has not been established.

## Optional Frame Generation and Neural Rendering

**F10** opens the optional OptiScaler menu. Setup's optional bridge checkbox downloads its pinned upscaler/FG dependencies; FG starts off so the user can compare it deliberately. The Neural Rendering model is not downloaded. See [the dependency policy](DEPENDENCIES.md).

Frame Generation can shimmer on foliage. Neural Rendering can change the game's appearance; **Default** is the preferred style from prototype testing, while **Natural** reduced saturation. Neither feature has broad hardware or fresh-install coverage. NR is not ray tracing and does not add distance-field or ray-traced shadows.
