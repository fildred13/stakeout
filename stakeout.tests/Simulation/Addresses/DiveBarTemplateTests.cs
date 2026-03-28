using System;
using System.Linq;
using Stakeout.Simulation;
using Stakeout.Simulation.Addresses;
using Stakeout.Simulation.Entities;
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
}
