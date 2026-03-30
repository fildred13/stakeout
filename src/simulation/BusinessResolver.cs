using System.Collections.Generic;
using Stakeout.Simulation.Entities;

namespace Stakeout.Simulation;

public static class BusinessResolver
{
    public static List<Person> Resolve(SimulationState state, Business business, PersonGenerator generator)
    {
        if (business.IsResolved) return new();

        var address = state.Addresses[business.AddressId];
        LocationGenerator.ResolveAddressInterior(address, state);

        var spawned = new List<Person>();
        foreach (var position in business.Positions)
        {
            if (position.AssignedPersonId != null) continue;

            var person = generator.GeneratePerson(state, new SpawnRequirements
            {
                BusinessId = business.Id,
                PositionId = position.Id
            });

            spawned.Add(person);
        }

        business.IsResolved = true;
        return spawned;
    }
}
