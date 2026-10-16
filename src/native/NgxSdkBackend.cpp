#include <windows.h>
#include <d3d11_1.h>
#include <nvsdk_ngx.h>
#include <nvsdk_ngx_helpers.h>
#include <nvsdk_ngx_helpers_d3d.h>
#include <sl_dlss_g.h>
#include "Upscaler.h"

// NVIDIA backend: NGX SDK (statically linked loader) plus Streamline frame-generation reporting.
static_assert(bw::FlagIsHDR == NVSDK_NGX_DLSS_Feature_Flags_IsHDR, "flag mismatch");
static_assert(bw::FlagMVLowRes == NVSDK_NGX_DLSS_Feature_Flags_MVLowRes, "flag mismatch");
static_assert(bw::FlagDepthInverted == NVSDK_NGX_DLSS_Feature_Flags_DepthInverted, "flag mismatch");
static_assert(bw::FlagAutoExposure == NVSDK_NGX_DLSS_Feature_Flags_AutoExposure, "flag mismatch");

namespace {
using bw::Log;
NVSDK_NGX_Parameter* parameters = nullptr;
NVSDK_NGX_Handle* feature = nullptr;
bool ngxReady = false;
}

namespace bw::backend {

const char* Name() { return "DLSS"; }

bool Attach(const std::wstring&) { return true; }

bool Initialize(ID3D11Device* device, const std::wstring& dataDirectory, const std::wstring& runtimeDirectory) {
    // NGX is initialized once per device. A retry never calls Init again; it fetches capability
    // parameters only if that failed before, and otherwise re-reads the cached availability.
    if (!ngxReady) {
        const wchar_t* paths[] = {runtimeDirectory.c_str()};
        NVSDK_NGX_FeatureCommonInfo info{};
        info.PathListInfo.Path = paths;
        info.PathListInfo.Length = 1;
        const auto result = NVSDK_NGX_D3D11_Init_with_ProjectID("ab31a96d-e80e-46d7-b5c1-78f9636183ec",
            NVSDK_NGX_ENGINE_TYPE_UNITY, "6000.3.17f1", dataDirectory.c_str(), device, &info);
        Log("NGX D3D11 project initialization result=0x%08x", unsigned(result));
        if (NVSDK_NGX_FAILED(result)) return false;
        ngxReady = true;
    }
    if (!parameters) {
        const auto capabilities = NVSDK_NGX_D3D11_GetCapabilityParameters(&parameters);
        if (NVSDK_NGX_FAILED(capabilities)) {
            parameters = nullptr;
            Log("DLSS capability result=0x%08x", unsigned(capabilities));
            return false;
        }
    }
    int available = 0;
    parameters->Get(NVSDK_NGX_Parameter_SuperSampling_Available, &available);
    Log("DLSS capability available=%d", available);
    return available != 0;
}

bool HasFeature() { return feature != nullptr; }

bool Create(ID3D11DeviceContext* context, const CreateInputs& inputs) {
    NVSDK_NGX_DLSS_Create_Params create{};
    create.Feature.InWidth = inputs.renderWidth;
    create.Feature.InHeight = inputs.renderHeight;
    create.Feature.InTargetWidth = inputs.outputWidth;
    create.Feature.InTargetHeight = inputs.outputHeight;
    create.Feature.InPerfQualityValue = static_cast<NVSDK_NGX_PerfQuality_Value>(inputs.quality);
    create.InFeatureCreateFlags = int(inputs.flags);
    for (const char* key : { NVSDK_NGX_Parameter_DLSS_Hint_Render_Preset_DLAA,
        NVSDK_NGX_Parameter_DLSS_Hint_Render_Preset_UltraQuality, NVSDK_NGX_Parameter_DLSS_Hint_Render_Preset_Quality,
        NVSDK_NGX_Parameter_DLSS_Hint_Render_Preset_Balanced, NVSDK_NGX_Parameter_DLSS_Hint_Render_Preset_Performance,
        NVSDK_NGX_Parameter_DLSS_Hint_Render_Preset_UltraPerformance })
        parameters->Set(key, unsigned(inputs.preset));
    Log("DLSS quality mode requested=%d, render preset hint=%d", inputs.quality, inputs.preset);
    const auto result = NGX_D3D11_CREATE_DLSS_EXT(context, &feature, parameters, &create);
    Log("DLSS CreateFeature result=0x%08x render=%ux%u output=%ux%u flags=0x%x", unsigned(result),
        inputs.renderWidth, inputs.renderHeight, inputs.outputWidth, inputs.outputHeight, inputs.flags);
    return !NVSDK_NGX_FAILED(result) && feature;
}

bool Evaluate(ID3D11DeviceContext* context, const EvaluateInputs& inputs, unsigned& result) {
    NVSDK_NGX_D3D11_DLSS_Eval_Params evaluate{};
    evaluate.Feature.pInColor = inputs.color; evaluate.Feature.pInOutput = inputs.output;
    evaluate.pInDepth = inputs.depth; evaluate.pInMotionVectors = inputs.motion;
    evaluate.InJitterOffsetX = inputs.jitterX; evaluate.InJitterOffsetY = inputs.jitterY;
    evaluate.InMVScaleX = inputs.motionScaleX; evaluate.InMVScaleY = inputs.motionScaleY;
    evaluate.InRenderSubrectDimensions = {inputs.renderWidth, inputs.renderHeight};
    evaluate.InReset = inputs.reset;
    evaluate.InPreExposure = 1.0f;
    // OptiScaler's bridge consumes these optional camera values for frame generation.
    parameters->Set("FSR.cameraNear", inputs.cameraNear);
    parameters->Set("FSR.cameraFar", inputs.cameraFar);
    parameters->Set("FSR.cameraFovAngleVertical", inputs.cameraFov);
    parameters->Set("FSR.viewSpaceToMetersFactor", 1.0f);
    parameters->Set("FSR.frameTimeDelta", inputs.deltaMilliseconds);
    const auto value = NGX_D3D11_EVALUATE_DLSS_EXT(context, feature, parameters, &evaluate);
    result = unsigned(value);
    return !NVSDK_NGX_FAILED(value);
}

void Release() {
    NVSDK_NGX_D3D11_ReleaseFeature(feature);
    feature = nullptr;
}

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
}
