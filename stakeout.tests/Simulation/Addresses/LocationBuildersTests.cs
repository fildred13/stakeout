// stakeout.tests/Simulation/Addresses/LocationBuildersTests.cs
using System;
using System.Linq;
using Stakeout.Simulation;
using Stakeout.Simulation.Addresses;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Fixtures;
using Xunit;

namespace Stakeout.Tests.Simulation.Addresses;

public class LocationBuildersTests
{
    private (SimulationState state, Address address) Setup()
    {
        var state = new SimulationState();
        var addr = new Address { Id = 1, CityId = 1, Type = AddressType.SuburbanHome };
        state.Addresses[1] = addr;
        return (state, addr);
    }

    [Fact]
    public void ExteriorParkingLot_HasCorrectTags()
    {
        var (state, addr) = Setup();
        var loc = LocationBuilders.ExteriorParkingLot(state, addr);
        Assert.True(loc.HasTag("exterior"));
        Assert.True(loc.HasTag("publicly_accessible"));
        Assert.True(loc.HasTag("parking"));
    }

    [Fact]
    public void ExteriorParkingLot_RegisteredInState()
    {
        var (state, addr) = Setup();
        var loc = LocationBuilders.ExteriorParkingLot(state, addr);
        Assert.True(state.Locations.ContainsKey(loc.Id));
        Assert.Contains(loc.Id, addr.LocationIds);
    }

    [Fact]
    public void ApartmentUnit_HasLockedDoor()
    {
        var (state, addr) = Setup();
        var rng = new Random(42);
        var loc = LocationBuilders.ApartmentUnit(state, addr, 2, "2B", rng);
        Assert.Single(loc.AccessPoints);
        Assert.True(loc.AccessPoints[0].IsLocked);
        Assert.Equal(AccessPointType.Door, loc.AccessPoints[0].Type);
    }

    [Fact]
    public void ApartmentUnit_HasExpectedSubLocations()
    {
        var (state, addr) = Setup();
        var rng = new Random(42);
        var loc = LocationBuilders.ApartmentUnit(state, addr, 2, "2B", rng);
        Assert.Equal("2B", loc.UnitLabel);
        Assert.Equal(2, loc.Floor);
        var subNames = loc.SubLocationIds.Select(id => state.SubLocations[id].Name).ToList();
        Assert.Contains("Bedroom", subNames);
        Assert.Contains("Kitchen", subNames);
        Assert.Contains("Living Room", subNames);
        Assert.Contains("Bathroom", subNames);
    }

    [Fact]
    public void ApartmentUnit_SubLocationsRegisteredInState()
    {
        var (state, addr) = Setup();
        var rng = new Random(42);
        var loc = LocationBuilders.ApartmentUnit(state, addr, 2, "2B", rng);
        foreach (var subId in loc.SubLocationIds)
        {
            Assert.True(state.SubLocations.ContainsKey(subId));
        }
    }

    [Fact]
    public void SecurityRoom_HasCorrectTags()
    {
        var (state, addr) = Setup();
        var loc = LocationBuilders.SecurityRoom(state, addr);
        Assert.True(loc.HasTag("security"));
        Assert.True(loc.HasTag("private"));
    }

    [Fact]
    public void Restroom_CreatesSubLocationWithTag()
    {
        var (state, addr) = Setup();
        var parent = new Location { Id = state.GenerateEntityId(), AddressId = addr.Id, Name = "Lobby" };
        state.Locations[parent.Id] = parent;
        addr.LocationIds.Add(parent.Id);

        var sub = LocationBuilders.Restroom(state, parent);
        Assert.True(sub.HasTag("restroom"));
        Assert.Contains(sub.Id, parent.SubLocationIds);
    }

    [Fact]
    public void CreateFixture_OnLocation_RegisteredInState()
    {
        var (state, addr) = Setup();
        var loc = LocationBuilders.CreateLocation(state, addr, "Kitchen", new[] { "kitchen" });
        var fixture = LocationBuilders.CreateFixture(state, FixtureType.TrashCan, "Trash Can",
            locationId: loc.Id, subLocationId: null);

        Assert.True(state.Fixtures.ContainsKey(fixture.Id));
        Assert.Equal(loc.Id, fixture.LocationId);
        Assert.Null(fixture.SubLocationId);
        Assert.Equal("Trash Can", fixture.Name);
        Assert.Equal(FixtureType.TrashCan, fixture.Type);
    }

    [Fact]
    public void CreateFixture_OnSubLocation_RegisteredInState()
    {
        var (state, addr) = Setup();
        var loc = LocationBuilders.CreateLocation(state, addr, "Interior", new[] { "residential" });
        var sub = LocationBuilders.CreateSubLocation(state, loc, "Kitchen", new[] { "kitchen" });
        var fixture = LocationBuilders.CreateFixture(state, FixtureType.TrashCan, "Trash Can",
            locationId: null, subLocationId: sub.Id);

        Assert.True(state.Fixtures.ContainsKey(fixture.Id));
        Assert.Null(fixture.LocationId);
        Assert.Equal(sub.Id, fixture.SubLocationId);
    }

    [Fact]
    public void CreateFixture_WithTags_HasTags()
    {
        var (state, addr) = Setup();
        var loc = LocationBuilders.CreateLocation(state, addr, "Kitchen", new[] { "kitchen" });
        var fixture = LocationBuilders.CreateFixture(state, FixtureType.TrashCan, "Trash Can",
            locationId: loc.Id, subLocationId: null, tags: new[] { "kitchen", "waste" });

        Assert.True(fixture.HasTag("kitchen"));
        Assert.True(fixture.HasTag("waste"));
    }
}
