using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
using PassData = UnityEngine.Rendering.Universal.PostProcessPassRenderGraph.DoFGaussianPassData;

namespace BigWalkDLSS;

[BepInPlugin("local.bigwalk.native-dlss", "Big Beautiful Walk", "0.8.0")]
public sealed class Plugin : BasePlugin
{
    internal static Plugin Instance = null!;
    internal static ConfigEntry<bool> Enabled = null!;
    internal static ConfigEntry<bool> ProbeOnly = null!;
    internal static ConfigEntry<float> RenderScale = null!;
    internal static ConfigEntry<int> QualityMode = null!;
    internal static ConfigEntry<int> Preset = null!;
    internal static ConfigEntry<bool> Bypass = null!;
    internal static ConfigEntry<bool> VerifyPixels = null!;
    internal static ConfigEntry<bool> RestorePostEffects = null!;
    internal static ConfigEntry<float> ShadowDistanceScale = null!;
    internal static ConfigEntry<bool> Diagnostics = null!;
    internal static ConfigEntry<bool> ForceBridgeNative = null!;
    internal static bool Compatible, NativeLoaded;
    // Rendering GPU vendor after correcting for OptiScaler's NVIDIA spoofing. AMD and Intel
    // GPUs use the bridge native plugin, which drives OptiScaler and contains no NVIDIA SDK code.
    internal static int GpuVendorId;
    internal static bool UsesBridge;
    internal static string SupportMessage = "Starting…";
    private static string _nativeUnavailable = "Upscaler unavailable — run Setup or check the log";
    internal static string RendererStatus => !Compatible ? SupportMessage : !NativeLoaded ? _nativeUnavailable :
        !Enabled.Value ? "Game renderer active" : UnityEngine.XR.XRSettings.enabled ? "VR session — reconstruction disabled" :
        _faulted || Native.Status() < 0 ? "Upscaling stopped — retry or select another quality mode" :
        ProbeOnly.Value ? "Diagnostic mode — reconstruction is off" : Bypass.Value ? "Comparison mode — basic scaling" :
        Native.Status() == 2 && Time.frameCount - _lastHookFrame < 120
            ? UpscalingPresentation.ActiveStatus(BridgeSettings.RunningBackend, QualityMode.Value) : "Waiting for a scene";
    internal static Camera? RenderingCamera;
    private static readonly Dictionary<int, CameraSnapshot> Cameras = new();
    private static readonly List<object> LiveCallbacks = new();
    private static UniversalRenderPipelineAsset? _asset;
    private static float _originalScale;
    private static int _originalMsaa;
    private static UpscalingFilterSelection _originalFilter;
    private static bool _wasEnabled;
    private static bool _loggedCamera;
    private static int _hookCount;
    private static int _lastHookFrame;
    private static bool _faulted;
    // Session-only: a failed stock-effects pass must not persist RestorePostEffects=false.
    private static bool _stockFaulted;
    private static IntPtr _renderEvent;
    private static int _historyEpoch;
    private static int _primaryCameraId;
    private static RenderTexture? _outputTexture;
    private static RTHandle? _outputHandle;
    private static bool _captureRequested;
    private static Harmony _harmony = null!;
    private static bool _patched;

