// stakeout.tests/Simulation/Sublocations/OfficeGeneratorTests.cs
using System;
using System.Linq;
using Stakeout.Simulation;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Sublocations;
using Xunit;

namespace Stakeout.Tests.Simulation.Sublocations;

public class OfficeGeneratorTests
{
    private SublocationGraph Generate(int seed = 42)
    {
        var state = new SimulationState();
        var gen = new OfficeGenerator();
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
    public void Generate_HasWorkArea()
    {
        var graph = Generate();
        var workArea = graph.FindByTag("work_area");
        Assert.NotNull(workArea);
    }

    [Fact]
    public void Generate_HasFoodArea()
    {
        var graph = Generate();
        var food = graph.FindByTag("food");
        Assert.NotNull(food);
    }

    [Fact]
    public void Generate_HasRestroom()
    {
        var graph = Generate();
        var restroom = graph.FindByTag("restroom");
        Assert.NotNull(restroom);
    }

    [Fact]
    public void Generate_HasElevatorOrStairsConnections()
    {
        var state = new SimulationState();
        var gen = new OfficeGenerator();
        gen.Generate(addressId: 1, state, new Random(42));

        bool hasElevator = state.SublocationConnections.Any(c => c.Type == ConnectionType.Elevator);
        bool hasStairs = state.SublocationConnections.Any(c => c.Type == ConnectionType.Stairs);
        Assert.True(hasElevator || hasStairs);
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
    public void Generate_CanReachWorkAreaFromRoad()
    {
        var graph = Generate();
        var road = graph.GetRoad();
        var workArea = graph.FindByTag("work_area");
        var path = graph.FindPath(road.Id, workArea.Id);
        Assert.True(path.Count >= 2);
    }

    [Fact]
    public void Generate_FloorCountVariesBySeed()
    {
        // Run with different seeds to verify floor count varies (1-5 floors)
        var floorCounts = new System.Collections.Generic.HashSet<int>();
        for (int seed = 0; seed < 20; seed++)
        {
            var graph = Generate(seed);
            // Count distinct non-zero floors that have work areas
            var floors = graph.AllSublocations.Values
                .Where(s => s.Floor.HasValue && s.Floor.Value > 0)
                .Select(s => s.Floor.Value)
                .Distinct()
                .Count();
            floorCounts.Add(floors);
        }
        // With 20 seeds there should be more than 1 unique floor count
        Assert.True(floorCounts.Count > 1, $"Expected variation in floor count, but only got: {string.Join(", ", floorCounts)}");
    }
}
