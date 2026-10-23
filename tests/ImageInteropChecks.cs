// CPU-only symbol/JIT check against locally generated game assemblies; no Unity calls or game launch.
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
if (args.Length != 2) { Console.Error.WriteLine("Usage: ImageInteropChecks <GameDir> <BigWalkDLSS.dll>"); return 2; }
string game = Path.GetFullPath(args[0]);
AssemblyLoadContext.Default.Resolving += (context,name) => {
    foreach (string folder in new[] { "BepInEx/interop", "BepInEx/core" }) {
        string file=Path.Combine(game,folder,name.Name+".dll");
        if(File.Exists(file)) return context.LoadFromAssemblyPath(file);
    }
    return null;
};
var unity=AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(game,"BepInEx/interop/UnityEngine.ImageConversionModule.dll"));
var spanLoad=unity.GetType("UnityEngine.ImageConversion",true)!.GetMethods(BindingFlags.Public|BindingFlags.Static)
    .Single(m=>m.Name=="LoadImage" && m.GetParameters().Length==3 && m.GetParameters()[1].ParameterType.Name.StartsWith("ReadOnlySpan"));
try { RuntimeHelpers.PrepareMethod(spanLoad.MethodHandle); Console.WriteLine("Original Unity span wrapper JIT is supported by these interop assemblies"); }
catch(MissingMethodException error) when(error.Message.Contains("GetPinnableReference")) { Console.WriteLine("PASS reproduced exact reported MissingMethodException from real Unity span wrapper without calling Unity or starting the game"); }
var mod=AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(args[1]));
var fixedLoad=mod.GetType("BigWalkDLSS.MenuTheme",true)!.GetMethod("LoadPng",BindingFlags.NonPublic|BindingFlags.Static)!;
RuntimeHelpers.PrepareMethod(fixedLoad.MethodHandle);
Console.WriteLine("PASS fixed loader JIT resolves against the real installed interop assemblies; no Unity texture operation executed");

return 0;
