using System;
using System.Linq;
using Stakeout.Simulation;
using Stakeout.Simulation.Addresses;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Fixtures;
using Xunit;

namespace Stakeout.Tests.Simulation.Addresses;

public class ParkTemplateTests
{
    private (SimulationState state, Address address) Generate(int seed = 42)
    {
        var state = new SimulationState();
        var address = new Address { Id = 1, CityId = 1, Type = AddressType.Park };
        state.Addresses[1] = address;
        new ParkTemplate().Generate(address, state, new Random(seed));
        return (state, address);
    }

    [Fact]
    public void Generate_HasParkingLot()
    {
        var (state, addr) = Generate();
        Assert.NotNull(state.FindLocationByTag(addr.Id, "parking"));
    }

    [Fact]
    public void Generate_HasEntrance()
    {
        var (state, addr) = Generate();
        Assert.NotNull(state.FindLocationByTag(addr.Id, "entrance"));
    }

    [Fact]
    public void Generate_AllLocationsAreExterior()
    {
        var (state, addr) = Generate();
        var outdoorLocs = state.GetLocationsForAddress(addr.Id)
            .Where(l => l.HasTag("exterior")).ToList();
        Assert.True(outdoorLocs.Count >= 4);
    }

    [Fact]
    public void Generate_AllLocationsArePublic()
    {
        var (state, addr) = Generate();
        var publicLocs = state.GetLocationsForAddress(addr.Id)
            .Where(l => l.HasTag("publicly_accessible")).ToList();
        Assert.True(publicLocs.Count >= 4);
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
