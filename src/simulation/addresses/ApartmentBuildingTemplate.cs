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
        lobby.AccessPoints.Add(new AccessPoint
        {
            Id = state.GenerateEntityId(),
            Name = "Lobby Door",
            Type = AccessPointType.Door,
            Tags = new[] { "main_entrance" }
        });
        if (random.NextDouble() < 0.4)
        {
            var camera = LocationBuilders.CreateFixture(state, FixtureType.Camera, "Lobby Camera",
                locationId: lobby.Id, subLocationId: null, tags: new[] { "indoor_camera" });
            camera.WatchedLocationId = lobby.Id;
        }

        LocationBuilders.SecurityRoom(state, address);

        int floors = random.Next(3, 9);
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
