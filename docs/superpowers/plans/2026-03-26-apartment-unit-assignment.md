# Apartment Unit Assignment Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Assign apartment residents to specific units, generate buildings eagerly, and allow building reuse across people.

**Architecture:** Add `HomeUnitTag` to Person and `UnitTag` to SimTask. Rewrite ApartmentBuildingGenerator to eagerly generate all floors/units with floor-scoped tags. PersonGenerator picks or reuses a building and assigns a vacant unit. Decomposition strategies scope room lookups by unit tag.

**Tech Stack:** C# / .NET 8 / xUnit / Godot 4.6

**Spec:** `docs/superpowers/specs/2026-03-26-progressive-apartment-generation-design.md`

**CRITICAL: Never prefix shell commands with `cd`. The working directory is already the project root. Run commands directly (e.g., `dotnet test stakeout.tests/`, not `cd path && dotnet test`).**

---

### Task 1: Add `HomeUnitTag` to Person

**Files:**
- Modify: `src/simulation/entities/Person.cs`
- Modify: `stakeout.tests/Simulation/Entities/PersonTests.cs`

- [ ] **Step 1: Write failing test for HomeUnitTag**

Add to `PersonTests.cs`:

```csharp
[Fact]
public void HomeUnitTag_DefaultsToNull()
{
    var person = new Person();
    Assert.Null(person.HomeUnitTag);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test stakeout.tests/ --filter "HomeUnitTag_DefaultsToNull" -v minimal`
Expected: FAIL — `Person` has no `HomeUnitTag` property.

- [ ] **Step 3: Add HomeUnitTag property to Person**

In `src/simulation/entities/Person.cs`, add after the `HomeAddressId` property:

```csharp
public string HomeUnitTag { get; set; }
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test stakeout.tests/ --filter "HomeUnitTag_DefaultsToNull" -v minimal`
Expected: PASS

- [ ] **Step 5: Commit**

```
git add src/simulation/entities/Person.cs stakeout.tests/Simulation/Entities/PersonTests.cs
git commit -m "feat: add HomeUnitTag property to Person"
```

---

### Task 2: Add `UnitTag` to SimTask

**Files:**
- Modify: `src/simulation/objectives/Task.cs`
- Modify: `stakeout.tests/Simulation/Objectives/TaskTests.cs`

- [ ] **Step 1: Write failing test for UnitTag**

Add to `TaskTests.cs`:

```csharp
[Fact]
public void UnitTag_DefaultsToNull()
{
    var task = new SimTask();
    Assert.Null(task.UnitTag);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test stakeout.tests/ --filter "UnitTag_DefaultsToNull" -v minimal`
Expected: FAIL

- [ ] **Step 3: Add UnitTag property to SimTask**

In `src/simulation/objectives/Task.cs`, add after `TargetSublocationId`:

```csharp
public string UnitTag { get; set; }
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test stakeout.tests/ --filter "UnitTag_DefaultsToNull" -v minimal`
Expected: PASS

- [ ] **Step 5: Commit**

```
git add src/simulation/objectives/Task.cs stakeout.tests/Simulation/Objectives/TaskTests.cs
git commit -m "feat: add UnitTag property to SimTask"
```

---

### Task 3: Rewrite ApartmentBuildingGenerator for eager full generation

This is the largest task. The current generator creates a skeleton with floor placeholders + a separate `ExpandFloor` method. Replace with a single pass that generates everything.

**Files:**
- Modify: `src/simulation/sublocations/ApartmentBuildingGenerator.cs`
- Modify: `stakeout.tests/Simulation/Sublocations/ApartmentBuildingGeneratorTests.cs`

- [ ] **Step 1: Write failing tests for eager generation with unit tags**

Replace the entire test file `stakeout.tests/Simulation/Sublocations/ApartmentBuildingGeneratorTests.cs` with new tests. The old tests verified floor placeholders and `ExpandFloor` which no longer exist.

```csharp
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
            // Every bedroom must have a unit tag matching unit_f{floor}_{num}
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
        // Pick two bedrooms from different floors — their unit tags should differ
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
        // Find all distinct unit tags
        var unitTags = graph.AllSublocations.Values
            .SelectMany(s => s.Tags)
            .Where(t => t.StartsWith("unit_f"))
            .Distinct()
            .ToList();

        Assert.NotEmpty(unitTags);
        foreach (var unitTag in unitTags)
        {
            var rooms = graph.FindAllByTag(unitTag);
            Assert.Equal(4, rooms.Count); // bedroom, kitchen, living, bathroom
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
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test stakeout.tests/ --filter "ApartmentBuildingGeneratorTests" -v minimal`
Expected: Several tests fail (no unit tags, floor placeholders still exist, etc.)

