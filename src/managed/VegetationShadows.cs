using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using BepInEx;
using HarmonyLib;
using Il2CppInterop.Runtime;
using MA.Flora;
using UnityEngine;

namespace BigWalkDLSS;

internal static class VegetationShadows
{
    private sealed class ParameterSnapshot
    {
        internal OverrideProperty<float> Distance = null!;
        internal OverrideProperty<bool> Override = null!;
    }
    private sealed class MaterialSnapshot
    {
        internal Material Material = null!;
        internal readonly List<IOverrideProperty> Properties = new();
    }
    private static readonly Dictionary<IntPtr, ParameterSnapshot> Parameters = new();
    private static readonly Dictionary<IntPtr, MaterialSnapshot> Materials = new();
    private static readonly string[] FadeProperties = { "_DitherShadowMode", "_DitherDistanceMinValue", "_DitherDistanceMaxValue" };
    private static readonly int[] FadePropertyIds = Array.ConvertAll(FadeProperties, name => Shader.PropertyToID(name));
    private static readonly string[] FadeKeywords = { "TVE_DITHER_SHADOW", "TVE_DITHER" };
    internal static Func<bool> Enabled = () => false;
    internal static Func<float> Distance = () => 2000;
    internal static Func<float> FadeWidth = () => 50;
    private static float _lastMin = float.NaN, _lastMax = float.NaN;
    private static bool _internalCall, _wasEnabled, _materialApplied;
    private static bool _reportedMaterials;
    private static float _nextScan;
    private static IntPtr _reportedManager;
    private static string _lastReport = "";
    private static Harmony _harmony = null!;
    internal static bool Ready => TerrainQualityManager.instance != null && TerrainQualityManager.instance._initialized;

    internal static void Initialize()
    {
        _harmony = new Harmony("local.bigwalk.vegetation-shadows");
        _harmony.Patch(AccessTools.Method(typeof(TerrainQualityManager), nameof(TerrainQualityManager.GetFloraRenderSettings)),
            postfix: new HarmonyMethod(typeof(VegetationShadows), nameof(SettingsReturned)));
        _harmony.Patch(AccessTools.Method(typeof(TerrainQualityManager), nameof(TerrainQualityManager.ApplyVegetationShadowDither)),
            prefix: new HarmonyMethod(typeof(VegetationShadows), nameof(FadePrefix)),
            postfix: new HarmonyMethod(typeof(VegetationShadows), nameof(FadePostfix)));
    }

