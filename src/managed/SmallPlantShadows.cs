using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using HarmonyLib;
using Il2CppInterop.Runtime;
using MA.Flora;
using UnityEngine;
using UnityEngine.Rendering;

namespace BigWalkDLSS;

internal static class SmallPlantShadows
{
    private sealed record Snapshot(MeshRenderer Renderer, OverrideOwnership<ShadowCastingMode> Mode);
    private static readonly Dictionary<IntPtr, Snapshot> Changed = new();
    private static readonly HashSet<IntPtr> Seen = new();
    private static ConfigEntry<bool> _request = null!;
    private static Harmony? _harmony;
    private static string _error = "";
    internal static bool StartupEnabled { get; private set; }
    internal static bool Requested => _request.Value;
    internal static string Status => _error.Length > 0 ? _error :
        "This launch: " + (StartupEnabled ? "requested On; renderer flags changed: " + Changed.Count : "Off") +
        (Requested != StartupEnabled ? ". Restart Big Walk to apply the queued choice." : ". Changes require a restart.");

    internal static void Initialize(ConfigEntry<bool> request, bool compatible)
    {
        _request = request;
        StartupEnabled = compatible && request.Value;
        if (!StartupEnabled) return;
        try
        {
            _harmony = new Harmony("local.bigwalk.small-plant-shadows");
            _harmony.Patch(AccessTools.Method(typeof(TerrainQualityManager), nameof(TerrainQualityManager.Initialize)),
                prefix: new HarmonyMethod(typeof(SmallPlantShadows), nameof(BeforeTerrainQuality)));
            _harmony.Patch(AccessTools.Method(typeof(FloraSystem), nameof(FloraSystem.RegisterTerrain), new[] { typeof(Terrain) }),
                prefix: new HarmonyMethod(typeof(SmallPlantShadows), nameof(BeforeTerrainRegistration)));
            _harmony.Patch(AccessTools.Method(typeof(FloraInstanceRenderer), nameof(FloraInstanceRenderer.OnEnable)),
                prefix: new HarmonyMethod(typeof(SmallPlantShadows), nameof(BeforeInstanceEnable)));
            Plugin.Instance.Log.LogInfo("Small-plant caster request enabled for this launch; applying before Flora registers sources.");
        }
        catch (Exception error)
        {
            StartupEnabled = false; Shutdown();
            _error = "Small-plant hooks unavailable: " + error.GetType().Name + ". See the BepInEx log.";
            Plugin.Instance.Log.LogWarning(_error + " " + error.Message);
        }
    }

    internal static void Request(bool value)
    {
        try { _request.Value = value; _request.ConfigFile.Save(); _error = ""; }
        catch (Exception error) { _error = "Could not save the plant-shadow request. See the BepInEx log."; Plugin.Instance.Log.LogWarning(_error + " " + error.Message); }
    }

    private static void BeforeTerrainQuality(TerrainQualityManager __instance)
    {
        try
        {
            var entries = __instance.floraQualityData?.entries;
            if (entries == null) return;
            for (int i = 0; i < entries.Count; i++) ApplyPrefab(entries[i].prefab);
        }
        catch (Exception error) { Report(error); }
    }
    private static void BeforeTerrainRegistration(Terrain __0)
    {
        try
        {
            var data = __0.terrainData;
            if (data == null) return;
            foreach (var detail in data.detailPrototypes) if (detail != null) ApplyPrefab(detail.prototype);
            foreach (var tree in data.treePrototypes) if (tree != null) ApplyPrefab(tree.prefab);
        }
        catch (Exception error) { Report(error); }
    }
    private static void BeforeInstanceEnable(FloraInstanceRenderer __instance)
    {
        try { ApplyPrefab(__instance.m_Prefab); }
        catch (Exception error) { Report(error); }
    }

    internal static void ApplyPrefab(GameObject? prefab)
    {
        if (!StartupEnabled || prefab == null || !PlantName(prefab.name) || !Seen.Add(prefab.Pointer)) return;
        int count = 0;
        bool deferred = false;
        try
        {
            foreach (var component in prefab.GetComponentsInChildren(Il2CppType.Of<MeshRenderer>(), true))
            {
                var renderer = component.TryCast<MeshRenderer>();
                if (renderer == null || renderer.shadowCastingMode != ShadowCastingMode.Off) continue;
                var materials = renderer.sharedMaterials;
                bool supported = materials.Length > 0;
                if (materials.Length == 0) deferred = true;
                foreach (var material in materials)
                    if (material == null || material.shader == null) { supported = false; deferred = true; }
                    else if (!SupportedShader(material.shader.name)) supported = false;
                if (!supported) continue;
                var mode = new OverrideOwnership<ShadowCastingMode>();
                mode.Observe(renderer.shadowCastingMode);
                Changed[renderer.Pointer] = new Snapshot(renderer, mode);
                try { renderer.shadowCastingMode = ShadowCastingMode.On; }
                finally { mode.Applied(ShadowCastingMode.On); }
                count++;
            }
        }
        catch { Seen.Remove(prefab.Pointer); throw; }
        if (deferred) Seen.Remove(prefab.Pointer);
        if (count > 0) Plugin.Instance.Log.LogInfo("Small-plant caster flags: " + prefab.name + ", enabled renderers=" + count + ". Visible shadows remain subject to Flora culling and the current shadow distance.");
    }
    internal static bool PlantName(string name) => name.IndexOf("grass", StringComparison.OrdinalIgnoreCase) >= 0 ||
        name.IndexOf("bush", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("shrub", StringComparison.OrdinalIgnoreCase) >= 0 ||
        name.IndexOf("fern", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("flower", StringComparison.OrdinalIgnoreCase) >= 0;
    internal static bool SupportedShader(string name) => name == "Flora/TerrainGrass" || name == "BOXOPHOBIC/The Visual Engine/Geometry/House Standard Lit";
    private static void Report(Exception error)
    {
        _error = "A plant-shadow source could not be changed. See the BepInEx log.";
        Plugin.Instance.Log.LogWarning(_error + " " + error.GetType().Name + ": " + error.Message);
    }
    internal static void Shutdown()
    {
        foreach (var snapshot in Changed.Values)
            if (snapshot.Renderer != null) { try { if (snapshot.Mode.TryRestore(snapshot.Renderer.shadowCastingMode, out var original)) snapshot.Renderer.shadowCastingMode = original; } catch (Exception error) { Report(error); } }
        Changed.Clear(); Seen.Clear();
        try { _harmony?.UnpatchSelf(); }
        catch (Exception error) { Report(error); }
        finally { _harmony = null; }
    }
}
