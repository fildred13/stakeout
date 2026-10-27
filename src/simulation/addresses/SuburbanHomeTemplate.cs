using System;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Fixtures;

namespace Stakeout.Simulation.Addresses;

public class SuburbanHomeTemplate : IAddressTemplate
{
    public void Generate(Address address, SimulationState state, Random random)
    {
        LocationBuilders.CreateLocation(state, address, "Front Yard",
            new[] { "exterior", "publicly_accessible" });

        var interior = LocationBuilders.CreateLocation(state, address, "Interior",
            new[] { "residential", "private", "entrance", "home" });

        interior.AccessPoints.Add(new AccessPoint
        {
            Id = state.GenerateEntityId(),
            Name = "Front Door",
            Type = AccessPointType.Door,
            Tags = new[] { "main_entrance" },
            IsLocked = true,
            LockMechanism = LockMechanism.Key
        });

        interior.AccessPoints.Add(new AccessPoint
        {
            Id = state.GenerateEntityId(),
            Name = "Back Door",
            Type = AccessPointType.Door,
            Tags = new[] { "back_entry", "covert_entry" },
            IsLocked = true,
            LockMechanism = LockMechanism.Key
        });

        interior.AccessPoints.Add(new AccessPoint
        {
            Id = state.GenerateEntityId(),
            Name = "Kitchen Window",
            Type = AccessPointType.Window,
            Tags = new[] { "covert_entry" },
            IsLocked = true
        });

        LocationBuilders.CreateSubLocation(state, interior, "Hallway", new[] { "hallway" });
        LocationBuilders.HomeRooms(state, interior, bedrooms: random.Next(2, 4), phoneInKitchen: true);
    }
}
