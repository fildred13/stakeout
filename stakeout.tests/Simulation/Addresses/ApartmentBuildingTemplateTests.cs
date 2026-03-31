using System;
using System.Linq;
using Stakeout.Simulation;
using Stakeout.Simulation.Addresses;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Fixtures;
using Xunit;

namespace Stakeout.Tests.Simulation.Addresses;

public class ApartmentBuildingTemplateTests
{
    private (SimulationState state, Address address) Generate(int seed = 42)
    {
        var state = new SimulationState();
        var address = new Address { Id = 1, CityId = 1, Type = AddressType.ApartmentBuilding };
        state.Addresses[1] = address;
        new ApartmentBuildingTemplate().Generate(address, state, new Random(seed));
        return (state, address);
    }

    [Fact]
    public void Generate_HasLobby()
    {
        var (state, addr) = Generate();
        Assert.NotNull(state.FindLocationByTag(addr.Id, "entrance"));
    }

    [Fact]
    public void Generate_HasParkingLot()
    {
        var (state, addr) = Generate();
        Assert.NotNull(state.FindLocationByTag(addr.Id, "parking"));
    }

    [Fact]
    public void Generate_HasResidentialUnits()
    {
        var (state, addr) = Generate();
        var units = state.GetLocationsForAddress(addr.Id)
            .Where(l => l.HasTag("residential")).ToList();
        Assert.True(units.Count >= 4);
    }

    [Fact]
    public void Generate_UnitsHaveUnitLabels()
    {
        var (state, addr) = Generate();
        var units = state.GetLocationsForAddress(addr.Id)
            .Where(l => l.HasTag("residential")).ToList();
        Assert.All(units, u => Assert.NotNull(u.UnitLabel));
    }

    [Fact]
    public void Generate_UnitsHaveLockedDoors()
    {
        var (state, addr) = Generate();
        var units = state.GetLocationsForAddress(addr.Id)
            .Where(l => l.HasTag("residential")).ToList();
        Assert.All(units, u =>
        {
            Assert.NotEmpty(u.AccessPoints);
            Assert.True(u.AccessPoints[0].IsLocked);
        });
    }

    [Fact]
    public void Generate_UnitsHaveSubLocations()
    {
        var (state, addr) = Generate();
        var unit = state.GetLocationsForAddress(addr.Id)
            .First(l => l.HasTag("residential"));
        Assert.True(unit.SubLocationIds.Count >= 4);
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
