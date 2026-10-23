using System;
using System.Collections.Generic;
using BigWalkDLSS;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.Universal.Internal;
var lines=new List<string>();
try
{
 ShadowPassProbe.Begin(lines.Add,()=>true);
 var pass=new MainLightShadowCasterPass();var data=new UniversalCameraData();
 ShadowPassProbe.AfterSetup(pass,true,data);
 if(lines.Count!=1||!lines[0].Contains("cascades=4")||!lines[0].Contains("effective_max_shadow_distance=1000")||!lines[0].Contains("cascade3_slice_resolution=8192"))throw new Exception("Produced cascade data missing or replaced by asset settings");
 ShadowPassProbe.AfterSetup(pass,true,data);if(lines.Count!=1)throw new Exception("Snapshot rate limit failed");
 var other=new UniversalCameraData();other.camera.targetTexture=new object();ShadowPassProbe.AfterSetup(pass,true,other);
 if(lines.Count!=2||!lines[1].Contains("target_texture=True"))throw new Exception("A second base render-to-texture camera was suppressed");
 Time.realtimeSinceStartupAsDouble=2;pass.m_CreateEmptyShadowmap=true;ShadowPassProbe.AfterSetup(pass,true,data);
 pass.m_CreateEmptyShadowmap=false;data.renderType=CameraRenderType.Overlay;ShadowPassProbe.AfterSetup(pass,true,data);
 if(lines.Count!=2)throw new Exception("Empty/overlay pass was misidentified as the world shadow pass");
 data.renderType=CameraRenderType.Base;pass.m_CascadeSplitDistances=Array.Empty<Vector4>();ShadowPassProbe.AfterSetup(pass,true,data);
 if(lines.Count!=3)throw new Exception("Short data arrays were not handled");
 ShadowPassProbe.Shutdown();Time.realtimeSinceStartupAsDouble=4;ShadowPassProbe.AfterSetup(pass,true,data);
 if(lines.Count!=3)throw new Exception("Stopped probe still recorded data");
 Console.WriteLine("Shadow pass probe checks passed: real-pass fields, camera distance clamp, slices, cadence, overlay/empty filtering, short arrays and shutdown.");return 0;
}
catch(Exception error){Console.Error.WriteLine("FAIL "+error.Message);return 1;}
namespace BigWalkDLSS{internal sealed class Log{internal void LogWarning(string value){}}internal sealed class Host{internal readonly Log Log=new();}internal static class Plugin{internal static readonly Host Instance=new();}}
namespace HarmonyLib{internal sealed class Harmony(string id){internal void Patch(object? method,HarmonyMethod postfix){}internal void UnpatchSelf(){}}internal sealed class HarmonyMethod(Type type,string name){}internal static class AccessTools{internal static object? Method(Type type,string name,Type[] args)=>new();}}
namespace UnityEngine
{
 internal enum CameraType{Game,SceneView}internal readonly record struct Vector3(float x,float y,float z);internal readonly record struct Vector4(float x,float y,float z,float w);
 internal sealed class Transform{internal Vector3 position=>new(1,2,3);internal Vector3 forward=>new(0,0,1);}
 internal sealed class Camera{internal CameraType cameraType=>CameraType.Game;internal bool stereoEnabled=>false;internal object? targetTexture;internal float nearClipPlane=>.1f;internal float farClipPlane=>1000;internal float fieldOfView=>60;internal readonly Transform transform=new();private static int next;private readonly int identity=++next;internal int GetInstanceID()=>identity;}
 internal static class Time{internal static int frameCount=>20;internal static double realtimeSinceStartupAsDouble;}
}
namespace UnityEngine.Rendering.Universal
{
 internal enum CameraRenderType{Base,Overlay}internal sealed class UniversalRenderingData{}internal sealed class UniversalLightData{}internal sealed class UniversalShadowData{}
 internal sealed class UniversalCameraData{internal readonly Camera camera=new();internal CameraRenderType renderType;internal float maxShadowDistance=>1000;}
}
namespace UnityEngine.Rendering.Universal.Internal
{
 internal readonly record struct Slice(int resolution);
 internal sealed class MainLightShadowCasterPass
 {
  internal bool m_CreateEmptyShadowmap;internal int m_ShadowCasterCascadesCount=>4;internal int m_RenderTargetWidth=>16384;internal int m_RenderTargetHeight=>16384;
  internal Vector4[] m_CascadeSplitDistances=new[]{new Vector4(0,0,1,25),new Vector4(0,0,5,100),new Vector4(0,0,20,400),new Vector4(0,0,50,1000)};
  internal Slice[] m_CascadeSlices=>new[]{new Slice(8192),new Slice(8192),new Slice(8192),new Slice(8192)};
  internal bool Setup(UniversalRenderingData a,UniversalCameraData b,UniversalLightData c,UniversalShadowData d)=>true;
 }
}
