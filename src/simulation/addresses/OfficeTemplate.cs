using System;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Fixtures;

namespace Stakeout.Simulation.Addresses;

public class OfficeTemplate : IAddressTemplate
{
    public void Generate(Address address, SimulationState state, Random random)
    {
        var lobby = LocationBuilders.CreateLocation(state, address, "Lobby",
            new[] { "publicly_accessible", "entrance" });
        var lobbyCamera = LocationBuilders.CreateFixture(state, FixtureType.Camera, "Lobby Camera",
            locationId: lobby.Id, subLocationId: null, tags: new[] { "indoor_camera" });
        lobbyCamera.WatchedLocationId = lobby.Id;
        // Its watched road cell is set once the road graph exists.
        LocationBuilders.CreateFixture(state, FixtureType.Camera, "Street Camera",
            locationId: lobby.Id, subLocationId: null, tags: new[] { "exterior_camera" });

        LocationBuilders.SecurityRoom(state, address);

        int floors = random.Next(1, 6);

        for (int f = 1; f <= floors; f++)
        {
            var floor = LocationBuilders.CreateLocation(state, address, $"Floor {f}",
                new[] { "commercial" }, f);

            var reception = LocationBuilders.CreateSubLocation(state, floor, "Reception",
                new[] { "publicly_accessible" });
            LocationBuilders.CreateFixture(state, FixtureType.Telephone, "Reception Telephone", locationId: null, subLocationId: reception.Id);
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
