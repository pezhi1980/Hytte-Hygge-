using System;
using System.Collections.Generic;
using Hytte.Rules.Core;

namespace Hytte.Rules.Tests;

/// <summary>Returns a fixed sequence of values. Fails loudly if a test rolls more than planned.</summary>
public sealed class FixedRng : IRng
{
    private readonly Queue<int> _values;

    public FixedRng(params int[] values) => _values = new Queue<int>(values);

    public int Next(int minInclusive, int maxExclusive)
    {
        if (_values.Count == 0)
            throw new InvalidOperationException("FixedRng ran out of values.");
        var v = _values.Dequeue();
        if (v < minInclusive || v >= maxExclusive)
            throw new InvalidOperationException($"FixedRng value {v} outside [{minInclusive},{maxExclusive}).");
        return v;
    }
}

/// <summary>Deterministic pseudo-random source for fuzz tests.</summary>
public sealed class SeededRng : IRng
{
    private readonly Random _random;

    public SeededRng(int seed) => _random = new Random(seed);

    public int Next(int minInclusive, int maxExclusive) => _random.Next(minInclusive, maxExclusive);
}
