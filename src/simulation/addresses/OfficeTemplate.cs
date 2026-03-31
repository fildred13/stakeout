using System;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Fixtures;

namespace Stakeout.Simulation.Addresses;

public class OfficeTemplate : IAddressTemplate
{
    public void Generate(Address address, SimulationState state, Random random)
    {
        LocationBuilders.CreateLocation(state, address, "Lobby",
            new[] { "publicly_accessible", "entrance" });

        LocationBuilders.SecurityRoom(state, address);

        int floors = random.Next(1, 6);

        for (int f = 1; f <= floors; f++)
        {
            var floor = LocationBuilders.CreateLocation(state, address, $"Floor {f}",
                new[] { "commercial" }, f);

            LocationBuilders.CreateSubLocation(state, floor, "Reception",
                new[] { "publicly_accessible" });
            LocationBuilders.CreateSubLocation(state, floor, "Cubicle Area",
                new[] { "work_area" });
            LocationBuilders.CreateSubLocation(state, floor, "Manager's Office",
                new[] { "private", "office" });
            var breakRoom = LocationBuilders.CreateSubLocation(state, floor, "Break Room",
                new[] { "food", "social" });
            LocationBuilders.CreateFixture(state, FixtureType.TrashCan, "Trash Can", locationId: null, subLocationId: breakRoom.Id);
            LocationBuilders.CreateSubLocation(state, floor, "Restroom",
                new[] { "restroom" });
        }
    }
}
