using System;
using Stakeout.Simulation;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Sublocations;
using Xunit;

namespace Stakeout.Tests.Simulation.Sublocations;

public class ParkGeneratorTests
{
    private SublocationGraph Generate(int seed = 42)
    {
        var state = new SimulationState();
        var address = new Address { Id = 1, Type = AddressType.Park };
        state.Addresses[1] = address;
        var gen = new ParkGenerator();
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
    public void Generate_HasFoodArea()
    {
        var graph = Generate();
        var food = graph.FindByTag("food");
        Assert.NotNull(food);
    }

    [Fact]
    public void Generate_HasSocialArea()
    {
        var graph = Generate();
        var social = graph.FindByTag("social");
        Assert.NotNull(social);
    }

    [Fact]
    public void Generate_HasCovertEntry()
    {
        var graph = Generate();
        // Wooded Area sublocation retains its covert_entry tag
        var covert = graph.FindByTag("covert_entry");
        Assert.NotNull(covert);
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
    public void Generate_CanReachPicnicAreaFromRoad()
    {
        var graph = Generate();
        var road = graph.GetRoad();
        var picnic = graph.FindByTag("food");
        var path = graph.FindPath(road.Id, picnic.Id);
        Assert.True(path.Count >= 2);
    }
}
