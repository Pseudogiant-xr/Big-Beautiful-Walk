#pragma once
#include <d3d11.h>
#include <string>

// Vendor-neutral contract between the shared core (Native.cpp) and the one backend linked into
// each DLL. Exactly one backend translation unit defines bw::backend; nothing here includes
// vendor SDK headers.
namespace bw {

void Log(const char* format, ...);

// Feature-creation flag bits, numerically identical to the NGX DLSS create flags.
enum CreateFlags : unsigned {
    FlagIsHDR = 1u << 0,
    FlagMVLowRes = 1u << 1,
    FlagDepthInverted = 1u << 3,
    FlagAutoExposure = 1u << 6,
};

struct CreateInputs {
    unsigned renderWidth, renderHeight, outputWidth, outputHeight;
    int quality, preset;
    unsigned flags;
};

struct EvaluateInputs {
    ID3D11Resource *color, *output, *depth, *motion;
    float jitterX, jitterY, motionScaleX, motionScaleY;
    unsigned renderWidth, renderHeight;
    int reset;
    // Near/far are already ordered for the depth convention (swapped for reversed depth).
    float cameraNear, cameraFar, cameraFov, deltaMilliseconds;
};

namespace backend {
// Short name used in log lines ("DLSS" or "OptiScaler").
const char* Name();
// Called from BW_Initialize; returns false when the backend cannot run in this process.
bool Attach(const std::wstring& runtimeDirectory);
// One-time runtime initialisation on the render thread. Retries must not re-run completed steps.
bool Initialize(ID3D11Device* device, const std::wstring& dataDirectory, const std::wstring& runtimeDirectory);
bool HasFeature();
bool Create(ID3D11DeviceContext* context, const CreateInputs& inputs);
bool Evaluate(ID3D11DeviceContext* context, const EvaluateInputs& inputs, unsigned& result);
// Caller guarantees the GPU is idle.
void Release();
void ReportFrameGeneration(unsigned long long evaluations);
}
}
