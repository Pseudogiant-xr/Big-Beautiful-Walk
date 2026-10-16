cmake_minimum_required(VERSION 3.20)
project(BigWalkDLSSNative LANGUAGES CXX)
if(NOT WIN32 OR NOT MSVC)
  message(FATAL_ERROR "Big Beautiful Walk requires Windows x64 and MSVC.")
endif()
if(NOT CMAKE_SIZEOF_VOID_P EQUAL 8)
  message(FATAL_ERROR "Configure an x64 build (for Visual Studio, use -A x64).")
endif()

# Both DLLs share Native.cpp and export the same BW_* API; each links exactly one upscaler backend.
function(bbw_native_target name)
  add_library(${name} SHARED Native.cpp Upscaler.h ${ARGN})
  target_compile_features(${name} PRIVATE cxx_std_17)
  target_compile_definitions(${name} PRIVATE UNICODE _UNICODE WIN32_LEAN_AND_MEAN NOMINMAX)
  target_link_libraries(${name} PRIVATE d3d11 dxgi)
  set_property(TARGET ${name} PROPERTY MSVC_RUNTIME_LIBRARY "MultiThreadedDLL")
  target_compile_options(${name} PRIVATE /W4 /EHsc)
  target_link_options(${name} PRIVATE $<$<CONFIG:Release>:/DEBUG:NONE>)
endfunction()

# NVIDIA GPUs: NGX SDK + Streamline. Requires the NVIDIA SDK paths.
option(BBW_BUILD_DLSS "Build BigWalkDLSSNative (requires NGX_ROOT and STREAMLINE_ROOT)" ON)
if(BBW_BUILD_DLSS)
  set(NGX_ROOT "" CACHE PATH "NVIDIA DLSS SDK headers and lib directory")
  set(STREAMLINE_ROOT "" CACHE PATH "NVIDIA Streamline header directory")
  find_path(NGX_INCLUDE nvsdk_ngx.h PATHS "${NGX_ROOT}/include" NO_DEFAULT_PATH REQUIRED)
  find_library(NGX_LIBRARY nvsdk_ngx_d PATHS "${NGX_ROOT}/lib/Windows_x86_64/x64" "${NGX_ROOT}/lib/Windows_x86_64" "${NGX_ROOT}/lib" NO_DEFAULT_PATH REQUIRED)
  find_path(STREAMLINE_INCLUDE sl_dlss_g.h PATHS "${STREAMLINE_ROOT}/include" "${STREAMLINE_ROOT}" NO_DEFAULT_PATH REQUIRED)
  bbw_native_target(BigWalkDLSSNative NgxSdkBackend.cpp)
  target_include_directories(BigWalkDLSSNative PRIVATE "${NGX_INCLUDE}" "${STREAMLINE_INCLUDE}")
  target_link_libraries(BigWalkDLSSNative PRIVATE "${NGX_LIBRARY}")
endif()

# AMD/Intel GPUs: drives the already-loaded OptiScaler through its NGX-compatible exports.
# Uses only NgxProtocol.h; it must never see NVIDIA SDK include paths or libraries.
bbw_native_target(BigWalkBridgeNative OptiBackend.cpp NgxProtocol.h)
