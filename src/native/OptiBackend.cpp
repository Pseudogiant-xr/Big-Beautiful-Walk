#include <windows.h>
#include "NgxProtocol.h"
#include "Upscaler.h"

// AMD/Intel backend: drives the NGX D3D11 entry points exported by OptiScaler, which the game
// loads at startup as version.dll from its root folder. OptiScaler chooses the actual upscaler and
// can fall back silently, so log lines report OptiScaler results only, never a specific upscaler.
namespace {
using bw::Log;

struct Exports {
    ngx::InitWithProjectIdFunction init = nullptr;
    ngx::GetCapabilityParametersFunction getCapabilityParameters = nullptr;
    ngx::CreateFeatureFunction createFeature = nullptr;
    ngx::EvaluateFeatureFunction evaluateFeature = nullptr;
    ngx::ReleaseFeatureFunction releaseFeature = nullptr;
};

bool attached = false;
Exports opti;
ngx::Parameter* parameters = nullptr;
ngx::Handle* feature = nullptr;
bool ngxReady = false;

template <typename Function>
bool Resolve(HMODULE module, const char* name, Function& function, const char*& missing) {
    function = reinterpret_cast<Function>(GetProcAddress(module, name));
    if (!function && !missing) missing = name;
    return function != nullptr;
}
}

