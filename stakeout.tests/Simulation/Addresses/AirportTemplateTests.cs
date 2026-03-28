using System;
using System.Linq;
using Stakeout.Simulation;
using Stakeout.Simulation.Addresses;
using Stakeout.Simulation.Entities;
using Xunit;

namespace Stakeout.Tests.Simulation.Addresses;

public class AirportTemplateTests
{
    private (SimulationState state, Address address) Generate(int seed = 42)
    {
        var state = new SimulationState();
        var address = new Address { Id = 1, CityId = 1, Type = AddressType.Airport };
        state.Addresses[1] = address;
        new AirportTemplate().Generate(address, state, new Random(seed));
        return (state, address);
    }

    [Fact]
    public void Generate_HasSingleTerminalLocation()
    {
        var (state, addr) = Generate();
        var locs = state.GetLocationsForAddress(addr.Id);
        Assert.Single(locs);
        Assert.Equal("Terminal", locs[0].Name);
    }

    [Fact]
    public void Generate_TerminalIsPublicAndEntrance()
    {
        var (state, addr) = Generate();
        var terminal = state.GetLocationsForAddress(addr.Id).First();
        Assert.True(terminal.HasTag("publicly_accessible"));
        Assert.True(terminal.HasTag("entrance"));
    }

    [Fact]
    public void Generate_NoSubLocations()
    {
        var (state, addr) = Generate();
        var terminal = state.GetLocationsForAddress(addr.Id).First();
        Assert.Empty(terminal.SubLocationIds);
    }
}
