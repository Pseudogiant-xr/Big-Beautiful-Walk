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
    private sealed class Snapshot
    {
        internal FloraRenderSettings Settings = null!;
        internal float Distance;
        internal bool Override;
    }
    private static readonly Dictionary<IntPtr, Snapshot> Originals = new();
    internal static Func<bool> Enabled = () => false;
    internal static Func<float> Distance = () => 2000;
    internal static Func<float> FadeWidth = () => 50;
    private static float _lastMin = float.NaN, _lastMax = float.NaN;
    private static float _originalMin, _originalMax;
    private static bool _haveOriginalFade, _internalCall, _wasEnabled;
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
        if (!Originals.ContainsKey(settings.Pointer))
        {
            Originals[settings.Pointer] = new Snapshot { Settings = settings, Distance = settings.MaxShadowDistance.value, Override = settings.MaxShadowDistance.overrideState };
            Log($"captured Flora settings {settings.name}: distance={settings.MaxShadowDistance.value:0.###}, override={settings.MaxShadowDistance.overrideState}, min shadow LOD={settings.MinShadowLOD.value}");
        }
        settings.MaxShadowDistance.overrideState = true;
        if (!Mathf.Approximately(settings.MaxShadowDistance.value, WantedDistance)) settings.MaxShadowDistance.value = WantedDistance;
    }
    private static void SettingsReturned(FloraRenderSettings __result)
    {
        if (!Enabled()) return;
        try { Apply(__result); } catch (Exception error) { Log("settings hook failed: " + error.Message); }
    }
    private static void FadePrefix(ref float __0, ref float __1)
    {
        if (!_internalCall)
        {
            _originalMin = __0; _originalMax = __1; _haveOriginalFade = true;
            Log($"game vegetation shadow fade={__0:0.###}..{__1:0.###}m");
        }
        if (!Enabled()) return;
        __1 = WantedDistance; __0 = Mathf.Max(0, __1 - WantedWidth);
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
            var manager = TerrainQualityManager.instance;
            if (manager == null || !manager._initialized) return;
            if (_reportedManager != manager.Pointer)
            {
                _reportedManager = manager.Pointer;
                var quality = manager.qualitySettings;
                int index = Mathf.Clamp(QualitySettings.GetQualityLevel(), 0, quality.Length - 1);
                var q = quality[index];
                Log($"quality {index}: vegetation fade={q.vegShadowDitherDistanceMin:0.###}..{q.vegShadowDitherDistanceMax:0.###}m, shadow flag={manager.currentShadowsValue}");
                if (!_haveOriginalFade) { _originalMin = q.vegShadowDitherDistanceMin; _originalMax = q.vegShadowDitherDistanceMax; _haveOriginalFade = true; }
                ReportPrefabs(manager);
            }
            if (!Enabled()) { if (_wasEnabled) Restore(); _wasEnabled = false; return; }
            _wasEnabled = true;
            if (Time.unscaledTime >= _nextScan)
            {
                _nextScan = Time.unscaledTime + 1;
                foreach (var item in Resources.FindObjectsOfTypeAll(Il2CppType.Of<FloraRenderSettings>()))
                {
                    var settings = item.TryCast<FloraRenderSettings>();
                    if (settings != null) Apply(settings);
                }
            }
            var current = manager.GetFloraRenderSettings();
            if (current != null) Apply(current);
            foreach (var snapshot in Originals.Values) if (snapshot.Settings != null) Apply(snapshot.Settings);
            float start = Mathf.Max(0, WantedDistance - WantedWidth);
            if ((!Mathf.Approximately(_lastMin, start) || !Mathf.Approximately(_lastMax, WantedDistance)) && manager.floraQualityData != null)
            {
                _internalCall = true;
                try { manager.ApplyVegetationShadowDither(start, WantedDistance, manager.floraQualityData); }
                finally { _internalCall = false; }
            }
            string report = $"active: Flora distance={(current != null ? current.MaxShadowDistance.value : 0):0.###}m, material fade={_lastMin:0.###}..{_lastMax:0.###}m, settings objects={Originals.Count}";
            if (report != _lastReport) { _lastReport = report; Log(report); }
        }
        catch (Exception error) { Log("update failed: " + error); }
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
        foreach (var snapshot in Originals.Values)
        {
            if (snapshot.Settings == null) continue;
            snapshot.Settings.MaxShadowDistance.value = snapshot.Distance;
            snapshot.Settings.MaxShadowDistance.overrideState = snapshot.Override;
        }
        Originals.Clear();
        var manager = TerrainQualityManager.instance;
        if (manager != null && manager.floraQualityData != null && _haveOriginalFade)
        {
            _internalCall = true;
            try { manager.ApplyVegetationShadowDither(_originalMin, _originalMax, manager.floraQualityData); }
            finally { _internalCall = false; }
        }
        _lastReport = ""; Log("captured Flora caps and vegetation material fade restored");
    }
    internal static void Shutdown() { Restore(); _harmony.UnpatchSelf(); }
}