    private static void Log(string message)
    {
        Plugin.Instance.Log.LogInfo("Vegetation shadows: " + message);
        File.AppendAllText(Path.Combine(Paths.BepInExRootPath, "BigWalkDLSS", "vegetation.log"),
            DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture) + " " + message + Environment.NewLine);
    }
    private static float WantedDistance => Mathf.Clamp(float.IsFinite(Distance()) ? Distance() : 2000, 1, 20000);
    private static float WantedWidth => Mathf.Clamp(float.IsFinite(FadeWidth()) ? FadeWidth() : 50, 1, WantedDistance);

    private static void Apply(FloraRenderSettings settings)
    {
        if (settings == null || settings.MaxShadowDistance == null) return;
        var parameter = settings.MaxShadowDistance;
        if (!Parameters.TryGetValue(parameter.Pointer, out var snapshot))
        {
            snapshot = new ParameterSnapshot
            {
                Distance = new OverrideProperty<float>(() => parameter.value, value => parameter.value = value),
                Override = new OverrideProperty<bool>(() => parameter.overrideState, value => parameter.overrideState = value)
            };
            Parameters.Add(parameter.Pointer, snapshot);
            Log($"captured active Flora parameter: distance={parameter.value:0.###}, override={parameter.overrideState}");
        }
        snapshot.Distance.Observe(); snapshot.Override.Observe();
        parameter.overrideState = true;
        if (!Mathf.Approximately(parameter.value, WantedDistance)) parameter.value = WantedDistance;
        snapshot.Distance.Applied(); snapshot.Override.Applied();
    }
    private static void SettingsReturned(FloraRenderSettings __result)
    {
        if (!Enabled() || !Ready) return;
        try { Apply(__result); } catch (Exception error) { Log("settings hook failed: " + error.Message); }
    }
    private static void FadePrefix(float __0, float __1)
    {
        if (!_internalCall)
        {
            Log($"game vegetation shadow fade={__0:0.###}..{__1:0.###}m");
            // Let the game finish its write, then observe it before reapplying our override.
            _materialApplied = false;
        }
    }
    private static void FadePostfix(float __0, float __1) { _lastMin = __0; _lastMax = __1; }
    internal static float CurrentDistance
    {
        get
        {
            if (!Ready) return 200;
            var settings = TerrainQualityManager.instance.GetFloraRenderSettings();
            return settings != null ? settings.MaxShadowDistance.value : 200;
        }
    }
    internal static float CurrentFadeWidth => float.IsFinite(_lastMax) ? Mathf.Max(1, _lastMax - _lastMin) : 50;

    internal static void Tick()
    {
        try
        {
            if (!Enabled())
            {
                if (_wasEnabled || Parameters.Count > 0 || Materials.Count > 0) Restore();
                _wasEnabled = false;
                return;
            }
            var manager = TerrainQualityManager.instance;
            if (manager == null || !manager._initialized) return;
            if (_reportedManager != manager.Pointer)
            {
                _reportedManager = manager.Pointer;
                var quality = manager.qualitySettings;
                int index = Mathf.Clamp(QualitySettings.GetQualityLevel(), 0, quality.Length - 1);
                var q = quality[index];
                Log($"quality {index}: vegetation fade={q.vegShadowDitherDistanceMin:0.###}..{q.vegShadowDitherDistanceMax:0.###}m, shadow flag={manager.currentShadowsValue}");
                ReportPrefabs(manager);
            }
            _wasEnabled = true;
            bool materialChanged = false;
            if (Time.unscaledTime >= _nextScan)
            {
                _nextScan = Time.unscaledTime + 1;
                materialChanged = MaterialsChanged(manager.floraQualityData);
            }
            var current = manager.GetFloraRenderSettings();
            if (current != null) Apply(current);
            float start = Mathf.Max(0, WantedDistance - WantedWidth);
            if ((!_materialApplied || materialChanged || !Mathf.Approximately(_lastMin, start) || !Mathf.Approximately(_lastMax, WantedDistance)) && manager.floraQualityData != null)
            {
                ObserveMaterials(manager.floraQualityData);
                _internalCall = true;
                try { manager.ApplyVegetationShadowDither(start, WantedDistance, manager.floraQualityData); _materialApplied = true; }
                finally
                {
                    _internalCall = false;
                    foreach (var snapshot in Materials.Values)
                        if (snapshot.Material != null) foreach (var property in snapshot.Properties) property.Applied();
                }
            }
            string report = $"active: Flora distance={(current != null ? current.MaxShadowDistance.value : 0):0.###}m, material fade={_lastMin:0.###}..{_lastMax:0.###}m, parameters={Parameters.Count}, materials={Materials.Count}";
            if (report != _lastReport) { _lastReport = report; Log(report); }
        }
        catch (Exception error) { Log("update failed: " + error); }
    }

    private static bool MaterialsChanged(HouseFloraQualityData data)
    {
        if (data == null || data.allFloraMaterials == null) return false;
        foreach (var material in data.allFloraMaterials)
        {
            if (material == null) continue;
            if (!Materials.TryGetValue(material.Pointer, out var snapshot)) return true;
            foreach (var property in snapshot.Properties) if (property.Changed()) return true;
        }
        return false;
    }

    private static void ObserveMaterials(HouseFloraQualityData data)
    {
        if (data.allFloraMaterials == null) return;
        int samples = 0;
        foreach (var material in data.allFloraMaterials)
        {
            if (material == null) continue;
            if (!Materials.TryGetValue(material.Pointer, out var snapshot))
            {
                snapshot = new MaterialSnapshot { Material = material };
                foreach (int id in FadePropertyIds)
                    if (material.HasProperty(id)) snapshot.Properties.Add(new OverrideProperty<float>(() => material.GetFloat(id), value => material.SetFloat(id, value)));
                foreach (string name in FadeKeywords)
                    snapshot.Properties.Add(new OverrideProperty<bool>(() => material.IsKeywordEnabled(name), value => { if (value) material.EnableKeyword(name); else material.DisableKeyword(name); }));
                snapshot.Properties.Add(new OverrideProperty<bool>(() => material.GetShaderPassEnabled("ShadowCaster"), value => material.SetShaderPassEnabled("ShadowCaster", value)));
                Materials.Add(material.Pointer, snapshot);
            }
            if (!_reportedMaterials && samples < 2 && material.HasProperty(FadePropertyIds[0]))
            {
                samples++;
                string values = "";
                for (int i = 0; i < FadePropertyIds.Length; i++)
                    if (material.HasProperty(FadePropertyIds[i])) values += $" {FadeProperties[i]}={material.GetFloat(FadePropertyIds[i]):0.###}";
                Log($"material baseline {material.name}:{values}, TVE_DITHER_SHADOW={material.IsKeywordEnabled("TVE_DITHER_SHADOW")}, TVE_DITHER={material.IsKeywordEnabled("TVE_DITHER")}");
            }
        }
        // Observe the complete touched set before the helper can change any shared material.
        foreach (var snapshot in Materials.Values)
            if (snapshot.Material != null) foreach (var property in snapshot.Properties) property.Observe();
        if (!_reportedMaterials) { Log($"material baseline captured: materials={Materials.Count}; raw properties retained, no quality-table fade fallback"); _reportedMaterials = true; }
    }

    private static void ReportPrefabs(TerrainQualityManager manager)
    {
        try
        {
            var data = manager.floraQualityData;
            Log($"vegetation entries={(data != null && data.entries != null ? data.entries.Count : 0)}, materials={(data != null && data.allFloraMaterials != null ? data.allFloraMaterials.Count : 0)}");
            int recorded = 0;
            if (data != null && data.entries != null)
                for (int i = 0; i < data.entries.Count && recorded < 12; i++)
                {
                    var prefab = data.entries[i].prefab;
                    if (prefab == null || prefab.name.IndexOf("tree", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    recorded++;
                    foreach (var component in prefab.GetComponentsInChildren(Il2CppType.Of<FloraAdditionalRendererSettings>(), true))
                    {
                        var additional = component.TryCast<FloraAdditionalRendererSettings>();
                        if (additional != null) Log($"prefab {prefab.name}: shadow cap={additional.MaxShadowDistance:0.###}, min shadow LOD={additional.MinShadowLOD}");
                    }
                    foreach (var component in prefab.GetComponentsInChildren(Il2CppType.Of<LODGroup>(), true))
                    {
                        var group = component.TryCast<LODGroup>();
                        if (group == null) continue;
                        var lods = group.GetLODs();
                        for (int level = 0; level < lods.Length; level++)
                        {
                            int off = 0, total = 0;
                            foreach (var renderer in lods[level].renderers)
                                if (renderer != null) { total++; if (renderer.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.Off) off++; }
                            Log($"prefab {prefab.name} LOD{level}: screen threshold={lods[level].screenRelativeTransitionHeight:0.####}, shadow-off renderers={off}/{total}");
                        }
                    }
                }
            foreach (var item in UnityEngine.Object.FindObjectsOfType(Il2CppType.Of<Light>()))
            {
                var light = item.TryCast<Light>();
                if (light == null || light.type != LightType.Directional) continue;
                var distances = light.layerShadowCullDistances;
                for (int layer = 0; layer < distances.Length; layer++)
                    if (distances[layer] > 0) Log($"directional {light.name}: layer {layer}/{LayerMask.LayerToName(layer)} shadow cap={distances[layer]:0.###}m");
            }
        }
        catch (Exception error) { Log("prefab diagnostics: " + error.Message); }
    }

    private static void Restore()
    {
        int parameters = 0, properties = 0, touched = 0;
        foreach (var snapshot in Parameters.Values)
        {
            try
            {
                bool distance = snapshot.Distance.Restore();
                bool state = snapshot.Override.Restore();
                if (distance || state) parameters++;
            }
            catch (Exception error) { Log("parameter restore failed: " + error.Message); }
        }
        foreach (var snapshot in Materials.Values)
            if (snapshot.Material != null)
                foreach (var property in snapshot.Properties)
                {
                    touched++;
                    try { if (property.Restore()) properties++; }
                    catch (Exception error) { Log("material restore failed: " + error.Message); }
                }
        Parameters.Clear(); Materials.Clear();
        _materialApplied = _reportedMaterials = false;
        _lastMin = _lastMax = float.NaN;
        _lastReport = "";
        Log($"restored owned Flora parameters={parameters}, material properties={properties}/{touched}; no fade-helper replay");
    }
    internal static void Shutdown() { Restore(); _harmony.UnpatchSelf(); }
}
