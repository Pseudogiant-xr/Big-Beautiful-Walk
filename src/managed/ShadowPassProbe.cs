using System;
using System.Globalization;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using MainShadowPass = UnityEngine.Rendering.Universal.Internal.MainLightShadowCasterPass;

namespace BigWalkDLSS;

// Capture the pass producer's state, not stale RenderGraph globals or proof of GPU allocation.
internal static class ShadowPassProbe
{
    private static Harmony? _harmony;
    private static Action<string>? _record;
    private static Func<bool>? _collecting;
    private static readonly Dictionary<int, double> NextSample = new();
    private static bool _cameraLimitReported;

    internal static void Begin(Action<string> record, Func<bool> collecting)
    {
        _record = record; _collecting = collecting; NextSample.Clear(); _cameraLimitReported = false;
        try
        {
            _harmony = new Harmony("local.bigwalk.shadow-pass-probe");
            _harmony.Patch(AccessTools.Method(typeof(MainShadowPass), nameof(MainShadowPass.Setup),
                new[] { typeof(UniversalRenderingData), typeof(UniversalCameraData), typeof(UniversalLightData), typeof(UniversalShadowData) }),
                postfix: new HarmonyMethod(typeof(ShadowPassProbe), nameof(AfterSetup)));
        }
        catch (Exception error)
        {
            record("# shadow_pass_probe_unavailable=" + error.GetType().Name);
            Shutdown();
        }
    }

    internal static void AfterSetup(MainShadowPass __instance, bool __result, UniversalCameraData __1)
    {
        var record = _record;
        if (record == null || _collecting?.Invoke() != true || !__result || __instance.m_CreateEmptyShadowmap) return;
        try
        {
            var camera = __1.camera;
            if (camera == null || camera.cameraType != CameraType.Game || camera.stereoEnabled || __1.renderType != CameraRenderType.Base) return;
            int identity = camera.GetInstanceID();
            if (NextSample.TryGetValue(identity, out double next) && Time.realtimeSinceStartupAsDouble < next) return;
            if (!NextSample.ContainsKey(identity) && NextSample.Count >= 64)
            {
                if (!_cameraLimitReported) { record("# shadow_pass_probe_camera_limit_reached=64"); _cameraLimitReported = true; }
                return;
            }
            NextSample[identity] = Time.realtimeSinceStartupAsDouble + 1;
            int count = __instance.m_ShadowCasterCascadesCount;
            var spheres = __instance.m_CascadeSplitDistances;
            var slices = __instance.m_CascadeSlices;
            var text = new StringBuilder("# shadow_pass frame=").Append(Time.frameCount)
                .Append(" camera_id=").Append(identity).Append(" target_texture=").Append(camera.targetTexture != null).Append(" cascades=").Append(count)
                .Append(" target_request=").Append(__instance.m_RenderTargetWidth).Append('x').Append(__instance.m_RenderTargetHeight)
                .Append(" camera_near=").Append(Number(camera.nearClipPlane)).Append(" camera_far=").Append(Number(camera.farClipPlane))
                .Append(" camera_fov=").Append(Number(camera.fieldOfView)).Append(" effective_max_shadow_distance=").Append(Number(__1.maxShadowDistance));
            Vector3 position = camera.transform.position, forward = camera.transform.forward;
            text.Append(" position=").Append(Number(position.x)).Append('/').Append(Number(position.y)).Append('/').Append(Number(position.z))
                .Append(" forward=").Append(Number(forward.x)).Append('/').Append(Number(forward.y)).Append('/').Append(Number(forward.z));
            for (int i = 0; i < Math.Clamp(count, 0, 4) && i < spheres.Length && i < slices.Length; i++)
            {
                Vector4 sphere = spheres[i];
                text.Append(" cascade").Append(i).Append("_sphere=")
                    .Append(Number(sphere.x)).Append('/').Append(Number(sphere.y)).Append('/').Append(Number(sphere.z)).Append('/').Append(Number(sphere.w))
                    .Append(" cascade").Append(i).Append("_slice_resolution=").Append(slices[i].resolution);
            }
            record(text.ToString());
        }
        catch (Exception error)
        {
            record("# shadow_pass_probe_read_failed=" + error.GetType().Name);

        }
    }
    private static string Number(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);
    internal static void Shutdown()
    {
        _record = null; _collecting = null; NextSample.Clear();
        try { _harmony?.UnpatchSelf(); }
        catch (Exception error) { Plugin.Instance.Log.LogWarning("Shadow pass probe could not unpatch: " + error.GetType().Name); }
        finally { _harmony = null; }
    }
}