- [ ] **Step 3: Rewrite ApartmentBuildingGenerator**

Replace the entire contents of `src/simulation/sublocations/ApartmentBuildingGenerator.cs`:

```csharp
using System;
using System.Collections.Generic;
using Stakeout.Simulation.Entities;

namespace Stakeout.Simulation.Sublocations;

public class ApartmentBuildingGenerator : ISublocationGenerator
{
    public SublocationGraph Generate(Address address, SimulationState state, Random rng)
    {
        var subs = new Dictionary<int, Sublocation>();
        var conns = new List<SublocationConnection>();

        Sublocation Make(string name, string[] tags, int? floor)
        {
            var sub = new Sublocation
            {
                Id = state.GenerateEntityId(),
                AddressId = address.Id,
                Name = name,
                Tags = tags,
                Floor = floor
            };
            subs[sub.Id] = sub;
            address.Sublocations[sub.Id] = sub;
            return sub;
        }

        void Connect(Sublocation from, Sublocation to, SublocationConnection template = null)
        {
            var conn = template ?? new SublocationConnection();
            conn.Id = state.GenerateEntityId();
            conn.FromSublocationId = from.Id;
            conn.ToSublocationId = to.Id;
            conns.Add(conn);
            address.Connections.Add(conn);
        }

        var road = Make("Road", new[] { "road" }, 0);
        var lobby = Make("Lobby", new[] { "entrance", "public" }, 0);
        var elevator = Make("Elevator", new[] { "elevator" }, null);

        Connect(road, lobby, new SublocationConnection
        {
            Type = ConnectionType.Door,
            Name = "Front Door",
            Tags = new[] { "entrance" },
            Lockable = new LockableProperty { Mechanism = LockMechanism.Key },
            Breakable = new BreakableProperty()
        });
        Connect(lobby, elevator, new SublocationConnection
        {
            Type = ConnectionType.Door,
            Name = "Elevator Doors (Lobby)"
        });

        int floorCount = rng.Next(4, 21);
        Sublocation prevHallway = lobby;

        for (int n = 1; n <= floorCount; n++)
        {
            var floorHallway = Make($"Floor {n} Hallway", new[] { "hallway" }, n);

            Connect(elevator, floorHallway, new SublocationConnection
            {
                Type = ConnectionType.Door,
                Name = $"Elevator Doors (Floor {n})"
            });
            Connect(prevHallway, floorHallway, new SublocationConnection
            {
                Type = ConnectionType.Stairs,
                Name = n == 1 ? "Stairs (Lobby to Floor 1)" : $"Stairs (Floor {n - 1} to {n})"
            });

            int unitCount = rng.Next(4, 9);
            for (int i = 1; i <= unitCount; i++)
            {
                var unitTag = $"unit_f{n}_{i}";

                var bedroom = Make($"Apt {i} Bedroom", new[] { "bedroom", "private", unitTag }, n);
                var kitchen = Make($"Apt {i} Kitchen", new[] { "kitchen", "food", unitTag }, n);
                var living = Make($"Apt {i} Living Room", new[] { "living", "social", unitTag }, n);
                var bathroom = Make($"Apt {i} Bathroom", new[] { "restroom", unitTag }, n);

                Connect(floorHallway, living, new SublocationConnection { Type = ConnectionType.Door });
                Connect(living, bedroom);
                Connect(living, kitchen);
                Connect(living, bathroom);
            }

            prevHallway = floorHallway;
        }

        return new SublocationGraph(subs, conns);
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test stakeout.tests/ --filter "ApartmentBuildingGeneratorTests" -v minimal`
Expected: All PASS

- [ ] **Step 5: Run full test suite to check for regressions**

Run: `dotnet test stakeout.tests/ -v minimal`
Expected: All PASS. Some tests elsewhere may reference `ExpandFloor` — if so, they will need updating in this step.

- [ ] **Step 6: Commit**

