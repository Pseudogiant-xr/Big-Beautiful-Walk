# Investigating sunrise and sunset slowdowns

A reported comparison with Extreme and NR off dropped from roughly 120–140 FPS during day/night to about 50 FPS near sunrise/sunset. Temporarily disabling **Main-light shadows** in the same view recovered more than 150 FPS. This isolates the main-light shadow path; it does not yet separate CPU culling, shadow-map drawing and shadow sampling on the GPU.

Extreme requests a 2 km shadow/vegetation range, four cascades and a 16384 atlas where supported. Low-angle light can include more casters and overlapping foliage in the light’s view. DLSS Performance reduces the scene’s input pixel count; it does not reduce that requested shadow-map resolution or casting range. These are reasons to investigate shadow work, not proof of the expensive pass. The atlas setting accepting 16384 still does not prove the GPU allocation.

## Is the experimental culling switch live?

Yes. Graphics overrides are read and applied in LateUpdate; **Experimental shadow culling** does not require a restart. The affected log shows the game already had conservative culling On. Setting the override to On therefore changed nothing.

There are two controls in that row: the left box enables the override, and the Enabled/Disabled toggle chooses its value. To compare the two values, keep the left override box ticked and change the value toggle. Unticking the override restores the game’s captured value, which may be On. A quality preset clears custom overrides.

## Capture two comparable samples

1. Use the same resolution, upscaler, graphics preset, NR and FG requests for both samples. Keep the camera in the same position and direction.
2. In daylight, open **Home → Overview → Capture 30-second performance sample**. Home closes. After a 3-second warm-up, keep the view still for 30 seconds.
3. Repeat during the sunrise/sunset slowdown, with the same settings and view. Keep the game focused; losing focus cancels the sample.
4. Retrieve the two `performance-*.csv` files from `BepInEx/BigWalkDLSS/`. Overview and the BepInEx log show the saved filenames.

Complete frame intervals are retained, including a frame that stalls across the deadline; such a stall can extend the nominal window. Focus loss is handled directly from Unity’s callback, even if the player pauses Update in the background.

The files contain game-update frame times and their mean/p95, available CPU/render/GPU counters, shadow-map setup/draw scopes, shadow/draw/triangle counters, sun altitude, camera position/direction and startup requests. Capturing does not toggle shadows or modify quality. Profiling adds some overhead, so use the same capture method for both samples. Treat marker units literally: timing counters commonly use nanoseconds; draw counts are not timings. Blank values mean unavailable, including GPU scopes that do not return a positive measurement. GPU model/memory capacity are context, not memory-usage measurements.

A game-update rate is not the same as a frame-generation overlay FPS. CPU frame times include waits. CPU shadow scopes do not measure GPU cost, and Flora instancing means a caster/draw count is not necessarily an individual plant count. Unsupported counters, mismatched camera poses and changing settings limit what a comparison can establish. Real marker availability on the affected PC remains unverified until its first capture.

The main-light Off/On comparison was temporary diagnosis, not a proposed quality reduction. This update does not lower Extreme’s resolution, shadow distance, cascade splits or object detail. A quality-preserving optimisation needs measurements before it can be claimed.

## Grass and shrub shadows

The inspected game assets include ShadowCaster passes in both `Flora/TerrainGrass` and the game’s House Standard Lit vegetation shader. Several grass/bush/fern mesh renderers explicitly have casting Off; extending the global shadow distance does not enable these casters.

**Home → Vegetation → Request grass / shrub shadow casting after restart** is a separate, off-by-default request. It is saved in the native mod profile under `Vegetation.EnableSmallPlantCasters`. Restart after changing it: Flora bakes renderer flags into templates, so the mod changes supported prefab flags before terrain/instance registration rather than promising a live cache rebuild.

The request targets named grass, bush, shrub, fern and flower prefab sources, only disabled mesh-renderer caster flags and the two inspected shader families. Existing On/TwoSided renderers, unrelated objects, unsupported shaders, materials, mesh shapes and shadow ranges are preserved. The log reports changed renderer flags. The request is independent of graphics presets and is not enabled by Extreme.

Additional casters add work over the current vegetation shadow range, which can be expensive at Extreme’s 2 km. Flora’s own culling/LOD rules and material state still apply; changed flags do not prove visible grass shadows. The pre-registration hooks and visible results need in-game acceptance. This feature is an optional visual addition, not a fix for the sunrise/sunset slowdown.

Unity references: [ProfilerRecorder](https://docs.unity.com/en-us/engine/6000.0/script-reference/unity/profiling/profilerrecorder), [main-light shadow pass](https://github.com/Unity-Technologies/Graphics/blob/6000.3/staging/Packages/com.unity.render-pipelines.universal/Runtime/Passes/MainLightShadowCasterPass.cs), and [conservative culling](https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@17.0/api/UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset.html#UnityEngine_Rendering_Universal_UniversalRenderPipelineAsset_conservativeEnclosingSphere).

## Checking the cascade hypothesis

Extreme’s configured split ratios are 0.0125 / 0.05 / 0.2 over a requested 2000 m range: nominal ends at 25 / 100 / 400 / 2000 m. The supplied log reads back those ratios and a cascade count of four. That confirms the requested asset state, not the rendered pass or which map a particular pixel samples. Camera clipping can further limit the effective shadow distance.

The capture therefore records `# shadow_pass` comment lines from successful, non-empty main-light shadow Setup calls on base Game cameras. Each line names the camera ID, clip planes/FOV and effective maximum shadow distance, produced cascade count, target dimensions, per-cascade culling sphere (x/y/z/radius) and tile resolution. Multiple base cameras are recorded separately, including render-to-texture cameras; a reported limit makes excess cameras explicit. This is pass-producer state, not proof of physical GPU texture allocation or final shader selection.

Unity’s standard shader selects the cascade using the receiving surface’s world position and the split spheres. An object outside the camera view can cast a long shadow onto a nearby receiving surface. A sharp mountain/tree silhouette by itself does not establish a broken split. Even the final cascade can be detailed with a large requested atlas: four equal tiles in a 16384-square atlas are 8192-square each if those requests are honoured. The [standard cascade selector](https://github.com/Unity-Technologies/Graphics/blob/6000.3/staging/Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl) and the actual pass snapshots let us check the mapping before changing quality.

Prefab root identities are lifetime-checked and bounded; destroyed roots/renderers are pruned and replacement instance IDs are not suppressed by a reused raw pointer. Additional tracking has a fixed limit; skipped requests are reported instead of growing the cache indefinitely.