    public override void Load()
    {
        Instance = this;
        Enabled = Config.Bind("General", "Enabled", true, "Enable the experimental monitor-only renderer integration. F8 toggles it.");
        ProbeOnly = Config.Bind("Diagnostics", "ProbeOnly", false, "Inspect textures without evaluating DLSS. Leave disabled for normal play.");
        RenderScale = Config.Bind("General", "RenderScale", 0.666667f, "Scene scale, updated by the selected quality mode.");
        QualityMode = Config.Bind("Upscaling", "QualityMode", 2, "NGX quality: Performance=0, Balanced=1, Quality=2, Ultra Performance=3, Ultra Quality=4, DLAA=5.");
        Preset = Config.Bind("Upscaling", "Preset", 12, "DLSS preset hint: Default=0, J=10, K=11, L=12, M=13, legacy E=5/F=6. Driver or bridge overrides may take precedence.");
        if (QualityMode.Value < 0 || QualityMode.Value > 5) QualityMode.Value = 2;
        if (!UpscalingOptions.ValidPreset(Preset.Value)) Preset.Value = 12;
        RenderScale.Value = UpscalingOptions.Scale(QualityMode.Value);
        Bypass = Config.Bind("Diagnostics", "Bypass", false, "F9 toggles DLSS output versus bilinear output at the same scene resolution.");
        VerifyPixels = Config.Bind("Diagnostics", "VerifyPixels", false, "Compare DLSS against bilinear using GPU readback. Diagnostic only; may hitch.");
        RestorePostEffects = Config.Bind("PostProcessing", "Enabled", true, "Restore the game's active stock post effects around DLSS. F7 toggles this for comparison.");
        ShadowDistanceScale = Config.Bind("Shadows", "DistanceScale", 1f, "Legacy profile migration only. New installations retain stock shadow settings.");
        Diagnostics = Config.Bind("Diagnostics", "Enabled", false, "Enable diagnostic shortcuts F7/F9, automatic screenshots and verbose shadow texture probes.");
        ForceBridgeNative = Config.Bind("Diagnostics", "ForceBridgeNative", false, "Testing only: use the AMD/Intel upscaler path through OptiScaler on an NVIDIA GPU. Requires the optional bridge.");
        bool allowUntested = Config.Bind("Compatibility", "AllowUntestedVersion", false, "Opt into an untested game/Unity update. Graphics API and XR restrictions still apply.").Value;
        int reportedVendor = SystemInfo.graphicsDeviceVendorID;
        GpuVendorId = GpuVendor.Resolve(reportedVendor, GpuVendor.InstalledVendors());
        if (ForceBridgeNative.Value && GpuVendor.IsNvidia(GpuVendorId)) { GpuVendorId = GpuVendor.Amd; BridgeSettings.SimulatedVendor = true; }
        UsesBridge = !GpuVendor.IsNvidia(GpuVendorId);
        BridgeSettings.GpuVendorId = GpuVendorId;
        SupportMessage = Compatibility.Check(Application.unityVersion, Application.version,
            SystemInfo.graphicsDeviceType == GraphicsDeviceType.Direct3D11, allowUntested);
        Compatible = SupportMessage.Length == 0;
        AdvancedGraphics.Initialize();
        BridgeSettings.Initialize();
        Directory.CreateDirectory(Path.Combine(Paths.BepInExRootPath, "BigWalkDLSS"));
        _harmony = new Harmony("local.bigwalk.native-dlss");
        if (Compatible)
        {
            try
            {
                string directory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
                if (UsesBridge && !BridgeSettings.Recognized)
                {
                    _nativeUnavailable = File.Exists(Path.Combine(Paths.GameRootPath, "version.dll"))
                        ? "Big Walk is running on the " + GpuVendor.Name(GpuVendorId) + " GPU, which needs this mod's OptiScaler bridge, but a different version.dll is installed. Remove it, then run Setup."
                        : "Big Walk is running on the " + GpuVendor.Name(GpuVendorId) + " GPU, which needs the optional upscaler bridge. " +
                          "Run Setup and tick the bridge, or set Windows to run Big Walk on an NVIDIA GPU if this PC has one.";
                    throw new InvalidOperationException("The OptiScaler bridge required for " + GpuVendor.Name(GpuVendorId) + " GPUs is not installed.");
                }
                IntPtr native = NativeLibrary.Load(Path.Combine(directory, UsesBridge ? "BigWalkBridgeNative.dll" : "BigWalkDLSSNative.dll"));
                NativeLibrary.SetDllImportResolver(typeof(Plugin).Assembly, (name, _, _) => name == Native.Dll ? native : IntPtr.Zero);
                Native.Initialize(Path.Combine(Paths.BepInExRootPath, "BigWalkDLSS"), Paths.GameRootPath);
                _renderEvent = Native.GetRenderEvent();
                if (Marshal.SizeOf<Frame>() != Native.FrameSize()) throw new InvalidOperationException("Managed/native frame ABI mismatch.");
                NativeLoaded = true;
                VegetationShadows.Initialize();
                if (Enabled.Value && !UnityEngine.XR.XRSettings.enabled) InstallPatch();
            }
            catch (Exception error) { _faulted = true; Log.LogError("Could not start the upscaler. Use Home for status; check installation and Visual C++ runtime. " + error); }
        }
        else Log.LogWarning(SupportMessage);
        AddComponent<Controller>();
        Log.LogInfo($"Big Beautiful Walk 0.8.0: API={SystemInfo.graphicsDeviceType}, Unity={Application.unityVersion}, game={Application.version}, native loaded={NativeLoaded}; XR is excluded.");
        Log.LogInfo($"GPU vendor reported=0x{reportedVendor:x4} resolved={GpuVendor.Name(GpuVendorId)}; native path={(UsesBridge ? "bridge (OptiScaler)" : "NVIDIA NGX")}{(ForceBridgeNative.Value ? " (forced for testing)" : "")}.");
    }

