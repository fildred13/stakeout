using System;
using System.Collections.Generic;
using Stakeout.Simulation.Entities;

namespace Stakeout.Simulation.Sublocations;

public static class SublocationGeneratorRegistry
{
    private static readonly Dictionary<AddressType, ISublocationGenerator> _generators = new();

    public static void Register(AddressType type, ISublocationGenerator generator)
    {
        _generators[type] = generator;
    }

    public static ISublocationGenerator Get(AddressType type)
    {
        return _generators.TryGetValue(type, out var gen) ? gen : null;
    }
}
