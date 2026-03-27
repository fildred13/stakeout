using System;
using System.Collections.Generic;
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
    public void Generate_HasNoFloorPlaceholders()
    {
        var (graph, _) = Generate();
        var placeholders = graph.FindAllByTag("floor_placeholder");
        Assert.Empty(placeholders);
    }

    [Fact]
    public void Generate_HasHallwaysForEachFloor()
    {
        var (graph, _) = Generate();
        var hallways = graph.FindAllByTag("hallway");
        Assert.True(hallways.Count >= 4);
        Assert.True(hallways.Count <= 20);
    }

    [Fact]
    public void Generate_HasBedroomsWithUnitTags()
    {
        var (graph, _) = Generate();
        var bedrooms = graph.FindAllByTag("bedroom");
        Assert.NotEmpty(bedrooms);
        foreach (var bedroom in bedrooms)
        {
            Assert.True(
                bedroom.Tags.Any(t => t.StartsWith("unit_f")),
                $"Bedroom '{bedroom.Name}' missing unit tag");
        }
    }

    [Fact]
    public void Generate_UnitTagsAreFloorScoped()
    {
        var (graph, _) = Generate();
        var bedrooms = graph.FindAllByTag("bedroom");
        var floors = bedrooms.Select(b => b.Floor).Distinct().ToList();
        if (floors.Count >= 2)
        {
            var floor1Bedroom = bedrooms.First(b => b.Floor == floors[0]);
            var floor2Bedroom = bedrooms.First(b => b.Floor == floors[1]);
            var tag1 = floor1Bedroom.Tags.First(t => t.StartsWith("unit_f"));
            var tag2 = floor2Bedroom.Tags.First(t => t.StartsWith("unit_f"));
            Assert.NotEqual(tag1, tag2);
        }
    }

    [Fact]
    public void Generate_EachUnitHasFourRooms()
    {
        var (graph, _) = Generate();
        var unitTags = graph.AllSublocations.Values
            .SelectMany(s => s.Tags)
            .Where(t => t.StartsWith("unit_f"))
            .Distinct()
            .ToList();

        Assert.NotEmpty(unitTags);
        foreach (var unitTag in unitTags)
        {
            var rooms = graph.FindAllByTag(unitTag);
            Assert.Equal(4, rooms.Count);
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
    public void Generate_CanReachBedroomFromRoad()
    {
        var (graph, _) = Generate();
        var road = graph.GetRoad();
        var bedroom = graph.FindByTag("bedroom");
        var path = graph.FindPath(road.Id, bedroom.Id);
        Assert.True(path.Count >= 2);
    }

    [Fact]
    public void Generate_UnitsPerFloorInRange()
    {
        var (graph, _) = Generate();
        var hallways = graph.FindAllByTag("hallway");
        foreach (var hallway in hallways)
        {
            int floor = hallway.Floor.Value;
            var unitTags = graph.AllSublocations.Values
                .Where(s => s.Floor == floor && s.Tags.Any(t => t.StartsWith("unit_f")))
                .SelectMany(s => s.Tags.Where(t => t.StartsWith("unit_f")))
                .Distinct()
                .ToList();
            Assert.True(unitTags.Count >= 4, $"Floor {floor} has {unitTags.Count} units, expected >= 4");
            Assert.True(unitTags.Count <= 8, $"Floor {floor} has {unitTags.Count} units, expected <= 8");
        }
    }
}
