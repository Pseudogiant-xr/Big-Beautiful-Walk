using System;
using System.Runtime.InteropServices;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace BigWalkDLSS;

internal static class MaterialPassState
{
    // Unity 6000.3's string binding takes a UTF-16 pointer/length pair. Avoid
    // the generated wrapper's unavailable Il2CppSystem.ReadOnlySpan method.
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    private struct NativeSpan
    {
        [FieldOffset(0)] internal IntPtr Begin;
        [FieldOffset(8)] internal int Length;
    }
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private delegate bool GetPassDelegate(IntPtr material, IntPtr passName);
    private static readonly GetPassDelegate GetPass = IL2CPP.ResolveICall<GetPassDelegate>("UnityEngine.Material::GetShaderPassEnabled_Injected");
    private const string PassName = "ShadowCaster";

    internal static unsafe bool ReadShadowCaster(Material material)
    {
        if (IntPtr.Size != 8) throw new PlatformNotSupportedException("The material pass binding requires x64.");
        var native = UnityEngine.Object.MarshalledUnityObject.MarshalNotNull(material);
        if (native == IntPtr.Zero) throw new InvalidOperationException("Material has no native instance.");
        fixed (char* name = PassName)
        {
            var span = new NativeSpan { Begin = (IntPtr)name, Length = PassName.Length };
            return GetPass(native, (IntPtr)(&span));
        }
    }
}
