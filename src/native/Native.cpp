#include <windows.h>
#include <d3d11_1.h>
#include <wrl/client.h>
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
#include "Upscaler.h"

// Shared core of both native DLLs. The upscaler backend (NVIDIA NGX SDK or the OptiScaler bridge)
// is chosen at link time; this file contains no vendor SDK code.
using Microsoft::WRL::ComPtr;
namespace backend = bw::backend;

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
}

void bw::Log(const char* format, ...) {
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

namespace {
using bw::Log;

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
    int width = 0, height = 0, outWidth = 0, outHeight = 0, cameraId = 0, epoch = -1, lastFrame = -1;
    unsigned flags = 0;
    int quality = -1, preset = -1;
    unsigned long long evaluations = 0;
    bool initialized = false, verified = false;
};

// The engine owns the process lifetime. Explicit GPU-idle teardown is used for feature recreation.
// Avoid calling graphics/NGX APIs from a DLL destructor after Unity has destroyed its device.
State& Engine() { static State* state = new State(); return *state; }

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

bool InitializeBackend(State& state) {
    if (state.initialized) return true;
    // The backend initializes once per device; a retry only repeats the steps that failed before.
    state.initialized = backend::Initialize(state.device.Get(), dataDirectory, runtimeDirectory);
    return state.initialized;
}

bool EnsureFeature(State& state, const Frame& frame, const D3D11_TEXTURE2D_DESC& color,
                   const D3D11_TEXTURE2D_DESC& motion) {
    unsigned flags = bw::FlagAutoExposure;
    if (frame.reversedDepth) flags |= bw::FlagDepthInverted;
    if (motion.Width <= unsigned(frame.renderWidth) && motion.Height <= unsigned(frame.renderHeight))
        flags |= bw::FlagMVLowRes;
    if (color.Format == DXGI_FORMAT_R16G16B16A16_FLOAT || color.Format == DXGI_FORMAT_R11G11B10_FLOAT ||
        color.Format == DXGI_FORMAT_R32G32B32A32_FLOAT) flags |= bw::FlagIsHDR;
    if (backend::HasFeature() && state.width == frame.renderWidth && state.height == frame.renderHeight &&
        state.outWidth == frame.outputWidth && state.outHeight == frame.outputHeight && state.flags == flags &&
        state.quality == frame.quality && state.preset == frame.preset) return true;
    if (backend::HasFeature()) {
        if (!WaitGpu(state)) { Log("Feature resize abandoned: GPU completion not confirmed."); return false; }
        backend::Release();
    }
    const bw::CreateInputs create{unsigned(frame.renderWidth), unsigned(frame.renderHeight),
        unsigned(frame.outputWidth), unsigned(frame.outputHeight), frame.quality, frame.preset, flags};
    if (!backend::Create(state.context.Get(), create)) return false;
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
    Log("A/B pixel evidence: %s versus bilinear, samples=%zu changed=%zu mean_absolute_linear_RGB_difference=%.8f",
        backend::Name(), count, changed, count ? absolute / double(count) : 0);
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
    if (!InitializeBackend(state) || !EnsureFeature(state, frame, color, motion)) { status = -1; return; }
    if (frame.mode == 2) { state.lastFrame = -1; return; }
    const bool verify = frame.verify && !state.verified && state.evaluations >= 30;
    auto before = verify ? Capture(state, frame.output) : ComPtr<ID3D11Texture2D>{};
    bw::EvaluateInputs evaluate{};
    evaluate.color = frame.color; evaluate.output = frame.output;
    evaluate.depth = frame.depth; evaluate.motion = frame.motion;
    evaluate.jitterX = frame.jitterX; evaluate.jitterY = frame.jitterY;
    evaluate.motionScaleX = frame.motionScaleX; evaluate.motionScaleY = frame.motionScaleY;
    evaluate.renderWidth = unsigned(frame.renderWidth); evaluate.renderHeight = unsigned(frame.renderHeight);
    evaluate.reset = state.lastFrame < 0 || state.cameraId != frame.cameraId || state.epoch != frame.historyEpoch || frame.frameIndex > state.lastFrame + 1;
    evaluate.cameraNear = frame.reversedDepth ? frame.cameraFar : frame.cameraNear;
    evaluate.cameraFar = frame.reversedDepth ? frame.cameraNear : frame.cameraFar;
    evaluate.cameraFov = frame.cameraFov;
    evaluate.deltaMilliseconds = frame.deltaMilliseconds;
    unsigned result = 0;
    if (!backend::Evaluate(state.context.Get(), evaluate, result)) {
        Log("%s EvaluateFeature failed result=0x%08x", backend::Name(), result); status = -1; return;
    }
    ++state.evaluations;
    state.lastFrame = frame.frameIndex; state.cameraId = frame.cameraId; state.epoch = frame.historyEpoch;
    if (state.evaluations == 1 || state.evaluations % 300 == 0) {
        Log("%s EvaluateFeature succeeded result=0x%08x evaluations=%llu reset=%d", backend::Name(), result,
            state.evaluations, evaluate.reset);
        Log("Camera inputs near=%.4f far=%.1f verticalFovRadians=%.5f", frame.cameraNear, frame.cameraFar, frame.cameraFov);
        backend::ReportFrameGeneration(state.evaluations);
    }
    if (before) { auto after = Capture(state, frame.output); if (after) Verify(state, before.Get(), after.Get()); }
    status = 2;
}

void __stdcall RenderEvent(int eventId, void* pointer) {
    if (eventId != 1 || !pointer) return;
    std::unique_ptr<Frame> frame(static_cast<Frame*>(pointer));
    if (retryRequested.exchange(false)) status = 0;
    try { Evaluate(*frame); }
    catch (...) { Log("Unhandled native %s exception; integration disabled.", backend::Name()); status = -1; }
    for (auto* resource : {frame->color, frame->depth, frame->motion, frame->output}) if (resource) resource->Release();
}
}

extern "C" __declspec(dllexport) void BW_Initialize(const wchar_t* data, const wchar_t* runtime) {
    dataDirectory = data; runtimeDirectory = runtime;
    Log("Big Beautiful Walk native 0.8.3 loaded; backend=%s; frame ABI=%zu", backend::Name(), sizeof(Frame));
    if (!backend::Attach(runtimeDirectory)) status = -1;
}
extern "C" __declspec(dllexport) int BW_FrameSize() { return sizeof(Frame); }
extern "C" __declspec(dllexport) int BW_Status() { return retryRequested.load() ? 0 : status.load(); }
// Re-entry happens on the render thread; callers never mutate backend/device state.
extern "C" __declspec(dllexport) void BW_RequestRetry() { retryRequested = true; }
extern "C" __declspec(dllexport) void* BW_GetRenderEvent() { return reinterpret_cast<void*>(&RenderEvent); }
extern "C" __declspec(dllexport) Frame* BW_Queue(const Frame* input) {
    if (!input || input->abi != 3 || input->quality < 0 || input->quality > 5 || input->preset < 0 || input->preset > 13 ||
        !input->color || !input->depth || !input->motion || !input->output) return nullptr;
    auto frame = std::make_unique<Frame>(*input);
    for (auto* resource : {frame->color, frame->depth, frame->motion, frame->output}) if (resource) resource->AddRef();
    return frame.release();
}
