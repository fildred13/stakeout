using System;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Fixtures;

namespace Stakeout.Simulation.Addresses;

public class DinerTemplate : IAddressTemplate
{
    public void Generate(Address address, SimulationState state, Random random)
    {
        LocationBuilders.ExteriorParkingLot(state, address);

        var dining = LocationBuilders.CreateLocation(state, address, "Dining Area",
            new[] { "publicly_accessible", "service_area", "entrance", "social" });
        dining.AccessPoints.Add(new AccessPoint
        {
            Id = state.GenerateEntityId(),
            Name = "Front Door",
            Type = AccessPointType.Door,
            Tags = new[] { "main_entrance" }
        });

        var kitchen = LocationBuilders.CreateLocation(state, address, "Kitchen",
            new[] { "staff_only", "work_area", "food" });
        kitchen.AccessPoints.Add(new AccessPoint
        {
            Id = state.GenerateEntityId(),
            Name = "Back Door",
            Type = AccessPointType.Door,
            Tags = new[] { "staff_entry" },
            IsLocked = true,
            LockMechanism = LockMechanism.Key
        });
        LocationBuilders.CreateFixture(state, FixtureType.TrashCan, "Trash Can", locationId: kitchen.Id, subLocationId: null);

        LocationBuilders.CreateLocation(state, address, "Storage",
            new[] { "staff_only", "storage" });
        LocationBuilders.CreateLocation(state, address, "Manager's Office",
            new[] { "staff_only", "private" });
        LocationBuilders.CreateLocation(state, address, "Restroom",
            new[] { "publicly_accessible", "restroom" });
    }
}
