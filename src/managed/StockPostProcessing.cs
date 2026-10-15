// Adapted from Unity's URP post-processing orchestration.
// Copyright (c) 2020 Unity Technologies ApS.
// Licensed under the Unity Companion License; see docs/licenses/Unity-Companion-License.txt.
using System;
using System.Collections.Generic;
using System.Reflection;
using Il2CppInterop.Runtime;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace BigWalkDLSS;

internal static class StockPostProcessing
{
    private static readonly HashSet<int> LoggedCameras = new();
    private static readonly IntPtr BloomSetupMethod = (IntPtr)typeof(PostProcessPassRenderGraph)
        .GetField("NativeMethodInfoPtr_UberPostSetupBloomPass_Public_Void_RenderGraph_Material_byref_TextureDesc_0", BindingFlags.NonPublic | BindingFlags.Static)!
        .GetValue(null)!;

    internal static bool Prepare(PostProcessPassRenderGraph pass, ContextContainer frame, UniversalCameraData camera,
        bool hasFinalPass, bool encodeOutput, bool hasLut)
    {
        var stack = VolumeManager.instance.stack;
        pass.m_DepthOfField = stack.GetComponent<DepthOfField>();
        pass.m_MotionBlur = stack.GetComponent<MotionBlur>();
        pass.m_PaniniProjection = stack.GetComponent<PaniniProjection>();
        pass.m_Bloom = stack.GetComponent<Bloom>();
        pass.m_LensFlareScreenSpace = stack.GetComponent<ScreenSpaceLensFlare>();
        pass.m_LensDistortion = stack.GetComponent<LensDistortion>();
        pass.m_ChromaticAberration = stack.GetComponent<ChromaticAberration>();
        pass.m_Vignette = stack.GetComponent<Vignette>();
        pass.m_ColorLookup = stack.GetComponent<ColorLookup>();
        pass.m_ColorAdjustments = stack.GetComponent<ColorAdjustments>();
        pass.m_Tonemapping = stack.GetComponent<Tonemapping>();
        pass.m_FilmGrain = stack.GetComponent<FilmGrain>();
        var settings = frame.Get<UniversalPostProcessingData>();
        pass.m_HasFinalPass = hasFinalPass;
        pass.m_EnableColorEncodingIfNeeded = encodeOutput;
        pass.m_UseFastSRGBLinearConversion = settings.useFastSRGBLinearConversion;
        pass.m_SupportDataDrivenLensFlare = settings.supportDataDrivenLensFlare;
        pass.m_SupportScreenSpaceLensFlare = settings.supportScreenSpaceLensFlare;
        if (LoggedCameras.Add(camera.camera.GetInstanceID()))
        {
            Plugin.Instance.Log.LogInfo($"Stock effects camera={camera.camera.name}: bloom={pass.m_Bloom.IsActive()} intensity={pass.m_Bloom.intensity.value:F3}, tone={pass.m_Tonemapping.mode.value}, postExposure={pass.m_ColorAdjustments.postExposure.value:F3}, contrast={pass.m_ColorAdjustments.contrast.value:F3}, saturation={pass.m_ColorAdjustments.saturation.value:F3}, colorLookup={pass.m_ColorLookup.IsActive()}, vignette={pass.m_Vignette.IsActive()}, chromatic={pass.m_ChromaticAberration.IsActive()}, distortion={pass.m_LensDistortion.IsActive()}, DoF={pass.m_DepthOfField.IsActive()}, motionBlur={pass.m_MotionBlur.IsActive()}, panini={pass.m_PaniniProjection.IsActive()}, grain={pass.m_FilmGrain.IsActive()}, dither={camera.isDitheringEnabled}, screenFlare={pass.m_LensFlareScreenSpace.IsActive()}, LUT={hasLut}, uber={(pass.m_Materials.uber != null)}.");
        }
        return !camera.xr.enabled && hasLut && pass.m_Materials.uber != null;
    }

