// src/simulation/addresses/LocationBuilders.cs
using System;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Fixtures;

namespace Stakeout.Simulation.Addresses;

public static class LocationBuilders
{
    public static Location CreateLocation(SimulationState state, Address address,
        string name, string[] tags, int? floor = null)
    {
        var loc = new Location
        {
            Id = state.GenerateEntityId(),
            AddressId = address.Id,
            Name = name,
            Tags = tags,
            Floor = floor
        };
        state.Locations[loc.Id] = loc;
        address.LocationIds.Add(loc.Id);
        return loc;
    }

    public static SubLocation CreateSubLocation(SimulationState state, Location parent,
        string name, string[] tags)
    {
        var sub = new SubLocation
        {
            Id = state.GenerateEntityId(),
            LocationId = parent.Id,
            Name = name,
            Tags = tags
        };
        state.SubLocations[sub.Id] = sub;
        parent.SubLocationIds.Add(sub.Id);
        return sub;
    }

    public static Location ExteriorParkingLot(SimulationState state, Address address)
    {
        return CreateLocation(state, address, "Exterior Parking Lot",
            new[] { "exterior", "publicly_accessible", "parking" });
    }

    public static Location SecurityRoom(SimulationState state, Address address)
    {
        return CreateLocation(state, address, "Security Room",
            new[] { "security", "private" });
    }

    public static Location ApartmentUnit(SimulationState state, Address address,
        int floor, string unitLabel, Random rng)
    {
        var unit = CreateLocation(state, address, $"Unit {unitLabel}",
            new[] { "residential", "private" }, floor);
        unit.UnitLabel = unitLabel;

        unit.AccessPoints.Add(new AccessPoint
        {
            Id = state.GenerateEntityId(),
            Name = "Front Door",
            Type = AccessPointType.Door,
            Tags = new[] { "main_entrance" },
            IsLocked = true,
            LockMechanism = Entities.LockMechanism.Key
        });

        CreateSubLocation(state, unit, "Bedroom", new[] { "bedroom", "private" });
        CreateSubLocation(state, unit, "Kitchen", new[] { "kitchen", "food" });
        var livingRoom = CreateSubLocation(state, unit, "Living Room", new[] { "living", "social" });
        CreateFixture(state, FixtureType.Telephone, "Telephone", locationId: null, subLocationId: livingRoom.Id);
        CreateSubLocation(state, unit, "Bathroom", new[] { "restroom" });

        return unit;
    }

    public static SubLocation Restroom(SimulationState state, Location parent)
    {
        return CreateSubLocation(state, parent, "Restroom", new[] { "restroom" });
    }

    public static Fixture CreateFixture(SimulationState state, FixtureType type,
        string name, int? locationId, int? subLocationId, string[] tags = null)
    {
        var fixture = new Fixture
        {
            Id = state.GenerateEntityId(),
            LocationId = locationId,
            SubLocationId = subLocationId,
            Name = name,
            Type = type,
            Tags = tags ?? Array.Empty<string>()
        };
        state.Fixtures[fixture.Id] = fixture;
        return fixture;
    }
}
