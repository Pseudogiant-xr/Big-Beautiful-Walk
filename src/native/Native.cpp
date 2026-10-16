#include <windows.h>
#include <d3d11_1.h>
#include <wrl/client.h>
#include <nvsdk_ngx.h>
#include <nvsdk_ngx_helpers.h>
#include <nvsdk_ngx_helpers_d3d.h>
#include <sl_dlss_g.h>
#include <atomic>
#include <chrono>
#include <cmath>
#include <cstdarg>
#include <cstdio>
#include <cstring>
#include <memory>
#include <mutex>
#include <string>
#include <thread>

using Microsoft::WRL::ComPtr;

struct Frame {
    int abi, cameraId, frameIndex, mode;
    ID3D11Resource *color, *depth, *motion, *output;
    int renderWidth, renderHeight, outputWidth, outputHeight;
    float jitterX, jitterY, motionScaleX, motionScaleY;
    int historyEpoch, verify, reversedDepth;
    float cameraNear, cameraFar, cameraFov, deltaMilliseconds;
    int quality, preset;
};
static_assert(sizeof(Frame) == 120, "Frame ABI must match managed Pack=8");

namespace {
std::atomic<int> status{0};
std::atomic<bool> retryRequested{false};
std::mutex logMutex;
std::wstring dataDirectory, runtimeDirectory;

void Log(const char* format, ...) {
    std::lock_guard<std::mutex> guard(logMutex);
    FILE* file = nullptr;
    _wfopen_s(&file, (dataDirectory + L"\\native.log").c_str(), L"ab");
    if (!file) return;
    SYSTEMTIME time{};
    GetLocalTime(&time);
    fprintf(file, "%02u:%02u:%02u.%03u ", time.wHour, time.wMinute, time.wSecond, time.wMilliseconds);
    va_list args;
    va_start(args, format);
    vfprintf(file, format, args);
    va_end(args);
    fputc('\n', file);
    fclose(file);
}

bool Describe(ID3D11Resource* resource, D3D11_TEXTURE2D_DESC& desc) {
    if (!resource) return false;
    ComPtr<ID3D11Texture2D> texture;
    if (FAILED(resource->QueryInterface(IID_PPV_ARGS(&texture)))) return false;
    texture->GetDesc(&desc);
    return desc.ArraySize == 1 && desc.SampleDesc.Count == 1;
}

struct State {
    ComPtr<ID3D11Device> device;
    ComPtr<ID3D11DeviceContext> context;
    ComPtr<ID3D11DeviceContext1> context1;
    ComPtr<ID3DDeviceContextState> pluginState;
    NVSDK_NGX_Parameter* parameters = nullptr;
    NVSDK_NGX_Handle* feature = nullptr;
    int width = 0, height = 0, outWidth = 0, outHeight = 0, cameraId = 0, epoch = -1, lastFrame = -1;
    unsigned flags = 0;
    int quality = -1, preset = -1;
    unsigned long long evaluations = 0;
    bool ngxReady = false, initialized = false, verified = false;
};

// The engine owns the process lifetime. Explicit GPU-idle teardown is used for feature recreation.
// Avoid calling graphics/NGX APIs from a DLL destructor after Unity has destroyed its device.
State& Engine() { static State* state = new State(); return *state; }

void ReportFrameGeneration(unsigned long long evaluations) {
    HMODULE module = GetModuleHandleW(L"sl.dlss_g.dll");
    if (!module) return;
    using PluginFunction = void* (*)(const char*);
    auto getFunction = reinterpret_cast<PluginFunction>(GetProcAddress(module, "slGetPluginFunction"));
    if (!getFunction) return;
    auto getState = reinterpret_cast<PFun_slDLSSGGetState*>(getFunction("slDLSSGGetState"));
    if (!getState) return;
    sl::DLSSGState fg{};
    sl::ViewportHandle viewport{0};
    const auto result = getState(viewport, fg, nullptr);
    Log("FG presentation sample result=%u status=0x%x presentedSinceSample=%u SR_evaluations=%llu maxGeneratedPerRendered=%u",
        unsigned(result), unsigned(fg.status), fg.numFramesActuallyPresented, evaluations, fg.numFramesToGenerateMax);
}

bool WaitGpu(State& state) {
    D3D11_QUERY_DESC desc{D3D11_QUERY_EVENT, 0};
    ComPtr<ID3D11Query> query;
    if (FAILED(state.device->CreateQuery(&desc, &query))) return false;
    state.context->End(query.Get());
    state.context->Flush();
    const auto deadline = std::chrono::steady_clock::now() + std::chrono::seconds(3);
    for (;;) {
        BOOL complete = FALSE;
        const HRESULT result = state.context->GetData(query.Get(), &complete, sizeof(complete), 0);
        if (FAILED(result)) return false;
        if (result == S_OK && complete) return true;
        if (std::chrono::steady_clock::now() >= deadline) return false;
        std::this_thread::sleep_for(std::chrono::milliseconds(1));
    }
}

struct RestoreContext {
    State& state;
    ComPtr<ID3DDeviceContextState> previous;
    explicit RestoreContext(State& value) : state(value) {
        state.context1->SwapDeviceContextState(state.pluginState.Get(), &previous);
    }
    ~RestoreContext() { state.context1->SwapDeviceContextState(previous.Get(), nullptr); }
};

bool BindDevice(State& state, ID3D11Resource* color) {
    ComPtr<ID3D11Device> device;
    color->GetDevice(&device);
    if (!device) return false;
    if (state.device) return device.Get() == state.device.Get();
    // Commit nothing until every interface and the saved context state exist, so a retry
    // after a partial failure rebinds instead of evaluating without context restoration.
    ComPtr<ID3D11DeviceContext> context;
    device->GetImmediateContext(&context);
    ComPtr<ID3D11Device1> device1;
    ComPtr<ID3D11DeviceContext1> context1;
    if (!context || FAILED(device.As(&device1)) || FAILED(context.As(&context1))) return false;
    D3D_FEATURE_LEVEL level = device->GetFeatureLevel(), selected{};
    ComPtr<ID3DDeviceContextState> pluginState;
    HRESULT result = device1->CreateDeviceContextState(0, &level, 1, D3D11_SDK_VERSION,
        __uuidof(ID3D11Device), &selected, &pluginState);
    if (FAILED(result) || !pluginState) { Log("Context-state creation failed: 0x%08x", unsigned(result)); return false; }
    state.device = device;
    state.context = context;
    state.context1 = context1;
    state.pluginState = pluginState;
    return true;
}

bool InitializeNgx(State& state) {
    if (state.initialized) return true;
    // NGX is initialized once per device. A retry never calls Init again; it fetches capability
    // parameters only if that failed before, and otherwise re-reads the cached availability.
    if (!state.ngxReady) {
        const wchar_t* paths[] = {runtimeDirectory.c_str()};
        NVSDK_NGX_FeatureCommonInfo info{};
        info.PathListInfo.Path = paths;
        info.PathListInfo.Length = 1;
        const auto result = NVSDK_NGX_D3D11_Init_with_ProjectID("ab31a96d-e80e-46d7-b5c1-78f9636183ec",
            NVSDK_NGX_ENGINE_TYPE_UNITY, "6000.3.17f1", dataDirectory.c_str(), state.device.Get(), &info);
        Log("NGX D3D11 project initialization result=0x%08x", unsigned(result));
        if (NVSDK_NGX_FAILED(result)) return false;
        state.ngxReady = true;
    }
    if (!state.parameters) {
        const auto capabilities = NVSDK_NGX_D3D11_GetCapabilityParameters(&state.parameters);
        if (NVSDK_NGX_FAILED(capabilities)) {
            state.parameters = nullptr;
            Log("DLSS capability result=0x%08x", unsigned(capabilities));
            return false;
        }
    }
    int available = 0;
    state.parameters->Get(NVSDK_NGX_Parameter_SuperSampling_Available, &available);
    Log("DLSS capability available=%d", available);
    state.initialized = available != 0;
    return state.initialized;
}

bool EnsureFeature(State& state, const Frame& frame, const D3D11_TEXTURE2D_DESC& color,
                   const D3D11_TEXTURE2D_DESC& motion) {
    unsigned flags = NVSDK_NGX_DLSS_Feature_Flags_AutoExposure;
    if (frame.reversedDepth) flags |= NVSDK_NGX_DLSS_Feature_Flags_DepthInverted;
    if (motion.Width <= unsigned(frame.renderWidth) && motion.Height <= unsigned(frame.renderHeight))
        flags |= NVSDK_NGX_DLSS_Feature_Flags_MVLowRes;
    if (color.Format == DXGI_FORMAT_R16G16B16A16_FLOAT || color.Format == DXGI_FORMAT_R11G11B10_FLOAT ||
        color.Format == DXGI_FORMAT_R32G32B32A32_FLOAT) flags |= NVSDK_NGX_DLSS_Feature_Flags_IsHDR;
    if (state.feature && state.width == frame.renderWidth && state.height == frame.renderHeight &&
        state.outWidth == frame.outputWidth && state.outHeight == frame.outputHeight && state.flags == flags &&
        state.quality == frame.quality && state.preset == frame.preset) return true;
    if (state.feature) {
        if (!WaitGpu(state)) { Log("Feature resize abandoned: GPU completion not confirmed."); return false; }
        NVSDK_NGX_D3D11_ReleaseFeature(state.feature);
        state.feature = nullptr;
    }
    NVSDK_NGX_DLSS_Create_Params create{};
    create.Feature.InWidth = unsigned(frame.renderWidth);
    create.Feature.InHeight = unsigned(frame.renderHeight);
    create.Feature.InTargetWidth = unsigned(frame.outputWidth);
    create.Feature.InTargetHeight = unsigned(frame.outputHeight);
    create.Feature.InPerfQualityValue = static_cast<NVSDK_NGX_PerfQuality_Value>(frame.quality);
    create.InFeatureCreateFlags = int(flags);
    for (const char* key : { NVSDK_NGX_Parameter_DLSS_Hint_Render_Preset_DLAA,
        NVSDK_NGX_Parameter_DLSS_Hint_Render_Preset_UltraQuality, NVSDK_NGX_Parameter_DLSS_Hint_Render_Preset_Quality,
        NVSDK_NGX_Parameter_DLSS_Hint_Render_Preset_Balanced, NVSDK_NGX_Parameter_DLSS_Hint_Render_Preset_Performance,
        NVSDK_NGX_Parameter_DLSS_Hint_Render_Preset_UltraPerformance })
        state.parameters->Set(key, unsigned(frame.preset));
    Log("DLSS quality mode requested=%d, render preset hint=%d", frame.quality, frame.preset);
    const auto result = NGX_D3D11_CREATE_DLSS_EXT(state.context.Get(), &state.feature, state.parameters, &create);
    Log("DLSS CreateFeature result=0x%08x render=%dx%d output=%dx%d flags=0x%x", unsigned(result),
        frame.renderWidth, frame.renderHeight, frame.outputWidth, frame.outputHeight, flags);
    if (NVSDK_NGX_FAILED(result) || !state.feature) return false;
    state.width = frame.renderWidth; state.height = frame.renderHeight;
    state.outWidth = frame.outputWidth; state.outHeight = frame.outputHeight; state.flags = flags;
    state.quality = frame.quality; state.preset = frame.preset;
    state.lastFrame = -1; state.verified = false;
    return true;
}

ComPtr<ID3D11Texture2D> Capture(State& state, ID3D11Resource* resource) {
    D3D11_TEXTURE2D_DESC desc{};
    if (!Describe(resource, desc)) return {};
    desc.BindFlags = 0; desc.MiscFlags = 0; desc.Usage = D3D11_USAGE_STAGING; desc.CPUAccessFlags = D3D11_CPU_ACCESS_READ;
    ComPtr<ID3D11Texture2D> texture;
    if (SUCCEEDED(state.device->CreateTexture2D(&desc, nullptr, &texture)))
        state.context->CopyResource(texture.Get(), resource);
    return texture;
}

float HalfFloat(unsigned short bits) {
    const unsigned sign = unsigned(bits & 0x8000) << 16;
    unsigned exponent = (bits >> 10) & 31, mantissa = bits & 1023, value;
    if (!exponent) {
        if (!mantissa) value = sign;
        else {
            int shift = 0;
            while (!(mantissa & 1024)) { mantissa <<= 1; ++shift; }
            value = sign | (unsigned(113 - shift) << 23) | ((mantissa & 1023) << 13);
        }
    } else if (exponent == 31) value = sign | 0x7f800000 | (mantissa << 13);
    else value = sign | ((exponent + 112) << 23) | (mantissa << 13);
    float output;
    memcpy(&output, &value, sizeof(output));
    return output;
}

void Verify(State& state, ID3D11Texture2D* before, ID3D11Texture2D* after) {
    D3D11_MAPPED_SUBRESOURCE a{}, b{};
    if (FAILED(state.context->Map(before, 0, D3D11_MAP_READ, 0, &a))) return;
    if (FAILED(state.context->Map(after, 0, D3D11_MAP_READ, 0, &b))) { state.context->Unmap(before, 0); return; }
    double absolute = 0; size_t count = 0, changed = 0;
    for (int y = 0; y < state.outHeight; y += 8) {
        const auto* rowA = reinterpret_cast<const unsigned short*>(static_cast<const char*>(a.pData) + size_t(y) * a.RowPitch);
        const auto* rowB = reinterpret_cast<const unsigned short*>(static_cast<const char*>(b.pData) + size_t(y) * b.RowPitch);
        for (int x = 0; x < state.outWidth; x += 8) for (int channel = 0; channel < 3; ++channel) {
            const float delta = std::abs(HalfFloat(rowA[x * 4 + channel]) - HalfFloat(rowB[x * 4 + channel]));
            if (!std::isfinite(delta)) continue;
            absolute += delta; ++count; if (delta > 0.0001f) ++changed;
        }
    }
    state.context->Unmap(before, 0); state.context->Unmap(after, 0);
    Log("A/B pixel evidence: DLSS versus bilinear, samples=%zu changed=%zu mean_absolute_linear_RGB_difference=%.8f",
        count, changed, count ? absolute / double(count) : 0);
    state.verified = true;
}

void Evaluate(Frame& frame) {
    auto& state = Engine();
    D3D11_TEXTURE2D_DESC color{}, depth{}, motion{}, output{};
    if (!Describe(frame.color, color) || !Describe(frame.depth, depth) || !Describe(frame.motion, motion)) {
        Log("Rejected missing or unsupported texture contract."); status = -1; return;
    }
    if (frame.frameIndex % 300 == 0 || status == 0) {
        Log("URP texture contract frame=%d mode=%d color=%ux%u fmt=%u depth=%ux%u fmt=%u motion=%ux%u fmt=%u jitter=(%.5f,%.5f) MVscale=(%.1f,%.1f)",
            frame.frameIndex, frame.mode, color.Width, color.Height, unsigned(color.Format), depth.Width, depth.Height,
            unsigned(depth.Format), motion.Width, motion.Height, unsigned(motion.Format), frame.jitterX, frame.jitterY,
            frame.motionScaleX, frame.motionScaleY);
    }
    if (frame.mode == 0) { status = 1; return; }
    if (status < 0) return;
    if (!Describe(frame.output, output) || color.Width != unsigned(frame.renderWidth) || color.Height != unsigned(frame.renderHeight) ||
        output.Width != unsigned(frame.outputWidth) || output.Height != unsigned(frame.outputHeight) ||
        (output.Format != DXGI_FORMAT_R16G16B16A16_FLOAT && output.Format != DXGI_FORMAT_R16G16B16A16_TYPELESS)) {
        Log("Rejected colour/output contract: colour=%ux%u expected=%dx%d output=%ux%u fmt=%u array=%u samples=%u expected=%dx%d.",
            color.Width, color.Height, frame.renderWidth, frame.renderHeight, output.Width, output.Height,
            unsigned(output.Format), output.ArraySize, output.SampleDesc.Count, frame.outputWidth, frame.outputHeight);
        status = -1; return;
    }
    if (!BindDevice(state, frame.color)) { Log("D3D11 device/context binding failed."); status = -1; return; }
    RestoreContext restore(state);
    if (!InitializeNgx(state) || !EnsureFeature(state, frame, color, motion)) { status = -1; return; }
    if (frame.mode == 2) { state.lastFrame = -1; return; }
    const bool verify = frame.verify && !state.verified && state.evaluations >= 30;
    auto before = verify ? Capture(state, frame.output) : ComPtr<ID3D11Texture2D>{};
    NVSDK_NGX_D3D11_DLSS_Eval_Params evaluate{};
    evaluate.Feature.pInColor = frame.color; evaluate.Feature.pInOutput = frame.output;
    evaluate.pInDepth = frame.depth; evaluate.pInMotionVectors = frame.motion;
    evaluate.InJitterOffsetX = frame.jitterX; evaluate.InJitterOffsetY = frame.jitterY;
    evaluate.InMVScaleX = frame.motionScaleX; evaluate.InMVScaleY = frame.motionScaleY;
    evaluate.InRenderSubrectDimensions = {unsigned(frame.renderWidth), unsigned(frame.renderHeight)};
    evaluate.InReset = state.lastFrame < 0 || state.cameraId != frame.cameraId || state.epoch != frame.historyEpoch || frame.frameIndex > state.lastFrame + 1;
    evaluate.InPreExposure = 1.0f;
    // OptiScaler's bridge consumes these optional camera values for frame generation.
    state.parameters->Set("FSR.cameraNear", frame.reversedDepth ? frame.cameraFar : frame.cameraNear);
    state.parameters->Set("FSR.cameraFar", frame.reversedDepth ? frame.cameraNear : frame.cameraFar);
    state.parameters->Set("FSR.cameraFovAngleVertical", frame.cameraFov);
    state.parameters->Set("FSR.viewSpaceToMetersFactor", 1.0f);
    state.parameters->Set("FSR.frameTimeDelta", frame.deltaMilliseconds);
    const auto result = NGX_D3D11_EVALUATE_DLSS_EXT(state.context.Get(), state.feature, state.parameters, &evaluate);
    if (NVSDK_NGX_FAILED(result)) { Log("DLSS EvaluateFeature failed result=0x%08x", unsigned(result)); status = -1; return; }
    ++state.evaluations;
    state.lastFrame = frame.frameIndex; state.cameraId = frame.cameraId; state.epoch = frame.historyEpoch;
    if (state.evaluations == 1 || state.evaluations % 300 == 0) {
        Log("DLSS EvaluateFeature succeeded result=0x%08x evaluations=%llu reset=%d", unsigned(result), state.evaluations, evaluate.InReset);
        Log("Camera inputs near=%.4f far=%.1f verticalFovRadians=%.5f", frame.cameraNear, frame.cameraFar, frame.cameraFov);
        ReportFrameGeneration(state.evaluations);
    }
    if (before) { auto after = Capture(state, frame.output); if (after) Verify(state, before.Get(), after.Get()); }
    status = 2;
}

void __stdcall RenderEvent(int eventId, void* pointer) {
    if (eventId != 1 || !pointer) return;
    std::unique_ptr<Frame> frame(static_cast<Frame*>(pointer));
    if (retryRequested.exchange(false)) status = 0;
    try { Evaluate(*frame); }
    catch (...) { Log("Unhandled native DLSS exception; integration disabled."); status = -1; }
    for (auto* resource : {frame->color, frame->depth, frame->motion, frame->output}) if (resource) resource->Release();
}
}