    internal static void BeforeDlss(PostProcessPassRenderGraph pass, RenderGraph graph,
        UniversalResourceData resources, UniversalCameraData camera, ref TextureHandle color)
    {
        if (camera.isStopNaNEnabled && pass.m_Materials.stopNaN != null)
        {
            pass.RenderStopNaN(graph, ref color, out var clean);
            color = clean;
        }
        var material = pass.m_DepthOfField.mode.value == DepthOfFieldMode.Gaussian
            ? pass.m_Materials.gaussianDepthOfField : pass.m_Materials.bokehDepthOfField;
        if (pass.m_DepthOfField.IsActive() && !camera.isSceneViewCamera && material != null)
        {
            pass.RenderDoF(graph, resources, camera, ref color, out var focused);
            color = focused;
        }
    }

    internal static void AfterDlss(PostProcessPassRenderGraph pass, RenderGraph graph, ContextContainer frame,
        UniversalResourceData resources, UniversalCameraData camera, ref TextureHandle color,
        ref TextureHandle destination, ref TextureHandle lut, ref TextureHandle overlay)
    {
        if (pass.m_MotionBlur.IsActive() && !camera.isSceneViewCamera)
        {
            pass.RenderMotionBlur(graph, resources, camera, ref color, out var blurred);
            color = blurred;
        }
        if (pass.m_PaniniProjection.IsActive() && !camera.isSceneViewCamera)
        {
            pass.RenderPaniniProjection(graph, camera.camera, ref color, out var projected);
            color = projected;
        }
        Material material = pass.m_Materials.uber;
        material.shaderKeywords = null;
        TextureDesc description = color.GetDescriptor(graph);
        TextureHandle bloom = TextureHandle.nullHandle;
        if (pass.m_Bloom.IsActive())
        {
            pass.RenderBloomTexture(graph, ref color, out bloom, camera.isAlphaOutputEnabled);
            SetupBloom(pass, graph, material, description);
        }
        pass.SetupLensDistortion(material, camera.isSceneViewCamera);
        pass.SetupChromaticAberration(material);
        pass.SetupVignette(material, camera.xr, description.width, description.height);
        pass.SetupGrain(camera, material);
        pass.SetupDithering(camera, material);
        CoreUtils.SetKeyword(material, ShaderKeywordStrings.LinearToSRGBConversion,
            pass.RequireSRGBConversionBlitToBackBuffer(camera.requireSrgbConversion));
        CoreUtils.SetKeyword(material, ShaderKeywordStrings.UseFastSRGBLinearConversion,
            pass.m_UseFastSRGBLinearConversion);
        bool hdr = PostProcessPassRenderGraph.RequireHDROutput(camera);
        if (hdr)
        {
            var operation = !pass.m_HasFinalPass && pass.m_EnableColorEncodingIfNeeded
                ? HDROutputUtils.Operation.ColorEncoding : HDROutputUtils.Operation.None;
            pass.SetupHDROutput(camera.hdrDisplayInformation, camera.hdrDisplayColorGamut,
                material, operation, camera.rendersOverlayUI);
            var rect = camera.pixelRect;
            RenderingUtils.SetupOffscreenUIViewportParams(material, ref rect, resources.isActiveTargetBackBuffer);
        }
        pass.RenderUberPost(graph, frame, camera, frame.Get<UniversalPostProcessingData>(), ref color,
            ref destination, ref lut, ref bloom, ref overlay, hdr, camera.isAlphaOutputEnabled);
    }

    private static unsafe void SetupBloom(PostProcessPassRenderGraph pass, RenderGraph graph,
        Material material, TextureDesc description)
    {
        // The generated by-ref TextureDesc wrapper passes the boxed object header. This
        // unmodified native method expects the value payload, so unbox that argument explicitly.
        IntPtr* arguments = stackalloc IntPtr[3];
        arguments[0] = graph.Pointer;
        arguments[1] = material.Pointer;
        arguments[2] = IL2CPP.il2cpp_object_unbox(description.Pointer);
        IntPtr error = IntPtr.Zero;
        IL2CPP.il2cpp_runtime_invoke(BloomSetupMethod, pass.Pointer, (void**)arguments, ref error);
        Il2CppException.RaiseExceptionIfNecessary(error);
    }
}
