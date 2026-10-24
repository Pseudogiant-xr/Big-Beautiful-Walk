using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using BepInEx;
using Unity.Profiling;
using Unity.Profiling.LowLevel;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace BigWalkDLSS;

internal static class PerformanceCapture
{
    private sealed class Counter(string name, bool gpu = false)
    {
        internal readonly string Name = name;
        internal readonly bool Gpu = gpu;
        internal ProfilerRecorder Recorder;
        internal bool Created, Failed;
        internal string Unit = "unavailable";
    }
    private sealed record Sample(double FrameMs, double SunAltitude, Vector3 Position, Vector3 Forward, bool HasCamera, double?[] Values);
    private static readonly List<Counter> Counters = new();
    private static readonly List<Sample> Samples = new();
    private static readonly List<string> ShadowStates = new();
    private static double _start, _nextContext, _sunAltitude = double.NaN;
    private static Vector3 _position, _forward;
    private static bool _hasCamera;
    private static string _metadata = "";
    internal static bool Active { get; private set; }
    internal static string Status { get; private set; } = "Capture once in daylight and once during the slowdown, from the same stationary view.";

    internal static void Begin()
    {
        if (Active) return;
        Samples.Clear(); Counters.Clear(); ShadowStates.Clear();
        try
        {
            foreach (string name in new[] { "CPU Main Thread Frame Time", "CPU Render Thread Frame Time", "GPU Frame Time",
                "Draw Main Light Shadowmap", "Setup Main Shadowmap", "Shadow Casters Count", "Draw Calls Count", "Triangles Count", "Gfx Used Memory", "Gfx Reserved Memory" })
                Add(name);
            Add("Draw Main Light Shadowmap", true);
            try
            {
                string flora = MA.Flora.FloraSystem.PerformBatchCullingMarkerName;
                if (!string.IsNullOrEmpty(flora)) Add(flora);
            }
            catch (Exception error) { Plugin.Instance.Log.LogInfo("Flora performance marker unavailable: " + error.GetType().Name + "."); }
            var asset = GraphicsSettings.currentRenderPipeline?.TryCast<UniversalRenderPipelineAsset>();
            int? floraFloor = null;
            try { floraFloor = TerrainQualityManager.instance?.GetFloraRenderSettings()?.MinShadowLOD?.value; } catch { }
            _metadata = "# GPU=" + SystemInfo.graphicsDeviceName + "; output=" + Screen.width + "x" + Screen.height +
                "; input_scale_request=" + Number(Plugin.RenderScale.Value) + "; backend_startup_request=" + BridgeSettings.RunningBackend +
                "; FG_startup_request=" + FeatureSettings.StartupFrameGen.Enabled + "; NR_startup_request=" + FeatureSettings.StartupNeural.Enabled +
                "; shadow_distance=" + Number(asset?.shadowDistance ?? double.NaN) + "; atlas_request=" + (asset?.mainLightShadowmapResolution ?? 0) +
                "; main_shadows_enabled=" + asset?.supportsMainLightShadows + "; cascades=" + asset?.shadowCascadeCount +
                "; soft_shadows=" + asset?.supportsSoftShadows + "; soft_quality=" + asset?.softShadowQuality + "; flora_min_shadow_lod=" + floraFloor + "; conservative_culling=" + asset?.conservativeEnclosingSphere + "; texture_memory_capacity_MiB=" + SystemInfo.graphicsMemorySize;
            _start = Time.realtimeSinceStartupAsDouble + 3;
            _nextContext = 0;
            Active = true;
            ShadowPassProbe.Begin(ShadowStates.Add, () => Active && Time.realtimeSinceStartupAsDouble >= _start && Time.realtimeSinceStartupAsDouble <= _start + 30);
            ShadowReceiverProbe.Begin(() => Active && Time.realtimeSinceStartupAsDouble >= _start && Time.realtimeSinceStartupAsDouble <= _start + 30);
            Status = "Capturing after a 3-second warm-up. Keep the same view for 30 seconds; no settings are changed.";
            Plugin.Instance.Log.LogInfo("Performance capture started; close Home and keep the camera still.");
        }
        catch (Exception error) { Fail(error); }
    }

    private static unsafe void Add(string name, bool gpu = false)
    {
        var counter = new Counter(name, gpu);
        Counters.Add(counter);
        try
        {
            // Avoid the generated string/span wrapper, as with the artwork binding.
            ProfilerRecorderHandle handle;
            fixed (char* pointer = name)
                handle = ProfilerRecorderHandle.GetByName_Unsafe(ProfilerCategory.Any, pointer, name.Length);
            if (!handle.Valid) return;
            counter.Recorder = new ProfilerRecorder(handle, 1, ProfilerRecorderOptions.Default |
                ProfilerRecorderOptions.StartImmediately | (gpu ? ProfilerRecorderOptions.GpuRecorder : ProfilerRecorderOptions.None));
            counter.Created = true;
            if (counter.Recorder.Valid) counter.Unit = counter.Recorder.UnitType.ToString();
        }
        catch (Exception error)
        {
            counter.Failed = true;
            Plugin.Instance.Log.LogInfo("Performance counter unavailable: " + name + " (" + error.GetType().Name + ").");
        }
    }

