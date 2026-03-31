using System;
using Stakeout.Simulation;
using Stakeout.Simulation.Entities;
using Xunit;

namespace Stakeout.Tests.Simulation;

public class SimulationStateQueryTests
{
    private SimulationState CreateStateWithAddress()
    {
        var state = new SimulationState();
        var city = new Stakeout.Simulation.Entities.City { Id = 1, Name = "Boston", CountryName = "USA" };
        state.Cities[1] = city;

        var addr = new Address { Id = 10, CityId = 1, Type = AddressType.SuburbanHome };
        state.Addresses[10] = addr;
        city.AddressIds.Add(10);

        var loc = new Location { Id = 100, AddressId = 10, Name = "Interior", Tags = new[] { "residential" } };
        state.Locations[100] = loc;
        addr.LocationIds.Add(100);

        var sub = new SubLocation { Id = 1000, LocationId = 100, Name = "Kitchen", Tags = new[] { "kitchen", "food" } };
        state.SubLocations[1000] = sub;
        loc.SubLocationIds.Add(1000);

        return state;
    }

    [Fact]
    public void GetLocationsForAddress_ReturnsLocations()
    {
        var state = CreateStateWithAddress();
        var locs = state.GetLocationsForAddress(10);
        Assert.Single(locs);
        Assert.Equal("Interior", locs[0].Name);
    }

    [Fact]
    public void GetSubLocationsForLocation_ReturnsSubLocations()
    {
        var state = CreateStateWithAddress();
        var subs = state.GetSubLocationsForLocation(100);
        Assert.Single(subs);
        Assert.Equal("Kitchen", subs[0].Name);
    }

    [Fact]
    public void FindLocationByTag_FindsMatch()
    {
        var state = CreateStateWithAddress();
        var loc = state.FindLocationByTag(10, "residential");
        Assert.NotNull(loc);
        Assert.Equal("Interior", loc.Name);
    }

    [Fact]
    public void FindLocationByTag_ReturnsNullForNoMatch()
    {
        var state = CreateStateWithAddress();
        var loc = state.FindLocationByTag(10, "exterior");
        Assert.Null(loc);
    }

    [Fact]
    public void FindSubLocationByTag_FindsMatch()
    {
        var state = CreateStateWithAddress();
        var sub = state.FindSubLocationByTag(100, "kitchen");
        Assert.NotNull(sub);
        Assert.Equal("Kitchen", sub.Name);
    }

    [Fact]
    public void GetAddressesForCity_ReturnsAddresses()
    {
        var state = CreateStateWithAddress();
        var addrs = state.GetAddressesForCity(1);
        Assert.Single(addrs);
        Assert.Equal(10, addrs[0].Id);
    }

    [Fact]
    public void GetCityForAddress_ReturnsCity()
    {
        var state = CreateStateWithAddress();
        var city = state.GetCityForAddress(10);
        Assert.Equal("Boston", city.Name);
    }
}
