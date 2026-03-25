// stakeout.tests/Simulation/Sublocations/SuburbanHomeGeneratorTests.cs
using System;
using Stakeout.Simulation;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Sublocations;
using Xunit;

namespace Stakeout.Tests.Simulation.Sublocations;

public class SuburbanHomeGeneratorTests
{
    private SublocationGraph Generate(int seed = 42)
    {
        var state = new SimulationState();
        var gen = new SuburbanHomeGenerator();
        return gen.Generate(addressId: 1, state, new Random(seed));
    }

    [Fact]
    public void Generate_HasRoadNode()
    {
        var graph = Generate();
        var road = graph.GetRoad();
        Assert.NotNull(road);
        Assert.True(road.HasTag("road"));
    }

    [Fact]
    public void Generate_HasEntrance()
    {
        var graph = Generate();
        var entrance = graph.FindByTag("entrance");
        Assert.NotNull(entrance);
    }

    [Fact]
    public void Generate_HasBedroom()
    {
        var graph = Generate();
        var bedroom = graph.FindByTag("bedroom");
        Assert.NotNull(bedroom);
    }

    [Fact]
    public void Generate_HasKitchen()
    {
        var graph = Generate();
        var kitchen = graph.FindByTag("kitchen");
        Assert.NotNull(kitchen);
    }

    [Fact]
    public void Generate_HasLivingRoom()
    {
        var graph = Generate();
        var living = graph.FindByTag("living");
        Assert.NotNull(living);
    }

    [Fact]
    public void Generate_HasCovertEntry()
    {
        var graph = Generate();
        var covert = graph.FindByTag("covert_entry");
        Assert.NotNull(covert);
    }

    [Fact]
    public void Generate_RoadConnectedToEntrance()
    {
        var graph = Generate();
        var road = graph.GetRoad();
        var entrance = graph.FindByTag("entrance");
        var path = graph.FindPath(road.Id, entrance.Id);
        Assert.True(path.Count <= 3);
    }

    [Fact]
    public void Generate_CanReachBedroomFromRoad()
    {
        var graph = Generate();
        var road = graph.GetRoad();
        var bedroom = graph.FindByTag("bedroom");
        var path = graph.FindPath(road.Id, bedroom.Id);
        Assert.True(path.Count >= 2);
    }

    [Fact]
    public void Generate_AllSublocationsHaveCorrectAddressId()
    {
        var graph = Generate();
        foreach (var sub in graph.AllSublocations.Values)
        {
            Assert.Equal(1, sub.AddressId);
        }
    }
}
