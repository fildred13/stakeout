using System;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Fixtures;

namespace Stakeout.Simulation.Addresses;

public class ApartmentBuildingTemplate : IAddressTemplate
{
    public void Generate(Address address, SimulationState state, Random random)
    {
        LocationBuilders.ExteriorParkingLot(state, address);

        var lobby = LocationBuilders.CreateLocation(state, address, "Lobby",
            new[] { "publicly_accessible", "entrance" });
        LocationBuilders.CreateFixture(state, FixtureType.TrashCan, "Trash Can", locationId: lobby.Id, subLocationId: null);

        LocationBuilders.SecurityRoom(state, address);

        int floors = random.Next(4, 21);
        int unitsPerFloor = random.Next(4, 9);

        for (int f = 1; f <= floors; f++)
        {
            for (int u = 1; u <= unitsPerFloor; u++)
            {
                char unitLetter = (char)('A' + u - 1);
                string unitLabel = $"{f}{unitLetter}";
                LocationBuilders.ApartmentUnit(state, address, f, unitLabel, random);
            }
        }
    }
}
