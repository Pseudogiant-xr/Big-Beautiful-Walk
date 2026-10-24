using System;
using System.Collections.Generic;
using BigWalkDLSS;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.Universal.Internal;

var lines=new List<string>();
static void Check(bool condition,string reason){if(!condition)throw new Exception(reason);}
try
{
 bool collecting=false;ShadowReceiverProbe.Begin(()=>collecting);
 var pass=new MainLightShadowCasterPass();var data=new MainLightShadowCasterPass.PassData();var command=new RasterCommandBuffer();
 ShadowReceiverProbe.BeforePass(pass,command,ref data,true,out var warmup);Check(warmup==null,"Warmup recorded");
 collecting=true;
 ShadowReceiverProbe.BeforePass(pass,command,ref data,true,out var sample);
 Check(sample!=null&&sample.Text.Contains("slice3_kept=")&&sample.Text.Contains("Crest.SampleShadowsURP"),"Actual execution data/consumer list missing");
 Check(sample!.Text.Contains("scissor_applied=False")&&sample.Text.Contains("receiver_contract=unaudited"),"Proposed bounds mislabeled as safe clipping");
 var originalFailure=new InvalidOperationException("original");
 Check(ReferenceEquals(ShadowReceiverProbe.AfterPass(originalFailure,sample),originalFailure),"Original exception suppressed/replaced");
 ShadowReceiverProbe.Drain(lines.Add);Check(lines.Count==1&&lines[0].Contains("pass_failed=True"),"Completed scope missing");
 ShadowReceiverProbe.BeforePass(pass,command,ref data,true,out var sameSecond);Check(sameSecond==null,"Sampling cadence failed");
 // Same draw pass reused for a different camera must use that call's data.
 var second=new MainLightShadowCasterPass.PassData();second.cameraData.camera.Id=99;second.cameraData.renderType=CameraRenderType.Overlay;
 ShadowReceiverProbe.BeforePass(pass,command,ref second,true,out var overlay);
 Check(overlay!=null&&overlay.Text.Contains("camera_id=99")&&overlay.Text.Contains("unsupported_camera"),"Overlay confused with earlier camera");
 ShadowReceiverProbe.AfterPass(null,overlay);ShadowReceiverProbe.Drain(lines.Add);
 Time.realtimeSinceStartupAsDouble=2;
 pass.m_CascadeSlices[0].offsetX=17;
 ShadowReceiverProbe.BeforePass(pass,command,ref data,true,out var layout);
 Check(layout!=null&&layout.Text.Contains("layout_fallback"),"Nonstandard layout accepted");
 ShadowReceiverProbe.AfterPass(null,layout);ShadowReceiverProbe.Drain(lines.Add);pass.m_CascadeSlices[0].offsetX=0;
 // Native finalizer from an old capture must not contaminate a new capture.
 Time.realtimeSinceStartupAsDouble=4;ShadowReceiverProbe.BeforePass(pass,command,ref data,true,out var delayed);
 ShadowReceiverProbe.Shutdown();ShadowReceiverProbe.Begin(()=>true);
 ShadowReceiverProbe.AfterPass(null,delayed);ShadowReceiverProbe.Drain(lines.Add);Check(lines.Count==3,"Old capture finalizer entered new capture");
 // Invalid reads produce explicit diagnostic fallback and no command changes.
 Time.realtimeSinceStartupAsDouble=5;data.cameraData.ThrowMatrices=true;
 ShadowReceiverProbe.BeforePass(pass,command,ref data,true,out var invalid);
 ShadowReceiverProbe.AfterPass(null,invalid);ShadowReceiverProbe.Drain(lines.Add);
 Check(lines.Count==4&&lines[3].Contains("read_failed_InvalidOperationException"),"Read failure not reported");
 data.cameraData.ThrowMatrices=false;
 // Camera and queue caps keep capture-owned state bounded.
 Time.realtimeSinceStartupAsDouble=6;
 for(int index=100;index<180;index++){data.cameraData.camera.Id=index;ShadowReceiverProbe.BeforePass(pass,command,ref data,true,out var many);ShadowReceiverProbe.AfterPass(null,many);}
 ShadowReceiverProbe.Drain(lines.Add);Check(lines.Exists(line=>line.Contains("probe_dropped=")),"Camera limit silently lost records");
 ShadowReceiverProbe.Shutdown();Time.realtimeSinceStartupAsDouble=20;
 ShadowReceiverProbe.BeforePass(pass,command,ref data,true,out var stopped);Check(stopped==null,"Stopped probe still sampled");
 Check(HarmonyLib.Harmony.ValueRefHooks==0,"Installed a struct-byref hook on affected backend");
 Check(RasterCommandBuffer.CommandWrites==0,"Read-only prototype issued GPU commands");
 Console.WriteLine("Receiver probe checks passed: actual pass camera/consumers, geometry/fallback, cadence/caps, generation isolation, original exception and zero GPU writes.");return 0;
}
catch(Exception error){Console.Error.WriteLine("FAIL "+error);return 1;}

