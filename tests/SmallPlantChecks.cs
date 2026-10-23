using System;
using System.Collections.Generic;
using BigWalkDLSS;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.Rendering;

void Check(bool value,string message){if(!value)throw new Exception(message);}
const string supported="BOXOPHOBIC/The Visual Engine/Geometry/House Standard Lit";
MeshRenderer Make(string shader,ShadowCastingMode mode=ShadowCastingMode.Off)=>new(shader,mode);
try
{
 if(Array.IndexOf(args,"--unload")>=0)
 {
  SmallPlantShadows.Initialize(new ConfigEntry<bool>(true),true);
  var renderer=Make(supported);var root=new GameObject("Unloaded_Grass",renderer);SmallPlantShadows.ApplyPrefab(root);
  root.Destroyed=renderer.Destroyed=true;Time.unscaledTime=2;SmallPlantShadows.Tick();
  foreach(string field in new[]{"Seen","Changed"}){object cache=typeof(SmallPlantShadows).GetField(field,System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static)!.GetValue(null)!;Check((int)cache.GetType().GetProperty("Count")!.GetValue(cache)! == 0,"Destroyed objects retained in "+field);}
  SmallPlantShadows.Shutdown();Console.WriteLine("PASS destroyed roots/renderers pruned after unload");return 0;
 }
 if(Array.IndexOf(args,"--root-cache")>=0||Array.IndexOf(args,"--pointer-reuse")>=0)
 {
  SmallPlantShadows.Initialize(new ConfigEntry<bool>(true),true);
  GameObject? last=null;
  int count=Array.IndexOf(args,"--root-cache")>=0?600:1;
  for(int i=0;i<count;i++){last=new GameObject("Cache_Grass_"+i,Make(supported));SmallPlantShadows.ApplyPrefab(last);}
  if(count>1)
  {
   object cache=typeof(SmallPlantShadows).GetField("Seen",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static)!.GetValue(null)!;
   int cached=(int)cache.GetType().GetProperty("Count")!.GetValue(cache)!;
   Check(cached<=512,"Root cache exceeded its lifetime bound");
  }
  var renderer=Make(supported);var replacement=new GameObject("Replacement_Grass",renderer);replacement.Pointer=last!.Pointer;
  SmallPlantShadows.ApplyPrefab(replacement);
  Check(renderer.shadowCastingMode==ShadowCastingMode.On,"Reused raw pointer suppressed a replacement prefab");
  SmallPlantShadows.Shutdown();Console.WriteLine("PASS bounded roots and pointer replacement");return 0;
 }
 var request=new ConfigEntry<bool>(false);
 var off=Make(supported);var already=Make(supported,ShadowCastingMode.TwoSided);var unsupported=Make("Unrelated/NoShadowPass");
 var plant=new GameObject("BlueTussockGrass",off,already,unsupported);
 SmallPlantShadows.Initialize(request,true);SmallPlantShadows.Request(true);SmallPlantShadows.ApplyPrefab(plant);
 Check(!SmallPlantShadows.StartupEnabled&&off.shadowCastingMode==ShadowCastingMode.Off,"Queued request changed this launch");
 SmallPlantShadows.Shutdown();SmallPlantShadows.Initialize(request,true);
 Check(HarmonyLib.Harmony.Patches==3,"Pre-registration hooks were not installed");
 var qualityRenderer=Make(supported);var detailRenderer=Make(supported);var instanceRenderer=Make(supported);
 var quality=new TerrainQualityManager();quality.floraQualityData!.entries.Add(new Entry{prefab=new GameObject("Quality_Bush",qualityRenderer)});
 HarmonyLib.Harmony.Prefixes["Initialize"].Invoke(quality);
 var terrain=new Terrain();terrain.terrainData!.detailPrototypes=new[]{new Detail{prototype=new GameObject("Detail_Grass",detailRenderer)}};
 HarmonyLib.Harmony.Prefixes["RegisterTerrain"].Invoke(terrain);
 HarmonyLib.Harmony.Prefixes["OnEnable"].Invoke(new MA.Flora.FloraInstanceRenderer{m_Prefab=new GameObject("Instance_Shrub",instanceRenderer)});
 Check(qualityRenderer.shadowCastingMode==ShadowCastingMode.On&&detailRenderer.shadowCastingMode==ShadowCastingMode.On&&instanceRenderer.shadowCastingMode==ShadowCastingMode.On,"A source registration prefix did not apply its prefab flags");
 Check(request.ConfigFile.Saves==1,"Queued request was not explicitly saved");
 SmallPlantShadows.ApplyPrefab(plant);
 Check(off.shadowCastingMode==ShadowCastingMode.On,"Supported disabled grass did not request casting");
 Check(already.shadowCastingMode==ShadowCastingMode.TwoSided&&unsupported.shadowCastingMode==ShadowCastingMode.Off,"Existing/unsupported renderer changed");
 var delayed=Make(supported);delayed.MaterialsReady=false;var delayedPrefab=new GameObject("Delayed_Bush",delayed);
 SmallPlantShadows.ApplyPrefab(delayedPrefab);delayed.MaterialsReady=true;SmallPlantShadows.ApplyPrefab(delayedPrefab);
 Check(delayed.shadowCastingMode==ShadowCastingMode.On,"A temporarily missing material permanently suppressed the source");
 var unrelated=Make(supported);SmallPlantShadows.ApplyPrefab(new GameObject("Building",unrelated));
 Check(unrelated.shadowCastingMode==ShadowCastingMode.Off,"Non-plant object was modified");
 var external=Make(supported);SmallPlantShadows.ApplyPrefab(new GameObject("Test_Bush",external));external.shadowCastingMode=ShadowCastingMode.TwoSided;
 var failing=Make(supported);failing.ThrowAfterWrite=true;
 try{SmallPlantShadows.ApplyPrefab(new GameObject("Test_Fern",failing));throw new Exception("Expected injected setter failure");}
 catch(InvalidOperationException){}
 failing.ThrowAfterWrite=false;
 SmallPlantShadows.Shutdown();
 Check(off.shadowCastingMode==ShadowCastingMode.Off&&failing.shadowCastingMode==ShadowCastingMode.Off,"Owned/partially written flag did not restore");
 Check(external.shadowCastingMode==ShadowCastingMode.TwoSided,"External flag update was overwritten on restore");
 Check(SmallPlantShadows.SupportedShader("Flora/TerrainGrass")&&!SmallPlantShadows.SupportedShader("Unknown"),"Shader support policy changed");
 Console.WriteLine("Small-plant checks passed: restart-only queue, 3 registration hooks, supported/off-only policy, existing flags, unrelated objects, partial-write rollback and external ownership.");return 0;
}
catch(Exception error){Console.Error.WriteLine("FAIL "+error.Message);return 1;}
namespace BepInEx.Configuration{internal sealed class ConfigEntry<T>(T value){internal T Value{get;set;}=value;internal FakeConfig ConfigFile{get;}=new();}}
internal sealed class FakeConfig{internal int Saves;internal void Save(){Saves++;}}
namespace BigWalkDLSS
{
 internal sealed class Log{internal void LogInfo(string value){}internal void LogWarning(string value){}}
 internal sealed class Host{internal readonly Log Log=new();}internal static class Plugin{internal static readonly Host Instance=new();}
}
namespace HarmonyLib
{
 internal sealed class Harmony(string id){internal static int Patches;internal static readonly Dictionary<string,HarmonyMethod> Prefixes=new();internal void Patch(object? method,HarmonyMethod prefix){Patches++;Prefixes[((System.Reflection.MethodInfo)method!).Name]=prefix;}internal void UnpatchSelf(){}}
 internal sealed class HarmonyMethod(Type type,string name){internal void Invoke(object source)=>type.GetMethod(name,System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic)!.Invoke(null,new[]{source});}
 internal static class AccessTools{internal static object? Method(Type type,string name,Type[]? args=null)=>type.GetMethod(name,System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic,null,args??Type.EmptyTypes,null);}
}
namespace Il2CppInterop.Runtime{internal static class Il2CppType{internal static Type Of<T>()=>typeof(T);}}
namespace UnityEngine.Rendering{internal enum ShadowCastingMode{Off,On,TwoSided}}
namespace UnityEngine
{
 internal static class Time{internal static float unscaledTime;}
 internal abstract class Component{private static long next;internal IntPtr Pointer=(IntPtr)(++next);private readonly int identity=(int)next;internal int GetInstanceID()=>identity;internal bool Destroyed;
 public static bool operator==(Component? a,Component? b){bool an=ReferenceEquals(a,null)||a.Destroyed;bool bn=ReferenceEquals(b,null)||b.Destroyed;return an||bn?an==bn:ReferenceEquals(a,b);}
 public static bool operator!=(Component? a,Component? b)=>!(a==b);public override bool Equals(object? value)=>ReferenceEquals(this,value);public override int GetHashCode()=>identity;
 internal T? TryCast<T>()where T:class=>this as T;}
 internal sealed class GameObject(string title,params MeshRenderer[] values):Component{internal string name=>title;internal Component[] GetComponentsInChildren(Type type,bool includeInactive)=>values;}
 internal sealed class Shader(string title){internal string name=>title;}
 internal sealed class Material(string shaderName){internal Shader shader=>new(shaderName);}
 internal sealed class MeshRenderer(string shader,ShadowCastingMode initial):Component
 {
  private ShadowCastingMode mode=initial;internal bool ThrowAfterWrite;
  internal ShadowCastingMode shadowCastingMode{get=>mode;set{mode=value;if(ThrowAfterWrite)throw new InvalidOperationException("Injected setter failure");}}
  internal bool MaterialsReady=true;internal Material[] sharedMaterials=>MaterialsReady?new[]{new Material(shader)}:Array.Empty<Material>();
 }
 internal sealed class Terrain{internal TerrainData? terrainData=new();}
 internal sealed class TerrainData{internal Detail[] detailPrototypes=Array.Empty<Detail>();internal Tree[] treePrototypes=Array.Empty<Tree>();}
 internal sealed class Detail{internal GameObject? prototype;}internal sealed class Tree{internal GameObject? prefab;}
}
internal sealed class TerrainQualityManager{internal Quality? floraQualityData=new();internal void Initialize(){}}
internal sealed class Quality{internal List<Entry> entries=new();}internal sealed class Entry{internal GameObject? prefab;}
namespace MA.Flora
{
 internal sealed class FloraSystem{internal void RegisterTerrain(Terrain terrain){}}
 internal sealed class FloraInstanceRenderer{internal GameObject? m_Prefab;internal void OnEnable(){}}
}
