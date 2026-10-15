using System;

namespace BigWalkDLSS;

internal static class GraphicsLimits
{
    internal static float[] CascadeSplits(float[] values)
    {
        var result = new float[values.Length];
        const float gap = .005f;
        float previous = 0;
        for (int i = 0; i < values.Length; i++)
        {
            float value = float.IsFinite(values[i]) ? values[i] : previous + gap;
            result[i] = Math.Clamp(value, previous + gap, 1 - gap * (values.Length - i));
            previous = result[i];
        }
        return result;
    }
}