    private static void InstallPatch()
    {
        _harmony.Patch(AccessTools.Method(typeof(PostProcessPassRenderGraph), "RenderPostProcessingRenderGraph"),
            prefix: new HarmonyMethod(typeof(Plugin), nameof(RenderPostProcessing)));
        _patched = true;
    }

    private static bool RenderPostProcessing(PostProcessPassRenderGraph __instance, RenderGraph renderGraph,
        ContextContainer frameData, ref TextureHandle activeCameraColorTexture, ref TextureHandle lutTexture,
        ref TextureHandle overlayUITexture, ref TextureHandle postProcessingTarget, bool hasFinalPass,
        bool resolveToDebugScreen, bool enableColorEndingIfNeeded)
    {
        if (!Compatible || !NativeLoaded || !Enabled.Value || _faulted || UnityEngine.XR.XRSettings.enabled) return true;
        try
        {
            return RenderReplacement(__instance, renderGraph, frameData, activeCameraColorTexture, ref lutTexture,
                ref overlayUITexture, ref postProcessingTarget, hasFinalPass, enableColorEndingIfNeeded);
        }
        catch (Exception error)
        {
            // Latch so Tick restores the game's renderer and removes the patch instead of failing every frame.
            _faulted = true;
            Instance.Log.LogError($"DLSS post-processing hook failed; restoring the game's renderer: {error}");
            return true;
        }
    }

    private static bool RenderReplacement(PostProcessPassRenderGraph __instance, RenderGraph renderGraph,
        ContextContainer frameData, TextureHandle activeCameraColorTexture, ref TextureHandle lutTexture,
        ref TextureHandle overlayUITexture, ref TextureHandle postProcessingTarget, bool hasFinalPass,
        bool enableColorEndingIfNeeded)
    {
        var resources = frameData.Get<UniversalResourceData>();
        var camera = frameData.Get<UniversalCameraData>();
        TextureHandle output = activeCameraColorTexture;
        bool stock = false;
        if (RestorePostEffects.Value && !_stockFaulted)
        {
            try
            {
                stock = StockPostProcessing.Prepare(__instance, frameData, camera,
                    hasFinalPass, enableColorEndingIfNeeded, lutTexture.IsValid());
                if (stock) StockPostProcessing.BeforeDlss(__instance, renderGraph, resources, camera, ref output);
            }
            catch (Exception error)
            {
                StockFailed(error);
                stock = false;
                output = activeCameraColorTexture;
            }
        }
        TextureHandle input = output;
        RenderStp(__instance, renderGraph, resources, camera, ref input, ref output);
        if (stock)
        {
            try
            {
                StockPostProcessing.AfterDlss(__instance, renderGraph, frameData, resources, camera,
                    ref output, ref postProcessingTarget, ref lutTexture, ref overlayUITexture);
                return false;
            }
            catch (Exception error) { StockFailed(error); }
        }
        // This integration owns the post-processing slot. Calling the original STP method
        // through this IL2CPP build's by-ref trampoline crashed during the first probe.
        var builder = renderGraph.AddUnsafePass<PassData>("Big Walk DLSS final output", out var data);
        try
        {
            data.sourceTexture = output;
            data.destination = postProcessingTarget;
            var common = builder.Cast<IBaseRenderGraphBuilder>();
            common.UseTexture(ref output, AccessFlags.Read);
            common.UseTexture(ref postProcessingTarget, AccessFlags.Write);
            common.AllowPassCulling(false);
            common.AllowGlobalStateModification(true);
            BaseRenderFunc<PassData, UnsafeGraphContext> callback = new Action<PassData, UnsafeGraphContext>((pass, context) =>
            {
                CommandBuffer cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
                RenderTargetIdentifier from = pass.sourceTexture;
                RenderTargetIdentifier to = pass.destination;
                cmd.Blit(from, to);
            });
            KeepCallback(callback);
            builder.SetRenderFunc(callback);
        }
        finally { builder.Cast<Il2CppSystem.IDisposable>().Dispose(); }
        return false;
    }

