using System;
using System.Collections.Generic;
using Stakeout.Simulation.Actions;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Objectives;
using Stakeout.Simulation.Scheduling;
using Stakeout.Simulation.Scheduling.Decomposition;
using Xunit;

namespace Stakeout.Tests.Simulation.Scheduling.Decomposition;

public class IntrudeDecompositionTests
{
    private static SublocationGraph CreateHomeGraphWithCovertEntry()
    {
        var subs = new Dictionary<int, Sublocation>
        {
            { 1, new Sublocation { Id = 1, AddressId = 10, Name = "Road", Tags = new[] { "road" } } },
            { 2, new Sublocation { Id = 2, AddressId = 10, Name = "Front Door", Tags = new[] { "entrance" } } },
            { 3, new Sublocation { Id = 3, AddressId = 10, Name = "Hallway", Tags = new[] { "living" } } },
            { 4, new Sublocation { Id = 4, AddressId = 10, Name = "Bedroom", Tags = new[] { "bedroom" } } },
            { 5, new Sublocation { Id = 5, AddressId = 10, Name = "Back Window", Tags = new[] { "covert_entry" } } },
        };
        var conns = new List<SublocationConnection>
        {
            new() { FromSublocationId = 1, ToSublocationId = 2, Type = ConnectionType.Door },
            new() { FromSublocationId = 2, ToSublocationId = 3, Type = ConnectionType.Door },
            new() { FromSublocationId = 3, ToSublocationId = 4, Type = ConnectionType.Door },
            new() { FromSublocationId = 1, ToSublocationId = 5, Type = ConnectionType.Window },
            new() { FromSublocationId = 5, ToSublocationId = 3, Type = ConnectionType.OpenPassage },
        };
        return new SublocationGraph(subs, conns);
    }

    private static SublocationGraph CreateHomeGraphWithoutCovertEntry()
    {
        var subs = new Dictionary<int, Sublocation>
        {
            { 1, new Sublocation { Id = 1, AddressId = 10, Name = "Road", Tags = new[] { "road" } } },
            { 2, new Sublocation { Id = 2, AddressId = 10, Name = "Front Door", Tags = new[] { "entrance" } } },
            { 3, new Sublocation { Id = 3, AddressId = 10, Name = "Hallway", Tags = new[] { "living" } } },
            { 4, new Sublocation { Id = 4, AddressId = 10, Name = "Bedroom", Tags = new[] { "bedroom" } } },
        };
        var conns = new List<SublocationConnection>
        {
            new() { FromSublocationId = 1, ToSublocationId = 2, Type = ConnectionType.Door },
            new() { FromSublocationId = 2, ToSublocationId = 3, Type = ConnectionType.Door },
            new() { FromSublocationId = 3, ToSublocationId = 4, Type = ConnectionType.Door },
        };
        return new SublocationGraph(subs, conns);
    }

    [Fact]
    public void UsesCovertEntry_NotMainEntrance()
    {
        var strategy = new IntrudeDecomposition();
        var task = new SimTask { ActionType = ActionType.KillPerson, TargetAddressId = 10 };
        var graph = CreateHomeGraphWithCovertEntry();
        var entries = strategy.Decompose(task, graph,
            new TimeSpan(2, 0, 0), new TimeSpan(4, 0, 0), new Random(42));
        Assert.NotEmpty(entries);
        Assert.Equal(5, entries[0].TargetSublocationId); // Back Window (covert_entry)
        Assert.NotEqual(2, entries[0].TargetSublocationId); // Not Front Door
    }

    [Fact]
    public void PathfindsToTargetBedroom()
    {
        var strategy = new IntrudeDecomposition();
        var task = new SimTask { ActionType = ActionType.KillPerson, TargetAddressId = 10 };
        var graph = CreateHomeGraphWithCovertEntry();
        var entries = strategy.Decompose(task, graph,
            new TimeSpan(2, 0, 0), new TimeSpan(4, 0, 0), new Random(42));
        Assert.Contains(entries, e => e.TargetSublocationId == 4); // Bedroom
    }

    [Fact]
    public void ExitsViaCovertEntry()
    {
        var strategy = new IntrudeDecomposition();
        var task = new SimTask { ActionType = ActionType.KillPerson, TargetAddressId = 10 };
        var graph = CreateHomeGraphWithCovertEntry();
        var entries = strategy.Decompose(task, graph,
            new TimeSpan(2, 0, 0), new TimeSpan(4, 0, 0), new Random(42));
        Assert.NotEmpty(entries);
        Assert.Equal(5, entries[^1].TargetSublocationId); // Back Window (covert_entry)
    }

    [Fact]
    public void FallsBackToEntrance_WhenNoCovertEntry()
    {
        var strategy = new IntrudeDecomposition();
        var task = new SimTask { ActionType = ActionType.KillPerson, TargetAddressId = 10 };
        var graph = CreateHomeGraphWithoutCovertEntry();
        var entries = strategy.Decompose(task, graph,
            new TimeSpan(2, 0, 0), new TimeSpan(4, 0, 0), new Random(42));
        Assert.NotEmpty(entries);
        Assert.Equal(2, entries[0].TargetSublocationId); // Front Door (entrance fallback)
    }

    [Fact]
    public void AllEntriesHaveKillPersonAction()
    {
        var strategy = new IntrudeDecomposition();
        var task = new SimTask { ActionType = ActionType.KillPerson, TargetAddressId = 10 };
        var graph = CreateHomeGraphWithCovertEntry();
        var entries = strategy.Decompose(task, graph,
            new TimeSpan(2, 0, 0), new TimeSpan(4, 0, 0), new Random(42));
        Assert.All(entries, e => Assert.Equal(ActionType.KillPerson, e.Action));
    }
}
