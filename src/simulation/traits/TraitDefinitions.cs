using System;
using System.Collections.Generic;
using System.Linq;
using Stakeout.Simulation.Objectives;

namespace Stakeout.Simulation.Traits;

public static class TraitDefinitions
{
    private static readonly Dictionary<string, Func<List<Objective>>> Registry = new()
    {
        ["runner"] = () => new List<Objective> { new GoForARunObjective() },
        ["foodie"] = () => new List<Objective> { new EatOutObjective() },
    };

    public static List<Objective> CreateObjectivesForTrait(string traitName)
    {
        return Registry.TryGetValue(traitName, out var factory)
            ? factory()
            : new List<Objective>();
    }

    public static IReadOnlyList<string> GetAllTraitNames() => Registry.Keys.ToList();
}
