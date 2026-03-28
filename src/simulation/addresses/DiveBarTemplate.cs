using System;
using Stakeout.Simulation.Entities;

namespace Stakeout.Simulation.Addresses;

public class DiveBarTemplate : IAddressTemplate
{
    public void Generate(Address address, SimulationState state, Random random)
    {
        LocationBuilders.CreateLocation(state, address, "Alley",
            new[] { "exterior", "covert_entry" });

        var barArea = LocationBuilders.CreateLocation(state, address, "Bar Area",
            new[] { "publicly_accessible", "service_area", "entrance", "social" });
        barArea.AccessPoints.Add(new AccessPoint
        {
            Id = state.GenerateEntityId(),
            Name = "Front Door",
            Type = AccessPointType.Door,
            Tags = new[] { "main_entrance" }
        });

        var backHall = LocationBuilders.CreateLocation(state, address, "Back Hallway",
            new[] { "staff_only" });
        backHall.AccessPoints.Add(new AccessPoint
        {
            Id = state.GenerateEntityId(),
            Name = "Back Door",
            Type = AccessPointType.Door,
            Tags = new[] { "staff_entry" },
            IsLocked = true,
            LockMechanism = LockMechanism.Key
        });

        LocationBuilders.CreateLocation(state, address, "Storage",
            new[] { "staff_only", "storage" });
        LocationBuilders.CreateLocation(state, address, "Manager's Office",
            new[] { "staff_only", "private" });
        LocationBuilders.CreateLocation(state, address, "Restroom",
            new[] { "publicly_accessible", "restroom" });
    }
}
