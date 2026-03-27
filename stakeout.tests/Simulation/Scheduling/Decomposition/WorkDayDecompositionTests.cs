using System;
using System.Collections.Generic;
using System.Linq;
using Stakeout.Simulation.Actions;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Objectives;
using Stakeout.Simulation.Scheduling;
using Stakeout.Simulation.Scheduling.Decomposition;
using Xunit;

namespace Stakeout.Tests.Simulation.Scheduling.Decomposition;

public class WorkDayDecompositionTests
{
    private static SublocationGraph CreateOfficeGraph()
    {
        var subs = new Dictionary<int, Sublocation>
        {
            { 1, new Sublocation { Id = 1, AddressId = 10, Name = "Road", Tags = new[] { "road" } } },
            { 2, new Sublocation { Id = 2, AddressId = 10, Name = "Lobby", Tags = new[] { "lobby" } } },
            { 3, new Sublocation { Id = 3, AddressId = 10, Name = "Cubicle Area", Tags = new[] { "work_area" } } },
            { 4, new Sublocation { Id = 4, AddressId = 10, Name = "Break Room", Tags = new[] { "food" } } },
            { 5, new Sublocation { Id = 5, AddressId = 10, Name = "Restroom", Tags = new[] { "restroom" } } },
        };
        var conns = new List<SublocationConnection>
        {
            new() { Id = 100, FromSublocationId = 1, ToSublocationId = 2, Type = ConnectionType.Door, Name = "Main Entrance", Tags = new[] { "entrance" } },
            new() { Id = 101, FromSublocationId = 2, ToSublocationId = 3, Type = ConnectionType.Door },
            new() { Id = 102, FromSublocationId = 3, ToSublocationId = 4, Type = ConnectionType.OpenPassage },
            new() { Id = 103, FromSublocationId = 3, ToSublocationId = 5, Type = ConnectionType.Door },
        };
        return new SublocationGraph(subs, conns);
    }

    [Fact]
    public void Decompose_StartsAtEntrance()
    {
        var strategy = new WorkDayDecomposition();
        var task = new SimTask { ActionType = ActionType.Work, TargetAddressId = 10 };
        var graph = CreateOfficeGraph();
        var entries = strategy.Decompose(task, graph,
            new TimeSpan(9, 0, 0), new TimeSpan(17, 0, 0), new Random(42));
        Assert.NotEmpty(entries);
        Assert.Equal(2, entries[0].TargetSublocationId); // Lobby (entrance)
    }

    [Fact]
    public void Decompose_GoesToWorkArea()
    {
        var strategy = new WorkDayDecomposition();
        var task = new SimTask { ActionType = ActionType.Work, TargetAddressId = 10 };
        var graph = CreateOfficeGraph();
        var entries = strategy.Decompose(task, graph,
            new TimeSpan(9, 0, 0), new TimeSpan(17, 0, 0), new Random(42));
        Assert.Contains(entries, e => e.TargetSublocationId == 3); // Cubicle Area
    }

    [Fact]
    public void Decompose_EndsAtEntrance()
    {
        var strategy = new WorkDayDecomposition();
        var task = new SimTask { ActionType = ActionType.Work, TargetAddressId = 10 };
        var graph = CreateOfficeGraph();
        var entries = strategy.Decompose(task, graph,
            new TimeSpan(9, 0, 0), new TimeSpan(17, 0, 0), new Random(42));
        var last = entries[^1];
        Assert.Equal(2, last.TargetSublocationId); // Lobby (entrance)
    }

    [Fact]
    public void Decompose_AllEntriesHaveAddressId()
    {
        var strategy = new WorkDayDecomposition();
        var task = new SimTask { ActionType = ActionType.Work, TargetAddressId = 10 };
        var graph = CreateOfficeGraph();
        var entries = strategy.Decompose(task, graph,
            new TimeSpan(9, 0, 0), new TimeSpan(17, 0, 0), new Random(42));
        Assert.All(entries, e => Assert.Equal(10, e.TargetAddressId));
    }

    [Fact]
    public void Decompose_NoWorkArea_ReturnsEntranceOnly()
    {
        var subs = new Dictionary<int, Sublocation>
        {
            { 1, new Sublocation { Id = 1, AddressId = 10, Name = "Road", Tags = new[] { "road" } } },
            { 2, new Sublocation { Id = 2, AddressId = 10, Name = "Lobby", Tags = new[] { "lobby" } } },
        };
        var graph = new SublocationGraph(subs, new List<SublocationConnection>
        {
            new() { Id = 100, FromSublocationId = 1, ToSublocationId = 2, Type = ConnectionType.Door, Name = "Main Entrance", Tags = new[] { "entrance" } },
        });
        var strategy = new WorkDayDecomposition();
        var task = new SimTask { ActionType = ActionType.Work, TargetAddressId = 10 };
        var entries = strategy.Decompose(task, graph,
            new TimeSpan(9, 0, 0), new TimeSpan(17, 0, 0), new Random(42));
        Assert.NotEmpty(entries);
        Assert.All(entries, e => Assert.Equal(2, e.TargetSublocationId));
    }
}
