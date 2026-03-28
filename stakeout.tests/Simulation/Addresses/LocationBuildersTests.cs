// stakeout.tests/Simulation/Addresses/LocationBuildersTests.cs
using System;
using System.Linq;
using Stakeout.Simulation;
using Stakeout.Simulation.Addresses;
using Stakeout.Simulation.Entities;
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
}
