using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using Il2CppInterop.Runtime;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace BigWalkDLSS;

internal static class AdvancedGraphics
{
    private enum Page { Overview, Upscaling, FrameGen, Neural, Geometry, Shadows, Vegetation, Terrain, Textures, Credits }
    private static readonly string[] TabNames = { "Overview", "Upscaling", "Frame Gen", "Neural", "Geometry", "Shadows", "Vegetation", "Terrain", "Textures" };
    private enum Kind { Number, Integer, Toggle, Choice }
    private enum Owner { Global, Pipeline, Camera, Terrain, Vegetation }
    private sealed class Setting
    {
        internal string Key = "", Label = "", Help = "", Unit = "";
        internal Page Page;
        internal Owner Owner;
        internal Kind Kind;
        internal float Min, Max, Original, LastApplied;
        internal Func<float> Read = null!;
        internal Action<float> Write = null!;
        internal Action<float>? RestoreWrite;
        internal Func<float, float>? Constrain;
        internal ConfigEntry<float> Value = null!;
        internal ConfigEntry<bool> Override = null!;
        internal bool Captured, EverApplied, Available = true;
        internal float[]? Choices;
        internal string[]? ChoiceNames;
        internal Func<bool>? Applicable;
        internal Func<float>? DisplayScale;
        internal Func<Terrain, float>? TerrainRead;
        internal Action<Terrain, float>? TerrainWrite;
        internal float Current => Available ? Read() : float.NaN;
        internal float Shown => Override.Value ? Value.Value : Current;
        internal float Scale => DisplayScale?.Invoke() ?? 1f;
        internal void Set(float value)
        {
            if (!float.IsFinite(value)) return;
            value = Mathf.Clamp(Kind == Kind.Integer ? Mathf.Round(value) : value, Min, Max);
            Value.Value = Constrain?.Invoke(value) ?? value;
            Override.Value = true;
            MarkDirty();
        }
        internal void Restore()
        {
            if (!Captured) return;
            if (Owner == Owner.Camera && _camera == null) { Captured = false; return; }
            try
            {
                if (Mathf.Approximately(Read(), LastApplied))
                {
                    (RestoreWrite ?? Write)(Original);
                    Log($"{Page}.{Key} restored captured={Original:0.###}, readback={Read():0.###}");
                }
            }
            catch (Exception error) { Log($"Restore {Key}: {error.Message}"); }
            Captured = false;
        }
    }
    private sealed class TerrainSnapshot
    {
        internal Terrain Terrain = null!;
        internal readonly Dictionary<string, float> Original = new();
        internal readonly Dictionary<string, float> LastApplied = new();
    }
    private static readonly List<Setting> Settings = new();
    private static readonly Dictionary<IntPtr, TerrainSnapshot> Terrains = new();
    private static readonly Dictionary<IntPtr, (Rewired.ControllerMap Map, bool Enabled)> Maps = new();
    private static readonly Dictionary<string, string> Edits = new();
    private static ConfigFile _config = null!;
    private static UniversalRenderPipelineAsset? _asset;
    private static Camera? _camera;
    private static int _quality = -1;
    private static bool _ready, _visible, _dirty, _guiFailed;
    private static bool _validateSplits;
    private static float _editedAt, _nextTerrainScan, _nextBridgeRefresh;
    private static int _captureAt = -1;
    private static float _nextShadowProbe;
    private static string _shadowTexture = "not yet observed";
    private static int _editingControl;
    private static Page _page;
    private static Vector2 _scroll;
    private static CursorLockMode _cursorLock;
    private static bool _cursorVisible;
    private static string _editing = "", _message = "Changes apply live. Profiles save after you finish editing.";
    private static GUIStyle? _title, _label, _small, _button, _field, _tab, _selectedTab, _toggle;
    internal static GUIStyle ToggleStyle => _toggle!;

    internal static void Initialize()
    {
        _config = new ConfigFile(Path.Combine(Paths.ConfigPath, "local.bigwalk.advanced-graphics.cfg"), false);
        _config.SaveOnConfigSet = false;
    }

