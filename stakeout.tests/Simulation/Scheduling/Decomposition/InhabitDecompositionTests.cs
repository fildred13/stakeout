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
            { 3, new Sublocation { Id = 3, AddressId = 10, Name = "Hallway", Tags = new[] { "living" } } },
            { 4, new Sublocation { Id = 4, AddressId = 10, Name = "Kitchen", Tags = new[] { "kitchen" } } },
            { 5, new Sublocation { Id = 5, AddressId = 10, Name = "Bathroom", Tags = new[] { "restroom" } } },
            { 6, new Sublocation { Id = 6, AddressId = 10, Name = "Bedroom", Tags = new[] { "bedroom" } } },
        };
        var conns = new List<SublocationConnection>
        {
            new() { Id = 100, FromSublocationId = 1, ToSublocationId = 3, Type = ConnectionType.Door, Name = "Front Door", Tags = new[] { "entrance" } },
            new() { Id = 101, FromSublocationId = 3, ToSublocationId = 4 },
            new() { Id = 102, FromSublocationId = 3, ToSublocationId = 5, Type = ConnectionType.Door },
            new() { Id = 103, FromSublocationId = 3, ToSublocationId = 6, Type = ConnectionType.Door },
        };
        return new SublocationGraph(subs, conns);
    }

    private static SublocationGraph CreateApartmentBuildingGraph()
    {
        var subs = new Dictionary<int, Sublocation>
        {
            { 1, new Sublocation { Id = 1, AddressId = 10, Name = "Road", Tags = new[] { "road" } } },
            { 2, new Sublocation { Id = 2, AddressId = 10, Name = "Lobby", Tags = new[] { "entrance", "public" } } },
            // Unit 1
            { 10, new Sublocation { Id = 10, AddressId = 10, Name = "Apt 1 Living", Tags = new[] { "living", "social", "unit_f1_1" }, Floor = 1 } },
            { 11, new Sublocation { Id = 11, AddressId = 10, Name = "Apt 1 Kitchen", Tags = new[] { "kitchen", "food", "unit_f1_1" }, Floor = 1 } },
            { 12, new Sublocation { Id = 12, AddressId = 10, Name = "Apt 1 Bathroom", Tags = new[] { "restroom", "unit_f1_1" }, Floor = 1 } },
            { 13, new Sublocation { Id = 13, AddressId = 10, Name = "Apt 1 Bedroom", Tags = new[] { "bedroom", "private", "unit_f1_1" }, Floor = 1 } },
            // Unit 2 (should NOT be selected)
            { 20, new Sublocation { Id = 20, AddressId = 10, Name = "Apt 2 Living", Tags = new[] { "living", "social", "unit_f1_2" }, Floor = 1 } },
            { 21, new Sublocation { Id = 21, AddressId = 10, Name = "Apt 2 Kitchen", Tags = new[] { "kitchen", "food", "unit_f1_2" }, Floor = 1 } },
            { 22, new Sublocation { Id = 22, AddressId = 10, Name = "Apt 2 Bathroom", Tags = new[] { "restroom", "unit_f1_2" }, Floor = 1 } },
            { 23, new Sublocation { Id = 23, AddressId = 10, Name = "Apt 2 Bedroom", Tags = new[] { "bedroom", "private", "unit_f1_2" }, Floor = 1 } },
        };
        var conns = new List<SublocationConnection>
        {
            new() { Id = 100, FromSublocationId = 1, ToSublocationId = 2, Type = ConnectionType.Door, Name = "Front Door", Tags = new[] { "entrance" } },
            new() { Id = 101, FromSublocationId = 2, ToSublocationId = 10, Type = ConnectionType.Door },
            new() { Id = 102, FromSublocationId = 10, ToSublocationId = 11 },
            new() { Id = 103, FromSublocationId = 10, ToSublocationId = 12, Type = ConnectionType.Door },
            new() { Id = 104, FromSublocationId = 10, ToSublocationId = 13, Type = ConnectionType.Door },
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
    public void MorningRoutine_EndsAtRoad()
    {
        var strategy = new InhabitDecomposition();
        var task = new SimTask { ActionType = ActionType.Sleep, TargetAddressId = 10 };
        var graph = CreateHomeGraph();
        var entries = strategy.Decompose(task, graph,
            new TimeSpan(6, 0, 0), new TimeSpan(9, 0, 0), new Random(42));
        Assert.NotEmpty(entries);
        Assert.Equal(1, entries[^1].TargetSublocationId); // Road (leaving)
        Assert.Equal(3, entries[^2].TargetSublocationId); // Entrance (Hallway)
    }

    [Fact]
    public void EveningRoutine_StartsAtRoad()
    {
        var strategy = new InhabitDecomposition();
        var task = new SimTask { ActionType = ActionType.Idle, TargetAddressId = 10 };
        var graph = CreateHomeGraph();
        var entries = strategy.Decompose(task, graph,
            new TimeSpan(17, 0, 0), new TimeSpan(22, 0, 0), new Random(42));
        Assert.NotEmpty(entries);
        Assert.Equal(1, entries[0].TargetSublocationId); // Road (arriving)
        Assert.Equal(3, entries[1].TargetSublocationId); // Entrance (Hallway)
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

    [Fact]
    public void EveningRoutine_WithUnitTag_UsesOnlyUnitRooms()
    {
        var strategy = new InhabitDecomposition();
        var task = new SimTask { ActionType = ActionType.Idle, TargetAddressId = 10, UnitTag = "unit_f1_1" };
        var graph = CreateApartmentBuildingGraph();
        var entries = strategy.Decompose(task, graph,
            new TimeSpan(17, 0, 0), new TimeSpan(22, 0, 0), new Random(42));
        Assert.NotEmpty(entries);
        var unitRoomIds = new HashSet<int> { 10, 11, 12, 13 };
        var structuralIds = new HashSet<int> { 1, 2 };
        foreach (var entry in entries)
        {
            if (entry.TargetSublocationId.HasValue && !structuralIds.Contains(entry.TargetSublocationId.Value))
            {
                Assert.Contains(entry.TargetSublocationId.Value, unitRoomIds);
            }
        }
    }
}
