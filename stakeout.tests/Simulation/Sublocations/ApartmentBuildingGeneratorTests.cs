using System;
using System.Linq;
using Stakeout.Simulation;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Sublocations;
using Xunit;

namespace Stakeout.Tests.Simulation.Sublocations;

public class ApartmentBuildingGeneratorTests
{
    private (SublocationGraph graph, SimulationState state) Generate(int seed = 42)
    {
        var state = new SimulationState();
        var address = new Address { Id = 1, Type = AddressType.ApartmentBuilding };
        state.Addresses[1] = address;
        var gen = new ApartmentBuildingGenerator();
        var graph = gen.Generate(address, state, new Random(seed));
        return (graph, state);
    }

    [Fact]
    public void Generate_HasRoadNode()
    {
        var (graph, _) = Generate();
        var road = graph.GetRoad();
        Assert.NotNull(road);
        Assert.True(road.HasTag("road"));
    }

    [Fact]
    public void Generate_HasEntrance()
    {
        var (graph, _) = Generate();
        var entrance = graph.FindByTag("entrance");
        Assert.NotNull(entrance);
    }

    [Fact]
    public void Generate_HasLobby()
    {
        var (graph, _) = Generate();
        var lobby = graph.FindByTag("public");
        Assert.NotNull(lobby);
    }

    [Fact]
    public void Generate_AllSublocationsHaveCorrectAddressId()
    {
        var (graph, _) = Generate();
        foreach (var sub in graph.AllSublocations.Values)
        {
            Assert.Equal(1, sub.AddressId);
        }
    }

    [Fact]
    public void Generate_HasFloorPlaceholders()
    {
        var (graph, _) = Generate();
        var placeholders = graph.FindAllByTag("floor_placeholder");
        Assert.True(placeholders.Count >= 4);
        Assert.True(placeholders.Count <= 20);
    }

    [Fact]
    public void Generate_FloorPlaceholdersAreNotGenerated()
    {
        var (graph, _) = Generate();
        var placeholders = graph.FindAllByTag("floor_placeholder");
        foreach (var placeholder in placeholders)
        {
            Assert.False(placeholder.IsGenerated);
        }
    }

    [Fact]
    public void Generate_FloorPlaceholderNamesMatchFloorNumbers()
    {
        var (graph, _) = Generate();
        var placeholders = graph.FindAllByTag("floor_placeholder");
        foreach (var placeholder in placeholders)
        {
            Assert.Equal($"Floor {placeholder.Floor}", placeholder.Name);
        }
    }

    [Fact]
    public void Generate_CanReachLobbyFromRoad()
    {
        var (graph, _) = Generate();
        var road = graph.GetRoad();
        var lobby = graph.FindByTag("entrance");
        var path = graph.FindPath(road.Id, lobby.Id);
        Assert.True(path.Count >= 2);
    }

    [Fact]
    public void ExpandFloor_CreatesRoomsWithCorrectTags()
    {
        var (graph, state) = Generate();
        var placeholder = graph.FindByTag("floor_placeholder");
        Assert.NotNull(placeholder);

        var expandedGraph = ApartmentBuildingGenerator.ExpandFloor(placeholder, state, new Random(42));

        var bedroom = expandedGraph.FindByTag("bedroom");
        var kitchen = expandedGraph.FindByTag("kitchen");
        var living = expandedGraph.FindByTag("living");
        var bathroom = expandedGraph.FindByTag("restroom");

        Assert.NotNull(bedroom);
        Assert.NotNull(kitchen);
        Assert.NotNull(living);
        Assert.NotNull(bathroom);
    }

    [Fact]
    public void ExpandFloor_CreatesCorrectNumberOfUnits()
    {
        var (graph, state) = Generate();
        var placeholder = graph.FindByTag("floor_placeholder");

        var expandedGraph = ApartmentBuildingGenerator.ExpandFloor(placeholder, state, new Random(42));

        var bedrooms = expandedGraph.FindAllByTag("bedroom");
        Assert.True(bedrooms.Count >= 4);
        Assert.True(bedrooms.Count <= 8);
    }

    [Fact]
    public void ExpandFloor_AllExpandedRoomsHaveCorrectAddressId()
    {
        var (graph, state) = Generate();
        var placeholder = graph.FindByTag("floor_placeholder");

        var expandedGraph = ApartmentBuildingGenerator.ExpandFloor(placeholder, state, new Random(42));

        foreach (var sub in expandedGraph.AllSublocations.Values)
        {
            Assert.Equal(1, sub.AddressId);
        }
    }

    [Fact]
    public void ExpandFloor_MarkesPlaceholderAsGenerated()
    {
        var (graph, state) = Generate();
        var placeholder = graph.FindByTag("floor_placeholder");
        Assert.False(placeholder.IsGenerated);

        ApartmentBuildingGenerator.ExpandFloor(placeholder, state, new Random(42));

        Assert.True(placeholder.IsGenerated);
    }
}