```
git add src/simulation/sublocations/ApartmentBuildingGenerator.cs stakeout.tests/Simulation/Sublocations/ApartmentBuildingGeneratorTests.cs
git commit -m "feat: rewrite ApartmentBuildingGenerator for eager full generation with unit tags"
```

---

### Task 4: Wire UnitTag through ObjectiveResolver

**Files:**
- Modify: `src/simulation/objectives/ObjectiveResolver.cs`
- Modify: `stakeout.tests/Simulation/Objectives/ObjectiveResolverTests.cs`

- [ ] **Step 1: Write failing tests for unitTag parameter**

Add to `ObjectiveResolverTests.cs`:

```csharp
[Fact]
public void CreateGetSleepObjective_SetsUnitTagOnTask()
{
    var state = new SimulationState();
    var objective = ObjectiveResolver.CreateGetSleepObjective(
        new TimeSpan(22, 0, 0), new TimeSpan(6, 0, 0), 1, "unit_f2_3");
    var tasks = ObjectiveResolver.ResolveTasks(new List<Objective> { objective }, state);
    Assert.Single(tasks);
    Assert.Equal("unit_f2_3", tasks[0].UnitTag);
}

[Fact]
public void CreateGetSleepObjective_NullUnitTag_TaskHasNullUnitTag()
{
    var state = new SimulationState();
    var objective = ObjectiveResolver.CreateGetSleepObjective(
        new TimeSpan(22, 0, 0), new TimeSpan(6, 0, 0), 1, null);
    var tasks = ObjectiveResolver.ResolveTasks(new List<Objective> { objective }, state);
    Assert.Single(tasks);
    Assert.Null(tasks[0].UnitTag);
}

[Fact]
public void CreateDefaultIdleObjective_SetsUnitTagOnTask()
{
    var state = new SimulationState();
    var objective = ObjectiveResolver.CreateDefaultIdleObjective(1, "unit_f1_5");
    var tasks = ObjectiveResolver.ResolveTasks(new List<Objective> { objective }, state);
    Assert.Single(tasks);
    Assert.Equal("unit_f1_5", tasks[0].UnitTag);
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test stakeout.tests/ --filter "ObjectiveResolverTests" -v minimal`
Expected: FAIL — method signatures don't accept `unitTag` parameter.

- [ ] **Step 3: Add unitTag parameter to factory methods**

In `src/simulation/objectives/ObjectiveResolver.cs`:

Change `CreateGetSleepObjective` signature to:
```csharp
public static Objective CreateGetSleepObjective(TimeSpan sleepTime, TimeSpan wakeTime, int homeAddressId, string unitTag = null)
```

Inside its `ResolveFunc`, add `UnitTag = unitTag` to the SimTask initializer.

Change `CreateDefaultIdleObjective` signature to:
```csharp
public static Objective CreateDefaultIdleObjective(int homeAddressId, string unitTag = null)
```

Inside its `ResolveFunc`, add `UnitTag = unitTag` to the SimTask initializer.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test stakeout.tests/ --filter "ObjectiveResolverTests" -v minimal`
Expected: All PASS

- [ ] **Step 5: Run full test suite for regressions**

Run: `dotnet test stakeout.tests/ -v minimal`
Expected: All PASS — existing callers use the default `null` value.

- [ ] **Step 6: Commit**

```
git add src/simulation/objectives/ObjectiveResolver.cs stakeout.tests/Simulation/Objectives/ObjectiveResolverTests.cs
git commit -m "feat: wire UnitTag through ObjectiveResolver factory methods"
```

---

### Task 5: Update SleepDecomposition to scope by unit tag

**Files:**
- Modify: `src/simulation/scheduling/decomposition/SleepDecomposition.cs`
- Create: `stakeout.tests/Simulation/Scheduling/Decomposition/SleepDecompositionTests.cs`

- [ ] **Step 1: Write tests for unit-scoped and unscoped sleep**

Create `stakeout.tests/Simulation/Scheduling/Decomposition/SleepDecompositionTests.cs`:

```csharp
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
        // Building with two units on floor 1 — only unit_f1_1 should be selected
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
        Assert.Equal(10, entries[0].TargetSublocationId); // Apt 1 Bedroom, not Apt 2
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
```

- [ ] **Step 2: Run tests to verify the unit-scoped test fails**

Run: `dotnet test stakeout.tests/ --filter "SleepDecompositionTests" -v minimal`
Expected: `Decompose_WithUnitTag_SelectsCorrectBedroom` FAILS (current code ignores UnitTag and picks first bedroom found). The unscoped test may pass.

- [ ] **Step 3: Update SleepDecomposition to scope by unit tag**

Replace `src/simulation/scheduling/decomposition/SleepDecomposition.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Objectives;

