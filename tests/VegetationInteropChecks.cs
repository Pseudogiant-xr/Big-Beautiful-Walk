using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using BigWalkDLSS;
using MA.Flora;
using UnityEngine;

internal static class Program
{
    private static int _checks;
    private static void Check(bool condition, string reason) { _checks++; if (!condition) throw new Exception(reason); }
    private static void Main()
    {
        string root = Path.Combine(Path.GetTempPath(), "bbw-vegetation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "BigWalkDLSS"));
        BepInEx.Paths.BepInExRootPath = root;
        try
        {
            var manager = new TerrainQualityManager();
            TerrainQualityManager.instance = manager;
            var material = new Material { Pointer = (IntPtr)20, name = "fixture" };
            NativePassFixture.Materials.Add(material.Pointer, material);
            manager.floraQualityData.allFloraMaterials.Add(material);
            manager.floraQualityData.allFloraMaterials.Add(material); // Shared aliases must retain one baseline.
            bool enabled = true;
            float distance = 300;
            VegetationShadows.Enabled = () => enabled;
            VegetationShadows.Distance = () => distance;
            VegetationShadows.FadeWidth = () => 30;
            foreach (float next in new[] { 300f, 1000f, 2000f })
            {
                distance = next; Time.unscaledTime += 2;
                VegetationShadows.Tick();
                Check(material.GetFloat("_DitherDistanceMaxValue") == next, "Native material adapter did not apply the requested distance");
                Check(manager.Settings.MaxShadowDistance.value == next, "Active parameter adapter did not apply");
            }
            Time.unscaledTime += 2; VegetationShadows.Tick();
            Check(manager.HelperCalls == 3, "Unchanged materials unexpectedly retriggered the fade helper");
            enabled = false; VegetationShadows.Tick();
            Check(material.GetFloat("_DitherShadowMode") == 0 && material.GetFloat("_DitherDistanceMaxValue") == 0, "Raw material values did not restore");
            Check(!material.IsKeywordEnabled("TVE_DITHER_SHADOW") && MaterialPassState.ReadShadowCaster(material), "Keyword/pass state did not restore");
            Check(manager.Settings.MaxShadowDistance.value == 100 && !manager.Settings.MaxShadowDistance.overrideState, "Active parameter did not restore");
            Check(manager.HelperCalls == 3, "Restore unexpectedly replayed the fade helper");

            enabled = true; Time.unscaledTime += 2; VegetationShadows.Tick();
            material.SetFloat("_DitherDistanceMaxValue", 125); // Native quality update while overridden.
            Time.unscaledTime += 2; VegetationShadows.Tick();
            Check(material.GetFloat("_DitherDistanceMaxValue") == distance, "Outside update was not reapplied");
            enabled = false; VegetationShadows.Tick();
            Check(material.GetFloat("_DitherDistanceMaxValue") == 125, "Latest outside material value was lost on restore");
            Check(manager.floraQualityData.allFloraMaterials.EnumeratorRequests == 0, "Production adapter requested the native value-type enumerator");
            Check(Light.LayerDistanceReads == 0, "Diagnostics invoked the unsafe native shadow-distance array getter");
            Check(NativePassFixture.Reads > 0 && !NativePassFixture.BadArguments, "Native shader-pass binding did not preserve UTF-16 span/object arguments");
            Console.WriteLine($"Vegetation interop checks passed: {_checks} assertions; production Tick/restore, indexed native lists, shared materials, external changes; Unity APIs stubbed.");
        }
        finally
        {
            File.Delete(Path.Combine(root, "BigWalkDLSS", "vegetation.log"));
            Directory.Delete(Path.Combine(root, "BigWalkDLSS")); Directory.Delete(root);
        }
    }
}

internal static class NativePassFixture
{
    internal static readonly Dictionary<IntPtr, Material> Materials = new();
    internal static bool BadArguments;
    internal static int Reads;
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe byte Read(IntPtr material, IntPtr span)
    {
        Reads++;
        var begin = *(IntPtr*)span;
        int length = *(int*)((byte*)span + 8);
        if (length != 12 || begin == IntPtr.Zero || new string((char*)begin, 0, length) != "ShadowCaster" || !Materials.TryGetValue(material, out var value))
        { BadArguments = true; return 0; }
        return value.PassEnabled ? (byte)1 : (byte)0;
    }
}

// Fail at the managed/native boundary rather than silently accepting a CLR List.
internal sealed class NativeList<T> : IEnumerable<T>
{
    private readonly List<T> _items = new();
    internal int EnumeratorRequests;
    internal int Count => _items.Count;
    internal T this[int index] => _items[index];
    internal void Add(T item) => _items.Add(item);
    public IEnumerator<T> GetEnumerator() { EnumeratorRequests++; throw new InvalidOperationException("Native value-type enumerator requested"); }
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
internal sealed class TerrainQualityManager
{
    internal static TerrainQualityManager instance = null!;
    internal bool _initialized = true, currentShadowsValue = true;
    internal IntPtr Pointer = (IntPtr)1;
    internal Quality[] qualitySettings = { new() };
    internal HouseFloraQualityData floraQualityData = new();
    internal FloraRenderSettings Settings = new();
    internal int HelperCalls;
    internal FloraRenderSettings GetFloraRenderSettings() => Settings;
    internal void ApplyVegetationShadowDither(float min, float max, HouseFloraQualityData data)
    {
        HelperCalls++;
        for (int i = 0; i < data.allFloraMaterials.Count; i++)
        {
            var material = data.allFloraMaterials[i];
            material.SetFloat("_DitherShadowMode", 1); material.SetFloat("_DitherDistanceMinValue", min); material.SetFloat("_DitherDistanceMaxValue", max);
            material.EnableKeyword("TVE_DITHER_SHADOW"); material.EnableKeyword("TVE_DITHER"); material.SetShaderPassEnabled("ShadowCaster", false);
        }
        // Simulate the installed Harmony postfix so change detection is tested
        // without forcing a reapply through unset fade readback values.
        typeof(VegetationShadows).GetMethod("FadePostfix", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
            .Invoke(null, new object[] { min, max });
    }
    internal sealed class Quality { internal float vegShadowDitherDistanceMin = 0, vegShadowDitherDistanceMax = 0; }
}
internal sealed class HouseFloraQualityData
{
    internal NativeList<Material> allFloraMaterials = new();
    internal NativeList<Entry> entries = new();
    internal sealed class Entry { internal GameObject prefab = null!; }
}
namespace BigWalkDLSS
{
    internal sealed class Plugin
    {
        internal static Plugin Instance = new(); internal Logger Log = new();
        internal sealed class Logger { internal void LogInfo(string message) { } }
    }
}
namespace BepInEx { internal static class Paths { internal static string BepInExRootPath = ""; } }
namespace HarmonyLib
{
    internal sealed class Harmony { internal Harmony(string id) { } internal void Patch(object method, HarmonyMethod? prefix = null, HarmonyMethod? postfix = null) { } internal void UnpatchSelf() { } }
    internal sealed class HarmonyMethod { internal HarmonyMethod(Type type, string name) { } }
    internal static class AccessTools { internal static object Method(Type type, string name) => new(); }
}
namespace Il2CppInterop.Runtime
{
    internal static class Il2CppType { internal static Type Of<T>() => typeof(T); }
    internal static class IL2CPP
    {
        internal static unsafe T ResolveICall<T>(string name) where T : Delegate
        {
            if (name != "UnityEngine.Material::GetShaderPassEnabled_Injected") throw new Exception("Unexpected native binding");
            return Marshal.GetDelegateForFunctionPointer<T>((IntPtr)(delegate* unmanaged[Cdecl]<IntPtr, IntPtr, byte>)&NativePassFixture.Read);
        }
    }
}
namespace MA.Flora
{
    internal sealed class Parameter { internal IntPtr Pointer = (IntPtr)2; internal float value = 100; internal bool overrideState; }
    internal sealed class FloraRenderSettings { internal Parameter MaxShadowDistance = new(); }
    internal sealed class FloraAdditionalRendererSettings : UnityEngine.Object { internal float MaxShadowDistance = 0; internal int MinShadowLOD = 0; }
}
namespace UnityEngine
{
    internal class Object
    {
        internal IntPtr Pointer; internal string name = "";
        internal T? TryCast<T>() where T : class => this as T;
        internal static Object[] FindObjectsOfType(Type type) => type == typeof(Light) ? new Object[] { new Light() } : Array.Empty<Object>();
        internal static class MarshalledUnityObject { internal static IntPtr MarshalNotNull<T>(T value) where T : Object => value.Pointer; }
    }
    internal sealed class Material : Object
    {
        private readonly Dictionary<int, float> _values = new(); private readonly HashSet<string> _keywords = new(); internal bool PassEnabled = true;
        internal bool HasProperty(int id) => true;
        internal float GetFloat(int id) => _values.TryGetValue(id, out var value) ? value : 0;
        internal float GetFloat(string name) => GetFloat(Shader.PropertyToID(name));
        internal void SetFloat(int id, float value) => _values[id] = value;
        internal void SetFloat(string name, float value) => SetFloat(Shader.PropertyToID(name), value);
        internal bool IsKeywordEnabled(string name) => _keywords.Contains(name);
        internal void EnableKeyword(string name) => _keywords.Add(name);
        internal void DisableKeyword(string name) => _keywords.Remove(name);
        internal bool GetShaderPassEnabled(string name) => throw new MissingMethodException("Il2CppSystem.ReadOnlySpan.GetPinnableReference");
        internal void SetShaderPassEnabled(string name, bool value) => PassEnabled = value;
    }
    internal static class Shader { internal static int PropertyToID(string name) => StringComparer.Ordinal.GetHashCode(name); }
    internal static class Mathf
    {
        internal static float Clamp(float value, float min, float max) => Math.Clamp(value, min, max);
        internal static int Clamp(int value, int min, int max) => Math.Clamp(value, min, max);
        internal static float Max(float a, float b) => Math.Max(a, b);
        internal static bool Approximately(float a, float b) => Math.Abs(a - b) < 0.00001f;
    }
    internal static class Time { internal static float unscaledTime; }
    internal static class QualitySettings { internal static int GetQualityLevel() => 0; }
    internal sealed class GameObject : Object { internal Object[] GetComponentsInChildren(Type type, bool includeInactive) => Array.Empty<Object>(); }
    internal sealed class LODGroup : Object { internal LOD[] GetLODs() => Array.Empty<LOD>(); }
    internal sealed class LOD { internal Renderer[] renderers = Array.Empty<Renderer>(); internal float screenRelativeTransitionHeight = 0; }
    internal sealed class Renderer { internal Rendering.ShadowCastingMode shadowCastingMode = Rendering.ShadowCastingMode.On; }
    internal sealed class Light : Object
    {
        internal static int LayerDistanceReads;
        internal LightType type = LightType.Directional;
        internal float[] layerShadowCullDistances { get { LayerDistanceReads++; throw new InvalidOperationException("Unsafe native array marshalling"); } }
    }
    internal enum LightType { Directional }
    internal static class LayerMask { internal static string LayerToName(int layer) => ""; }
}
namespace UnityEngine.Rendering { internal enum ShadowCastingMode { Off, On } }