    private static void Log(string message)
    {
        Plugin.Instance.Log.LogInfo("Advanced graphics: " + message);
        File.AppendAllText(Path.Combine(Paths.BepInExRootPath, "BigWalkDLSS", "advanced.log"),
            DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture) + " " + message + Environment.NewLine);
    }

    internal static void Update()
    {
        if (Input.GetKeyDown(KeyCode.Home))
        {
            if (_visible) Close();
            else
            {
                BridgeSettings.Reload();
                _nextBridgeRefresh = Time.unscaledTime + 1f;
                _guiFailed = false;
                _cursorLock = Cursor.lockState; _cursorVisible = Cursor.visible;
                _visible = true; _captureAt = Time.frameCount + 5;
                Log("panel opened");
            }
        }
        if (_visible && Input.GetKeyDown(KeyCode.Escape)) Close();
        if (_visible && Input.GetKeyDown(KeyCode.F10)) Close();
        if (_visible && Time.unscaledTime >= _nextBridgeRefresh)
        {
            _nextBridgeRefresh = Time.unscaledTime + 1f;
            BridgeSettings.RefreshSavedSettings();
        }
        if (_dirty && Time.unscaledTime - _editedAt > 1f) Save();
        if (Plugin.Diagnostics.Value && _visible && _captureAt > 0 && Time.frameCount >= _captureAt)
        {
            _captureAt = -1;
            ScreenCapture.CaptureScreenshot(Path.Combine(Paths.BepInExRootPath, "BigWalkDLSS", "advanced-graphics.png"));
        }
    }

    internal static void LateUpdate()
    {
        if (!Plugin.Compatible) return;
        try
        {
            var asset = GraphicsSettings.currentRenderPipeline?.TryCast<UniversalRenderPipelineAsset>();
            if (asset == null) return;
            int quality = QualitySettings.GetQualityLevel();
            if (_asset == null || _asset.Pointer != asset.Pointer || quality != _quality)
            {
                bool qualityChanged = _quality >= 0 && quality != _quality;
                foreach (var setting in Settings)
                {
                    if (setting.Owner == Owner.Camera || setting.Owner == Owner.Terrain) continue;
                    if (qualityChanged && (setting.Owner == Owner.Global || (_asset != null && _asset.Pointer == asset.Pointer)))
                        setting.Captured = false;
                    else setting.Restore();
                }
                ValidateSplits();
                _asset = asset; _quality = quality;
                if (!_ready) Register();
                Log($"pipeline={asset.name}, quality={quality}, LOD bias={QualitySettings.lodBias:F2}, shadow distance={asset.shadowDistance:F1}, cascades={asset.shadowCascadeCount}, atlas={asset.mainLightShadowmapResolution}, conservative culling={asset.conservativeEnclosingSphere}, sphere iterations={asset.numIterationsEnclosingSphere}");
            }
            var camera = Plugin.RenderingCamera != null ? Plugin.RenderingCamera : Camera.main;
            if (camera == null)
                foreach (var candidate in Camera.allCameras)
                    if (candidate != null && candidate.cameraType == CameraType.Game && !candidate.stereoEnabled && candidate.targetTexture == null)
                    { camera = candidate; break; }
            if (camera != null && camera.cameraType == CameraType.Game && !camera.stereoEnabled && camera.targetTexture == null &&
                (_camera == null || _camera.Pointer != camera.Pointer))
            {
                foreach (var setting in Settings) if (setting.Owner == Owner.Camera) setting.Restore();
                _camera = camera;
                Log($"camera={camera.name}, far clip={camera.farClipPlane:F1}");
            }
            if (Time.unscaledTime >= _nextTerrainScan)
            {
                _nextTerrainScan = Time.unscaledTime + 1f;
                foreach (var terrain in Terrain.activeTerrains)
                {
                    if (terrain == null || Terrains.ContainsKey(terrain.Pointer)) continue;
                    var snapshot = new TerrainSnapshot { Terrain = terrain };
                    Terrains[terrain.Pointer] = snapshot;
                    foreach (var setting in Settings) if (setting.Owner == Owner.Terrain)
                        snapshot.Original[setting.Key] = setting.TerrainRead!(terrain);
                    Log($"terrain discovered: {terrain.name}");
                }
            }
            foreach (var setting in Settings)
            {
                if (!setting.Available || setting.Owner == Owner.Terrain || setting.Owner == Owner.Vegetation) continue;
                if (setting.Owner == Owner.Camera && _camera == null) continue;
                if (!setting.Override.Value || !(setting.Applicable?.Invoke() ?? true)) { setting.Restore(); continue; }
                try
                {
                    bool firstApply = !setting.Captured;
                    if (firstApply) { setting.Original = setting.Read(); setting.Captured = true; }
                    float desired = setting.Value.Value;
                    if (!float.IsFinite(desired)) desired = setting.Original;
                    desired = Mathf.Clamp(desired, setting.Min, setting.Max);
                    desired = setting.Constrain?.Invoke(desired) ?? desired;
                    if (!Mathf.Approximately(setting.Read(), desired)) setting.Write(desired);
                    setting.LastApplied = setting.Read(); setting.EverApplied = true;
                    if (firstApply) Log($"applied {setting.Page}.{setting.Key}: requested={setting.Value.Value:0.###}, readback={setting.LastApplied:0.###}");
                }
                catch (Exception error) { setting.Available = false; setting.Restore(); Log($"{setting.Key} unavailable: {error.Message}"); }
            }
            ValidateSplits();
            ApplyTerrain();
            VegetationShadows.Tick();
            if (Plugin.Diagnostics.Value && Time.unscaledTime >= _nextShadowProbe)
            {
                _nextShadowProbe = Time.unscaledTime + 5f;
                var texture = Shader.GetGlobalTexture("_MainLightShadowmapTexture");
                // RenderGraph resets global bindings after execution. Inspect the retained
                // named render target when the global points to a fallback texture.
                if (texture == null || texture.width < 256)
                    foreach (var item in Resources.FindObjectsOfTypeAll(Il2CppType.Of<RenderTexture>()))
                    {
                        var candidate = item.TryCast<RenderTexture>();
                        if (candidate != null && candidate.IsCreated() && candidate.width == _asset.mainLightShadowmapResolution &&
                            candidate.name.IndexOf("MainLightShadow", StringComparison.OrdinalIgnoreCase) >= 0)
                        { texture = candidate; break; }
                    }
                if (texture != null && texture.width >= 256 && texture.height >= 256)
                {
                    string size = $"{texture.width}×{texture.height}";
                    if (_shadowTexture != size)
                    {
                        _shadowTexture = size;
                        Log($"main shadow texture={size}, name={texture.name}, requested atlas={_asset.mainLightShadowmapResolution}, hardware texture limit={SystemInfo.maxTextureSize}");
                    }
                }
            }
            if (_visible) BlockInput();
        }
        catch (Exception error) { Log("update failed: " + error.Message); Close(); }
    }

    private static Setting Add(Page page, string key, string label, string help, float min, float max,
        Func<float> read, Action<float> write, Owner owner = Owner.Global, Kind kind = Kind.Number,
        string unit = "", float[]? choices = null, string[]? names = null, bool initialOverride = false, float? initialValue = null)
    {
        float current = 0;
        bool available = true;
        try { current = read(); }
        catch (Exception error) { available = false; Log($"{page}.{key} getter unavailable: {error.Message}"); }
        var setting = new Setting
        {
            Page = page, Key = key, Label = label, Help = help, Min = min, Max = max,
            Read = read, Write = write, Owner = owner, Kind = kind, Unit = unit, Choices = choices, ChoiceNames = names, Available = available,
            Value = _config.Bind(page.ToString(), key, initialValue ?? current, help),
            Override = _config.Bind(page.ToString(), key + "Override", initialOverride, "Apply the custom " + label.ToLowerInvariant() + " value.")
        };
        Settings.Add(setting); return setting;
    }

    private static void Register()
    {
        var shadow = _asset!;
        Add(Page.Geometry, "LodBias", "LOD distance bias", "Keeps detailed objects visible farther away. Higher values can reduce frame rate; only supported objects change.", .25f, 8f,
            () => QualitySettings.lodBias, v => QualitySettings.lodBias = v, unit: "×");
        Add(Page.Geometry, "MaximumLod", "Highest allowed LOD index", "0 allows the finest object shapes. Higher values use simpler shapes to reduce drawing work.", 0, 5,
            () => QualitySettings.maximumLODLevel, v => QualitySettings.maximumLODLevel = (int)v, kind: Kind.Integer);
        Add(Page.Geometry, "MeshLodThreshold", "Mesh LOD pixel threshold", "Lower values keep small objects more detailed, at a possible frame-rate cost. Only supported objects change.", .1f, 10f,
            () => QualitySettings.meshLodThreshold, v => QualitySettings.meshLodThreshold = v, unit: "px");
        Add(Page.Geometry, "FarClip", "Camera draw distance", "How far the camera can see. Raising it cannot make the game load objects it normally leaves out.", 100, 40000,
            () => _camera != null ? _camera.farClipPlane : 20000, v => { if (_camera != null) { _camera.farClipPlane = v; Plugin.ResetHistory(); } },
            Owner.Camera, unit: "m");
        Add(Page.Shadows, "Distance", "Shadow distance", "How far sunlight shadows reach. Longer distances cost more and spread the available shadow detail thinner.", 1, 2000,
            () => _asset!.shadowDistance, v => _asset!.shadowDistance = v, Owner.Pipeline, unit: "m", initialOverride: false,
            initialValue: shadow.shadowDistance * Mathf.Clamp(Plugin.ShadowDistanceScale.Value, 1f, 2f));
        Add(Page.Shadows, "MainEnabled", "Main-light shadows", "Turns sunlight shadows on or off. Turning them off can improve frame rate but makes the world look flatter.", 0, 1,
            () => _asset!.supportsMainLightShadows ? 1 : 0, v => _asset!.supportsMainLightShadows = v > .5f, Owner.Pipeline, Kind.Toggle);
        Add(Page.Shadows, "Cascades", "Shadow cascade count", "Splits sunlight shadows into detail zones. More zones help nearby shadows stay sharp over a long distance.", 1, 4,
            () => _asset!.shadowCascadeCount, v => _asset!.shadowCascadeCount = (int)v, Owner.Pipeline, Kind.Integer);
        Add(Page.Shadows, "ConservativeCulling", "Reduce duplicate shadow work", "Tries to avoid redrawing objects in overlapping shadow zones. Keeps range and resolution; experimental, so compare shadow edges.", 0, 1,
            () => _asset!.conservativeEnclosingSphere ? 1 : 0, v => _asset!.conservativeEnclosingSphere = v > .5f, Owner.Pipeline, Kind.Toggle);
        for (int count = 2; count <= 4; count++)
        {
            for (int index = 0; index < count - 1; index++)
            {
                int c = count, i = index;
                var split = Add(Page.Shadows, "Split" + c + "_" + (i + 1), "Cascade " + (i + 1) + " end", "Where this shadow-detail zone ends. A longer zone covers more ground but spreads its detail more thinly.", .005f, .995f,
                    () => ReadSplit(c, i), v => WriteSplit(c, i, v), Owner.Pipeline, unit: "m");
                split.DisplayScale = () => _asset!.shadowDistance;
                split.Applicable = () => _asset!.shadowCascadeCount == c;
                split.RestoreWrite = v => WriteSplit(c, i, v, false);
                split.Constrain = v => ConstrainSplit(c, i, v);
            }
        }
        float[] resolutions = { 256, 512, 1024, 2048, 4096, 8192 };
        string[] resolutionNames = { "256", "512", "1024", "2048", "4096", "8192" };
        bool supports16k = SystemInfo.maxTextureSize >= 16384;
        Add(Page.Shadows, "MainResolution", "Main shadow atlas", "Higher values sharpen sunlight shadows and use more graphics memory. Experimental 16384 uses 4x the memory of 8192.", 256, supports16k ? 16384 : 8192,
            () => _asset!.mainLightShadowmapResolution, v => _asset!.mainLightShadowmapResolution = (int)v, Owner.Pipeline, Kind.Choice, "px",
            supports16k ? new float[] { 256, 512, 1024, 2048, 4096, 8192, 16384 } : resolutions,
            supports16k ? new[] { "256", "512", "1024", "2048", "4096", "8192", "16384 (experimental)" } : resolutionNames);
        Add(Page.Shadows, "FadeBorder", "Final cascade fade", "Makes distant sunlight shadows disappear gradually instead of ending with a hard line. Higher means a wider fade.", 0, 1,
            () => _asset!.cascadeBorder, v => _asset!.cascadeBorder = v, Owner.Pipeline);
        Add(Page.Shadows, "DepthBias", "Shadow depth bias", "Helps remove dark speckles from surfaces. Too high can make shadows look detached from their objects.", 0, 10,
            () => _asset!.shadowDepthBias, v => _asset!.shadowDepthBias = v, Owner.Pipeline);
        Add(Page.Shadows, "NormalBias", "Shadow normal bias", "Another fix for dark speckles on surfaces. Too high can shrink or shift shadows; too low can show speckles.", 0, 10,
            () => _asset!.shadowNormalBias, v => _asset!.shadowNormalBias = v, Owner.Pipeline);
        Add(Page.Shadows, "SoftEnabled", "Soft shadows", "Smooths hard shadow edges. Can cost more rendering time when supported by the game.", 0, 1,
            () => _asset!.supportsSoftShadows ? 1 : 0, v => _asset!.supportsSoftShadows = v > .5f, Owner.Pipeline, Kind.Toggle);
        Add(Page.Shadows, "SoftQuality", "Soft-shadow quality", "Higher gives smoother shadow edges, with a possible frame-rate cost. Only applies when soft shadows are on.", 1, 3,
            () => (int)_asset!.softShadowQuality, v => _asset!.softShadowQuality = (SoftShadowQuality)(int)v, Owner.Pipeline, Kind.Choice,
            choices: new float[] { 1, 2, 3 }, names: new[] { "Low", "Medium", "High" });
        Add(Page.Shadows, "AdditionalEnabled", "Local-light shadows", "Lets lamps and spotlights cast shadows where the game supports them. More shadow-casting lights can slow rendering.", 0, 1,
            () => _asset!.supportsAdditionalLightShadows ? 1 : 0, v => _asset!.supportsAdditionalLightShadows = v > .5f, Owner.Pipeline, Kind.Toggle);
        Add(Page.Shadows, "AdditionalResolution", "Local-light shadow atlas", "Graphics memory shared by lamp and spotlight shadows. Larger values allow more detail but use more memory.", 256, 8192,
            () => _asset!.additionalLightsShadowmapResolution, v => _asset!.additionalLightsShadowmapResolution = (int)v, Owner.Pipeline, Kind.Choice, "px", resolutions, resolutionNames);
        Add(Page.Shadows, "LocalLowResolution", "Local shadow low tier", "Shadow detail for lights the game marks Low. Higher is sharper and uses more graphics memory.", 256, 8192,
            () => _asset!.additionalLightsShadowResolutionTierLow, v => _asset!.additionalLightsShadowResolutionTierLow = (int)v, Owner.Pipeline, Kind.Choice, "px", resolutions, resolutionNames);
        Add(Page.Shadows, "LocalMediumResolution", "Local shadow medium tier", "Shadow detail for lights the game marks Medium. Higher is sharper and uses more graphics memory.", 256, 8192,
            () => _asset!.additionalLightsShadowResolutionTierMedium, v => _asset!.additionalLightsShadowResolutionTierMedium = (int)v, Owner.Pipeline, Kind.Choice, "px", resolutions, resolutionNames);
        Add(Page.Shadows, "LocalHighResolution", "Local shadow high tier", "Shadow detail for lights the game marks High. Higher uses more memory; the shared shadow-map size still limits it.", 256, 8192,
            () => _asset!.additionalLightsShadowResolutionTierHigh, v => _asset!.additionalLightsShadowResolutionTierHigh = (int)v, Owner.Pipeline, Kind.Choice, "px", resolutions, resolutionNames);
        Add(Page.Shadows, "ShadowCaching", "Local shadow caching", "Reuses lamp and spotlight shadows when possible, reducing repeated drawing work.", 0, 1,
            () => _asset!.additionalLightsShadowCachingEnabled ? 1 : 0, v => _asset!.additionalLightsShadowCachingEnabled = v > .5f, Owner.Pipeline, Kind.Toggle);
        Add(Page.Shadows, "CacheUpdates", "Shadow-cache updates/frame", "How many stored lamp shadows can refresh each frame. Higher catches changes sooner but adds rendering work.", 1, 128,
            () => _asset!.additionalLightsShadowCachingMaxUpdatesPerFrame, v => _asset!.additionalLightsShadowCachingMaxUpdatesPerFrame = (int)v, Owner.Pipeline, Kind.Integer);
        Add(Page.Shadows, "CachePriorityUpdates", "Priority cache updates/frame", "How many urgent lamp-shadow changes can refresh each frame. Higher reacts sooner but adds rendering work.", 0, 128,
            () => _asset!.additionalLightsShadowCachingMaxPriorityUpdatesPerFrame, v => _asset!.additionalLightsShadowCachingMaxPriorityUpdatesPerFrame = (int)v, Owner.Pipeline, Kind.Integer);
        var vegetation = Add(Page.Vegetation, "ShadowDistance", "Vegetation shadow distance", "How far trees and plants cast shadows. The main Shadow distance must also reach that far; longer ranges can slow rendering.", 1, 20000,
            () => VegetationShadows.CurrentDistance, _ => { }, Owner.Vegetation, unit: "m", initialValue: Settings.Find(s => s.Page == Page.Shadows && s.Key == "Distance")!.Value.Value);
        var vegetationFade = Add(Page.Vegetation, "FadeWidth", "Vegetation shadow fade band", "Makes tree and plant shadows fade away smoothly over this distance. Needs the vegetation distance override.", 1, 500,
            () => VegetationShadows.CurrentFadeWidth, _ => { }, Owner.Vegetation, unit: "m", initialValue: 50);
        vegetation.Applicable = () => VegetationShadows.Ready;
        vegetationFade.Applicable = () => VegetationShadows.Ready;
        VegetationShadows.Enabled = () => vegetation.Override.Value;
        VegetationShadows.Distance = () => vegetation.Value.Value;
        VegetationShadows.FadeWidth = () => vegetationFade.Override.Value ? vegetationFade.Value.Value : 50;
        Add(Page.Textures, "MipmapLimit", "Texture mip reduction", "Full resolution keeps surfaces sharpest. Smaller choices save graphics memory but can look blurry; some textures have separate limits.", 0, 4,
            () => QualitySettings.globalTextureMipmapLimit, v => QualitySettings.globalTextureMipmapLimit = (int)v, kind: Kind.Choice,
            choices: new float[] { 0, 1, 2, 3, 4 }, names: new[] { "Full resolution", "Half", "Quarter", "Eighth", "Sixteenth" });
        Add(Page.Textures, "Anisotropy", "Anisotropic filtering", "Keeps angled surfaces, such as paths, clearer. Per texture follows the game; Forced applies it wherever supported.", 0, 2,
            () => (int)QualitySettings.anisotropicFiltering, v => QualitySettings.anisotropicFiltering = (AnisotropicFiltering)(int)v, kind: Kind.Choice,
            choices: new float[] { 0, 1, 2 }, names: new[] { "Disabled", "Per texture", "Forced" });
        Add(Page.Textures, "StreamingBudget", "Texture streaming budget", "Graphics memory available for streamed textures. More can reduce blurry surfaces; has no effect when texture streaming is off.", 128, 8192,
            () => QualitySettings.streamingMipmapsMemoryBudget, v => QualitySettings.streamingMipmapsMemoryBudget = v, unit: "MiB");
        AddTerrain("PixelError", "Terrain LOD pixel error", "Lower values make supported ground surfaces more detailed, at a possible frame-rate cost.", 1, 200, t => t.heightmapPixelError, (t, v) => t.heightmapPixelError = v, 5);
        AddTerrain("MaximumLod", "Terrain maximum LOD", "0 allows the most detailed ground shapes. Higher values use simpler ground to reduce drawing work.", 0, 8, t => t.heightmapMaximumLOD, (t, v) => t.heightmapMaximumLOD = (int)v, 0, Kind.Integer);
        AddTerrain("BasemapDistance", "Terrain texture distance", "How far detailed ground textures remain visible before switching to a simpler distant image.", 1, 100000, t => t.basemapDistance, (t, v) => t.basemapDistance = v, 1000, unit: "m");
        AddTerrain("DetailDistance", "Grass/detail distance", "How far small plants and grass remain visible on supported terrain. Longer ranges draw more objects.", 0, 250, t => t.detailObjectDistance, (t, v) => t.detailObjectDistance = v, 80, unit: "m");
        AddTerrain("DetailDensity", "Grass/detail density", "How much grass and small vegetation appears on supported terrain. Higher fills out the ground but draws more objects.", 0, 1, t => t.detailObjectDensity, (t, v) => t.detailObjectDensity = v, 1, unit: "×");
        AddTerrain("TreeDistance", "Tree draw distance", "How far trees remain visible on supported terrain. Longer ranges can reduce frame rate.", 0, 20000, t => t.treeDistance, (t, v) => t.treeDistance = v, 5000, unit: "m");
        AddTerrain("BillboardDistance", "Tree billboard start", "Where supported trees switch to a flat distant image. Raise it to keep full tree shapes visible farther away.", 1, 10000, t => t.treeBillboardDistance, (t, v) => t.treeBillboardDistance = v, 50, unit: "m");
        AddTerrain("TreeFade", "Tree crossfade length", "How gradually supported trees blend into their flat distant images. A wider blend makes the switch less abrupt.", 0, 500, t => t.treeCrossFadeLength, (t, v) => t.treeCrossFadeLength = v, 5, unit: "m");
        AddTerrain("TreeLodBias", "Terrain tree LOD bias", "Keeps supported terrain trees detailed farther away. Higher values can reduce frame rate.", .25f, 8, t => t.treeLODBiasMultiplier, (t, v) => t.treeLODBiasMultiplier = v, 1, unit: "×");
        AddTerrain("FullTreeLimit", "Full-detail tree limit", "Maximum number of supported terrain trees drawn at full detail. Higher can look better but draw more geometry.", 0, 10000, t => t.treeMaximumFullLODCount, (t, v) => t.treeMaximumFullLODCount = (int)v, 50, Kind.Integer);
        _ready = true; Log($"registered {Settings.Count} controls; initial shadow range={Settings.Find(s => s.Key == "Distance")!.Value.Value:F1}m");
    }

    private static void AddTerrain(string key, string label, string help, float min, float max,
        Func<Terrain, float> read, Action<Terrain, float> write, float fallback, Kind kind = Kind.Number, string unit = "")
    {
        var setting = Add(Page.Terrain, key, label, help, min, max,
            () => { foreach (var snapshot in Terrains.Values) if (snapshot.Terrain != null) return read(snapshot.Terrain); return fallback; },
            _ => { }, Owner.Terrain, kind, unit);
        setting.TerrainRead = read; setting.TerrainWrite = write;
    }

    private static void ApplyTerrain()
    {
        foreach (var snapshot in Terrains.Values)
        {
            if (snapshot.Terrain == null) continue;
            foreach (var setting in Settings)
            {
                if (setting.Owner != Owner.Terrain || !setting.Available) continue;
                try
                {
                    var terrain = snapshot.Terrain;
                    if (setting.Override.Value)
                    {
                        bool firstApply = !snapshot.LastApplied.ContainsKey(setting.Key);
                        if (firstApply) snapshot.Original[setting.Key] = setting.TerrainRead!(terrain);
                        float value = float.IsFinite(setting.Value.Value) ? Mathf.Clamp(setting.Value.Value, setting.Min, setting.Max) : snapshot.Original[setting.Key];
                        if (!Mathf.Approximately(setting.TerrainRead!(terrain), value)) setting.TerrainWrite!(terrain, value);
                        snapshot.LastApplied[setting.Key] = setting.TerrainRead!(terrain); setting.EverApplied = true;
                        if (firstApply) Log($"applied {terrain.name}/{setting.Key}: requested={setting.Value.Value:0.###}, readback={snapshot.LastApplied[setting.Key]:0.###}");
                    }
                    else if (snapshot.LastApplied.TryGetValue(setting.Key, out float last))
                    {
                        if (Mathf.Approximately(setting.TerrainRead!(terrain), last)) setting.TerrainWrite!(terrain, snapshot.Original[setting.Key]);
                        snapshot.LastApplied.Remove(setting.Key);
                    }
                }
                catch (Exception error) { setting.Available = false; Log($"Terrain {setting.Key} unavailable: {error.Message}"); }
            }
        }
    }

    private static float ReadSplit(int count, int index)
    {
        if (count == 2) return _asset!.cascade2Split;
        if (count == 3) return index == 0 ? _asset!.cascade3Split.x : _asset!.cascade3Split.y;
        var v = _asset!.cascade4Split; return index == 0 ? v.x : index == 1 ? v.y : v.z;
    }

    private static float ConstrainSplit(int count, int index, float value)
    {
        if (count == 2) return Mathf.Clamp(value, .005f, .995f);
        float lower = index == 0 ? .005f : ReadSplit(count, index - 1) + .005f;
        float upper = index == count - 2 ? .995f : ReadSplit(count, index + 1) - .005f;
        return Mathf.Clamp(value, Mathf.Min(lower, upper), upper);
    }

    private static void WriteSplit(int count, int index, float value, bool constrain = true)
    {
        if (!constrain) _validateSplits = true;
        if (constrain) value = ConstrainSplit(count, index, value);
        if (count == 2) { _asset!.cascade2Split = value; return; }
        if (count == 3)
        {
            var v = _asset!.cascade3Split;
            if (index == 0) v.x = value; else v.y = value;
            _asset.cascade3Split = v; return;
        }
        var s = _asset!.cascade4Split;
        if (index == 0) s.x = value;
        else if (index == 1) s.y = value;
        else s.z = value;
        _asset.cascade4Split = s;
    }

    private static void ValidateSplits()
    {
        if (_asset == null || !_validateSplits) return;
        _validateSplits = false;
        float a = _asset.cascade2Split;
        var two = GraphicsLimits.CascadeSplits(new[] { a });
        if (a != two[0]) _asset.cascade2Split = two[0];
        var b = _asset.cascade3Split;
        var three = GraphicsLimits.CascadeSplits(new[] { b.x, b.y });
        if (b.x != three[0] || b.y != three[1]) _asset.cascade3Split = new Vector2(three[0], three[1]);
        var c = _asset.cascade4Split;
        var four = GraphicsLimits.CascadeSplits(new[] { c.x, c.y, c.z });
        if (c.x != four[0] || c.y != four[1] || c.z != four[2]) _asset.cascade4Split = new Vector3(four[0], four[1], four[2]);
    }

    private static void BlockInput()
    {
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        if (!Rewired.ReInput.isReady) return;
        var players = Rewired.ReInput.players.GetPlayers(true);
        int count = players.Cast<Il2CppSystem.Collections.Generic.ICollection<Rewired.Player>>().Count;
        for (int i = 0; i < count; i++)
        {
            var maps = new Il2CppSystem.Collections.Generic.List<Rewired.ControllerMap>();
            players[i].controllers.maps.GetAllMaps(maps);
            for (int j = 0; j < maps.Count; j++)
            {
                var map = maps[j];
                if (!Maps.ContainsKey(map.Pointer)) Maps[map.Pointer] = (map, map.enabled);
                map.enabled = false;
            }
        }
    }

    internal static void Close()
    {
        if (!_visible && Maps.Count == 0) return;
        _visible = false;
        foreach (var state in Maps.Values) try { state.Map.enabled = state.Enabled; } catch { }
        Maps.Clear(); Cursor.lockState = _cursorLock; Cursor.visible = _cursorVisible;
        if (_dirty) Save();
        Log("panel closed; previous controller maps and cursor restored");
    }

    private static void MarkDirty() { _dirty = true; _editedAt = Time.unscaledTime; _message = "Applying live…"; }
    private static void Save()
    {
        _config.Save(); _dirty = false; _message = "Profile saved. Overrides will load next time.";
        Log("profile saved");
        foreach (var setting in Settings)
            if (setting.Available && setting.Override.Value)
                Log($"{setting.Page}.{setting.Key} requested={setting.Value.Value:0.###}, readback={setting.Current:0.###}");
    }
    private static void RestorePage(bool all)
    {
        foreach (var setting in Settings) if (all || setting.Page == _page) { setting.Override.Value = false; setting.Restore(); }
        ValidateSplits(); ApplyTerrain(); VegetationShadows.Tick(); Edits.Clear(); _editing = ""; MarkDirty();
        Log(all ? "all custom overrides disabled" : $"{_page} overrides disabled");
    }

    internal static void Shutdown()
    {
        Close();
        foreach (var setting in Settings) { setting.Override.Value = false; setting.Restore(); }
        ValidateSplits(); ApplyTerrain();
    }

    private static void ApplyPreset(string name)
    {
        if (!_ready) return;
        RestorePage(true);
        if (name != "Game defaults")
        {
            // Assign ordered splits together, without constraining them against the
            // previous preset's boundaries.
            foreach (var pair in GraphicsPresets.Values(name, SystemInfo.maxTextureSize))
            {
                var setting = Settings.Find(s => s.Page + "." + s.Key == pair.Key);
                if (setting == null || !setting.Available) continue;
                setting.Value.Value = pair.Value;
                setting.Override.Value = true;
            }
        }
        MarkDirty();
        _message = name + " applied. Individual controls remain available in the other tabs.";
        Log("graphics preset=" + name);
    }

    private static void DrawOverview(float x, float y)
    {
        GUI.Label(new Rect(x + 25, y + 185, 900, 45), Plugin.RendererStatus, _label);
        GUI.Label(new Rect(x + 25, y + 237, 900, 45),
            $"Selected: {UpscalingPresentation.BackendName(BridgeSettings.RunningBackend)}  •  {UpscalingPresentation.QualityName(BridgeSettings.RunningBackend, Plugin.QualityMode.Value)}  •  {SystemInfo.graphicsDeviceName}{(Plugin.UsesBridge ? "  •  AMD/Intel path (experimental)" : "")}", _small);
        GUI.enabled = Plugin.Compatible && Plugin.NativeLoaded;
        bool sr = GUI.Toggle(new Rect(x + 25, y + 292, 480, 30), Plugin.Enabled.Value,
            UpscalingPresentation.ToggleLabel(BridgeSettings.RunningBackend, Plugin.QualityMode.Value), _toggle);
        if (sr != Plugin.Enabled.Value) { Plugin.Enabled.Value = sr; Plugin.ResetHistory(); }
        if (GUI.Button(new Rect(x + 610, y + 289, 290, 34), "Change upscaler / quality", _button)) _page = Page.Upscaling;
        GUI.Label(new Rect(x + 25, y + 333, 900, 45),
            BridgeSettings.Recognized ? "Upscaling: smoother edges with less graphics work. Frame Gen: extra pictures for smoother movement. Neural: AI lighting and surface detail, with a performance cost." :
                "Upscaling can improve frame rate and smooth jagged edges. Setup's optional bridge adds other upscalers and smoother-motion tools.", _small);
        GUI.enabled = _ready;
        GUI.Label(new Rect(x + 25, y + 398, 900, 34), "Not sure where to start? Try Balanced. Presets replace custom graphics choices; upscaling, frame generation and AI lighting stay as set.", _small);
        string[] presets = { "Game defaults", "Balanced", "Quality", "Extreme" };
        for (int i = 0; i < presets.Length; i++)
            if (GUI.Button(new Rect(x + 25 + i * 225, y + 443, 210, 40), presets[i], _button)) ApplyPreset(presets[i]);
        GUI.Label(new Rect(x + 25, y + 495, 900, 85),
            "Game defaults: let the game choose. Balanced: more detail with a lighter graphics load. Quality: sharper, longer shadows. Extreme: the longest shadows and highest detail, with a large performance cost. Try each in the same view.", _small);
        GUI.enabled = true;
        GUI.Label(new Rect(x + 25, y + 591, 900, 60),
            "Monitor play on DirectX 11 only. Disable the mod and optional injector before VR. In-world time continues while this panel is open.", _small);
    }

    internal static void Draw()
    {
        if (!_visible || _guiFailed) return;
        Matrix4x4 matrix = GUI.matrix; Color colour = GUI.color; bool enabled = GUI.enabled;
        try
        {
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            if (_title == null)
            {
                _title = new GUIStyle(GUI.skin.label) { fontSize = 28, fontStyle = FontStyle.Bold };
                _label = new GUIStyle(GUI.skin.label) { fontSize = 16 };
                _toggle = new GUIStyle(GUI.skin.toggle) { fontSize = 16 };
                _toggle.normal.textColor = MenuTheme.Text; _toggle.onNormal.textColor = MenuTheme.Ochre;
                _small = new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true };
                _button = MenuTheme.Button(GUI.skin.button);
                _tab = new GUIStyle(_button) { fontSize = 13, padding = new RectOffset(4, 4, 4, 4) };
                _selectedTab = MenuTheme.Button(_tab, true);
                _selectedTab.fontSize = 13;
                _field = MenuTheme.Field(GUI.skin.textField);
                _title.normal.textColor = MenuTheme.Text; _label.normal.textColor = MenuTheme.Text;
                _small.normal.textColor = MenuTheme.Muted;
            }
            float scale = Mathf.Min(1.35f, Mathf.Min(Screen.height / 960f, Screen.width / 1128f));
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            float w = Screen.width / scale, h = Screen.height / scale;
            float frameX = Mathf.Max(8, (w - 1088) / 2), frameY = Mathf.Max(8, (h - 920) / 2);
            float x = frameX + 64, y = frameY + 64;
            GUI.color = new Color(0, 0, 0, .55f); GUI.DrawTexture(new Rect(0, 0, w, h), Texture2D.whiteTexture);
            GUI.color = Color.white;
            MenuTheme.Fill(new Rect(frameX, frameY, 1088, 920), MenuTheme.Background);
            MenuTheme.Fill(new Rect(x, y, 960, 792), MenuTheme.Background);
            MenuTheme.Fill(new Rect(x, y, 960, 120), MenuTheme.Surface);
            if (MenuTheme.Artwork is { } artwork)
                GUI.DrawTexture(new Rect(x + 565, y + 4, 335, 112), artwork, ScaleMode.ScaleToFit, true);
            MenuTheme.Fill(new Rect(x, y, 960, 4), MenuTheme.Ochre);
            MenuTheme.Fill(new Rect(x + 25, y + 169, 900, 1), MenuTheme.Raised);
            MenuTheme.DrawBorder(new Rect(frameX, frameY, 1088, 920));
            GUI.color = Color.white;
            GUI.Label(new Rect(x + 25, y + 18, 790, 38), "Big Beautiful Walk", _title);
            GUI.Label(new Rect(x + 25, y + 57, 870, 28), BridgeSettings.Recognized
                ? "Home closes this panel  •  F10 opens extra upscaler controls"
                : "Home closes this panel  •  More upscalers are available through Setup", _label);
            if (GUI.Button(new Rect(x + 905, y + 18, 32, 32), "×", _button)) Close();
            GUI.Label(new Rect(x + 25, y + 91, 535, 21), "Picture quality · smoother motion · lighting and detail", _small);
            y += 32;
            for (int i = 0; i < TabNames.Length; i++)
                if (GUI.Button(new Rect(x + 25 + i * 101, y + 98, 94, 35), TabNames[i], _page == (Page)i ? _selectedTab : _tab))
                { _page = (Page)i; _scroll = Vector2.zero; }
            bool graphicsPage = _page >= Page.Geometry && _page <= Page.Textures;
            bool featurePage = _page == Page.FrameGen || _page == Page.Neural;
            GUI.Label(new Rect(x + 25, y + 145, 900, 37), graphicsPage
                ? "Each setting explains its effect below. Tick a box to use your value; untick it to let the game decide."
                : _page == Page.FrameGen ? "Makes motion look smoother by creating extra pictures between the frames the game draws. Controls do not respond faster. Restart after changing."
                : _page == Page.Neural ? "Uses AI to redraw lighting and surface detail for a more realistic look. This changes the art style and can lower frame rate; it is not ray tracing."
                : _page == Page.Credits ? "Libraries and community work used by Big Beautiful Walk."
                : _page == Page.Upscaling ? "These tools can improve frame rate and smooth jagged edges by rebuilding a detailed picture from fewer pixels. Each option explains how below."
                : "Start with a graphics preset, then adjust individual settings. Your changes save automatically.", _small);
            if (_page == Page.Overview) DrawOverview(x, y);
            else if (_page == Page.Upscaling) UpscalingMenu.Draw(x, y, _label!, _small!, _button!);
            else if (_page == Page.FrameGen) FeatureMenu.DrawFrameGen(x, y, _label!, _small!, _button!);
            else if (_page == Page.Neural) FeatureMenu.DrawNeural(x, y, _label!, _small!, _button!);
            else if (_page == Page.Credits) CreditsMenu.Draw(x, y, _label!, _small!, _button!);
            else if (!_ready) GUI.Label(new Rect(x + 25, y + 200, 900, 70), Plugin.Compatible ? "Waiting for the game's rendering pipeline…" : Plugin.SupportMessage, _label);
            else
            {
                int terrainCount = 0; foreach (var snapshot in Terrains.Values) if (snapshot.Terrain != null) terrainCount++;
                string note = _page == Page.Vegetation ? (VegetationShadows.Ready ? "Trees and plants have their own shadow limit. Raise the main Shadow distance too if you want longer shadows." : "Vegetation controls become available after entering the world.") :
                    _page == Page.Terrain ? $"Supported terrain areas: {terrainCount}. These controls only affect ground and plants that use Unity Terrain." :
                    _page == Page.Geometry ? "Choose how detailed objects stay as you walk away. Only objects that support these controls will change." :
                    _page == Page.Textures ? $"Textures are the surface images on objects. On-demand texture loading is {(QualitySettings.streamingMipmapsActive ? "on" : "off")} in this game profile." :
                    $"Range: {_asset!.shadowDistance:F0} m  •  Cascades: {_asset.shadowCascadeCount}  •  Atlas setting: {_asset.mainLightShadowmapResolution}px";
                GUI.Label(new Rect(x + 25, y + 181, 900, 40), note, _small);
                var rows = Settings.FindAll(s => s.Page == _page && (s.Applicable?.Invoke() ?? true));
                _scroll = GUI.BeginScrollView(new Rect(x + 25, y + 227, 910, 427), _scroll, new Rect(0, 0, 885, rows.Count * 67 + 12));
                try
                {
                    for (int i = 0; i < rows.Count; i++)
                    {
                        GUI.enabled = rows[i].Available && (rows[i].Applicable?.Invoke() ?? true) &&
                            (rows[i].Owner != Owner.Camera || _camera != null) && (rows[i].Owner != Owner.Terrain || terrainCount > 0);
                        DrawSetting(rows[i], i * 67);
                    }
                }
                finally { GUI.EndScrollView(); GUI.enabled = true; }
                if (_editing.Length != 0 && (GUIUtility.keyboardControl != _editingControl ||
                    (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return)))
                {
                    var setting = Settings.Find(s => s.Page + "." + s.Key == _editing);
                    if (setting != null && Edits.TryGetValue(_editing, out string? input) && float.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
                        setting.Set(value / setting.Scale);
                    Edits.Remove(_editing); _editing = "";
                }
            }
            if (graphicsPage)
            {
                if (GUI.Button(new Rect(x + 25, y + 680, 190, 36), "Save profile", _button)) Save();
                if (GUI.Button(new Rect(x + 230, y + 680, 220, 36), "Restore this section", _button)) RestorePage(false);
                if (GUI.Button(new Rect(x + 465, y + 680, 210, 36), "Restore all overrides", _button)) RestorePage(true);
            }
            else if (_page == Page.Upscaling && GUI.Button(new Rect(x + 25, y + 680, 260, 36), "Reset quality settings", _button))
            {
                Plugin.QualityMode.Value = 2; Plugin.RenderScale.Value = .666667f; Plugin.Preset.Value = 12; Plugin.ResetHistory();
            }
            else if (_page == Page.Overview && GUI.Button(new Rect(x + 25, y + 680, 260, 36), "Credits and licences", _button)) _page = Page.Credits;
            else if (featurePage && GUI.Button(new Rect(x + 25, y + 680, 260, 36), "Refresh saved settings", _button)) BridgeSettings.Reload();
            if (!graphicsPage && GUI.Button(new Rect(x + 735, y + 680, 190, 36), "Done", _button)) Close();
            GUI.Label(new Rect(x + 25, y + 724, 905, 28), graphicsPage ? _message : featurePage
                ? "Settings save automatically for the next launch. The world continues running while this panel is open."
                : "Settings save automatically. The world continues running while this panel is open.", _small);
        }
        catch (Exception error) { _guiFailed = true; Log("GUI unavailable: " + error); Close(); }
        finally { GUI.matrix = matrix; GUI.color = colour; GUI.enabled = enabled; }
    }

    private static void DrawSetting(Setting setting, float y)
    {
        MenuTheme.Fill(new Rect(0, y, 885, 65), MenuTheme.Surface);
        if (setting.Override.Value) MenuTheme.Fill(new Rect(0, y, 3, 65), MenuTheme.Ochre);
        string key = setting.Page + "." + setting.Key;
        bool custom = GUI.Toggle(new Rect(2, y + 5, 23, 25), setting.Override.Value, "");
        if (custom != setting.Override.Value)
        {
            if (custom && !setting.EverApplied && setting.Owner != Owner.Vegetation) setting.Value.Value = setting.Current;
            setting.Override.Value = custom; MarkDirty();
        }
        GUI.Label(new Rect(30, y + 1, 258, 27), setting.Label, _label);
        GUI.Label(new Rect(30, y + 29, 820, 36), setting.Help, _small);
        float value = setting.Shown;
        if (!float.IsFinite(value)) value = setting.Min;
        if (setting.Kind == Kind.Toggle)
        {
            bool next = GUI.Toggle(new Rect(310, y + 2, 245, 27), value > .5f, value > .5f ? "Enabled" : "Disabled", _toggle);
            if (next != (value > .5f)) setting.Set(next ? 1 : 0);
        }
        else if (setting.Kind == Kind.Choice)
        {
            int selected = 0;
            for (int i = 0; i < setting.Choices!.Length; i++) if (Mathf.Approximately(value, setting.Choices[i])) selected = i;
            if (GUI.Button(new Rect(310, y + 1, 28, 27), "‹", _button)) setting.Set(setting.Choices[(selected + setting.Choices.Length - 1) % setting.Choices.Length]);
            GUI.Label(new Rect(345, y + 1, 170, 27), setting.ChoiceNames![selected], _label);
            if (GUI.Button(new Rect(522, y + 1, 28, 27), "›", _button)) setting.Set(setting.Choices[(selected + 1) % setting.Choices.Length]);
        }
        else
        {
            float clamped = Mathf.Clamp(value, setting.Min, setting.Max);
            float next = GUI.HorizontalSlider(new Rect(310, y + 8, 240, 24), clamped, setting.Min, setting.Max);
            if (!Mathf.Approximately(next, clamped)) { setting.Set(next); Edits.Remove(key); if (_editing == key) _editing = ""; }
            string text = Edits.TryGetValue(key, out string? edit) ? edit : (value * setting.Scale).ToString(setting.Kind == Kind.Integer ? "F0" : "0.###", CultureInfo.InvariantCulture);
            string input = GUI.TextField(new Rect(572, y + 1, 100, 27), text, 12, _field);
            if (input != text) { Edits[key] = input; _editing = key; _editingControl = GUIUtility.keyboardControl; }
            GUI.Label(new Rect(678, y + 1, 40, 27), setting.Unit, _small);
        }
        float live = setting.Current * setting.Scale;
        GUI.Label(new Rect(724, y + 1, 150, 27), setting.Available ? "Live " + live.ToString("0.###", CultureInfo.InvariantCulture) + " " + setting.Unit : "Unavailable", _small);
    }
}
