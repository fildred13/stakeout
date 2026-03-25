using System;
using System.Collections.Generic;
using Stakeout.Simulation.Actions;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Objectives;
using Stakeout.Simulation.Scheduling;
using Stakeout.Simulation.Scheduling.Decomposition;
using Xunit;

namespace Stakeout.Tests.Simulation.Scheduling.Decomposition;

public class InhabitDecompositionTests
{
    private static SublocationGraph CreateHomeGraph()
    {
        var subs = new Dictionary<int, Sublocation>
        {
            { 1, new Sublocation { Id = 1, AddressId = 10, Name = "Road", Tags = new[] { "road" } } },
            { 2, new Sublocation { Id = 2, AddressId = 10, Name = "Front Door", Tags = new[] { "entrance" } } },
            { 3, new Sublocation { Id = 3, AddressId = 10, Name = "Hallway", Tags = new[] { "living" } } },
            { 4, new Sublocation { Id = 4, AddressId = 10, Name = "Kitchen", Tags = new[] { "kitchen" } } },
            { 5, new Sublocation { Id = 5, AddressId = 10, Name = "Bathroom", Tags = new[] { "restroom" } } },
            { 6, new Sublocation { Id = 6, AddressId = 10, Name = "Bedroom", Tags = new[] { "bedroom" } } },
        };
        var conns = new List<SublocationConnection>
        {
            new() { FromSublocationId = 1, ToSublocationId = 2, Type = ConnectionType.Door },
            new() { FromSublocationId = 2, ToSublocationId = 3, Type = ConnectionType.Door },
            new() { FromSublocationId = 3, ToSublocationId = 4, Type = ConnectionType.Door },
            new() { FromSublocationId = 3, ToSublocationId = 5, Type = ConnectionType.Door },
            new() { FromSublocationId = 3, ToSublocationId = 6, Type = ConnectionType.Door },
        };
        return new SublocationGraph(subs, conns);
    }

    [Fact]
    public void MorningRoutine_StartsAtBedroom()
    {
        var strategy = new InhabitDecomposition();
        var task = new SimTask { ActionType = ActionType.Sleep, TargetAddressId = 10 };
        var graph = CreateHomeGraph();
        var entries = strategy.Decompose(task, graph,
            new TimeSpan(6, 0, 0), new TimeSpan(9, 0, 0), new Random(42));
        Assert.NotEmpty(entries);
        Assert.Equal(6, entries[0].TargetSublocationId); // Bedroom
    }

    [Fact]
    public void MorningRoutine_EndsAtEntrance()
    {
        var strategy = new InhabitDecomposition();
        var task = new SimTask { ActionType = ActionType.Sleep, TargetAddressId = 10 };
        var graph = CreateHomeGraph();
        var entries = strategy.Decompose(task, graph,
            new TimeSpan(6, 0, 0), new TimeSpan(9, 0, 0), new Random(42));
        Assert.NotEmpty(entries);
        Assert.Equal(2, entries[^1].TargetSublocationId); // Entrance (Front Door)
    }

    [Fact]
    public void EveningRoutine_StartsAtEntrance()
    {
        var strategy = new InhabitDecomposition();
        var task = new SimTask { ActionType = ActionType.Idle, TargetAddressId = 10 };
        var graph = CreateHomeGraph();
        var entries = strategy.Decompose(task, graph,
            new TimeSpan(17, 0, 0), new TimeSpan(22, 0, 0), new Random(42));
        Assert.NotEmpty(entries);
        Assert.Equal(2, entries[0].TargetSublocationId); // Entrance (Front Door)
    }

    [Fact]
    public void EveningRoutine_EndsAtBedroom()
    {
        var strategy = new InhabitDecomposition();
        var task = new SimTask { ActionType = ActionType.Idle, TargetAddressId = 10 };
        var graph = CreateHomeGraph();
        var entries = strategy.Decompose(task, graph,
            new TimeSpan(17, 0, 0), new TimeSpan(22, 0, 0), new Random(42));
        Assert.NotEmpty(entries);
        Assert.Equal(6, entries[^1].TargetSublocationId); // Bedroom
    }

    [Fact]
    public void AllEntriesHaveAddressId()
    {
        var strategy = new InhabitDecomposition();
        var task = new SimTask { ActionType = ActionType.Idle, TargetAddressId = 10 };
        var graph = CreateHomeGraph();
        var entries = strategy.Decompose(task, graph,
            new TimeSpan(17, 0, 0), new TimeSpan(22, 0, 0), new Random(42));
        Assert.All(entries, e => Assert.Equal(10, e.TargetAddressId));
    }
}
