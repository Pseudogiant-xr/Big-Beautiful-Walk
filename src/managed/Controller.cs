using System;
using UnityEngine;

namespace BigWalkDLSS;

public sealed class Controller : MonoBehaviour
{
    public Controller(IntPtr pointer) : base(pointer) { }
    public void Update()
    {
        try { AdvancedGraphics.Update(); Plugin.Tick(); PerformanceCapture.Tick(); SmallPlantShadows.Tick(); }
        catch (Exception error) { Plugin.StopAfterError(error); }
    }
    public void LateUpdate() => AdvancedGraphics.LateUpdate();
    public void OnGUI() => AdvancedGraphics.Draw();
    public void OnApplicationFocus(bool focused) { if (!focused) { PerformanceCapture.FocusChanged(false); AdvancedGraphics.Close(); } }
    public void OnApplicationQuit() { PerformanceCapture.Shutdown(); SmallPlantShadows.Shutdown(); AdvancedGraphics.Shutdown(); VegetationShadows.Shutdown(); Plugin.Restore(); }
}
