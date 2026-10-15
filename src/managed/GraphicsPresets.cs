using System.Collections.Generic;

namespace BigWalkDLSS;

internal static class GraphicsPresets
{
    // These are starting points, not hardware performance guarantees.
    internal static Dictionary<string, float> Values(string name, int textureLimit)
    {
        float distance = name == "Balanced" ? 300 : name == "Quality" ? 1000 : 2000;
        float atlas = name == "Balanced" ? 4096 : name == "Quality" ? 8192 : textureLimit >= 16384 ? 16384 : 8192;
        float third = name == "Balanced" ? 200 : name == "Quality" ? 350 : 400;
        return new Dictionary<string, float>
        {
            ["Geometry.LodBias"] = name == "Balanced" ? 2 : name == "Quality" ? 3 : 4,
            ["Shadows.Distance"] = distance,
            ["Shadows.MainEnabled"] = 1,
            ["Shadows.Cascades"] = 4,
            ["Shadows.Split4_1"] = 25 / distance,
            ["Shadows.Split4_2"] = 100 / distance,
            ["Shadows.Split4_3"] = third / distance,
            ["Shadows.MainResolution"] = atlas,
            ["Vegetation.ShadowDistance"] = distance,
            ["Vegetation.FadeWidth"] = name == "Balanced" ? 30 : 50
        };
    }
}
