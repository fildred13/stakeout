using System;
using Stakeout.Simulation;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Sublocations;
using Xunit;

namespace Stakeout.Tests.Simulation.Sublocations;

public class DiveBarGeneratorTests
{
    private SublocationGraph Generate(int seed = 42)
    {
        var state = new SimulationState();
        var gen = new DiveBarGenerator();
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
    public void Generate_HasServiceArea()
    {
        var graph = Generate();
        var serviceArea = graph.FindByTag("service_area");
        Assert.NotNull(serviceArea);
    }

    [Fact]
    public void Generate_HasCovertEntry()
    {
        var graph = Generate();
        var covert = graph.FindByTag("covert_entry");
        Assert.NotNull(covert);
    }

    [Fact]
    public void Generate_HasStorage()
    {
        var graph = Generate();
        var storage = graph.FindByTag("storage");
        Assert.NotNull(storage);
    }

    [Fact]
    public void Generate_HasRestroom()
    {
        var graph = Generate();
        var restroom = graph.FindByTag("restroom");
        Assert.NotNull(restroom);
    }

    [Fact]
    public void Generate_HasPrivateOffice()
    {
        var graph = Generate();
        var office = graph.FindByTag("private");
        Assert.NotNull(office);
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

    [Fact]
    public void Generate_CanReachEntranceFromRoad()
    {
        var graph = Generate();
        var road = graph.GetRoad();
        var entrance = graph.FindByTag("entrance");
        var path = graph.FindPath(road.Id, entrance.Id);
        Assert.True(path.Count >= 2);
    }

    [Fact]
    public void Generate_CanReachBarAreaFromRoad()
    {
        var graph = Generate();
        var road = graph.GetRoad();
        var barArea = graph.FindByTag("service_area");
        var path = graph.FindPath(road.Id, barArea.Id);
        Assert.True(path.Count >= 2);
    }
}
