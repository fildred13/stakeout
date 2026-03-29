using System;
using System.Linq;
using Stakeout.Simulation;
using Stakeout.Simulation.Addresses;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Fixtures;
using Xunit;

namespace Stakeout.Tests.Simulation.Addresses;

public class OfficeTemplateTests
{
    private (SimulationState state, Address address) Generate(int seed = 42)
    {
        var state = new SimulationState();
        var address = new Address { Id = 1, CityId = 1, Type = AddressType.Office };
        state.Addresses[1] = address;
        new OfficeTemplate().Generate(address, state, new Random(seed));
        return (state, address);
    }

    [Fact]
    public void Generate_HasLobby()
    {
        var (state, addr) = Generate();
        Assert.NotNull(state.FindLocationByTag(addr.Id, "entrance"));
    }

    [Fact]
    public void Generate_HasWorkAreas()
    {
        var (state, addr) = Generate();
        var floors = state.GetLocationsForAddress(addr.Id)
            .Where(l => l.HasTag("commercial")).ToList();
        Assert.True(floors.Count >= 1);
    }

    [Fact]
    public void Generate_FloorsHaveSubLocations()
    {
        var (state, addr) = Generate();
        var floor = state.GetLocationsForAddress(addr.Id)
            .First(l => l.HasTag("commercial"));
        Assert.True(floor.SubLocationIds.Count >= 3);
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
