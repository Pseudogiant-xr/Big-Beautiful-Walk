using System;
using BigWalkDLSS;

static void Check(bool value, string reason) { if (!value) throw new Exception(reason); }
Check(UpscalingOptions.Scale(5) == 1f, "DLAA must use native resolution");
Check(UpscalingOptions.Scale(3) > .33f && UpscalingOptions.Scale(3) < .34f, "Ultra Performance ratio incorrect");
Check(UpscalingOptions.Scale(1) < UpscalingOptions.Scale(2), "Balanced must render below Quality");
Check(!UpscalingOptions.ValidPreset(7) && !UpscalingOptions.ValidPreset(9) && !UpscalingOptions.ValidPreset(14), "Reserved preset exposed");
foreach(int preset in UpscalingOptions.PresetValues) Check(UpscalingOptions.ValidPreset(preset), "UI preset rejected");
foreach(int backend in new[] { 1, 2, 3, -1 })
{
    Check(!UpscalingPresentation.QualityName(backend, 5).Contains("DLAA"), "Other upscaler labelled DLAA");
    Check(!UpscalingPresentation.ActiveStatus(backend, 2).Contains("DLSS"), "Other upscaler status labelled DLSS");
}
Check(UpscalingPresentation.QualityName(0, 5).Contains("DLAA"), "DLSS native AA lost its name");
Check(Compatibility.Check("6000.3.17f1", "1.6.0 2609301522", true, false) == "", "Tested game rejected");
Check(Compatibility.Check("6000.3.17f1", "1.6.0 2610010000", true, false).Length > 0, "Different build stamp accepted");
Check(Compatibility.Check("6000.4.0f1", "1.7.0", true, false).Length > 0, "Untested update accepted");
Check(Compatibility.Check("6000.4.0f1", "1.7.0", true, true) == "", "Explicit version override ignored");
Check(Compatibility.Check("6000.3.17f1", "1.6.0", false, true).Length > 0, "Version override bypassed API restriction");
foreach (string preset in new[] { "Balanced", "Quality", "Extreme" })
{
    var values = GraphicsPresets.Values(preset, 16384);
    float distance = values["Shadows.Distance"];
    float previous = 0;
    for (int index = 1; index <= 3; index++)
    {
        float split = values["Shadows.Split4_" + index];
        Check(split > previous && split < 1, "Preset cascade order invalid");
        if (index == 1) Check(Math.Abs(split * distance - 25) < .001f, "Preset lost near coverage");
        previous = split;
    }
    Check(values["Vegetation.ShadowDistance"] == distance, "Vegetation and main range differ");
}
Check(GraphicsPresets.Values("Extreme", 8192)["Shadows.MainResolution"] == 8192, "16K selected on unsupported hardware");
var extreme = GraphicsPresets.Values("Extreme", 16384);
Check(extreme["Shadows.MainResolution"] == 16384 && extreme["Shadows.Distance"] == 2000 &&
    extreme["Vegetation.ShadowDistance"] == 2000 && extreme["Shadows.Cascades"] == 4 && extreme["Geometry.LodBias"] == 4,
    "Experimental performance work reduced Extreme's image-quality settings");
Check(!extreme.ContainsKey("Shadows.ConservativeCulling"), "Experimental culling must remain a separate opt-in");
var captured = new[] { .067f, .2f, .467f };
var restored = GraphicsLimits.CascadeSplits(captured);
for (int i = 0; i < captured.Length; i++) Check(restored[i] == captured[i], "Captured splits changed");
var random = new Random(7481);
for (int iteration = 0; iteration < 20000; iteration++)
{
    float[] input = iteration == 0 ? new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity } :
        new[] { (float)random.NextDouble() * 3 - 1, (float)random.NextDouble() * 3 - 1, (float)random.NextDouble() * 3 - 1 };
    float previous = 0;
    foreach (float split in GraphicsLimits.CascadeSplits(input))
    { Check(float.IsFinite(split) && split > previous && split < 1, "Cascade bounds invalid"); previous = split; }
}
Console.WriteLine("Graphics checks passed: compatibility gates, preset coverage/hardware fallback, captured restoration, 20000 cascade cases.");