extern "C" __declspec(dllexport) void BW_Initialize(const wchar_t* data, const wchar_t* runtime) {
    dataDirectory = data; runtimeDirectory = runtime;
    Log("Big Beautiful Walk native 0.7.0 loaded; frame ABI=%zu", sizeof(Frame));
}
extern "C" __declspec(dllexport) int BW_FrameSize() { return sizeof(Frame); }
extern "C" __declspec(dllexport) int BW_Status() { return retryRequested.load() ? 0 : status.load(); }
// Re-entry happens on the render thread; callers never mutate NGX/device state.
extern "C" __declspec(dllexport) void BW_RequestRetry() { retryRequested = true; }
extern "C" __declspec(dllexport) void* BW_GetRenderEvent() { return reinterpret_cast<void*>(&RenderEvent); }
extern "C" __declspec(dllexport) Frame* BW_Queue(const Frame* input) {
    if (!input || input->abi != 3 || input->quality < 0 || input->quality > 5 || input->preset < 0 || input->preset > 13 ||
        !input->color || !input->depth || !input->motion || !input->output) return nullptr;
    auto frame = std::make_unique<Frame>(*input);
    for (auto* resource : {frame->color, frame->depth, frame->motion, frame->output}) if (resource) resource->AddRef();
    return frame.release();
}