namespace BigWalkDLSS{internal sealed class Log{internal void LogWarning(string value){}}internal sealed class Host{internal readonly Log Log=new();}internal static class Plugin{internal static readonly Host Instance=new();}}
namespace HarmonyLib
{
 internal sealed class Harmony(string id){internal static int ValueRefHooks;internal void Patch(System.Reflection.MethodInfo? method,HarmonyMethod prefix,HarmonyMethod finalizer){if(method==null)throw new Exception("Missing hook");foreach(var p in method.GetParameters())if(p.ParameterType.IsByRef&&p.ParameterType.GetElementType()!.IsValueType)ValueRefHooks++;}internal void UnpatchSelf(){}}
 internal sealed class HarmonyMethod(Type type,string name){}internal static class AccessTools{internal static System.Reflection.MethodInfo? Method(Type type,string name)=>type.GetMethod(name);}
}
namespace UnityEngine
{
 internal enum CameraType{Game,SceneView}internal readonly record struct Vector3(float x,float y,float z);
 internal sealed class Transform{internal Vector3 forward=>new(0,-.1f,1);}
 internal sealed class Light{internal readonly Transform transform=new();}internal static class RenderSettings{internal static readonly Light sun=new();}
 internal sealed class Camera{internal int Id=1;internal int GetInstanceID()=>Id;internal CameraType cameraType=>CameraType.Game;internal bool stereoEnabled=>false;internal bool orthographic=>false;internal object? targetTexture=>null;}
 internal static class Time{internal static int frameCount=>20;internal static double realtimeSinceStartupAsDouble;}
 internal struct Matrix4x4
 {
  internal float m00,m01,m02,m03,m10,m11,m12,m13,m20,m21,m22,m23,m30,m31,m32,m33;
  internal static Matrix4x4 Identity=>new(){m00=1,m11=1,m22=1,m33=1};
 }
}
namespace UnityEngine.Rendering{internal sealed class RasterCommandBuffer{internal static int CommandWrites=0;internal IntPtr Pointer=>new(42);}}
namespace UnityEngine.Rendering.Universal
{
 internal enum CameraRenderType{Base,Overlay}internal readonly record struct Descriptor(int width,int height);
 internal sealed class UniversalCameraData
 {
  internal readonly Camera camera=new();internal CameraRenderType renderType;internal bool xrRendering=>false;internal object? targetTexture=>null;internal Descriptor cameraTargetDescriptor=>new(1920,1080);internal bool ThrowMatrices;
  internal readonly Renderer renderer=new();internal Matrix4x4 GetViewMatrix()=>Matrix4x4.Identity;
  internal Matrix4x4 GetProjectionMatrix(){if(ThrowMatrices)throw new InvalidOperationException();return new(){m00=10,m03=-5,m11=5,m13=-2.5f,m22=1,m33=1};}
 }
 internal sealed class Renderer{internal readonly Features supportedRenderingFeatures=new();internal readonly Feature[] rendererFeatures={new("Crest.SampleShadowsURP"),new("Inactive",false)};}
 internal sealed class Features{internal bool cameraStacking=>true;}
 internal sealed class Feature(string name,bool active=true){internal bool isActive=>active;internal TypeName GetIl2CppType()=>new(name);}
 internal sealed class TypeName(string name){internal string FullName=>name;}
 internal struct ShadowSliceData{internal int resolution,offsetX,offsetY;internal Matrix4x4 shadowTransform;}
}
namespace UnityEngine.Rendering.Universal.Internal
{
 internal sealed class MainLightShadowCasterPass
 {
  internal sealed class PassData{internal readonly UniversalCameraData cameraData=new();}
  internal bool m_CreateEmptyShadowmap=>false;internal int m_ShadowCasterCascadesCount=>4;internal int m_RenderTargetWidth=>16384;internal int m_RenderTargetHeight=>16384;
  internal readonly ShadowSliceData[] m_CascadeSlices=new ShadowSliceData[4];
  internal MainLightShadowCasterPass(){for(int i=0;i<4;i++)m_CascadeSlices[i]=new(){resolution=8192,offsetX=i%2*8192,offsetY=i/2*8192,shadowTransform=new(){m00=.5f,m11=.5f,m22=1,m33=1,m03=i%2*.5f,m13=i/2*.5f}};}
  public void RenderMainLightCascadeShadowmap(RasterCommandBuffer cmd,ref PassData data,bool graph){}
 }
}
