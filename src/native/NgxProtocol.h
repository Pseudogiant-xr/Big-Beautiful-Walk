#pragma once
#include <d3d11.h>

// Minimal, independently written declarations of the NGX D3D11 C entry points and parameter
// interface as exported by OptiScaler. Used only by the bridge DLL, which must not include
// NVIDIA SDK headers or link NVIDIA SDK code.
struct ID3D12Resource;

namespace ngx {

using Result = unsigned int;
constexpr Result Success = 0x1;
inline bool Failed(Result value) { return (value & 0xFFF00000u) == 0xBAD00000u; }

constexpr int EngineTypeUnity = 2;
constexpr int FeatureSuperSampling = 1;
constexpr int SdkVersion = 0x15;

// Opaque feature handle owned by the exporting module.
struct Handle;

// The declaration order (including overload order) must match the exporter's parameter class
// so MSVC lays out an identical vtable. Do not reorder, add or remove members.
struct Parameter {
    virtual void Set(const char* name, unsigned long long value) = 0;
    virtual void Set(const char* name, float value) = 0;
    virtual void Set(const char* name, double value) = 0;
    virtual void Set(const char* name, unsigned int value) = 0;
    virtual void Set(const char* name, int value) = 0;
    virtual void Set(const char* name, ID3D11Resource* value) = 0;
    virtual void Set(const char* name, ID3D12Resource* value) = 0;
    virtual void Set(const char* name, void* value) = 0;

    virtual Result Get(const char* name, unsigned long long* value) const = 0;
    virtual Result Get(const char* name, float* value) const = 0;
    virtual Result Get(const char* name, double* value) const = 0;
    virtual Result Get(const char* name, unsigned int* value) const = 0;
    virtual Result Get(const char* name, int* value) const = 0;
    virtual Result Get(const char* name, ID3D11Resource** value) const = 0;
    virtual Result Get(const char* name, ID3D12Resource** value) const = 0;
    virtual Result Get(const char* name, void** value) const = 0;

    virtual void Reset() = 0;
};

// extern "C" exports, resolved by name with GetProcAddress.
using InitWithProjectIdFunction = Result(__cdecl*)(const char* projectId, int engineType, const char* engineVersion,
    const wchar_t* dataPath, ID3D11Device* device, const void* featureInfo, int sdkVersion);
using GetCapabilityParametersFunction = Result(__cdecl*)(Parameter** parameters);
using CreateFeatureFunction = Result(__cdecl*)(ID3D11DeviceContext* context, int feature, Parameter* parameters,
    Handle** handle);
using EvaluateFeatureFunction = Result(__cdecl*)(ID3D11DeviceContext* context, const Handle* handle,
    const Parameter* parameters, void* progressCallback);
using ReleaseFeatureFunction = Result(__cdecl*)(Handle* handle);
}
