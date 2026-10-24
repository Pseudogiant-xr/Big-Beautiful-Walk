// CPU-only regression. No Unity APIs, native game libraries, detour installation or GPU calls.
// With GameDir, invoke the installed generator and enter its reverse-P/Invoke thunk via calli.
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Text.RegularExpressions;

if (args.Length > 2)
{
    Console.Error.WriteLine("Usage: NativeObserverBoundaryChecks [GameDir [SourceRoot]]");
    return 2;
}
try
{
    BoundaryChecks.CheckPortableBoundary();
    if (args.Length > 0) BoundaryChecks.CheckInstalledTrampoline(Path.GetFullPath(args[0]));
    else Console.WriteLine("SKIP installed generator: pass GameDir; portable callback simulation is not native Unity validation.");
    BoundaryChecks.CheckObserverSources(args.Length > 1 ? Path.GetFullPath(args[1]) : BoundaryChecks.FindSourceRoot());
    return 0;
}
catch (Exception error)
{
    Console.Error.WriteLine("FAIL " + error);
    return 1;
}

public static class BoundaryChecks
{
    private static readonly Exception OriginalFailure = new InvalidOperationException("original shadow draw failed");
    private static int _originalCalls;
    private static int _finalizerCalls;
    private static Exception? _finalizerFailure;
    private static bool _failOriginal;

    private static void Check(bool condition, string reason)
    {
        if (!condition) throw new InvalidOperationException(reason);
    }

    // Static void/no arguments deliberately avoids native class, instance, argument and return marshalling.
    public static void Original()
    {
        _originalCalls++;
        if (_failOriginal) throw OriginalFailure;
    }

    private static Exception? PreservingFinalizer(Exception? error)
    {
        _finalizerCalls++;
        _finalizerFailure = error;
        return error;
    }

    public static void OldObserverReplacement()
    {
        Exception? failure = null;
        try { Original(); }
        catch (Exception error) { failure = error; }
        var returned = PreservingFinalizer(failure);
        if (returned != null) ExceptionDispatchInfo.Capture(returned).Throw();
    }

    private static Action CatchLogTrampoline(Action target, List<Exception> log) => () =>
    {
        try { target(); }
        catch (Exception error) { log.Add(error); }
    };

    private static Exception? RenderGraphCaller(Action draw)
    {
        try { draw(); return null; }
        catch (Exception error) { return error; }
    }

    public static void CheckPortableBoundary()
    {
        var log = new List<Exception>();
        _failOriginal = true;
        _originalCalls = _finalizerCalls = 0;
        var failure = RenderGraphCaller(CatchLogTrampoline(OldObserverReplacement, log));
        Check(failure == null && _originalCalls == 1 && _finalizerCalls == 1,
            "Old observer did not reproduce the outer boundary swallowing the draw failure");
        Check(ReferenceEquals(_finalizerFailure, OriginalFailure) && log.Count == 1 && ReferenceEquals(log[0], OriginalFailure),
            "Finalizer must return the exact original failure even though the outer trampoline consumes it");

        // Observation is within the already-owned postprocess record callback; no shadow draw is wrapped.
        Action nativeDrawPath = Original;
        Action beforeObservation = nativeDrawPath;
        int snapshots = 0;
        int callsBeforeRecord = _originalCalls;
        void ExistingOwnedRecordCallback(Action observe)
        {
            try { observe(); }
            catch (Exception error) { log.Add(error); }
        }
        ExistingOwnedRecordCallback(() => snapshots++);
        Check(snapshots == 1 && _originalCalls == callsBeforeRecord && _finalizerCalls == 1,
            "Graph-record observation must neither execute nor finalize the shadow draw");
        Check(ReferenceEquals(nativeDrawPath, beforeObservation), "Observation replaced the native draw path");
        failure = RenderGraphCaller(nativeDrawPath);
        Check(ReferenceEquals(failure, OriginalFailure) && _originalCalls == callsBeforeRecord + 1,
            "Without an observer detour the caller must observe the exact original failure");
        var observerFailure = new Exception("snapshot read failed");
        ExistingOwnedRecordCallback(() => throw observerFailure);
        Check(ReferenceEquals(log[^1], observerFailure) && ReferenceEquals(RenderGraphCaller(nativeDrawPath), OriginalFailure),
            "A failed graph-record observation changed subsequent native draw error propagation");
        Console.WriteLine("PASS portable boundary: preserving finalizer still loses failure through catch/log; record-only observation preserves original path/failure.");
    }

