using System;
using Stakeout.Simulation.Entities;

namespace Stakeout.Simulation.Addresses;

public class SuburbanHomeTemplate : IAddressTemplate
{
    public void Generate(Address address, SimulationState state, Random random)
    {
        var yard = LocationBuilders.CreateLocation(state, address, "Front Yard",
            new[] { "exterior", "publicly_accessible" });

        yard.AccessPoints.Add(new AccessPoint
        {
            Id = state.GenerateEntityId(),
            Name = "Window",
            Type = AccessPointType.Window,
            Tags = new[] { "covert_entry" },
            IsLocked = true,
            LockMechanism = LockMechanism.Key
        });

        var interior = LocationBuilders.CreateLocation(state, address, "Interior",
            new[] { "residential", "private", "entrance" });

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
            Tags = new[] { "staff_entry" },
            IsLocked = true,
            LockMechanism = LockMechanism.Key
        });

        LocationBuilders.CreateSubLocation(state, interior, "Hallway", new[] { "hallway" });
        LocationBuilders.CreateSubLocation(state, interior, "Kitchen", new[] { "kitchen", "food" });
        LocationBuilders.CreateSubLocation(state, interior, "Living Room", new[] { "living", "social" });
        LocationBuilders.CreateSubLocation(state, interior, "Bathroom", new[] { "restroom" });

        int bedroomCount = random.Next(2, 4);
        for (int i = 1; i <= bedroomCount; i++)
        {
            LocationBuilders.CreateSubLocation(state, interior, $"Bedroom {i}",
                new[] { "bedroom", "private" });
        }
    }
}