    private static void StockFailed(Exception error)
    {
        _stockFaulted = true;
        Instance.Log.LogError($"Stock post-processing setup failed; continuing without stock effects until Retry upscaler, F7 or restart: {error}");
    }

    internal static void Tick()
    {
        if (Input.GetKeyDown(KeyCode.F8)) { Enabled.Value = !Enabled.Value; _historyEpoch++; }
        if (Diagnostics.Value && Input.GetKeyDown(KeyCode.F9)) { Bypass.Value = !Bypass.Value; _historyEpoch++; }
        if (Diagnostics.Value && Input.GetKeyDown(KeyCode.F7))
        {
            RestorePostEffects.Value = !RestorePostEffects.Value;
            _stockFaulted = false;
            _historyEpoch++;
            Instance.Log.LogInfo($"Stock post-processing enabled={RestorePostEffects.Value}.");
        }
        if (!Compatible || !NativeLoaded || !Enabled.Value || _faulted || UnityEngine.XR.XRSettings.enabled || SystemInfo.graphicsDeviceType != GraphicsDeviceType.Direct3D11)
        {
            Restore();
            if (_patched) { _harmony.UnpatchSelf(); _patched = false; }
            return;
        }
        if (!_patched) InstallPatch();
        var asset = GraphicsSettings.currentRenderPipeline?.TryCast<UniversalRenderPipelineAsset>();
        if (asset == null) return;
        if (_asset == null)
        {
            _asset = asset;
            _originalScale = asset.renderScale;
            _originalMsaa = asset.msaaSampleCount;
            _originalFilter = asset.upscalingFilter;
            Instance.Log.LogInfo($"Shadow range original={asset.shadowDistance:F1}, cascades={asset.shadowCascadeCount}, splits={asset.cascade4Split}, atlas={asset.mainLightShadowmapResolution}.");
        }
        if (_asset.Pointer != asset.Pointer)
        {
            Restore();
            return;
        }
        // STP requests URP's existing temporal resources and jitter. RenderSTP is replaced below.
        asset.msaaSampleCount = 1;
        asset.upscalingFilter = UpscalingFilterSelection.STP;
        asset.renderScale = UpscalingOptions.Scale(QualityMode.Value);
        Camera? primary = Camera.main;
        foreach (var camera in Camera.allCameras)
        {
            if (camera == null || camera.cameraType != CameraType.Game || camera.stereoEnabled || camera.targetTexture != null) continue;
            if (primary != null && camera.Pointer != primary.Pointer) continue;
            var additional = camera.GetComponent<UniversalAdditionalCameraData>();
            if (additional == null || additional.renderType != CameraRenderType.Base || additional.cameraStack.Count != 0) continue;
            int id = camera.GetInstanceID();
            RenderingCamera = camera;
            _primaryCameraId = id;
            if (!Cameras.ContainsKey(id))
            {
                Cameras[id] = new CameraSnapshot(camera, additional);
                Instance.Log.LogInfo($"Camera selected id={id} name={camera.name}, original PP={additional.renderPostProcessing}, AA={additional.antialiasing}, dynamic={camera.allowDynamicResolution}.");
            }
            camera.allowDynamicResolution = false;
            additional.requiresDepthTexture = true;
            additional.renderPostProcessing = true;
            additional.antialiasing = AntialiasingMode.TemporalAntiAliasing;
            if (!_loggedCamera)
            {
                Instance.Log.LogInfo($"Temporal input request: camera={camera.name}, source scale={asset.renderScale:F4}, AA={additional.antialiasing}, filter={asset.upscalingFilter}, MSAA={asset.msaaSampleCount}.");
                _loggedCamera = true;
            }
            break;
        }
        _wasEnabled = true;
        if (Native.Status() < 0 && !ProbeOnly.Value)
        {
            _faulted = true;
            Instance.Log.LogError("Native DLSS failed; restoring the game's renderer settings. See BigWalkDLSS/native.log.");
            Restore();
        }
        if (Time.frameCount % 600 == 0)
            Instance.Log.LogInfo($"DLSS heartbeat: hook calls={_hookCount}, last hook frame={_lastHookFrame}, current frame={Time.frameCount}, native status={Native.Status()}, primary={_primaryCameraId}, scale={asset.renderScale:F4}, bypass={Bypass.Value}.");
        if (Diagnostics.Value && !_captureRequested && _hookCount >= 2 && Native.Status() > 0)
        {
            _captureRequested = true;
            ScreenCapture.CaptureScreenshot(Path.Combine(Paths.BepInExRootPath, "BigWalkDLSS", ProbeOnly.Value ? "probe.png" : "dlss.png"));
        }
    }

