using System;
using System.Linq;
using Stakeout.Simulation;
using Stakeout.Simulation.Addresses;
using Stakeout.Simulation.Entities;
using Xunit;

namespace Stakeout.Tests.Simulation.Addresses;

public class DinerTemplateTests
{
    private (SimulationState state, Address address) Generate(int seed = 42)
    {
        var state = new SimulationState();
        var address = new Address { Id = 1, CityId = 1, Type = AddressType.Diner };
        state.Addresses[1] = address;
        new DinerTemplate().Generate(address, state, new Random(seed));
        return (state, address);
    }

    [Fact]
    public void Generate_HasParkingLot()
    {
        var (state, addr) = Generate();
        Assert.NotNull(state.FindLocationByTag(addr.Id, "parking"));
    }

    [Fact]
    public void Generate_HasDiningArea()
    {
        var (state, addr) = Generate();
        Assert.NotNull(state.FindLocationByTag(addr.Id, "service_area"));
    }

    [Fact]
    public void Generate_HasKitchen()
    {
        var (state, addr) = Generate();
        Assert.NotNull(state.FindLocationByTag(addr.Id, "work_area"));
    }

    [Fact]
    public void Generate_HasStaffEntry()
    {
        var (state, addr) = Generate();
        var allAPs = state.GetLocationsForAddress(addr.Id).SelectMany(l => l.AccessPoints);
        Assert.Contains(allAPs, ap => ap.HasTag("staff_entry"));
    }

    [Fact]
    public void Generate_HasRestroom()
    {
        var (state, addr) = Generate();
        var locs = state.GetLocationsForAddress(addr.Id);
        Assert.Contains(locs, l => l.HasTag("restroom") || l.Name == "Restroom");
    }
}
