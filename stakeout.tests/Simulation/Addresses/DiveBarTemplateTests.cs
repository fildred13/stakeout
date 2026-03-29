using System;
using System.Linq;
using Stakeout.Simulation;
using Stakeout.Simulation.Addresses;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Fixtures;
using Xunit;

namespace Stakeout.Tests.Simulation.Addresses;

public class DiveBarTemplateTests
{
    private (SimulationState state, Address address) Generate(int seed = 42)
    {
        var state = new SimulationState();
        var address = new Address { Id = 1, CityId = 1, Type = AddressType.DiveBar };
        state.Addresses[1] = address;
        new DiveBarTemplate().Generate(address, state, new Random(seed));
        return (state, address);
    }

    [Fact]
    public void Generate_HasAlley()
    {
        var (state, addr) = Generate();
        var allLocs = state.GetLocationsForAddress(addr.Id);
        Assert.Contains(allLocs, l => l.Name == "Alley");
    }

    [Fact]
    public void Generate_HasBarArea()
    {
        var (state, addr) = Generate();
        Assert.NotNull(state.FindLocationByTag(addr.Id, "service_area"));
    }

    [Fact]
    public void Generate_HasCovertEntry()
    {
        var (state, addr) = Generate();
        var allLocs = state.GetLocationsForAddress(addr.Id);
        Assert.Contains(allLocs, l => l.HasTag("covert_entry"));
    }

    [Fact]
    public void Generate_HasTrashCan()
    {
        var (state, addr) = Generate();
        var allFixtures = state.Fixtures.Values.Where(f =>
            state.GetLocationsForAddress(addr.Id).Any(l => l.Id == f.LocationId) ||
            state.GetLocationsForAddress(addr.Id).SelectMany(l => state.GetSubLocationsForLocation(l.Id)).Any(s => s.Id == f.SubLocationId));
        Assert.Contains(allFixtures, f => f.Type == FixtureType.TrashCan);
    }
}