    private static bool RenderStp(PostProcessPassRenderGraph __instance, RenderGraph renderGraph,
        UniversalResourceData resourceData, UniversalCameraData cameraData, ref TextureHandle source,
        ref TextureHandle destination)
    {
        // Calling the original through this build's IL2CPP by-ref trampoline crashes.
        // A defined source fallback also covers auxiliary cameras while the asset requests STP.
        destination = source;
        if (!Enabled.Value || _faulted || cameraData.camera.GetInstanceID() != _primaryCameraId || cameraData.xr.enabled || cameraData.camera.stereoEnabled ||
            SystemInfo.graphicsDeviceType != GraphicsDeviceType.Direct3D11) return false;
        try
        {
            TextureHandle color = source;
            TextureHandle depth = resourceData.cameraDepthTexture;
            TextureHandle motion = resourceData.motionVectorColor;
            if (!color.IsValid() || !depth.IsValid() || !motion.IsValid())
                throw new InvalidOperationException("URP did not provide valid colour, depth and motion handles.");
            TextureDesc input = color.GetDescriptor(renderGraph);
            TextureDesc motionDesc = motion.GetDescriptor(renderGraph);
            if (input.dimension != TextureDimension.Tex2D || input.slices != 1 || input.msaaSamples != MSAASamples.None)
            throw new InvalidOperationException("DLSS requires a single-sample, single-view 2D input.");
            bool probe = ProbeOnly.Value;
            TextureHandle output;
            // This IL2CPP build's by-ref TextureDesc wrapper passes a boxed header to native code.
            // Import a Unity-owned RTHandle instead of calling CreateTexture(ref TextureDesc).
            if (_outputTexture == null || _outputTexture.width != cameraData.pixelWidth || _outputTexture.height != cameraData.pixelHeight)
            {
                _outputHandle?.Release();
                if (_outputTexture != null) { _outputTexture.Release(); UnityEngine.Object.Destroy(_outputTexture); }
                _outputTexture = new RenderTexture(cameraData.pixelWidth, cameraData.pixelHeight, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear)
                {
                    name = "BigWalkDLSSOutput",
                    enableRandomWrite = true,
                    useMipMap = false,
                    autoGenerateMips = false,
                    filterMode = FilterMode.Bilinear
                };
                if (!_outputTexture.Create()) throw new InvalidOperationException("Could not create the DLSS output texture.");
                _outputHandle = RTHandles.Alloc(_outputTexture, false);
            }
            output = renderGraph.ImportTexture(_outputHandle);
            int cameraId = cameraData.camera.GetInstanceID();
            int frameIndex = Time.frameCount;
            Matrix4x4 jitter = cameraData.m_JitterMatrix;
            var packet = new Frame
            {
                Abi = 3,
                CameraId = cameraId,
                FrameIndex = frameIndex,
                Mode = probe ? 0 : (Bypass.Value ? 2 : 1),
                RenderWidth = input.width,
                RenderHeight = input.height,
                OutputWidth = cameraData.pixelWidth,
                OutputHeight = cameraData.pixelHeight,
                JitterX = jitter.m03 * input.width * 0.5f,
                JitterY = jitter.m13 * input.height * 0.5f,
                MotionScaleX = -motionDesc.width,
                MotionScaleY = -motionDesc.height,
                CameraNear = cameraData.camera.nearClipPlane,
                CameraFar = cameraData.camera.farClipPlane,
                CameraFov = cameraData.camera.fieldOfView * Mathf.Deg2Rad,
                DeltaMilliseconds = Time.unscaledDeltaTime * 1000f,
                HistoryEpoch = _historyEpoch,
                Verify = VerifyPixels.Value ? 1 : 0,
                ReversedDepth = SystemInfo.usesReversedZBuffer ? 1 : 0,
                Quality = Mathf.Clamp(QualityMode.Value, 0, 5),
                Preset = UpscalingOptions.ValidPreset(Preset.Value) ? Preset.Value : 12
            };
            var builder = renderGraph.AddUnsafePass<PassData>("Big Walk DLSS", out var passData);
            try
            {
                var baseBuilder = builder.Cast<IBaseRenderGraphBuilder>();
                passData.sourceTexture = color;
                passData.depthTexture = depth;
                passData.halfCoCTexture = motion;
                passData.destination = output;
                baseBuilder.UseTexture(ref color, AccessFlags.Read);
                baseBuilder.UseTexture(ref depth, AccessFlags.Read);
                baseBuilder.UseTexture(ref motion, AccessFlags.Read);
                baseBuilder.UseTexture(ref output, AccessFlags.Write);
                baseBuilder.AllowPassCulling(false);
                baseBuilder.AllowGlobalStateModification(true);
                // Use an already-compiled URP pass-data type to avoid introducing a new AOT generic.
                BaseRenderFunc<PassData, UnsafeGraphContext> callback = new Action<PassData, UnsafeGraphContext>((data, context) =>
                {
                    try
                    {
                        Frame frame = packet;
                        RenderTexture colorTexture = data.sourceTexture;
                        RenderTexture depthTexture = data.depthTexture;
                        RenderTexture motionTexture = data.halfCoCTexture;
                        frame.Color = colorTexture.GetNativeTexturePtr();
                        frame.Depth = depthTexture.GetNativeTexturePtr();
                        frame.Motion = motionTexture.GetNativeTexturePtr();
                        RenderTexture outputTexture = data.destination;
                        frame.Output = outputTexture.GetNativeTexturePtr();
                        // A valid bilinear image remains in probe/bypass mode or if NGX rejects the frame.
                        CommandBufferHelpers.GetNativeCommandBuffer(context.cmd).Blit(colorTexture, outputTexture);
                        IntPtr queued = Native.Queue(ref frame);
                        if (queued != IntPtr.Zero) context.cmd.IssuePluginEventAndData(_renderEvent, 1, queued);
                    }
                    catch (Exception error)
                    {
                        _faulted = true;
                        Instance.Log.LogError($"DLSS graph execution failed: {error}");
                    }
                });
                KeepCallback(callback);
                builder.SetRenderFunc(callback);
            }
            finally { builder.Cast<Il2CppSystem.IDisposable>().Dispose(); }
            _hookCount++;
            _lastHookFrame = frameIndex;
            if (_hookCount == 1)
                Instance.Log.LogInfo($"STP hook reached: scene={input.width}x{input.height}, output={cameraData.pixelWidth}x{cameraData.pixelHeight}, motion={motionDesc.width}x{motionDesc.height}, probe={probe}.");
            destination = output;
            __instance.UpdateCameraResolution(renderGraph, cameraData, new Vector2Int(cameraData.pixelWidth, cameraData.pixelHeight));
            return false;
        }
        catch (Exception error)
        {
            _faulted = true;
            Instance.Log.LogError($"DLSS insertion failed; retaining source image: {error}");
            return false;
        }
    }

