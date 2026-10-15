namespace BigWalkDLSS;

internal static class UpscalingOptions
{
    internal static readonly int[] QualityValues = { 5, 4, 2, 1, 0, 3 };
    internal static readonly string[] QualityNames = { "DLAA (native)", "Ultra Quality (experimental)", "Quality", "Balanced", "Performance", "Ultra Performance" };
    internal static readonly int[] PresetValues = { 0, 10, 11, 12, 13, 5, 6 };
    internal static readonly string[] PresetNames = { "Default", "J", "K", "L", "M", "E (legacy)", "F (legacy)" };
    internal static float Scale(int quality) => quality switch
    {
        5 => 1f,
        4 => .77f,
        2 => .666667f,
        1 => .58f,
        0 => .5f,
        3 => .333333f,
        _ => .666667f
    };
    internal static bool ValidPreset(int preset) => preset == 0 || preset == 5 || preset == 6 || (preset >= 10 && preset <= 13);
}