namespace Stakeout.Simulation.Scheduling.Decomposition;

public class SleepDecomposition : IDecompositionStrategy
{
    public List<ScheduleEntry> Decompose(SimTask task, SublocationGraph graph,
        TimeSpan startTime, TimeSpan endTime, Random rng)
    {
        var bedroom = FindRoom(graph, "bedroom", task.UnitTag);
        if (bedroom == null)
        {
            return new List<ScheduleEntry>
            {
                new ScheduleEntry
                {
                    Action = task.ActionType,
                    StartTime = startTime,
                    EndTime = endTime,
                    TargetAddressId = task.TargetAddressId
                }
            };
        }

        return new List<ScheduleEntry>
        {
            new ScheduleEntry
            {
                Action = task.ActionType,
                StartTime = startTime,
                EndTime = endTime,
                TargetAddressId = task.TargetAddressId,
                TargetSublocationId = bedroom.Id
            }
        };
    }

    internal static Sublocation FindRoom(SublocationGraph graph, string roomTag, string unitTag)
    {
        if (unitTag != null)
        {
            var unitRooms = graph.FindAllByTag(unitTag);
            return unitRooms.FirstOrDefault(s => s.HasTag(roomTag));
        }
        return graph.FindByTag(roomTag);
    }
}
```

Note: `FindRoom` is `internal static` so `InhabitDecomposition` can reuse it in Task 6.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test stakeout.tests/ --filter "SleepDecompositionTests" -v minimal`
Expected: All PASS

- [ ] **Step 5: Commit**

```
git add src/simulation/scheduling/decomposition/SleepDecomposition.cs stakeout.tests/Simulation/Scheduling/Decomposition/SleepDecompositionTests.cs
git commit -m "feat: SleepDecomposition scopes bedroom lookup by unit tag"
```

---

### Task 6: Update InhabitDecomposition to scope by unit tag

**Files:**
- Modify: `src/simulation/scheduling/decomposition/InhabitDecomposition.cs`
- Modify: `stakeout.tests/Simulation/Scheduling/Decomposition/InhabitDecompositionTests.cs`

- [ ] **Step 1: Write failing test for unit-scoped inhabit**

Add to `InhabitDecompositionTests.cs` a new graph helper and test:

