using System;
using System.Collections.Generic;

namespace BigWalkDLSS;

// Capture values at the write boundary so an outside writer can replace the baseline.
internal sealed class OverrideOwnership<T>
{
    private T _original = default!, _lastApplied = default!;
    private bool _observed, _owned;
    private static bool Equal(T left, T right) => EqualityComparer<T>.Default.Equals(left, right);

    internal bool Changed(T current) => _observed && !Equal(current, _lastApplied);

    internal void Observe(T current)
    {
        if (!_owned || !Equal(current, _lastApplied)) _original = current;
    }

    internal void Applied(T current)
    {
        _lastApplied = current;
        _observed = true;
        _owned = !Equal(_original, current);
    }

    internal bool TryRestore(T current, out T original)
    {
        original = _original;
        bool restore = _owned && Equal(current, _lastApplied) && !Equal(current, _original);
        _observed = _owned = false;
        return restore;
    }
}

internal interface IOverrideProperty
{
    bool Changed();
    void Observe();
    void Applied();
    bool Restore();
}

internal sealed class OverrideProperty<T> : IOverrideProperty
{
    private readonly Func<T> _read;
    private readonly Action<T> _write;
    private readonly OverrideOwnership<T> _ownership = new();
    internal OverrideProperty(Func<T> read, Action<T> write) { _read = read; _write = write; }
    public bool Changed() => _ownership.Changed(_read());
    public void Observe() => _ownership.Observe(_read());
    public void Applied() => _ownership.Applied(_read());
    public bool Restore()
    {
        if (!_ownership.TryRestore(_read(), out var original)) return false;
        _write(original);
        return true;
    }
}