    internal static void Restore()
    {
        if (!_wasEnabled) return;
        if (_asset != null)
        {
            _asset.renderScale = _originalScale;
            _asset.upscalingFilter = _originalFilter;
            _asset.msaaSampleCount = _originalMsaa;
            // shadowDistance is owned by Advanced Graphics' override; restoring a snapshot here could revive a disabled override.
        }
        foreach (var snapshot in Cameras.Values) snapshot.Restore();
        Cameras.Clear();
        _asset = null;
        _wasEnabled = false;
        Instance.Log.LogInfo("Original runtime graphics settings restored.");
    }

    internal static void RetryUpscaler()
    {
        _stockFaulted = false;
        ResetHistory();
    }

    internal static void ResetHistory()
    {
        _historyEpoch++;
        if (NativeLoaded && (_faulted || Native.Status() < 0))
        {
            Native.RequestRetry();
            _faulted = false;
        }
    }

    internal static void StopAfterError(Exception error)
    {
        if (!_faulted) Instance.Log.LogError(error);
        _faulted = true;
        Restore();
        if (_patched) { _harmony.UnpatchSelf(); _patched = false; }
    }

    private static void KeepCallback(object callback)
    {
        LiveCallbacks.Add(callback);
        if (LiveCallbacks.Count > 64) LiveCallbacks.RemoveRange(0, 32);
    }