```csharp
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
public void EveningRoutine_WithUnitTag_UsesOnlyUnitRooms()
{
    var strategy = new InhabitDecomposition();
    var task = new SimTask { ActionType = ActionType.Idle, TargetAddressId = 10, UnitTag = "unit_f1_1" };
    var graph = CreateApartmentBuildingGraph();
    var entries = strategy.Decompose(task, graph,
        new TimeSpan(17, 0, 0), new TimeSpan(22, 0, 0), new Random(42));
    Assert.NotEmpty(entries);
    // All room entries (excluding road/entrance) should be from unit 1 (ids 10-13)
    var unitRoomIds = new HashSet<int> { 10, 11, 12, 13 };
    var structuralIds = new HashSet<int> { 1, 2 }; // road, entrance
    foreach (var entry in entries)
    {
        if (entry.TargetSublocationId.HasValue && !structuralIds.Contains(entry.TargetSublocationId.Value))
        {
            Assert.Contains(entry.TargetSublocationId.Value, unitRoomIds);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify the new test fails**

Run: `dotnet test stakeout.tests/ --filter "EveningRoutine_WithUnitTag_UsesOnlyUnitRooms" -v minimal`
Expected: FAIL — current code picks first `bedroom`/`kitchen`/etc. found, not scoped to unit.

- [ ] **Step 3: Update InhabitDecomposition to scope room lookups**

In `src/simulation/scheduling/decomposition/InhabitDecomposition.cs`, replace the room lookup section. Change lines that call `graph.FindByTag(...)` for room types to use `SleepDecomposition.FindRoom`:

```csharp
var road = graph.GetRoad();
var bedroom = SleepDecomposition.FindRoom(graph, "bedroom", task.UnitTag);
var restroom = SleepDecomposition.FindRoom(graph, "restroom", task.UnitTag);
var kitchen = SleepDecomposition.FindRoom(graph, "kitchen", task.UnitTag);
var entryResult = graph.FindEntryPoint("entrance");
var entrance = entryResult?.target;
```

And in the evening branch:
```csharp
var living = SleepDecomposition.FindRoom(graph, "living", task.UnitTag);
```

Note: `road`, `entrance` remain unscoped — they are shared building infrastructure.

- [ ] **Step 4: Run all InhabitDecomposition tests**

Run: `dotnet test stakeout.tests/ --filter "InhabitDecompositionTests" -v minimal`
Expected: All PASS (both old and new tests)

- [ ] **Step 5: Commit**

```
git add src/simulation/scheduling/decomposition/InhabitDecomposition.cs stakeout.tests/Simulation/Scheduling/Decomposition/InhabitDecompositionTests.cs
git commit -m "feat: InhabitDecomposition scopes room lookups by unit tag"
```

---

### Task 7: Update PersonGenerator for building reuse and unit assignment

**Files:**
- Modify: `src/simulation/PersonGenerator.cs`
- Modify: `stakeout.tests/Simulation/PersonGeneratorTests.cs`

- [ ] **Step 1: Write failing tests for apartment unit assignment and building reuse**

Add to `PersonGeneratorTests.cs`:

```csharp
[Fact]
public void GeneratePerson_ApartmentResident_HasHomeUnitTag()
{
    var state = CreateState();
    var gen = CreateGenerator();
    // Generate enough people to guarantee at least one apartment resident
    for (int i = 0; i < 100; i++)
    {
        gen.GeneratePerson(state);
    }
    var apartmentResident = state.People.Values
        .FirstOrDefault(p => state.Addresses[p.HomeAddressId].Type == AddressType.ApartmentBuilding);
    Assert.NotNull(apartmentResident); // With 100 people and 50% chance, extremely unlikely to have zero
    Assert.NotNull(apartmentResident.HomeUnitTag);
    Assert.StartsWith("unit_f", apartmentResident.HomeUnitTag);
}

[Fact]
public void GeneratePerson_SuburbanResident_HasNullHomeUnitTag()
{
    var state = CreateState();
    var gen = CreateGenerator();
    for (int i = 0; i < 100; i++)
    {
        gen.GeneratePerson(state);
    }
    var suburbanResident = state.People.Values
        .FirstOrDefault(p => state.Addresses[p.HomeAddressId].Type == AddressType.SuburbanHome);
    Assert.NotNull(suburbanResident);
    Assert.Null(suburbanResident.HomeUnitTag);
}

[Fact]
public void GeneratePerson_MultipleApartmentResidents_CanShareBuilding()
{
    var state = CreateState();
    var gen = CreateGenerator();
    // Generate enough people that building reuse should occur
    for (int i = 0; i < 100; i++)
    {
        gen.GeneratePerson(state);
    }
    var apartmentAddressIds = state.People.Values
        .Where(p => state.Addresses[p.HomeAddressId].Type == AddressType.ApartmentBuilding)
        .Select(p => p.HomeAddressId)
        .ToList();
    // With 50% reuse chance and ~100 people, some should share a building
    var grouped = apartmentAddressIds.GroupBy(id => id).Where(g => g.Count() > 1);
    Assert.NotEmpty(grouped);
}

[Fact]
public void GeneratePerson_SharedBuilding_DifferentUnitTags()
{
    var state = CreateState();
    var gen = CreateGenerator();
    for (int i = 0; i < 100; i++)
    {
        gen.GeneratePerson(state);
    }
    var byAddress = state.People.Values
        .Where(p => state.Addresses[p.HomeAddressId].Type == AddressType.ApartmentBuilding)
        .GroupBy(p => p.HomeAddressId)
        .Where(g => g.Count() > 1);

    foreach (var group in byAddress)
    {
        var tags = group.Select(p => p.HomeUnitTag).ToList();
        Assert.Equal(tags.Count, tags.Distinct().Count()); // All unique
    }
}
```

Also add `using System.Linq;` to the top of the file if not already present.

- [ ] **Step 2: Run tests to verify new tests fail**

Run: `dotnet test stakeout.tests/ --filter "PersonGeneratorTests" -v minimal`
Expected: New tests fail (HomeUnitTag is never set, no building reuse).

- [ ] **Step 3: Update PersonGenerator**

In `src/simulation/PersonGenerator.cs`, modify the home address generation section (around lines 31-33). Replace:

```csharp
var homeType = _random.NextDouble() < 0.5 ? AddressType.SuburbanHome : AddressType.ApartmentBuilding;
var homeAddress = _locationGenerator.GenerateAddress(state, homeType);
```

With:

```csharp
var homeType = _random.NextDouble() < 0.5 ? AddressType.SuburbanHome : AddressType.ApartmentBuilding;
var homeAddress = homeType == AddressType.ApartmentBuilding
    ? FindOrCreateApartmentBuilding(state)
    : _locationGenerator.GenerateAddress(state, homeType);
