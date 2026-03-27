using System;
using Stakeout.Simulation;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Sublocations;
using Xunit;

namespace Stakeout.Tests.Simulation.Sublocations;

public class DinerGeneratorTests
{
    private SublocationGraph Generate(int seed = 42)
    {
        var state = new SimulationState();
        var address = new Address { Id = 1, Type = AddressType.Diner };
        state.Addresses[1] = address;
        var gen = new DinerGenerator();
        return gen.Generate(address, state, new Random(seed));
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
        var entry = graph.FindEntryPoint("entrance");
        Assert.NotNull(entry);
    }

    [Fact]
    public void Generate_HasServiceArea()
    {
        var graph = Generate();
        var serviceArea = graph.FindByTag("service_area");
        Assert.NotNull(serviceArea);
    }

    [Fact]
    public void Generate_HasFoodArea()
    {
        var graph = Generate();
        var food = graph.FindByTag("food");
        Assert.NotNull(food);
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
        var entry = graph.FindEntryPoint("entrance");
        Assert.NotNull(entry);
        var path = graph.FindPath(road.Id, entry.Value.target.Id);
        Assert.True(path.Count >= 2);
    }

    [Fact]
    public void Generate_CanReachKitchenFromRoad()
    {
        var graph = Generate();
        var road = graph.GetRoad();
        var kitchen = graph.FindByTag("food");
        var path = graph.FindPath(road.Id, kitchen.Id);
        Assert.True(path.Count >= 2);
    }
}
