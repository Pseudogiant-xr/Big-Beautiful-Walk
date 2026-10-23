using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BigWalkDLSS;
using UnityEngine;
using Unity.Profiling;

void Check(bool value,string message){if(!value)throw new Exception(message);}
string parent=Path.GetFullPath(Path.GetTempPath());
string root=Path.GetFullPath(Path.Combine(parent,"bbw-performance-"+Guid.NewGuid().ToString("N")));
BepInEx.Paths.BepInExRootPath=root;
try
{
    PerformanceCapture.Begin();
    Check(PerformanceCapture.Active,"Capture did not start");
    int created=ProfilerRecorder.All.Count;
    PerformanceCapture.Begin();Check(ProfilerRecorder.All.Count==created,"Second begin leaked counters");
    Time.realtimeSinceStartupAsDouble=2;PerformanceCapture.Tick();
    Check(!Directory.Exists(root),"Warm-up wrote samples prematurely");
    Time.realtimeSinceStartupAsDouble=3.1;Time.unscaledDeltaTime=.01f;PerformanceCapture.Tick();
    Time.realtimeSinceStartupAsDouble=4.2;Time.unscaledDeltaTime=.02f;PerformanceCapture.Tick();
    Time.realtimeSinceStartupAsDouble=33.1;PerformanceCapture.Tick();
    Check(!PerformanceCapture.Active && ProfilerRecorder.All.All(d=>d.Disposed),"Completed capture leaked recorders");
    string[] files=Directory.GetFiles(Path.Combine(root,"BigWalkDLSS"));Check(files.Length==1,"Unexpected output files");
    string data=File.ReadAllText(files[0]);
    Check(data.Contains("frames=2; mean_frame_ms=15; mean_game_FPS=66.667; p95_frame_ms=20"),"Frame-time summary is incorrect");
    string[] rows=data.Split((char)10).Where(line=>line.Length>0 && !line.StartsWith('#')).ToArray();
    Check(rows.Length==3,"Sample count differs from summary");
    string[] headings=rows[0].Split(','), first=rows[1].Split(',');
    Check(first[1]=="30" && first[2]=="1" && first[4]=="3","Sun/camera context missing");
    int unavailable=Array.FindIndex(headings,h=>h.Contains("GPU Frame Time"));
    int zeroGpu=Array.FindIndex(headings,h=>h.Contains("Shadowmap GPU"));
    Check(first[unavailable]=="" && first[zeroGpu]=="","Unsupported/zero GPU timing was presented as measured data");
    Check(Plugin.RenderScale.Writes==0,"Capture altered rendering settings");
    PerformanceCapture.Begin();Application.isFocused=false;PerformanceCapture.Tick();
    Check(!PerformanceCapture.Active && PerformanceCapture.Status.Contains("cancelled") && ProfilerRecorder.All.All(d=>d.Disposed),"Focus-loss cancellation leaked recorders");
    Check(Directory.GetFiles(Path.Combine(root,"BigWalkDLSS")).Length==1,"Cancelled capture wrote a misleading file");
    Console.WriteLine("Performance capture checks passed: warm-up, 30-second deadline, mean/p95, missing GPU data, context, no setting writes, cancellation and cleanup.");
    return 0;
}
catch(Exception error){Console.Error.WriteLine("FAIL "+error.Message);return 1;}
finally
{
    PerformanceCapture.Shutdown();
    if(!root.StartsWith(parent,StringComparison.OrdinalIgnoreCase)||!Path.GetFileName(root).StartsWith("bbw-performance-"))throw new Exception("Unsafe fixture cleanup");
    if(Directory.Exists(root))Directory.Delete(root,true);
}
namespace BepInEx{internal static class Paths{internal static string BepInExRootPath="";}}
namespace BigWalkDLSS
{
 internal sealed class Setting<T>(T value){internal int Writes; internal T Value{get=>value;set{Writes++;throw new Exception("Unexpected setting write");}}}
 internal sealed class Log{internal void LogInfo(string text){} internal void LogWarning(string text){}}
 internal sealed class Host{internal readonly Log Log=new();}
 internal static class Plugin{internal static readonly Host Instance=new();internal static readonly Setting<float> RenderScale=new(.5f);internal static Camera RenderingCamera=>Camera.main;}
 internal static class BridgeSettings{internal static int RunningBackend=>0;}
 internal sealed class Feature{internal bool Enabled=>false;}
 internal static class FeatureSettings{internal static readonly Feature StartupFrameGen=new(),StartupNeural=new();}
}
internal sealed class TerrainQualityManager{internal static TerrainQualityManager? instance=>null;internal MA.Flora.FloraRenderSettings? GetFloraRenderSettings()=>null;}
namespace MA.Flora{internal sealed class FloraRenderSettings{internal Parameter? MinShadowLOD=>null;}internal sealed class Parameter{internal int value=>0;}internal static class FloraSystem{internal static string PerformBatchCullingMarkerName=>"Flora.Culling";}}
namespace UnityEngine
{
 internal readonly record struct Vector3(float x,float y,float z){internal static Vector3 zero=>new(0,0,0);}
 internal sealed class Transform{internal Vector3 position=new(1,2,3);internal Vector3 forward=new(0,-.5f,0);}
 internal sealed class Camera{internal static readonly Camera main=new();internal readonly Transform transform=new();}
 internal sealed class Light{internal readonly Transform transform=new();}
 internal static class RenderSettings{internal static readonly Light sun=new();}
 internal static class Application{internal static bool isFocused=true;}
 internal static class SystemInfo{internal static string graphicsDeviceName=>"Test GPU";internal static int graphicsMemorySize=>16384;}
 internal static class Screen{internal static int width=>3840;internal static int height=>2160;}
 internal static class Time{internal static double realtimeSinceStartupAsDouble;internal static float unscaledDeltaTime;}
}
namespace UnityEngine.Rendering
{
 internal static class GraphicsSettings{internal static readonly Universal.UniversalRenderPipelineAsset currentRenderPipeline=new();}
}
namespace UnityEngine.Rendering.Universal
{
 internal sealed class UniversalRenderPipelineAsset{internal float shadowDistance=>2000;internal int mainLightShadowmapResolution=>16384;internal bool supportsMainLightShadows=>true;internal int shadowCascadeCount=>4;internal bool supportsSoftShadows=>true;internal int softShadowQuality=>3;internal bool conservativeEnclosingSphere=>true;internal T? TryCast<T>()where T:class=>this as T;}
}
namespace Unity.Profiling.LowLevel
{
 internal enum ProfilerMarkerDataUnit{TimeNanoseconds,Count,Bytes}
}
namespace Unity.Profiling.LowLevel.Unsafe
{
 internal readonly record struct ProfilerRecorderHandle(string Name)
 {
  internal bool Valid=>Name!="GPU Frame Time";
  internal static unsafe ProfilerRecorderHandle GetByName_Unsafe(Unity.Profiling.ProfilerCategory category,char* pointer,int length)
  {GC.Collect();return new(new string(pointer,0,length));}
 }
}
namespace Unity.Profiling
{
 [Flags] internal enum ProfilerRecorderOptions{None=0,StartImmediately=1,Default=24,GpuRecorder=64}
 internal readonly struct ProfilerCategory{internal static ProfilerCategory Any=>new();}
 internal sealed class Data(string name,bool gpu){internal readonly string Name=name;internal readonly bool Gpu=gpu;internal bool Disposed;}
 internal struct ProfilerRecorder
 {
  internal static readonly List<Data> All=new();private readonly Data data;
  internal ProfilerRecorder(LowLevel.Unsafe.ProfilerRecorderHandle handle,int capacity,ProfilerRecorderOptions options){data=new(handle.Name,(options&ProfilerRecorderOptions.GpuRecorder)!=0);All.Add(data);}
  internal bool Valid=>!data.Disposed;internal int Count=>1;
  internal LowLevel.ProfilerMarkerDataUnit UnitType=>data.Name.EndsWith("Count")?LowLevel.ProfilerMarkerDataUnit.Count:data.Name.Contains("Memory")?LowLevel.ProfilerMarkerDataUnit.Bytes:LowLevel.ProfilerMarkerDataUnit.TimeNanoseconds;
  internal double LastValueAsDouble=>data.Gpu?0:UnitType==LowLevel.ProfilerMarkerDataUnit.Count?123:8000000;
  internal void Dispose(){data.Disposed=true;}
 }
}