namespace bw::backend {

const char* Name() { return "OptiScaler"; }

bool Attach(const std::wstring& runtimeDirectory) {
    if (attached) return true;
    // Only an already-loaded OptiScaler is supported; loading it late would miss its startup hooks.
    std::wstring path = runtimeDirectory;
    for (auto& character : path) if (character == L'/') character = L'\\';
    while (!path.empty() && path.back() == L'\\') path.pop_back();
    path += L"\\version.dll";
    HMODULE module = GetModuleHandleW(path.c_str());
    // The loader may have recorded the game folder under another spelling (a junction or
    // short name). Fall back to the loaded module's base name; its exports are checked below.
    if (!module) module = GetModuleHandleW(L"version.dll");
    Exports found;
    const char* missing = nullptr;
    if (module) {
        Resolve(module, "NVSDK_NGX_D3D11_Init_with_ProjectID", found.init, missing);
        Resolve(module, "NVSDK_NGX_D3D11_GetCapabilityParameters", found.getCapabilityParameters, missing);
        Resolve(module, "NVSDK_NGX_D3D11_CreateFeature", found.createFeature, missing);
        Resolve(module, "NVSDK_NGX_D3D11_EvaluateFeature", found.evaluateFeature, missing);
        Resolve(module, "NVSDK_NGX_D3D11_ReleaseFeature", found.releaseFeature, missing);
    }
    if (!module || missing) {
        Log("OptiScaler bridge is not loaded; AMD/Intel GPUs need the optional bridge from Setup");
        if (!module) Log("No version.dll is loaded from the game folder.");
        else {
            wchar_t loaded[MAX_PATH] = {};
            GetModuleFileNameW(module, loaded, MAX_PATH);
            Log("The loaded version.dll (%ls) does not export %s, so it is not OptiScaler.", loaded, missing);
        }
        return false;
    }
    opti = found;
    attached = true;
    Log("OptiScaler bridge found: the game folder's version.dll exports the NGX D3D11 entry points.");
    return true;
}

bool Initialize(ID3D11Device* device, const std::wstring& dataDirectory, const std::wstring& runtimeDirectory) {
    if (!Attach(runtimeDirectory)) return false;
    // Initialised once per device, as on the NVIDIA path: a retry only repeats steps that failed.
    if (!ngxReady) {
        const auto result = opti.init("ab31a96d-e80e-46d7-b5c1-78f9636183ec", ngx::EngineTypeUnity, "6000.3.17f1",
            dataDirectory.c_str(), device, nullptr, ngx::SdkVersion);
        Log("OptiScaler NGX D3D11 project initialization result=0x%08x", result);
        if (ngx::Failed(result)) return false;
        ngxReady = true;
    }
    if (!parameters) {
        const auto capabilities = opti.getCapabilityParameters(&parameters);
        if (ngx::Failed(capabilities) || !parameters) {
            parameters = nullptr;
            Log("OptiScaler capability parameters result=0x%08x", capabilities);
            return false;
        }
    }
    // OptiScaler may not publish the key; only an explicit zero means it refuses super sampling.
    int available = 0;
    const auto lookup = parameters->Get("SuperSampling.Available", &available);
    if (ngx::Failed(lookup)) {
        Log("OptiScaler capability SuperSampling.Available not reported (result=0x%08x); assuming available", lookup);
        return true;
    }
    Log("OptiScaler capability SuperSampling.Available=%d", available);
    return available != 0;
}

bool HasFeature() { return feature != nullptr; }

bool Create(ID3D11DeviceContext* context, const CreateInputs& inputs) {
    // DLSS render-preset hints are deliberately not sent; they have no meaning for OptiScaler's upscalers.
    parameters->Set("Width", inputs.renderWidth);
    parameters->Set("Height", inputs.renderHeight);
    parameters->Set("OutWidth", inputs.outputWidth);
    parameters->Set("OutHeight", inputs.outputHeight);
    parameters->Set("PerfQualityValue", inputs.quality);
    parameters->Set("DLSS.Feature.Create.Flags", int(inputs.flags));
    parameters->Set("DLSS.Enable.Output.Subrects", 0);
    Log("OptiScaler quality mode requested=%d", inputs.quality);
    const auto result = opti.createFeature(context, ngx::FeatureSuperSampling, parameters, &feature);
    Log("OptiScaler CreateFeature result=0x%08x render=%ux%u output=%ux%u flags=0x%x", result,
        inputs.renderWidth, inputs.renderHeight, inputs.outputWidth, inputs.outputHeight, inputs.flags);
    return !ngx::Failed(result) && feature;
}

bool Evaluate(ID3D11DeviceContext* context, const EvaluateInputs& inputs, unsigned& result) {
    // Same values the NGX SDK evaluate helper sends, including its zero-to-one motion-scale default.
    parameters->Set("Color", inputs.color);
    parameters->Set("Output", inputs.output);
    parameters->Set("Depth", inputs.depth);
    parameters->Set("MotionVectors", inputs.motion);
    parameters->Set("Jitter.Offset.X", inputs.jitterX);
    parameters->Set("Jitter.Offset.Y", inputs.jitterY);
    parameters->Set("Sharpness", 0.0f);
    parameters->Set("MV.Scale.X", inputs.motionScaleX == 0.0f ? 1.0f : inputs.motionScaleX);
    parameters->Set("MV.Scale.Y", inputs.motionScaleY == 0.0f ? 1.0f : inputs.motionScaleY);
    parameters->Set("Reset", inputs.reset);
    parameters->Set("DLSS.Render.Subrect.Dimensions.Width", inputs.renderWidth);
    parameters->Set("DLSS.Render.Subrect.Dimensions.Height", inputs.renderHeight);
    parameters->Set("DLSS.Pre.Exposure", 1.0f);
    parameters->Set("DLSS.Exposure.Scale", 1.0f);
    parameters->Set("FrameTimeDeltaInMsec", inputs.deltaMilliseconds);
    // Optional camera values OptiScaler uses for frame generation.
    parameters->Set("FSR.cameraNear", inputs.cameraNear);
    parameters->Set("FSR.cameraFar", inputs.cameraFar);
    parameters->Set("FSR.cameraFovAngleVertical", inputs.cameraFov);
    parameters->Set("FSR.viewSpaceToMetersFactor", 1.0f);
    parameters->Set("FSR.frameTimeDelta", inputs.deltaMilliseconds);
    result = opti.evaluateFeature(context, feature, parameters, nullptr);
    return !ngx::Failed(result);
}

void Release() {
    opti.releaseFeature(feature);
    feature = nullptr;
}

// Streamline frame-generation reporting is NVIDIA-only; the bridge has no equivalent query.
void ReportFrameGeneration(unsigned long long) {}
}
