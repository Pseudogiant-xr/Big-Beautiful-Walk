using System;
using System.Collections.Generic;
using BigWalkDLSS;

int assertions = 0;
void Check(bool value, string reason) { assertions++; if (!value) throw new Exception(reason); }
void Apply(IOverrideProperty[] properties, Action write)
{
    foreach (var property in properties) property.Observe();
    write();
    foreach (var property in properties) property.Applied();
}

// Raw zero fade distances with the mode disabled are a valid captured state.
float mode = 0, min = 0, max = 0;
bool keyword = false, pass = true;
int restoredWrites = 0;
var material = new IOverrideProperty[]
{
    new OverrideProperty<float>(() => mode, value => { mode = value; restoredWrites++; }),
    new OverrideProperty<float>(() => min, value => { min = value; restoredWrites++; }),
    new OverrideProperty<float>(() => max, value => { max = value; restoredWrites++; }),
    new OverrideProperty<bool>(() => keyword, value => { keyword = value; restoredWrites++; }),
    new OverrideProperty<bool>(() => pass, value => { pass = value; restoredWrites++; })
};
foreach (float distance in new[] { 300f, 1000f, 2000f })
    Apply(material, () => { mode = 1; min = distance - 50; max = distance; keyword = true; });
foreach (var property in material) property.Restore();
Check(mode == 0 && min == 0 && max == 0 && !keyword && pass, "Preset cycles lost the raw material baseline");
Check(restoredWrites == 4, "An untouched pass was written on restore");
foreach (var property in material) property.Restore();
Check(restoredWrites == 4, "Second restore wrote a released property");

// An outside quality change is preserved whether it happens just before restore
// or is observed and overridden again first.
Apply(material, () => { mode = 1; min = 950; max = 1000; keyword = true; });
min = 75; max = 100; mode = 2;
Check(material[0].Changed() && material[1].Changed() && material[2].Changed(), "Outside material changes were not detected");
foreach (var property in material) property.Restore();
Check(mode == 2 && min == 75 && max == 100, "Restore overwrote newer outside values");
Check(!keyword && pass, "Outside changes discarded ownership of independent properties");
Apply(material, () => { mode = 1; min = 1950; max = 2000; keyword = true; });
mode = 3; min = 150; max = 200; keyword = false;
Apply(material, () => { mode = 1; min = 950; max = 1000; keyword = true; });
foreach (var property in material) property.Restore();
Check(mode == 3 && min == 150 && max == 200 && !keyword, "Reapply failed to advance the outside baseline");

// Two component aliases share one parameter identity, captured before either write.
float parameter = 0;
bool overrideState = false;
int parameterWrites = 0;
var parameters = new Dictionary<long, (OverrideProperty<float> Distance, OverrideProperty<bool> State)>();
void ApplyParameter(long identity, float distance)
{
    if (!parameters.TryGetValue(identity, out var snapshot))
    {
        snapshot = (new OverrideProperty<float>(() => parameter, value => { parameter = value; parameterWrites++; }),
            new OverrideProperty<bool>(() => overrideState, value => { overrideState = value; parameterWrites++; }));
        parameters.Add(identity, snapshot);
    }
    Apply(new IOverrideProperty[] { snapshot.Distance, snapshot.State }, () => { parameter = distance; overrideState = true; });
}
ApplyParameter(42, 300);
ApplyParameter(42, 1000);
ApplyParameter(42, 2000);
Check(parameters.Count == 1, "Shared parameter acquired duplicate snapshots");
parameters[42].Distance.Restore(); parameters[42].State.Restore();
Check(parameter == 0 && !overrideState && parameterWrites == 2, "Shared parameter restoration was contaminated by a preset");
ApplyParameter(42, 1000);
parameter = 125;
ApplyParameter(42, 2000);
parameters[42].Distance.Restore(); parameters[42].State.Restore();
Check(parameter == 125 && !overrideState, "A distance-only outside write changed the untouched flag baseline");

// Outside writers can disable the override state without their choice being lost.
ApplyParameter(42, 1000);
overrideState = false;
parameters[42].Distance.Restore(); parameters[42].State.Restore();
Check(parameter == 125 && !overrideState, "Restore overwrote an outside flag change");

float untouched = 12;
int untouchedWrites = 0;
var unchanged = new OverrideProperty<float>(() => untouched, value => { untouched = value; untouchedWrites++; });
Apply(new IOverrideProperty[] { unchanged }, () => { });
Check(!unchanged.Restore() && untouchedWrites == 0, "Unchanged helper property was claimed or written");
Console.WriteLine($"Vegetation restore checks passed: {assertions} assertions; raw mode/fade/keyword state, repeated presets, shared parameters, outside changes, untouched properties.");
