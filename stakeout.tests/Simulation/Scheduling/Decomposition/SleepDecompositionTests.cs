using System;
using System.Collections.Generic;
using Stakeout.Simulation.Actions;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Objectives;
using Stakeout.Simulation.Scheduling;
using Stakeout.Simulation.Scheduling.Decomposition;
using Xunit;

namespace Stakeout.Tests.Simulation.Scheduling.Decomposition;

public class SleepDecompositionTests
{
    private static SublocationGraph CreateApartmentBuildingGraph()
    {
        var subs = new Dictionary<int, Sublocation>
        {
            { 1, new Sublocation { Id = 1, AddressId = 10, Name = "Road", Tags = new[] { "road" } } },
            { 2, new Sublocation { Id = 2, AddressId = 10, Name = "Lobby", Tags = new[] { "entrance", "public" } } },
            { 10, new Sublocation { Id = 10, AddressId = 10, Name = "Apt 1 Bedroom", Tags = new[] { "bedroom", "private", "unit_f1_1" }, Floor = 1 } },
            { 11, new Sublocation { Id = 11, AddressId = 10, Name = "Apt 2 Bedroom", Tags = new[] { "bedroom", "private", "unit_f1_2" }, Floor = 1 } },
        };
        var conns = new List<SublocationConnection>
        {
            new() { Id = 100, FromSublocationId = 1, ToSublocationId = 2, Type = ConnectionType.Door, Tags = new[] { "entrance" } },
        };
        return new SublocationGraph(subs, conns);
    }

    private static SublocationGraph CreateSuburbanHomeGraph()
    {
        var subs = new Dictionary<int, Sublocation>
        {
            { 1, new Sublocation { Id = 1, AddressId = 10, Name = "Road", Tags = new[] { "road" } } },
            { 2, new Sublocation { Id = 2, AddressId = 10, Name = "Bedroom", Tags = new[] { "bedroom" } } },
        };
        var conns = new List<SublocationConnection>
        {
            new() { Id = 100, FromSublocationId = 1, ToSublocationId = 2, Type = ConnectionType.Door, Tags = new[] { "entrance" } },
        };
        return new SublocationGraph(subs, conns);
    }

    [Fact]
    public void Decompose_WithUnitTag_SelectsCorrectBedroom()
    {
        var strategy = new SleepDecomposition();
        var task = new SimTask { ActionType = ActionType.Sleep, TargetAddressId = 10, UnitTag = "unit_f1_1" };
        var graph = CreateApartmentBuildingGraph();
        var entries = strategy.Decompose(task, graph,
            new TimeSpan(22, 0, 0), new TimeSpan(6, 0, 0), new Random(42));
        Assert.Single(entries);
        Assert.Equal(10, entries[0].TargetSublocationId);
    }

    [Fact]
    public void Decompose_WithoutUnitTag_SelectsFirstBedroom()
    {
        var strategy = new SleepDecomposition();
        var task = new SimTask { ActionType = ActionType.Sleep, TargetAddressId = 10 };
        var graph = CreateSuburbanHomeGraph();
        var entries = strategy.Decompose(task, graph,
            new TimeSpan(22, 0, 0), new TimeSpan(6, 0, 0), new Random(42));
        Assert.Single(entries);
        Assert.Equal(2, entries[0].TargetSublocationId);
    }
}
