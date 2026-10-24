using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using MainShadowPass = UnityEngine.Rendering.Universal.Internal.MainLightShadowCasterPass;

namespace BigWalkDLSS;

// Inspect actual execution-time receiver coverage before enabling scissoring.
// Crest/Enviro and material-level consumers have not yet been bounded: this
// prototype never changes GPU commands, shadow maps, matrices or caster flags.
internal static class ShadowReceiverProbe
{
    private static Harmony? _harmony;
    private static Func<bool>? _collecting;
    private static readonly object ReportsLock = new();
    private static readonly Queue<string> Reports = new();
    private static readonly Dictionary<int, double> NextReport = new();
    private static int _droppedReports;
    private static long _generation;

    internal sealed class Snapshot
    {
        internal string Text = "";
        internal long Generation;
    }

    internal static void Begin(Func<bool> collecting)
    {
        Shutdown();
        lock (ReportsLock) _collecting = collecting;
        try
        {
            _harmony = new Harmony("local.bigwalk.shadow-receiver-probe");
            // Class-byref PassData is supported by the installed backend.
            // Avoid detouring RenderShadowSlice: its struct-byref arguments
            // are mishandled by this installation's HarmonySupport version.
            _harmony.Patch(AccessTools.Method(typeof(MainShadowPass), nameof(MainShadowPass.RenderMainLightCascadeShadowmap)),
                prefix: new HarmonyMethod(typeof(ShadowReceiverProbe), nameof(BeforePass)),
                finalizer: new HarmonyMethod(typeof(ShadowReceiverProbe), nameof(AfterPass)));
        }
        catch (Exception error)
        {
            Shutdown();
            lock (ReportsLock) Reports.Enqueue("# shadow_receiver_probe_unavailable=" + error.GetType().Name);
        }
    }

    internal static void BeforePass(MainShadowPass __instance, RasterCommandBuffer __0,
        ref MainShadowPass.PassData __1, bool __2, out Snapshot? __state)
    {
        __state = null;
        long generation = Volatile.Read(ref _generation);
        try
        {
            if (_collecting?.Invoke() != true) return;
            var data = __1?.cameraData;
            var camera = data?.camera;
            if (camera == null || __0 == null || __instance.m_CreateEmptyShadowmap) return;
            int identity = camera.GetInstanceID();
            double now = Time.realtimeSinceStartupAsDouble;
            lock (ReportsLock)
            {
                if (_collecting == null || generation != _generation) return;
                if (NextReport.TryGetValue(identity, out double next) && now < next) return;
                if (!NextReport.ContainsKey(identity) && NextReport.Count >= 64) { _droppedReports++; return; }
                NextReport[identity] = now + 1;
            }
            var text = new StringBuilder("# shadow_receiver_bounds frame=").Append(Time.frameCount)
                .Append(" camera_id=").Append(identity).Append(" scissor_applied=False receiver_contract=unaudited")
                .Append(" render_graph=").Append(__2).Append(" render_type=").Append(data!.renderType)
                .Append(" target_texture=").Append(camera.targetTexture != null || data.targetTexture != null);
            __state = new Snapshot { Generation = generation };
            try
            {
                DescribeConsumers(data, text);
                if (!__2 || camera.cameraType != CameraType.Game || data.renderType != CameraRenderType.Base ||
                    camera.stereoEnabled || data.xrRendering || camera.orthographic)
                { text.Append(" bounds_reason=unsupported_camera"); return; }
                int atlasWidth = __instance.m_RenderTargetWidth, atlasHeight = __instance.m_RenderTargetHeight;
                text.Append(" atlas=").Append(atlasWidth).Append('x').Append(atlasHeight);
                if (__instance.m_ShadowCasterCascadesCount != 4 || atlasWidth != 16384 || atlasHeight != 16384)
                { text.Append(" bounds_reason=requires_four_8k_tiles_in_16k_atlas"); return; }
                var descriptor = data.cameraTargetDescriptor;
                int width = descriptor.width, height = descriptor.height;
                if (width < 64 || height < 64 || width > 32768 || height > 32768)
                { text.Append(" bounds_reason=invalid_render_size"); return; }
                Span<double> p = stackalloc double[16], v = stackalloc double[16], combined = stackalloc double[16];
                Copy(data.GetProjectionMatrix(), p); Copy(data.GetViewMatrix(), v); combined.Clear();
                // Four input pixels on every side. This is an explicit proposed
                // envelope, not proof that arbitrary spatial jitter is covered.
                for (int column = 0; column < 4; column++) { p[column] /= 1 + 8.0 / width; p[4 + column] /= 1 + 8.0 / height; }
                for (int row = 0; row < 4; row++)
                    for (int column = 0; column < 4; column++)
                        for (int k = 0; k < 4; k++) combined[row * 4 + column] += p[row * 4 + k] * v[k * 4 + column];
                AppendMatrix(text, "camera_clip_from_world", combined);
                var sun = RenderSettings.sun;
                if (sun != null)
                {
                    var direction = sun.transform.forward;
                    text.Append(" sun_forward=").Append(Number(direction.x)).Append('/').Append(Number(direction.y)).Append('/').Append(Number(direction.z));
                }
                var slices = __instance.m_CascadeSlices;
                if (slices == null || slices.Length != 4) { text.Append(" bounds_reason=invalid_slice_count"); return; }
                Span<ShadowScissor> initial = stackalloc ShadowScissor[4], guarded = stackalloc ShadowScissor[4];
                Span<double> atlas = stackalloc double[16], clip = stackalloc double[16];
                for (int index = 0; index < 4; index++)
                {
                    var slice = slices[index];
                    if (slice.resolution != 8192 || slice.offsetX != index % 2 * 8192 || slice.offsetY != index / 2 * 8192)
                    { text.Append(" bounds_reason=layout_fallback"); return; }
                    Copy(slice.shadowTransform, atlas);
                    AppendMatrix(text, "slice" + index + "_world_to_atlas", atlas);
                    bool reduced = ShadowClipProjection.TryBuild(combined, atlas, atlasWidth, atlasHeight,
                        slice.offsetX, slice.offsetY, slice.resolution, clip) &&
                        ShadowReceiverBounds.TryCompute(clip, slice.resolution, 16, out initial[index]);
                    if (!reduced) initial[index] = new ShadowScissor(0, 0, 8192, 8192);
                }
                ShadowClipProjection.IncludeNeighbourFilters(initial, 8192, 16, guarded);
                for (int index = 0; index < 4; index++)
                {
                    var bounds = guarded[index];
                    double fraction = (double)bounds.Width * bounds.Height / (8192.0 * 8192);
                    text.Append(" slice").Append(index).Append("_proposed=").Append(bounds.X).Append('/').Append(bounds.Y).Append('/').Append(bounds.Width).Append('/').Append(bounds.Height)
                        .Append(" slice").Append(index).Append("_kept=").Append(Number(fraction));
                }
                text.Append(" bounds_reason=proposed_view_cone_only");
            }
            catch (Exception error) { text.Append(" bounds_reason=read_failed_").Append(error.GetType().Name); }
            finally { __state.Text = text.ToString(); }
        }
        catch (Exception error) { Enqueue("# shadow_receiver_probe_read_failed=" + error.GetType().Name, generation); }
    }

