namespace BigWalkDLSS;

internal static class Compatibility
{
    internal const string TestedUnity = "6000.3.17f1";
    internal const string TestedGame = "1.6.0 2609301522";

    internal static string Check(string unity, string game, bool direct3D11, bool allowUntested)
    {
        if (!direct3D11) return "Reconstruction requires DirectX 11. Restart with Steam launch option -force-d3d11.";
        if (!allowUntested && (unity != TestedUnity || game != TestedGame))
            return $"Untested game update ({game}, Unity {unity}). Rendering changes are disabled; check for a mod update.";
        return "";
    }
}
