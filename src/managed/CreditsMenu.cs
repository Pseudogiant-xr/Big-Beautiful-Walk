using UnityEngine;

namespace BigWalkDLSS;

internal static class CreditsMenu
{
    internal static void Draw(float x, float y, GUIStyle label, GUIStyle small, GUIStyle button)
    {
        GUI.Label(new Rect(x + 25, y + 185, 900, 30), "Credits and licences", label);
        GUI.Label(new Rect(x + 25, y + 225, 900, 48),
            "Big Beautiful Walk is an unofficial community mod. Original project code is licensed under Apache 2.0; third-party code and runtimes retain their own licences.", small);
        GUI.Label(new Rect(x + 25, y + 283, 900, 48),
            "Post-processing adapts Unity Technologies' Universal Render Pipeline code under the Unity Companion License. Big Walk is developed by House House.", small);
        GUI.Label(new Rect(x + 25, y + 341, 900, 48),
            "Loader and interoperability: BepInEx contributors, HarmonyX, Il2CppInterop, Cpp2IL, MonoMod and UnityDoorstop, with their upstream dependencies.", small);
        GUI.Label(new Rect(x + 25, y + 399, 900, 48),
            "Optional integration: OptiScaler contributors and the DLSS 5 work by Dagherbou and Skynizz, including RenoDX and the libraries credited in the accompanying notices.", small);
        GUI.Label(new Rect(x + 25, y + 457, 900, 48),
            "Core integration uses the NVIDIA DLSS / NGX SDK. Optional frame generation uses NVIDIA Streamline, AMD FidelityFX or Intel XeSS / XeLL technology under their respective terms.", small);
        GUI.Label(new Rect(x + 25, y + 515, 900, 55),
            "Full copyright statements, licence texts and pinned sources are included in the release and installed under BepInEx/BigWalkDLSS/notices. A Neural Rendering model is not included.", small);
        GUI.Label(new Rect(x + 25, y + 588, 900, 42),
            "Product names identify their respective owners. No affiliation or endorsement is implied. See THIRD_PARTY_NOTICES.md for component-specific terms.", small);
    }
}