    internal static Exception? AfterPass(Exception? __exception, Snapshot? __state)
    {
        if (__state != null) Enqueue(__state.Text + " pass_failed=" + (__exception != null), __state.Generation);
        return __exception; // Observe the original; never suppress or replace its failure.
    }

    private static void DescribeConsumers(UniversalCameraData data, StringBuilder text)
    {
        var renderer = data.renderer;
        if (renderer == null) { text.Append(" active_features=unavailable"); return; }
        text.Append(" stacking_capable=").Append(renderer.supportedRenderingFeatures?.cameraStacking).Append(" active_features=");
        var features = renderer.rendererFeatures;
        if (features == null) { text.Append("unavailable"); return; }
        int active = 0;
        foreach (var feature in features)
        {
            if (feature == null || !feature.isActive) continue;
            if (active++ >= 32) { text.Append(";feature_limit"); break; }
            if (active > 1) text.Append(';');
            text.Append(feature.GetIl2CppType().FullName);
        }
        if (active == 0) text.Append("none");
    }

    private static void Copy(Matrix4x4 matrix, Span<double> output)
    {
        output[0] = matrix.m00; output[1] = matrix.m01; output[2] = matrix.m02; output[3] = matrix.m03;
        output[4] = matrix.m10; output[5] = matrix.m11; output[6] = matrix.m12; output[7] = matrix.m13;
        output[8] = matrix.m20; output[9] = matrix.m21; output[10] = matrix.m22; output[11] = matrix.m23;
        output[12] = matrix.m30; output[13] = matrix.m31; output[14] = matrix.m32; output[15] = matrix.m33;
    }
    private static string Number(double value) => value.ToString("R", CultureInfo.InvariantCulture);
    private static void AppendMatrix(StringBuilder text, string name, ReadOnlySpan<double> matrix)
    {
        text.Append(' ').Append(name).Append('=');
        for (int i = 0; i < matrix.Length; i++) { if (i > 0) text.Append('/'); text.Append(Number(matrix[i])); }
    }
    private static void Enqueue(string text, long generation)
    {
        lock (ReportsLock)
        {
            if (_collecting == null || generation != _generation) return;
            if (Reports.Count >= 256) { _droppedReports++; return; }
            Reports.Enqueue(text);
        }
    }
    internal static void Drain(Action<string> record)
    {
        lock (ReportsLock)
        {
            while (Reports.Count > 0) record(Reports.Dequeue());
            if (_droppedReports > 0) { record("# shadow_receiver_probe_dropped=" + _droppedReports); _droppedReports = 0; }
        }
    }
    internal static void Shutdown()
    {
        lock (ReportsLock) { _collecting = null; _generation++; }
        try { _harmony?.UnpatchSelf(); }
        catch (Exception error) { Plugin.Instance.Log.LogWarning("Receiver probe cleanup failed: " + error.GetType().Name); }
        finally { _harmony = null; }
        lock (ReportsLock) { Reports.Clear(); NextReport.Clear(); _droppedReports = 0; }
    }
}