    public static string FindSourceRoot()
    {
        foreach (string start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
            for (var directory = new DirectoryInfo(start); directory != null; directory = directory.Parent)
                if (File.Exists(Path.Combine(directory.FullName, "src/managed/ShadowReceiverProbe.cs"))) return directory.FullName;
        throw new DirectoryNotFoundException("Cannot find observer source root; pass GameDir and SourceRoot explicitly.");
    }

    public static void CheckObserverSources(string root)
    {
        // This source gate complements the executable boundary test: even an exception-preserving
        // prefix/finalizer is unsafe when an additional native-to-managed trampoline surrounds it.
        foreach (string file in new[] { "ShadowReceiverProbe.cs", "ShadowPassProbe.cs" })
        {
            string source = File.ReadAllText(Path.Combine(root, "src/managed", file));
            string code = Regex.Replace(source, "//[^\\r\\n]*|/\\*[\\s\\S]*?\\*/|\"(?:\\\\.|[^\"\\\\])*\"", " ");
            Check(!Regex.IsMatch(code, @"\b(Harmony|HarmonyMethod|AccessTools)\b|\.\s*(Patch|PatchAll|CreateAndPatchAll|UnpatchSelf)\s*\("),
                file + " still owns a Harmony observer hook; the native catch/log boundary can suppress original failures");
        }
        Console.WriteLine("PASS production source gate: neither receiver nor producer observer owns Harmony hook installation/removal.");
    }

    public static unsafe void CheckInstalledTrampoline(string game)
    {
        // Refuse native imports from every installed assembly loaded by this harness. The real
        // generator is invoked, but its patcher constructor/Init/DetourTo/CopyOriginal are never run.
        Assembly? Resolve(AssemblyLoadContext context, AssemblyName name)
        {
            foreach (string folder in new[] { "BepInEx/core", "BepInEx/interop", "dotnet" })
            {
                string file = Path.Combine(game, folder, name.Name + ".dll");
                if (!File.Exists(file)) continue;
                var assembly = context.LoadFromAssemblyPath(file);
                NativeLibrary.SetDllImportResolver(assembly, (library, _, _) =>
                    throw new InvalidOperationException("Native import prohibited in CPU harness: " + library));
                return assembly;
            }
            return null;
        }
        AssemblyLoadContext.Default.Resolving += Resolve;
        try
        {
            var support = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(game, "BepInEx/core/Il2CppInterop.HarmonySupport.dll"));
            NativeLibrary.SetDllImportResolver(support, (library, _, _) =>
                throw new InvalidOperationException("Native import prohibited in CPU harness: " + library));
            var patcherType = support.GetType("Il2CppInterop.HarmonySupport.Il2CppDetourMethodPatcher", true)!;
            var generator = patcherType.GetMethod("GenerateNativeToManagedTrampoline", BindingFlags.Instance | BindingFlags.NonPublic)!;
            Check(generator != null, "Installed trampoline generator not found");
            var original = typeof(BoundaryChecks).GetMethod(nameof(Original))!;
            var replacement = typeof(BoundaryChecks).GetMethod(nameof(OldObserverReplacement))!;
            object patcher = RuntimeHelpers.GetUninitializedObject(patcherType);
            patcherType.BaseType!.GetField("<Original>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(patcher, original);

            var common = AssemblyLoadContext.Default.LoadFromAssemblyName(new AssemblyName("Il2CppInterop.Common"));
            var loggerType = common.GetType("Il2CppInterop.Common.Logger", true)!;
            var loggerConstructor = loggerType.GetConstructors().Single();
            var loggerInterface = loggerConstructor.GetParameters()[0].ParameterType;
            var logger = DispatchProxy.Create(loggerInterface, typeof(RecordingLogger));
            var capture = (RecordingLogger)logger;
            using var installedLogger = (IDisposable)loggerConstructor.Invoke(new[] { logger });

            object definition = generator!.Invoke(patcher, new object[] { replacement })!;
            try
            {
                // Inspect the actual generated Cecil handler before entering reverse P/Invoke.
                // The throwing managed target must remain entirely inside its catch(Exception).
                dynamic generatedDefinition = definition.GetType().GetProperty("Definition")!.GetValue(definition)!;
                dynamic body = generatedDefinition.Body;
                var handlers = ((System.Collections.IEnumerable)body.ExceptionHandlers).Cast<object>().ToArray();
                Check(handlers.Length == 1, "Expected one generated exception handler");
                dynamic handler = handlers[0];
                Check(handler.HandlerType.ToString() == "Catch" && handler.CatchType.FullName == "System.Exception",
                    "Generated callback does not catch System.Exception; refusing reverse P/Invoke");
                bool protectsTarget = false;
                for (dynamic instruction = handler.TryStart; instruction != null && instruction != handler.TryEnd; instruction = instruction.Next)
                    if (instruction!.OpCode.ToString() == "call" && instruction.Operand.Name == replacement.Name &&
                        instruction.Operand.DeclaringType.FullName == replacement.DeclaringType!.FullName) protectsTarget = true;
                Check(protectsTarget, "Throwing target is not protected by the generated catch; refusing reverse P/Invoke");
                bool reports = false;
                for (dynamic instruction = handler.HandlerStart; instruction != null && instruction != handler.HandlerEnd; instruction = instruction.Next)
                {
                    string opcode = instruction!.OpCode.ToString();
                    Check(opcode != "throw" && opcode != "rethrow", "Generated catch rethrows; refusing reverse P/Invoke");
                    if (opcode == "call" && instruction.Operand != null && instruction.Operand.Name == "ReportException") reports = true;
                }
                Check(reports, "Generated catch does not call ReportException");

                // The installed MonoMod dynamic-method emitter uses CLR private field names
                // predating .NET 8. Its supported Cecil emitter runs the same generated IL.
                string? previousEmitter = Environment.GetEnvironmentVariable("MONOMOD_DMD_TYPE");
                MethodInfo generated;
                try
                {
                    Environment.SetEnvironmentVariable("MONOMOD_DMD_TYPE", "cecil");
                    var generate = definition.GetType().GetMethod("Generate", Type.EmptyTypes)!;
                    generated = (MethodInfo)generate.Invoke(definition, null)!;
                }
                finally { Environment.SetEnvironmentVariable("MONOMOD_DMD_TYPE", previousEmitter); }
                Check(generated.ReturnType == typeof(void) && generated.GetParameters().Length == 1 && generated.GetParameters()[0].ParameterType.IsPointer,
                    "Unexpected trampoline ABI; only void(Il2CppMethodInfo*) is allowed");
                var harmony = patcherType.BaseType.Assembly;
                var factoryType = harmony.GetType("HarmonyLib.DelegateTypeFactory", true)!;
                object factory = factoryType.GetField("instance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
                var createDelegateType = factoryType.GetMethod("CreateDelegateType", new[] { typeof(MethodInfo), typeof(CallingConvention) })!;
                var delegateType = (Type)createDelegateType.Invoke(factory, new object[] { generated, CallingConvention.Cdecl })!;
                var callback = generated.CreateDelegate(delegateType);
                var pointer = (delegate* unmanaged[Cdecl]<IntPtr, void>)(void*)Marshal.GetFunctionPointerForDelegate(callback);

                _originalCalls = _finalizerCalls = 0;
                _failOriginal = false;
                pointer(IntPtr.Zero);
                Check(_originalCalls == 1 && _finalizerCalls == 1 && capture.Errors.Count == 0,
                    "Installed success callback did not return normally");
                _failOriginal = true;
                pointer(IntPtr.Zero);
                Check(_originalCalls == 2 && _finalizerCalls == 2 && ReferenceEquals(_finalizerFailure, OriginalFailure),
                    "Installed trampoline did not execute the preserving finalizer");
                Check(capture.Errors.Count == 1 && ReferenceEquals(capture.Errors[0], OriginalFailure),
                    "Installed trampoline did not log and swallow the exact original failure");
                GC.KeepAlive(callback);
                Console.WriteLine("PASS installed " + support.GetName().Name + " " + support.GetName().Version +
                    ": real generator (Cecil emitter on .NET 8) + unmanaged calli/reverse-P/Invoke returned after logging original failure; no Unity/native library calls.");
            }
            finally { (definition as IDisposable)?.Dispose(); }
        }
        finally { AssemblyLoadContext.Default.Resolving -= Resolve; }
    }
}

public class RecordingLogger : DispatchProxy
{
    public readonly List<Exception> Errors = new();
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (targetMethod!.Name == "IsEnabled") return true;
        if (targetMethod.Name == "Log" && args![3] is Exception error) Errors.Add(error);
        return null;
    }
}