string homeUnitTag = null;
if (homeType == AddressType.ApartmentBuilding)
{
    homeUnitTag = AssignVacantUnit(state, homeAddress);
}
```

Then pass `homeUnitTag` to the objective factory methods (around lines 44-49):

```csharp
var objectives = new List<Objective>
{
    ObjectiveResolver.CreateGetSleepObjective(sleepTime, wakeTime, homeAddress.Id, homeUnitTag),
    ObjectiveResolver.CreateMaintainJobObjective(job.ShiftStart, job.ShiftEnd, workAddress.Id),
    ObjectiveResolver.CreateDefaultIdleObjective(homeAddress.Id, homeUnitTag)
};
```

And set `HomeUnitTag` on the person (around line 77 in the Person initializer):

```csharp
HomeUnitTag = homeUnitTag,
```

Add these two private methods:

```csharp
private Address FindOrCreateApartmentBuilding(SimulationState state)
{
    if (_random.NextDouble() < 0.5)
    {
        var apartments = new List<Address>();
        foreach (var addr in state.Addresses.Values)
        {
            if (addr.Type == AddressType.ApartmentBuilding)
                apartments.Add(addr);
        }
        if (apartments.Count > 0)
        {
            var candidate = apartments[_random.Next(apartments.Count)];
            if (HasVacancy(state, candidate))
                return candidate;
        }
    }
    return _locationGenerator.GenerateAddress(state, AddressType.ApartmentBuilding);
}

private static bool HasVacancy(SimulationState state, Address building)
{
    var occupiedTags = new HashSet<string>();
    foreach (var person in state.People.Values)
    {
        if (person.HomeAddressId == building.Id && person.HomeUnitTag != null)
            occupiedTags.Add(person.HomeUnitTag);
    }

    foreach (var sub in building.Sublocations.Values)
    {
        foreach (var tag in sub.Tags)
        {
            if (tag.StartsWith("unit_f") && !occupiedTags.Contains(tag))
                return true;
        }
    }
    return false;
}

private string AssignVacantUnit(SimulationState state, Address building)
{
    var occupiedTags = new HashSet<string>();
    foreach (var person in state.People.Values)
    {
        if (person.HomeAddressId == building.Id && person.HomeUnitTag != null)
            occupiedTags.Add(person.HomeUnitTag);
    }

    var vacantTags = new HashSet<string>();
    foreach (var sub in building.Sublocations.Values)
    {
        foreach (var tag in sub.Tags)
        {
            if (tag.StartsWith("unit_f") && !occupiedTags.Contains(tag))
                vacantTags.Add(tag);
        }
    }

    var list = new List<string>(vacantTags);
    return list[_random.Next(list.Count)];
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test stakeout.tests/ --filter "PersonGeneratorTests" -v minimal`
Expected: All PASS

- [ ] **Step 5: Run full test suite**

Run: `dotnet test stakeout.tests/ -v minimal`
Expected: All PASS

- [ ] **Step 6: Commit**

```
git add src/simulation/PersonGenerator.cs stakeout.tests/Simulation/PersonGeneratorTests.cs
git commit -m "feat: PersonGenerator assigns apartment units and reuses buildings"
```

---

### Task 8: Full integration verification

- [ ] **Step 1: Run the complete test suite**

Run: `dotnet test stakeout.tests/ -v minimal`
Expected: All PASS

- [ ] **Step 2: Verify the build compiles cleanly**

Run: `dotnet build stakeout.csproj`
Expected: Build succeeded, 0 errors, 0 warnings (or only pre-existing warnings)

- [ ] **Step 3: Commit any remaining fixes if needed**

If any tests needed fixing in Step 1, commit them.