    private sealed class CameraSnapshot
    {
        private readonly Camera _camera;
        private readonly UniversalAdditionalCameraData _additional;
        private readonly bool _dynamic, _post, _depth;
        private readonly AntialiasingMode _aa;
        public CameraSnapshot(Camera camera, UniversalAdditionalCameraData additional)
        {
            _camera = camera; _additional = additional;
            _dynamic = camera.allowDynamicResolution; _post = additional.renderPostProcessing;
            _depth = additional.requiresDepthTexture; _aa = additional.antialiasing;
        }
        public void Restore()
        {
            if (_camera == null || _additional == null) return;
            _camera.allowDynamicResolution = _dynamic; _additional.renderPostProcessing = _post;
            _additional.requiresDepthTexture = _depth; _additional.antialiasing = _aa;
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    internal struct Frame
    {
        public int Abi, CameraId, FrameIndex, Mode;
        public IntPtr Color, Depth, Motion, Output;
        public int RenderWidth, RenderHeight, OutputWidth, OutputHeight;
        public float JitterX, JitterY, MotionScaleX, MotionScaleY;
        public int HistoryEpoch, Verify, ReversedDepth;
        public float CameraNear, CameraFar, CameraFov, DeltaMilliseconds;
        public int Quality, Preset;
    }

    internal static class Native
    {
        internal const string Dll = "BigWalkDLSSNative";
        [DllImport(Dll, EntryPoint = "BW_Initialize", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void Initialize(string dataDirectory, string runtimeDirectory);
        [DllImport(Dll, EntryPoint = "BW_GetRenderEvent", CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr GetRenderEvent();
        [DllImport(Dll, EntryPoint = "BW_FrameSize", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int FrameSize();
        [DllImport(Dll, EntryPoint = "BW_Queue", CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr Queue(ref Frame frame);
        [DllImport(Dll, EntryPoint = "BW_Status", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int Status();
        [DllImport(Dll, EntryPoint = "BW_RequestRetry", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void RequestRetry();
    }
}

public sealed class Controller : MonoBehaviour
{
    public Controller(IntPtr pointer) : base(pointer) { }
    public void Update()
    {
        try { AdvancedGraphics.Update(); Plugin.Tick(); }
        catch (Exception error) { Plugin.StopAfterError(error); }
    }
    public void LateUpdate() => AdvancedGraphics.LateUpdate();
    public void OnGUI() => AdvancedGraphics.Draw();
    public void OnApplicationFocus(bool focused) { if (!focused) AdvancedGraphics.Close(); }
    public void OnApplicationQuit() { AdvancedGraphics.Shutdown(); VegetationShadows.Shutdown(); Plugin.Restore(); }
}