    internal static void Tick()
    {
        if (!Active) return;
        try
        {
            if (!Application.isFocused) { FocusChanged(false); return; }
            double now = Time.realtimeSinceStartupAsDouble;
            ShadowReceiverProbe.Drain(ShadowStates.Add);
            if (now < _start) return;
            if (now >= _nextContext)
            {
                _nextContext = now + 1;
                var sun = RenderSettings.sun;
                _sunAltitude = sun != null ? Math.Asin(Math.Clamp(-sun.transform.forward.y, -1, 1)) * 180 / Math.PI : double.NaN;
                var camera = Plugin.RenderingCamera != null ? Plugin.RenderingCamera : Camera.main;
                _hasCamera = camera != null;
                _position = camera != null ? camera.transform.position : Vector3.zero;
                _forward = camera != null ? camera.transform.forward : Vector3.zero;
            }
            var values = new double?[Counters.Count];
            for (int i = 0; i < Counters.Count; i++)
            {
                var counter = Counters[i];
                if (!counter.Created || counter.Failed) continue;
                try
                {
                    if (!counter.Recorder.Valid || counter.Recorder.Count == 0) continue;
                    double value = counter.Recorder.LastValueAsDouble;
                    if (double.IsFinite(value) && (!(counter.Gpu || counter.Name == "GPU Frame Time") || value > 0)) values[i] = value;
                }
                catch (Exception error) { counter.Failed = true; Plugin.Instance.Log.LogInfo("Performance counter stopped: " + counter.Name + " (" + error.GetType().Name + ")."); }
            }
            double frameMs = Time.unscaledDeltaTime * 1000;
            if (double.IsFinite(frameMs) && frameMs > 0)
                Samples.Add(new Sample(frameMs, _sunAltitude, _position, _forward, _hasCamera, values));
            // Retain the complete finishing interval, including a stall across the deadline.
            if (now >= _start + 30) { Save(); Shutdown(); }
        }
        catch (Exception error) { Fail(error); }
    }

    private static void Save()
    {
        var output = new StringBuilder(_metadata).AppendLine();
        output.AppendLine("# Complete frame intervals are retained, including the finishing frame; a boundary stall can extend the requested 30-second window.");
        output.AppendLine("# Game update timings are not generated-frame FPS. Marker values use the stated native units; blank means unavailable. CPU scopes do not measure GPU cost.");
        foreach (string state in ShadowStates) output.AppendLine(state);
        var frames = Samples.ConvertAll(sample => sample.FrameMs);
        double total = 0; foreach (double value in frames) total += value;
        frames.Sort();
        output.AppendLine("# frames=" + frames.Count + "; mean_frame_ms=" + Number(frames.Count > 0 ? total / frames.Count : double.NaN) +
            "; mean_game_FPS=" + Number(total > 0 ? frames.Count * 1000 / total : double.NaN) +
            "; p95_frame_ms=" + Number(frames.Count > 0 ? frames[(int)Math.Ceiling(frames.Count * .95) - 1] : double.NaN));
        output.Append("frame_ms,sun_altitude_deg,camera_x,camera_y,camera_z,forward_x,forward_y,forward_z");
        foreach (var counter in Counters) output.Append(',').Append('"').Append(counter.Name.Replace("\"", "\"\"")).Append(counter.Gpu ? " GPU" : " CPU/counter").Append(" [").Append(counter.Unit).Append(']').Append('"');
        output.AppendLine();
        foreach (var sample in Samples)
        {
            output.Append(Number(sample.FrameMs)).Append(',').Append(Number(sample.SunAltitude)).Append(',')
                .Append(sample.HasCamera ? Number(sample.Position.x) : "").Append(',').Append(sample.HasCamera ? Number(sample.Position.y) : "").Append(',').Append(sample.HasCamera ? Number(sample.Position.z) : "").Append(',')
                .Append(sample.HasCamera ? Number(sample.Forward.x) : "").Append(',').Append(sample.HasCamera ? Number(sample.Forward.y) : "").Append(',').Append(sample.HasCamera ? Number(sample.Forward.z) : "");
            foreach (double? value in sample.Values) output.Append(',').Append(value.HasValue ? Number(value.Value) : "");
            output.AppendLine();
        }
        string folder = Path.Combine(Paths.BepInExRootPath, "BigWalkDLSS");
        Directory.CreateDirectory(folder);
        string name = "performance-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture) + ".csv";
        File.WriteAllText(Path.Combine(folder, name), output.ToString(), new UTF8Encoding(false));
        Status = "Saved BepInEx/BigWalkDLSS/" + name + ". Capture the same view at the other time of day.";
        Plugin.Instance.Log.LogInfo(Status);
    }

    internal static void FocusChanged(bool focused)
    {
        if (focused || !Active) return;
        Status = "Capture cancelled because the game lost focus. Try again in the game window.";
        Shutdown();
    }

    private static string Number(double value) => double.IsFinite(value) ? value.ToString("0.###", CultureInfo.InvariantCulture) : "";
    private static void Fail(Exception error)
    {
        Status = "Performance capture failed: " + error.GetType().Name + ". See the BepInEx log.";
        Plugin.Instance.Log.LogWarning(Status + " " + error.Message);
        Shutdown();
    }
    internal static void Shutdown()
    {
        Active = false;
        ShadowPassProbe.Shutdown();
        ShadowReceiverProbe.Shutdown();
        ShadowStates.Clear();
        foreach (var counter in Counters)
            if (counter.Created) { try { counter.Recorder.Dispose(); } catch { } }
        Counters.Clear(); Samples.Clear();
    }
}
