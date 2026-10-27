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
            new[] { "residential", "private", "home" }, floor);
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

        HomeRooms(state, unit, bedrooms: 1, phoneInKitchen: false);
        return unit;
    }

    /// <summary>The rooms every home has, with the fixtures that hold evidence.</summary>
    public static void HomeRooms(SimulationState state, Location home, int bedrooms, bool phoneInKitchen)
    {
        var kitchen = CreateSubLocation(state, home, "Kitchen", new[] { "kitchen", "food" });
        CreateFixture(state, FixtureType.KnifeBlock, "Knife Block", null, kitchen.Id);
        CreateFixture(state, FixtureType.Table, "Kitchen Table", null, kitchen.Id);
        CreateFixture(state, FixtureType.TrashCan, "Trash Can", null, kitchen.Id);
        var living = CreateSubLocation(state, home, "Living Room", new[] { "living", "social" });
        CreateFixture(state, FixtureType.Table, "Coffee Table", null, living.Id);
        CreateFixture(state, FixtureType.Drawer, "Writing Desk", null, living.Id);
        CreateFixture(state, FixtureType.Telephone, "Telephone", null, phoneInKitchen ? kitchen.Id : living.Id);
        for (int i = 1; i <= bedrooms; i++)
        {
            var bedroom = CreateSubLocation(state, home, bedrooms == 1 ? "Bedroom" : $"Bedroom {i}",
                new[] { "bedroom", "private" });
            CreateFixture(state, FixtureType.Nightstand, "Nightstand", null, bedroom.Id);
            CreateFixture(state, FixtureType.Closet, "Closet", null, bedroom.Id);
            CreateFixture(state, FixtureType.Hamper, "Laundry Hamper", null, bedroom.Id);
        }
        CreateSubLocation(state, home, "Bathroom", new[] { "restroom" });
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
        state.AddFixture(fixture);
        return fixture;
    }
}
