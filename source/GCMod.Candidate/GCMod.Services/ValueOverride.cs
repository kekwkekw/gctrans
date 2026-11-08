using System;
using System.Collections.Generic;

namespace GCMod.Services;

// Restore only our own write. A subsequent native change becomes the new baseline.
public sealed class ValueOverride<T>
{
    private readonly Func<T, T, bool> equals;
    public ValueOverride(Func<T, T, bool> equals = null) => this.equals = equals ?? EqualityComparer<T>.Default.Equals;
    public bool Active { get; private set; }
    public T Original { get; private set; }
    public T Applied { get; private set; }
    public T Source(T current) => Active && equals(current, Applied) ? Original : current;
    public T Apply(T current, T value)
    {
        Original = Source(current);
        Applied = value;
        Active = true;
        return value;
    }
    public T Restore(T current)
    {
        T value = Source(current);
        Active = false;
        return value;
    }
}
