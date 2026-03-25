# Sublocations & Recursive Task System Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add sublocation hierarchy (rooms/zones within addresses) with recursive task decomposition, NPC intra-location movement, and two visualization prototypes.

**Architecture:** Extends the existing Objective → Task → Schedule pipeline. TaskResolver decomposes address-level tasks into sublocation-level sub-tasks using pluggable decomposition strategies and graph pathfinding. The task tree is an intermediate build-time representation; runtime uses the existing flat `DailySchedule` extended with `TargetSublocationId`. Six sublocation generators produce location graphs for each AddressType.

**Tech Stack:** Godot 4.6, C# / .NET 8, XUnit for tests

**Spec:** `docs/superpowers/specs/2026-03-25-sublocations-design.md`

**Working directory is the project root.** CRITICAL: Never prefix shell commands with `cd`. Run commands directly (e.g., `git add file.cs`, not `cd path && git add file.cs`).

---

## File Structure

### New Files

| File | Responsibility |
|------|---------------|
| `src/simulation/entities/Sublocation.cs` | Sublocation entity, SublocationConnection, ConnectionType enum |
| `src/simulation/entities/SublocationGraph.cs` | Graph wrapper for an address's sublocations — tag queries, pathfinding |
| `src/simulation/sublocations/SuburbanHomeGenerator.cs` | Generates sublocation graph for SuburbanHome |
| `src/simulation/sublocations/OfficeGenerator.cs` | Generates sublocation graph for Office |
| `src/simulation/sublocations/DinerGenerator.cs` | Generates sublocation graph for Diner |
| `src/simulation/sublocations/DiveBarGenerator.cs` | Generates sublocation graph for DiveBar |
| `src/simulation/sublocations/ApartmentBuildingGenerator.cs` | Generates sublocation graph for ApartmentBuilding (lazy floors) |
| `src/simulation/sublocations/ParkGenerator.cs` | Generates sublocation graph for Park |
| `src/simulation/sublocations/ISublocationGenerator.cs` | Interface for sublocation generators |
| `src/simulation/sublocations/SublocationGeneratorRegistry.cs` | Maps AddressType → generator |
| `src/simulation/scheduling/TaskResolver.cs` | Decomposes root tasks into sublocation sub-tasks using strategies |
| `src/simulation/scheduling/decomposition/IDecompositionStrategy.cs` | Interface for decomposition strategies |
| `src/simulation/scheduling/decomposition/WorkDayDecomposition.cs` | Strategy for Work action at workplaces |
| `src/simulation/scheduling/decomposition/InhabitDecomposition.cs` | Strategy for home activities (idle, sleep) |
| `src/simulation/scheduling/decomposition/PatronizeDecomposition.cs` | Strategy for customer visits (diner, bar) |
| `src/simulation/scheduling/decomposition/StaffShiftDecomposition.cs` | Strategy for staff at commercial locations |
| `src/simulation/scheduling/decomposition/IntrudeDecomposition.cs` | Strategy for covert entry (kill, break-in) |
| `src/simulation/scheduling/decomposition/VisitDecomposition.cs` | Generic fallback strategy |
| `scenes/address/GraphView.cs` | Node-and-edge sublocation graph visualization |
| `scenes/address/GraphView.tscn` | Scene file for graph view |
| `scenes/address/BlueprintView.cs` | Overhead floor plan visualization |
| `scenes/address/BlueprintView.tscn` | Scene file for blueprint view |
| `stakeout.benchmarks/Program.cs` | Load test harness entry point |
| `stakeout.benchmarks/stakeout.benchmarks.csproj` | Benchmark project file |

### New Test Files

| File | Tests |
|------|-------|
| `stakeout.tests/Simulation/Entities/SublocationTests.cs` | Sublocation entity, tags |
| `stakeout.tests/Simulation/Entities/SublocationGraphTests.cs` | Graph queries, pathfinding |
| `stakeout.tests/Simulation/Sublocations/SuburbanHomeGeneratorTests.cs` | Home graph structure |
| `stakeout.tests/Simulation/Sublocations/OfficeGeneratorTests.cs` | Office graph structure |
| `stakeout.tests/Simulation/Sublocations/DinerGeneratorTests.cs` | Diner graph structure |
| `stakeout.tests/Simulation/Sublocations/DiveBarGeneratorTests.cs` | DiveBar graph structure |
| `stakeout.tests/Simulation/Sublocations/ApartmentBuildingGeneratorTests.cs` | Apartment graph + lazy gen |
| `stakeout.tests/Simulation/Sublocations/ParkGeneratorTests.cs` | Park graph structure |
| `stakeout.tests/Simulation/Scheduling/TaskResolverTests.cs` | Task decomposition |
| `stakeout.tests/Simulation/Scheduling/Decomposition/WorkDayDecompositionTests.cs` | WorkDay strategy |
| `stakeout.tests/Simulation/Scheduling/Decomposition/InhabitDecompositionTests.cs` | Inhabit strategy |
| `stakeout.tests/Simulation/Scheduling/Decomposition/IntrudeDecompositionTests.cs` | Intrude strategy |

### Modified Files

| File | Changes |
|------|---------|
| `src/simulation/entities/AddressType.cs` | Add `ApartmentBuilding`, `Park` to enum; add `Public` category |
| `src/simulation/entities/Person.cs` | Add `CurrentSublocationId` field |
| `src/simulation/SimulationState.cs` | Add `Sublocations` and `SublocationConnections` collections |
| `src/simulation/LocationGenerator.cs` | Call sublocation generator after creating address |
| `src/simulation/scheduling/DailySchedule.cs` | Add `TargetSublocationId` to `ScheduleEntry` |
| `src/simulation/scheduling/ScheduleBuilder.cs` | Integrate TaskResolver to decompose tasks before building schedule |
| `src/simulation/scheduling/PersonBehavior.cs` | Update `CurrentSublocationId` on transitions |
| `src/simulation/SimulationManager.cs` | Pass sublocation data through pipeline |
| `scenes/address/AddressView.cs` | Add graph/blueprint view toggle |
| `scenes/game_shell/GameShell.cs` | Person Inspector shows sublocation |

---

## Task 1: Sublocation Entity & Data Model

**Files:**
- Create: `src/simulation/entities/Sublocation.cs`
- Modify: `src/simulation/SimulationState.cs`
- Test: `stakeout.tests/Simulation/Entities/SublocationTests.cs`

- [ ] **Step 1: Write failing tests for Sublocation entity and ConnectionType**

```csharp
// stakeout.tests/Simulation/Entities/SublocationTests.cs
using Stakeout.Simulation.Entities;
using Xunit;

namespace Stakeout.Tests.Simulation.Entities;

public class SublocationTests
{
    [Fact]
    public void Constructor_SetsDefaultValues()
    {
        var sub = new Sublocation();

        Assert.Equal(0, sub.Id);
        Assert.Equal(0, sub.AddressId);
        Assert.Null(sub.ParentId);
        Assert.Null(sub.Name);
        Assert.Empty(sub.Tags);
        Assert.Null(sub.Floor);
        Assert.True(sub.IsGenerated);
    }

    [Fact]
    public void HasTag_TagPresent_ReturnsTrue()
    {
        var sub = new Sublocation { Tags = new[] { "entrance", "public" } };

        Assert.True(sub.HasTag("entrance"));
        Assert.True(sub.HasTag("public"));
    }

    [Fact]
    public void HasTag_TagAbsent_ReturnsFalse()
    {
        var sub = new Sublocation { Tags = new[] { "entrance" } };

        Assert.False(sub.HasTag("work_area"));
    }

    [Fact]
    public void SublocationConnection_Defaults()
    {
        var conn = new SublocationConnection();

        Assert.Equal(0, conn.FromSublocationId);
        Assert.Equal(0, conn.ToSublocationId);
        Assert.Equal(ConnectionType.Door, conn.Type);
        Assert.True(conn.IsBidirectional);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test stakeout.tests/ --filter "FullyQualifiedName~SublocationTests" -v minimal`
Expected: Build failure — types don't exist yet.

- [ ] **Step 3: Implement Sublocation entity**

```csharp
// src/simulation/entities/Sublocation.cs
using System;

namespace Stakeout.Simulation.Entities;

public enum ConnectionType
{
    Door,
    Window,
    Elevator,
    Stairs,
    OpenPassage,
    Gate,
    HiddenPath,
    Trail
}

public class Sublocation
{
    public int Id { get; set; }
    public int AddressId { get; set; }
    public int? ParentId { get; set; }
    public string Name { get; set; }
    public string[] Tags { get; set; } = Array.Empty<string>();
    public int? Floor { get; set; }
    public bool IsGenerated { get; set; } = true;

    public bool HasTag(string tag) => Array.IndexOf(Tags, tag) >= 0;
}

public class SublocationConnection
{
    public int FromSublocationId { get; set; }
    public int ToSublocationId { get; set; }
    public ConnectionType Type { get; set; } = ConnectionType.Door;
    public bool IsBidirectional { get; set; } = true;
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test stakeout.tests/ --filter "FullyQualifiedName~SublocationTests" -v minimal`
Expected: All 4 tests pass.

- [ ] **Step 5: Add sublocation storage to SimulationState**

Add to `src/simulation/SimulationState.cs`:
- `public Dictionary<int, Sublocation> Sublocations { get; } = new();`
- `public List<SublocationConnection> SublocationConnections { get; } = new();`

- [ ] **Step 6: Update SimulationState test**

Add a test to `stakeout.tests/Simulation/SimulationStateTests.cs` verifying the new collections are initialized empty:

```csharp
[Fact]
public void Constructor_InitializesSublocations()
{
    var state = new SimulationState();

    Assert.Empty(state.Sublocations);
    Assert.Empty(state.SublocationConnections);
}
```

- [ ] **Step 7: Run all tests**

Run: `dotnet test stakeout.tests/ -v minimal`
Expected: All tests pass (existing + new).

- [ ] **Step 8: Commit**

```
git add src/simulation/entities/Sublocation.cs src/simulation/SimulationState.cs stakeout.tests/Simulation/Entities/SublocationTests.cs stakeout.tests/Simulation/SimulationStateTests.cs
git commit -m "feat: add Sublocation entity and SimulationState storage"
```

---

## Task 2: SublocationGraph (Tag Queries & Pathfinding)

**Files:**
- Create: `src/simulation/entities/SublocationGraph.cs`
- Test: `stakeout.tests/Simulation/Entities/SublocationGraphTests.cs`

- [ ] **Step 1: Write failing tests for SublocationGraph**

```csharp
// stakeout.tests/Simulation/Entities/SublocationGraphTests.cs
using System.Collections.Generic;
using System.Linq;
using Stakeout.Simulation.Entities;
using Xunit;

namespace Stakeout.Tests.Simulation.Entities;

public class SublocationGraphTests
{
    private static SublocationGraph CreateSimpleOffice()
    {
        // Road → Lobby(entrance) → Cubicle(work_area), BreakRoom(food)
        var subs = new Dictionary<int, Sublocation>
        {
            { 1, new Sublocation { Id = 1, AddressId = 10, Name = "Road", Tags = new[] { "road" } } },
            { 2, new Sublocation { Id = 2, AddressId = 10, Name = "Lobby", Tags = new[] { "entrance", "public" } } },
            { 3, new Sublocation { Id = 3, AddressId = 10, Name = "Cubicle Area", Tags = new[] { "work_area" } } },
            { 4, new Sublocation { Id = 4, AddressId = 10, Name = "Break Room", Tags = new[] { "food" } } },
        };
        var conns = new List<SublocationConnection>
        {
            new() { FromSublocationId = 1, ToSublocationId = 2, Type = ConnectionType.Door },
            new() { FromSublocationId = 2, ToSublocationId = 3, Type = ConnectionType.Door },
            new() { FromSublocationId = 3, ToSublocationId = 4, Type = ConnectionType.OpenPassage },
        };
        return new SublocationGraph(subs, conns);
    }

    [Fact]
    public void FindByTag_ExistingTag_ReturnsSublocation()
    {
        var graph = CreateSimpleOffice();

        var result = graph.FindByTag("entrance");

        Assert.NotNull(result);
        Assert.Equal("Lobby", result.Name);
    }

    [Fact]
    public void FindByTag_NoMatch_ReturnsNull()
    {
        var graph = CreateSimpleOffice();

        var result = graph.FindByTag("restroom");

        Assert.Null(result);
    }

    [Fact]
    public void FindAllByTag_ReturnsAllMatching()
    {
        var graph = CreateSimpleOffice();

        var results = graph.FindAllByTag("road");

        Assert.Single(results);
        Assert.Equal("Road", results[0].Name);
    }

    [Fact]
    public void GetRoad_ReturnsRoadNode()
    {
        var graph = CreateSimpleOffice();

        var road = graph.GetRoad();

        Assert.NotNull(road);
        Assert.Equal("Road", road.Name);
    }

    [Fact]
    public void FindPath_AdjacentRooms_ReturnsDirectPath()
    {
        var graph = CreateSimpleOffice();

        var path = graph.FindPath(2, 3); // Lobby → Cubicle

        Assert.Equal(2, path.Count);
        Assert.Equal(2, path[0].Id); // Lobby
        Assert.Equal(3, path[1].Id); // Cubicle
    }

    [Fact]
    public void FindPath_TwoHops_ReturnsFullPath()
    {
        var graph = CreateSimpleOffice();

        var path = graph.FindPath(1, 3); // Road → Lobby → Cubicle

        Assert.Equal(3, path.Count);
        Assert.Equal(1, path[0].Id); // Road
        Assert.Equal(2, path[1].Id); // Lobby
        Assert.Equal(3, path[2].Id); // Cubicle
    }

    [Fact]
    public void FindPath_SameRoom_ReturnsSingleElement()
    {
        var graph = CreateSimpleOffice();

        var path = graph.FindPath(2, 2);

        Assert.Single(path);
        Assert.Equal(2, path[0].Id);
    }

    [Fact]
    public void FindPath_Unreachable_ReturnsEmptyList()
    {
        // Add an isolated room with no connections
        var subs = new Dictionary<int, Sublocation>
        {
            { 1, new Sublocation { Id = 1, AddressId = 10, Name = "Road", Tags = new[] { "road" } } },
            { 2, new Sublocation { Id = 2, AddressId = 10, Name = "Island", Tags = new[] { "isolated" } } },
        };
        var graph = new SublocationGraph(subs, new List<SublocationConnection>());

        var path = graph.FindPath(1, 2);

        Assert.Empty(path);
    }

    [Fact]
    public void GetNeighbors_ReturnsConnectedRooms()
    {
        var graph = CreateSimpleOffice();

        var neighbors = graph.GetNeighbors(2); // Lobby neighbors

        Assert.Equal(2, neighbors.Count);
        Assert.Contains(neighbors, n => n.Id == 1); // Road
        Assert.Contains(neighbors, n => n.Id == 3); // Cubicle
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test stakeout.tests/ --filter "FullyQualifiedName~SublocationGraphTests" -v minimal`
Expected: Build failure — SublocationGraph doesn't exist.

- [ ] **Step 3: Implement SublocationGraph**

```csharp
// src/simulation/entities/SublocationGraph.cs
using System.Collections.Generic;
using System.Linq;

namespace Stakeout.Simulation.Entities;

public class SublocationGraph
{
    private readonly Dictionary<int, Sublocation> _sublocations;
    private readonly Dictionary<int, List<int>> _adjacency;

    public SublocationGraph(
        Dictionary<int, Sublocation> sublocations,
        List<SublocationConnection> connections)
    {
        _sublocations = sublocations;
        _adjacency = new Dictionary<int, List<int>>();

        foreach (var sub in sublocations.Keys)
            _adjacency[sub] = new List<int>();

        foreach (var conn in connections)
        {
            _adjacency[conn.FromSublocationId].Add(conn.ToSublocationId);
            if (conn.IsBidirectional)
                _adjacency[conn.ToSublocationId].Add(conn.FromSublocationId);
        }
    }

    public Sublocation FindByTag(string tag)
    {
        return _sublocations.Values.FirstOrDefault(s => s.HasTag(tag));
    }

    public List<Sublocation> FindAllByTag(string tag)
    {
        return _sublocations.Values.Where(s => s.HasTag(tag)).ToList();
    }

    public Sublocation GetRoad()
    {
        return FindByTag("road");
    }

    public Sublocation Get(int id)
    {
        return _sublocations.TryGetValue(id, out var sub) ? sub : null;
    }

    public List<Sublocation> GetNeighbors(int sublocationId)
    {
        if (!_adjacency.TryGetValue(sublocationId, out var neighborIds))
            return new List<Sublocation>();

        return neighborIds
            .Where(id => _sublocations.ContainsKey(id))
            .Select(id => _sublocations[id])
            .ToList();
    }

    public List<Sublocation> FindPath(int fromId, int toId)
    {
        if (fromId == toId)
            return new List<Sublocation> { _sublocations[fromId] };

        // BFS
        var visited = new HashSet<int> { fromId };
        var queue = new Queue<int>();
        var parent = new Dictionary<int, int>();
        queue.Enqueue(fromId);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current == toId)
            {
                // Reconstruct path
                var path = new List<Sublocation>();
                var node = toId;
                while (node != fromId)
                {
                    path.Add(_sublocations[node]);
                    node = parent[node];
                }
                path.Add(_sublocations[fromId]);
                path.Reverse();
                return path;
            }

            if (!_adjacency.TryGetValue(current, out var neighbors))
                continue;

            foreach (var neighbor in neighbors)
            {
                if (visited.Add(neighbor))
                {
                    parent[neighbor] = current;
                    queue.Enqueue(neighbor);
                }
            }
        }

        return new List<Sublocation>(); // unreachable
    }

    public IReadOnlyDictionary<int, Sublocation> AllSublocations => _sublocations;
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test stakeout.tests/ --filter "FullyQualifiedName~SublocationGraphTests" -v minimal`
Expected: All 8 tests pass.

- [ ] **Step 5: Run all tests**

Run: `dotnet test stakeout.tests/ -v minimal`
Expected: All tests pass.

- [ ] **Step 6: Commit**

```
git add src/simulation/entities/SublocationGraph.cs stakeout.tests/Simulation/Entities/SublocationGraphTests.cs
git commit -m "feat: add SublocationGraph with tag queries and BFS pathfinding"
```

---

## Task 3: AddressType Extensions

**Files:**
- Modify: `src/simulation/entities/AddressType.cs`
- Modify: `stakeout.tests/Simulation/Entities/AddressTypeTests.cs`

- [ ] **Step 1: Write failing tests for new address types**

Add to `stakeout.tests/Simulation/Entities/AddressTypeTests.cs`:

```csharp
[Fact]
public void GetCategory_ApartmentBuilding_ReturnsResidential()
{
    Assert.Equal(AddressCategory.Residential, AddressType.ApartmentBuilding.GetCategory());
}

[Fact]
public void GetCategory_Park_ReturnsPublic()
{
    Assert.Equal(AddressCategory.Public, AddressType.Park.GetCategory());
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test stakeout.tests/ --filter "FullyQualifiedName~AddressTypeTests" -v minimal`
Expected: Build failure — enum values and category don't exist.

- [ ] **Step 3: Update AddressType.cs**

Add `ApartmentBuilding` and `Park` to the `AddressType` enum. Add `Public` to `AddressCategory`. Update `GetCategory()` switch to map `ApartmentBuilding` → `Residential`, `Park` → `Public`.

Read `src/simulation/entities/AddressType.cs` first to see the exact current code, then modify.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test stakeout.tests/ --filter "FullyQualifiedName~AddressTypeTests" -v minimal`
Expected: All tests pass.

- [ ] **Step 5: Run all tests**

Run: `dotnet test stakeout.tests/ -v minimal`
Expected: All tests pass. Check that no existing test breaks from the enum change.

- [ ] **Step 6: Commit**

```
git add src/simulation/entities/AddressType.cs stakeout.tests/Simulation/Entities/AddressTypeTests.cs
git commit -m "feat: add ApartmentBuilding and Park address types with Public category"
```

---

## Task 4: ScheduleEntry & Person Sublocation Fields

**Files:**
- Modify: `src/simulation/scheduling/DailySchedule.cs`
- Modify: `src/simulation/entities/Person.cs`

- [ ] **Step 1: Add TargetSublocationId to ScheduleEntry**

Add `public int? TargetSublocationId { get; set; }` to the `ScheduleEntry` class in `src/simulation/scheduling/DailySchedule.cs`.

- [ ] **Step 2: Add CurrentSublocationId to Person**

Add `public int? CurrentSublocationId { get; set; }` to the `Person` class in `src/simulation/entities/Person.cs`.

- [ ] **Step 3: Run all tests to verify nothing breaks**

Run: `dotnet test stakeout.tests/ -v minimal`
Expected: All tests pass. These are additive fields with null defaults.

- [ ] **Step 4: Commit**

```
git add src/simulation/scheduling/DailySchedule.cs src/simulation/entities/Person.cs
git commit -m "feat: add sublocation fields to ScheduleEntry and Person"
```

---

## Task 5: Sublocation Generator Interface & Registry

**Files:**
- Create: `src/simulation/sublocations/ISublocationGenerator.cs`
- Create: `src/simulation/sublocations/SublocationGeneratorRegistry.cs`

- [ ] **Step 1: Create ISublocationGenerator interface**

```csharp
// src/simulation/sublocations/ISublocationGenerator.cs
using System;
using Stakeout.Simulation.Entities;

namespace Stakeout.Simulation.Sublocations;

public interface ISublocationGenerator
{
    SublocationGraph Generate(int addressId, SimulationState state, Random rng);
}
```

- [ ] **Step 2: Create SublocationGeneratorRegistry**

```csharp
// src/simulation/sublocations/SublocationGeneratorRegistry.cs
using System;
using System.Collections.Generic;
using Stakeout.Simulation.Entities;

namespace Stakeout.Simulation.Sublocations;

public static class SublocationGeneratorRegistry
{
    private static readonly Dictionary<AddressType, ISublocationGenerator> _generators = new();

    public static void Register(AddressType type, ISublocationGenerator generator)
    {
        _generators[type] = generator;
    }

    public static ISublocationGenerator Get(AddressType type)
    {
        return _generators.TryGetValue(type, out var gen) ? gen : null;
    }
}
```

- [ ] **Step 3: Run all tests**

Run: `dotnet test stakeout.tests/ -v minimal`
Expected: All tests pass.

- [ ] **Step 4: Commit**

```
git add src/simulation/sublocations/ISublocationGenerator.cs src/simulation/sublocations/SublocationGeneratorRegistry.cs
git commit -m "feat: add sublocation generator interface and registry"
```

---

## Task 6: SuburbanHome Generator

**Files:**
- Create: `src/simulation/sublocations/SuburbanHomeGenerator.cs`
- Test: `stakeout.tests/Simulation/Sublocations/SuburbanHomeGeneratorTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
// stakeout.tests/Simulation/Sublocations/SuburbanHomeGeneratorTests.cs
using System;
using Stakeout.Simulation;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Sublocations;
using Xunit;

namespace Stakeout.Tests.Simulation.Sublocations;

public class SuburbanHomeGeneratorTests
{
    private SublocationGraph Generate(int seed = 42)
    {
        var state = new SimulationState();
        var gen = new SuburbanHomeGenerator();
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
    public void Generate_HasBedroom()
    {
        var graph = Generate();
        var bedroom = graph.FindByTag("bedroom");
        Assert.NotNull(bedroom);
    }

    [Fact]
    public void Generate_HasKitchen()
    {
        var graph = Generate();
        var kitchen = graph.FindByTag("kitchen");
        Assert.NotNull(kitchen);
    }

    [Fact]
    public void Generate_HasLivingRoom()
    {
        var graph = Generate();
        var living = graph.FindByTag("living");
        Assert.NotNull(living);
    }

    [Fact]
    public void Generate_HasCovertEntry()
    {
        var graph = Generate();
        var covert = graph.FindByTag("covert_entry");
        Assert.NotNull(covert);
    }

    [Fact]
    public void Generate_RoadConnectedToEntrance()
    {
        var graph = Generate();
        var road = graph.GetRoad();
        var entrance = graph.FindByTag("entrance");
        var path = graph.FindPath(road.Id, entrance.Id);
        Assert.True(path.Count <= 3); // Road → FrontYard → FrontDoor or similar
    }

    [Fact]
    public void Generate_CanReachBedroomFromRoad()
    {
        var graph = Generate();
        var road = graph.GetRoad();
        var bedroom = graph.FindByTag("bedroom");
        var path = graph.FindPath(road.Id, bedroom.Id);
        Assert.True(path.Count >= 2);
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
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test stakeout.tests/ --filter "FullyQualifiedName~SuburbanHomeGeneratorTests" -v minimal`
Expected: Build failure — generator doesn't exist.

- [ ] **Step 3: Implement SuburbanHomeGenerator**

Create `src/simulation/sublocations/SuburbanHomeGenerator.cs`. The generator should:
- Create Road node (tag: `road`)
- Create Front Yard connected to Road via OpenPassage
- Create Front Door connected to Front Yard via Door (tags: `entrance`)
- Create Ground Floor Hallway connected to Front Door
- Create Kitchen (tags: `kitchen`, `food`) connected to Hallway
- Create Living Room (tags: `living`, `social`) connected to Hallway
- Create Ground Floor Bathroom (tags: `restroom`) connected to Hallway
- Create Back Door (tags: `covert_entry`, `staff_entry`) connected to Hallway, also connected to Backyard
- Create Backyard connected to Road via OpenPassage
- Create Stairs connected to Hallway
- Create Upstairs Hallway connected to Stairs
- Create 2-3 Bedrooms (tags: `bedroom`, `private`) connected to Upstairs Hallway (count via RNG)
- Create Upstairs Bathroom (tags: `restroom`) connected to Upstairs Hallway
- Windows on ground floor (tags: `covert_entry`) connected to Front Yard via Window connection
- Use `state.GenerateEntityId()` for all sublocation IDs
- Register sublocations and connections in state

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test stakeout.tests/ --filter "FullyQualifiedName~SuburbanHomeGeneratorTests" -v minimal`
Expected: All 9 tests pass.

- [ ] **Step 5: Run all tests**

Run: `dotnet test stakeout.tests/ -v minimal`
Expected: All tests pass.

- [ ] **Step 6: Commit**

```
git add src/simulation/sublocations/SuburbanHomeGenerator.cs stakeout.tests/Simulation/Sublocations/SuburbanHomeGeneratorTests.cs
git commit -m "feat: add SuburbanHome sublocation generator"
```

---

## Task 7: Office Generator

**Files:**
- Create: `src/simulation/sublocations/OfficeGenerator.cs`
- Test: `stakeout.tests/Simulation/Sublocations/OfficeGeneratorTests.cs`

- [ ] **Step 1: Write failing tests**

Test that generated office has: road, entrance (lobby), work_area (cubicle area), food (break room), restroom, elevator/stairs connections between floors. Test that floor count varies (1-5 floors via RNG). Test that all sublocations have correct addressId.

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test stakeout.tests/ --filter "FullyQualifiedName~OfficeGeneratorTests" -v minimal`

- [ ] **Step 3: Implement OfficeGenerator**

Road → Lobby(entrance, public) → Elevator(elevator) + Stairwell(stairs). Per floor: Reception, Cubicle Area(work_area), Offices, Break Room(food), Restrooms(restroom). Floor count 1-5 via RNG. Manager/executive offices tagged `private`.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test stakeout.tests/ --filter "FullyQualifiedName~OfficeGeneratorTests" -v minimal`

- [ ] **Step 5: Commit**

```
git add src/simulation/sublocations/OfficeGenerator.cs stakeout.tests/Simulation/Sublocations/OfficeGeneratorTests.cs
git commit -m "feat: add Office sublocation generator"
```

---

## Task 8: Diner, DiveBar, Park, ApartmentBuilding Generators

**Files:**
- Create: `src/simulation/sublocations/DinerGenerator.cs`
- Create: `src/simulation/sublocations/DiveBarGenerator.cs`
- Create: `src/simulation/sublocations/ParkGenerator.cs`
- Create: `src/simulation/sublocations/ApartmentBuildingGenerator.cs`
- Test: `stakeout.tests/Simulation/Sublocations/DinerGeneratorTests.cs`
- Test: `stakeout.tests/Simulation/Sublocations/DiveBarGeneratorTests.cs`
- Test: `stakeout.tests/Simulation/Sublocations/ParkGeneratorTests.cs`
- Test: `stakeout.tests/Simulation/Sublocations/ApartmentBuildingGeneratorTests.cs`

- [ ] **Step 1: Write failing tests for all four generators**

For each generator, test: has road, has entrance, has relevant tagged rooms, all sublocations have correct addressId, can reach key rooms from road.

**DinerGenerator specifics:** road, entrance (front door), service_area (dining area), food (kitchen — staff side), staff_entry (back door), restroom.

**DiveBarGenerator specifics:** road, entrance (front door), social/service_area (bar area), staff_entry (back door → alley), storage, restroom.

**ParkGenerator specifics:** road, entrance (main entrance), connections are mostly OpenPassage/Trail. Has restroom building connected via Door. All outdoor zones reachable from road.

**ApartmentBuildingGenerator specifics:** road, entrance (lobby), floors are generated as `IsGenerated = false` placeholders. Test that floor placeholder count is configurable via RNG (4-20 floors). Test lazy expansion: calling an `ExpandFloor` method on a placeholder creates individual rooms.

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test stakeout.tests/ --filter "FullyQualifiedName~DinerGeneratorTests|FullyQualifiedName~DiveBarGeneratorTests|FullyQualifiedName~ParkGeneratorTests|FullyQualifiedName~ApartmentBuildingGeneratorTests" -v minimal`

- [ ] **Step 3: Implement all four generators**

Follow the patterns established by SuburbanHomeGenerator and OfficeGenerator. Each implements `ISublocationGenerator`.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test stakeout.tests/ --filter "FullyQualifiedName~DinerGeneratorTests|FullyQualifiedName~DiveBarGeneratorTests|FullyQualifiedName~ParkGeneratorTests|FullyQualifiedName~ApartmentBuildingGeneratorTests" -v minimal`

- [ ] **Step 5: Register all generators in SublocationGeneratorRegistry**

Add a static initializer or a `RegisterAll()` method that registers all 6 generators:

```csharp
SublocationGeneratorRegistry.Register(AddressType.SuburbanHome, new SuburbanHomeGenerator());
SublocationGeneratorRegistry.Register(AddressType.Office, new OfficeGenerator());
SublocationGeneratorRegistry.Register(AddressType.Diner, new DinerGenerator());
SublocationGeneratorRegistry.Register(AddressType.DiveBar, new DiveBarGenerator());
SublocationGeneratorRegistry.Register(AddressType.ApartmentBuilding, new ApartmentBuildingGenerator());
SublocationGeneratorRegistry.Register(AddressType.Park, new ParkGenerator());
```

- [ ] **Step 6: Run all tests**

Run: `dotnet test stakeout.tests/ -v minimal`

- [ ] **Step 7: Commit**

```
git add src/simulation/sublocations/ stakeout.tests/Simulation/Sublocations/
git commit -m "feat: add Diner, DiveBar, Park, and ApartmentBuilding sublocation generators"
```

---

## Task 9: Integrate Sublocation Generation into LocationGenerator

**Files:**
- Modify: `src/simulation/LocationGenerator.cs`
- Modify: `stakeout.tests/Simulation/LocationGeneratorTests.cs`

- [ ] **Step 1: Write failing test**

Add to `stakeout.tests/Simulation/LocationGeneratorTests.cs`:

```csharp
[Fact]
public void GenerateAddress_CreatesSublocations()
{
    var state = new SimulationState();
    LocationGenerator.GenerateCityScaffolding(state);
    var address = LocationGenerator.GenerateAddress(state, new Random(42));

    // Should have sublocations registered in state for this address
    var addressSubs = state.Sublocations.Values
        .Where(s => s.AddressId == address.Id)
        .ToList();

    Assert.NotEmpty(addressSubs);
    Assert.Contains(addressSubs, s => s.HasTag("road"));
    Assert.Contains(addressSubs, s => s.HasTag("entrance"));
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test stakeout.tests/ --filter "FullyQualifiedName~LocationGeneratorTests.GenerateAddress_CreatesSublocations" -v minimal`

- [ ] **Step 3: Modify LocationGenerator.GenerateAddress**

After creating the Address and adding it to state, call:
```csharp
var generator = SublocationGeneratorRegistry.Get(address.Type);
if (generator != null)
{
    generator.Generate(address.Id, state, rng);
}
```

Make sure `SublocationGeneratorRegistry.RegisterAll()` is called during initialization (in SimulationManager._Ready or LocationGenerator static constructor).

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test stakeout.tests/ --filter "FullyQualifiedName~LocationGeneratorTests" -v minimal`

- [ ] **Step 5: Run all tests**

Run: `dotnet test stakeout.tests/ -v minimal`

- [ ] **Step 6: Commit**

```
git add src/simulation/LocationGenerator.cs src/simulation/sublocations/SublocationGeneratorRegistry.cs stakeout.tests/Simulation/LocationGeneratorTests.cs
git commit -m "feat: integrate sublocation generation into LocationGenerator"
```

---

## Task 10: Decomposition Strategy Interface & WorkDay Strategy

**Files:**
- Create: `src/simulation/scheduling/decomposition/IDecompositionStrategy.cs`
- Create: `src/simulation/scheduling/decomposition/WorkDayDecomposition.cs`
- Test: `stakeout.tests/Simulation/Scheduling/Decomposition/WorkDayDecompositionTests.cs`

- [ ] **Step 1: Create IDecompositionStrategy interface**

```csharp
// src/simulation/scheduling/decomposition/IDecompositionStrategy.cs
using System.Collections.Generic;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Objectives;
using Stakeout.Simulation.Scheduling;

namespace Stakeout.Simulation.Scheduling.Decomposition;

public interface IDecompositionStrategy
{
    /// <summary>
    /// Decomposes a root-level task into sublocation-aware schedule entries.
    /// </summary>
    /// <param name="task">The address-level task to decompose</param>
    /// <param name="graph">The sublocation graph for the target address</param>
    /// <param name="startTime">When this task block starts</param>
    /// <param name="endTime">When this task block ends</param>
    /// <param name="rng">Seeded RNG for randomized behavior</param>
    /// <returns>Ordered list of schedule entries with sublocation targets</returns>
    List<ScheduleEntry> Decompose(SimTask task, SublocationGraph graph,
        System.TimeSpan startTime, System.TimeSpan endTime, System.Random rng);
}
```

- [ ] **Step 2: Write failing tests for WorkDayDecomposition**

```csharp
// stakeout.tests/Simulation/Scheduling/Decomposition/WorkDayDecompositionTests.cs
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
            { 2, new Sublocation { Id = 2, AddressId = 10, Name = "Lobby", Tags = new[] { "entrance" } } },
            { 3, new Sublocation { Id = 3, AddressId = 10, Name = "Cubicle Area", Tags = new[] { "work_area" } } },
            { 4, new Sublocation { Id = 4, AddressId = 10, Name = "Break Room", Tags = new[] { "food" } } },
            { 5, new Sublocation { Id = 5, AddressId = 10, Name = "Restroom", Tags = new[] { "restroom" } } },
        };
        var conns = new List<SublocationConnection>
        {
            new() { FromSublocationId = 1, ToSublocationId = 2, Type = ConnectionType.Door },
            new() { FromSublocationId = 2, ToSublocationId = 3, Type = ConnectionType.Door },
            new() { FromSublocationId = 3, ToSublocationId = 4, Type = ConnectionType.OpenPassage },
            new() { FromSublocationId = 3, ToSublocationId = 5, Type = ConnectionType.Door },
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
            { 2, new Sublocation { Id = 2, AddressId = 10, Name = "Lobby", Tags = new[] { "entrance" } } },
        };
        var graph = new SublocationGraph(subs, new List<SublocationConnection>
        {
            new() { FromSublocationId = 1, ToSublocationId = 2, Type = ConnectionType.Door },
        });

        var strategy = new WorkDayDecomposition();
        var task = new SimTask { ActionType = ActionType.Work, TargetAddressId = 10 };

        var entries = strategy.Decompose(task, graph,
            new TimeSpan(9, 0, 0), new TimeSpan(17, 0, 0), new Random(42));

        Assert.NotEmpty(entries);
        Assert.All(entries, e => Assert.Equal(2, e.TargetSublocationId));
    }
}
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test stakeout.tests/ --filter "FullyQualifiedName~WorkDayDecompositionTests" -v minimal`

- [ ] **Step 4: Implement WorkDayDecomposition**

The strategy should:
1. Find entrance via `[entrance]` tag
2. Find work area via `[work_area]` tag (graceful: fall back to entrance if none)
3. Build entry sequence: entrance → pathfind to work area (intermediate rooms become move entries)
4. At work area: insert periodic breaks — every 1-3 hours, 50% chance to visit `[food]`, 70% chance `[restroom]`
5. Build exit sequence: pathfind from work area to entrance
6. All entries get `TargetAddressId` from the task and `TargetSublocationId` from the resolved room
7. Pathfinding between rooms inserts intermediate move entries for each room traversed

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test stakeout.tests/ --filter "FullyQualifiedName~WorkDayDecompositionTests" -v minimal`

- [ ] **Step 6: Commit**

```
git add src/simulation/scheduling/decomposition/ stakeout.tests/Simulation/Scheduling/Decomposition/
git commit -m "feat: add IDecompositionStrategy interface and WorkDayDecomposition"
```

---

## Task 11: Inhabit & Intrude Decomposition Strategies

**Files:**
- Create: `src/simulation/scheduling/decomposition/InhabitDecomposition.cs`
- Create: `src/simulation/scheduling/decomposition/IntrudeDecomposition.cs`
- Test: `stakeout.tests/Simulation/Scheduling/Decomposition/InhabitDecompositionTests.cs`
- Test: `stakeout.tests/Simulation/Scheduling/Decomposition/IntrudeDecompositionTests.cs`

- [ ] **Step 1: Write failing tests for InhabitDecomposition**

Test: morning routine (bedroom → restroom → kitchen → entrance), evening routine (entrance → kitchen → living → restroom → bedroom). The strategy should detect time-of-day from the startTime parameter to choose morning vs evening pattern.

- [ ] **Step 2: Write failing tests for IntrudeDecomposition**

Test: enters via `[covert_entry]` tag (not `[entrance]`), pathfinds to target room, exits via same entry point. Test that if no `[covert_entry]` exists, falls back to `[entrance]`. Test that `ActionData["VictimId"]` is used to find the room containing the target (for now, use `TargetSublocationId` on the task if set, or fall back to first `[bedroom]`).

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test stakeout.tests/ --filter "FullyQualifiedName~InhabitDecompositionTests|FullyQualifiedName~IntrudeDecompositionTests" -v minimal`

- [ ] **Step 4: Implement both strategies**

Follow patterns from WorkDayDecomposition. Each implements `IDecompositionStrategy`.

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test stakeout.tests/ --filter "FullyQualifiedName~InhabitDecompositionTests|FullyQualifiedName~IntrudeDecompositionTests" -v minimal`

- [ ] **Step 6: Commit**

```
git add src/simulation/scheduling/decomposition/ stakeout.tests/Simulation/Scheduling/Decomposition/
git commit -m "feat: add Inhabit and Intrude decomposition strategies"
```

---

## Task 12: Remaining Decomposition Strategies

**Files:**
- Create: `src/simulation/scheduling/decomposition/PatronizeDecomposition.cs`
- Create: `src/simulation/scheduling/decomposition/StaffShiftDecomposition.cs`
- Create: `src/simulation/scheduling/decomposition/VisitDecomposition.cs`

- [ ] **Step 1: Implement PatronizeDecomposition**

Enter [entrance] → go to [service_area] or [social] → optionally [restroom] → exit [entrance]. Used for Idle action at commercial locations.

- [ ] **Step 2: Implement StaffShiftDecomposition**

Enter [staff_entry] (fallback [entrance]) → go to [work_area] → periodic [food]/[restroom] → exit [staff_entry]. Like WorkDay but with staff entry.

- [ ] **Step 3: Implement VisitDecomposition**

Generic fallback. Enter [entrance] → go to specific sublocation (if TargetSublocationId set) or first available tagged room → exit [entrance].

- [ ] **Step 4: Run all tests**

Run: `dotnet test stakeout.tests/ -v minimal`

- [ ] **Step 5: Commit**

```
git add src/simulation/scheduling/decomposition/
git commit -m "feat: add Patronize, StaffShift, and Visit decomposition strategies"
```

---

## Task 13: TaskResolver

**Files:**
- Create: `src/simulation/scheduling/TaskResolver.cs`
- Test: `stakeout.tests/Simulation/Scheduling/TaskResolverTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
// stakeout.tests/Simulation/Scheduling/TaskResolverTests.cs
using System;
using System.Collections.Generic;
using System.Linq;
using Stakeout.Simulation;
using Stakeout.Simulation.Actions;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Objectives;
using Stakeout.Simulation.Scheduling;
using Xunit;

namespace Stakeout.Tests.Simulation.Scheduling;

public class TaskResolverTests
{
    private SimulationState CreateStateWithOffice()
    {
        var state = new SimulationState();
        var address = new Address { Id = 1, Type = AddressType.Office };
        state.Addresses[1] = address;
        // Generate sublocations for the office
        var gen = new Sublocations.OfficeGenerator();
        gen.Generate(1, state, new Random(42));
        return state;
    }

    [Fact]
    public void Resolve_WorkTask_ProducesEntriesWithSublocationIds()
    {
        var state = CreateStateWithOffice();
        var task = new SimTask
        {
            Id = 1, ActionType = ActionType.Work, Priority = 20,
            TargetAddressId = 1,
            WindowStart = new TimeSpan(9, 0, 0),
            WindowEnd = new TimeSpan(17, 0, 0)
        };

        var entries = TaskResolver.Resolve(task, state, new Random(42));

        Assert.NotEmpty(entries);
        Assert.All(entries, e => Assert.NotNull(e.TargetSublocationId));
        Assert.All(entries, e => Assert.Equal(1, e.TargetAddressId));
    }

    [Fact]
    public void Resolve_TaskWithNoSublocations_ReturnsEntryWithNullSublocation()
    {
        var state = new SimulationState();
        var address = new Address { Id = 1, Type = AddressType.Office };
        state.Addresses[1] = address;
        // No sublocations generated

        var task = new SimTask
        {
            Id = 1, ActionType = ActionType.Work, Priority = 20,
            TargetAddressId = 1,
            WindowStart = new TimeSpan(9, 0, 0),
            WindowEnd = new TimeSpan(17, 0, 0)
        };

        var entries = TaskResolver.Resolve(task, state, new Random(42));

        Assert.NotEmpty(entries);
        // Falls back to address-only entry (no sublocation graph)
        Assert.All(entries, e => Assert.Null(e.TargetSublocationId));
    }

    [Fact]
    public void Resolve_SleepTask_UsesInhabitDecomposition()
    {
        var state = new SimulationState();
        var address = new Address { Id = 1, Type = AddressType.SuburbanHome };
        state.Addresses[1] = address;
        var gen = new Sublocations.SuburbanHomeGenerator();
        gen.Generate(1, state, new Random(42));

        var task = new SimTask
        {
            Id = 1, ActionType = ActionType.Sleep, Priority = 30,
            TargetAddressId = 1,
            WindowStart = new TimeSpan(22, 0, 0),
            WindowEnd = new TimeSpan(6, 0, 0)
        };

        var entries = TaskResolver.Resolve(task, state, new Random(42));

        Assert.NotEmpty(entries);
        // Should end at bedroom for sleep
        var bedroomSubs = state.Sublocations.Values
            .Where(s => s.AddressId == 1 && s.HasTag("bedroom"))
            .Select(s => s.Id)
            .ToHashSet();
        Assert.Contains(entries, e => bedroomSubs.Contains(e.TargetSublocationId ?? 0));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test stakeout.tests/ --filter "FullyQualifiedName~TaskResolverTests" -v minimal`

- [ ] **Step 3: Implement TaskResolver**

```csharp
// src/simulation/scheduling/TaskResolver.cs
using System;
using System.Collections.Generic;
using System.Linq;
using Stakeout.Simulation.Actions;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Objectives;
using Stakeout.Simulation.Scheduling.Decomposition;

namespace Stakeout.Simulation.Scheduling;

public static class TaskResolver
{
    private static readonly Dictionary<ActionType, IDecompositionStrategy> _strategies = new()
    {
        { ActionType.Work, new WorkDayDecomposition() },
        { ActionType.Sleep, new InhabitDecomposition() },
        { ActionType.Idle, new InhabitDecomposition() },
        { ActionType.KillPerson, new IntrudeDecomposition() },
    };

    public static List<ScheduleEntry> Resolve(SimTask task, SimulationState state, Random rng)
    {
        if (!task.TargetAddressId.HasValue)
            return FallbackEntry(task);

        var graph = BuildGraphForAddress(task.TargetAddressId.Value, state);
        if (graph == null)
            return FallbackEntry(task);

        var strategy = GetStrategy(task);
        return strategy.Decompose(task, graph, task.WindowStart, task.WindowEnd, rng);
    }

    private static SublocationGraph BuildGraphForAddress(int addressId, SimulationState state)
    {
        var subs = state.Sublocations.Values
            .Where(s => s.AddressId == addressId)
            .ToDictionary(s => s.Id);

        if (subs.Count == 0) return null;

        var conns = state.SublocationConnections
            .Where(c => subs.ContainsKey(c.FromSublocationId) || subs.ContainsKey(c.ToSublocationId))
            .ToList();

        return new SublocationGraph(subs, conns);
    }

    private static IDecompositionStrategy GetStrategy(SimTask task)
    {
        // Check ActionData for strategy override
        if (task.ActionData != null &&
            task.ActionData.TryGetValue("DecompositionStrategy", out var strategyName) &&
            strategyName is string name)
        {
            // Allow override for special cases like staff shifts
            if (name == "StaffShift") return new StaffShiftDecomposition();
            if (name == "Patronize") return new PatronizeDecomposition();
        }

        return _strategies.TryGetValue(task.ActionType, out var strategy)
            ? strategy
            : new VisitDecomposition();
    }

    private static List<ScheduleEntry> FallbackEntry(SimTask task)
    {
        return new List<ScheduleEntry>
        {
            new ScheduleEntry
            {
                Action = task.ActionType,
                StartTime = task.WindowStart,
                EndTime = task.WindowEnd,
                TargetAddressId = task.TargetAddressId,
                TargetSublocationId = null
            }
        };
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test stakeout.tests/ --filter "FullyQualifiedName~TaskResolverTests" -v minimal`

- [ ] **Step 5: Run all tests**

Run: `dotnet test stakeout.tests/ -v minimal`

- [ ] **Step 6: Commit**

```
git add src/simulation/scheduling/TaskResolver.cs stakeout.tests/Simulation/Scheduling/TaskResolverTests.cs
git commit -m "feat: add TaskResolver with strategy dispatch and address graph building"
```

---

## Task 14: Integrate TaskResolver into ScheduleBuilder

**Files:**
- Modify: `src/simulation/scheduling/ScheduleBuilder.cs`
- Modify: `stakeout.tests/Simulation/Scheduling/ScheduleBuilderTests.cs`

- [ ] **Step 1: Write failing test**

Add to `stakeout.tests/Simulation/Scheduling/ScheduleBuilderTests.cs`:

```csharp
[Fact]
public void BuildFromTasks_WithSublocations_EntriesHaveSublocationIds()
{
    var state = new SimulationState();
    var home = new Address { Id = 1, Position = new Vector2(100, 100), Type = AddressType.SuburbanHome };
    var work = new Address { Id = 2, Position = new Vector2(600, 100), Type = AddressType.Office };
    state.Addresses[1] = home;
    state.Addresses[2] = work;

    // Generate sublocations
    new Sublocations.SuburbanHomeGenerator().Generate(1, state, new Random(42));
    new Sublocations.OfficeGenerator().Generate(2, state, new Random(42));

    var tasks = CreateOfficeWorkerTasks(home, work);
    var schedule = ScheduleBuilder.BuildFromTasks(tasks, state, DefaultConfig);

    // Non-travel entries at addresses with sublocations should have sublocation IDs
    var nonTravelEntries = schedule.Entries
        .Where(e => e.Action != ActionType.TravelByCar)
        .ToList();

    Assert.True(nonTravelEntries.Any(e => e.TargetSublocationId.HasValue));
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test stakeout.tests/ --filter "FullyQualifiedName~ScheduleBuilderTests.BuildFromTasks_WithSublocations" -v minimal`

- [ ] **Step 3: Update ScheduleBuilder**

The key change: `BuildFromTasks` gains an overload that accepts `SimulationState` (instead of just `Dictionary<int, Address>`). After building the flat schedule from priority resolution, it passes each non-travel task block through `TaskResolver.Resolve()` to expand into sublocation-level entries. Travel entries remain address-level only.

Keep the existing `BuildFromTasks(List<SimTask>, Dictionary<int, Address>, MapConfig)` overload working for backward compatibility — it just produces entries without sublocation IDs.

The new overload signature:
```csharp
public static DailySchedule BuildFromTasks(List<SimTask> tasks, SimulationState state, MapConfig config)
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test stakeout.tests/ --filter "FullyQualifiedName~ScheduleBuilderTests" -v minimal`
Expected: All existing tests still pass (they use the old overload). New test passes.

- [ ] **Step 5: Run all tests**

Run: `dotnet test stakeout.tests/ -v minimal`

- [ ] **Step 6: Commit**

```
git add src/simulation/scheduling/ScheduleBuilder.cs stakeout.tests/Simulation/Scheduling/ScheduleBuilderTests.cs
git commit -m "feat: integrate TaskResolver into ScheduleBuilder for sublocation-aware schedules"
```

---

## Task 15: Update PersonBehavior for Sublocation Tracking

**Files:**
- Modify: `src/simulation/scheduling/PersonBehavior.cs`
- Modify: `stakeout.tests/Simulation/Scheduling/PersonBehaviorTests.cs`

- [ ] **Step 1: Write failing test**

Add to `stakeout.tests/Simulation/Scheduling/PersonBehaviorTests.cs`:

```csharp
[Fact]
public void Update_EntryWithSublocation_UpdatesCurrentSublocationId()
{
    // Create a person with a schedule that has sublocation entries
    var state = new SimulationState();
    var address = new Address { Id = 1, Position = new Vector2(0, 0), Type = AddressType.SuburbanHome };
    state.Addresses[1] = address;

    var person = new Person
    {
        Id = 1, HomeAddressId = 1, CurrentAddressId = 1,
        CurrentAction = ActionType.Idle,
        Schedule = new DailySchedule()
    };
    person.Schedule.Entries.Add(new ScheduleEntry
    {
        Action = ActionType.Sleep,
        StartTime = new TimeSpan(22, 0, 0),
        EndTime = new TimeSpan(6, 0, 0),
        TargetAddressId = 1,
        TargetSublocationId = 42 // bedroom
    });
    person.Schedule.Entries.Add(new ScheduleEntry
    {
        Action = ActionType.Idle,
        StartTime = new TimeSpan(6, 0, 0),
        EndTime = new TimeSpan(22, 0, 0),
        TargetAddressId = 1,
        TargetSublocationId = 43 // living room
    });
    state.People[1] = person;

    state.Clock.Tick(0); // init
    // Set time to 23:00 (sleep time)
    var targetTime = new DateTime(1980, 1, 1, 23, 0, 0);
    var delta = (targetTime - state.Clock.CurrentTime).TotalSeconds;
    state.Clock.Tick(delta);

    var behavior = new PersonBehavior(new MapConfig());
    behavior.Update(person, state);

    Assert.Equal(42, person.CurrentSublocationId);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test stakeout.tests/ --filter "FullyQualifiedName~PersonBehaviorTests.Update_EntryWithSublocation" -v minimal`

- [ ] **Step 3: Update PersonBehavior.Transition**

In the `Transition` method, after setting `person.CurrentAction = entry.Action`, also set:
```csharp
person.CurrentSublocationId = entry.TargetSublocationId;
```

Also clear `CurrentSublocationId` when starting travel:
```csharp
person.CurrentSublocationId = null;
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test stakeout.tests/ --filter "FullyQualifiedName~PersonBehaviorTests" -v minimal`

- [ ] **Step 5: Run all tests**

Run: `dotnet test stakeout.tests/ -v minimal`

- [ ] **Step 6: Commit**

```
git add src/simulation/scheduling/PersonBehavior.cs stakeout.tests/Simulation/Scheduling/PersonBehaviorTests.cs
git commit -m "feat: PersonBehavior tracks CurrentSublocationId from schedule entries"
```

---

## Task 16: Update SimulationManager to Use New ScheduleBuilder

**Files:**
- Modify: `src/simulation/SimulationManager.cs`

- [ ] **Step 1: Update RebuildSchedule to pass SimulationState**

In `SimulationManager.RebuildSchedule()`, change the `ScheduleBuilder.BuildFromTasks` call to use the new overload that accepts `SimulationState`:

```csharp
person.Schedule = ScheduleBuilder.BuildFromTasks(tasks, State, _mapConfig);
```

Instead of the current:
```csharp
person.Schedule = ScheduleBuilder.BuildFromTasks(tasks, State.Addresses, _mapConfig);
```

- [ ] **Step 2: Ensure sublocation generators are registered**

Add `SublocationGeneratorRegistry.RegisterAll()` call in `_Ready()` before any addresses are generated.

- [ ] **Step 3: Run the game in Godot to verify it launches without errors**

Open the project in Godot and run it. NPCs should still move between addresses. The Person Inspector won't show sublocations yet (that's Task 18), but the simulation should work.

- [ ] **Step 4: Run all tests**

Run: `dotnet test stakeout.tests/ -v minimal`

- [ ] **Step 5: Commit**

```
git add src/simulation/SimulationManager.cs
git commit -m "feat: SimulationManager uses sublocation-aware schedule building"
```

---

## Task 17: Person Inspector Sublocation Display

**Files:**
- Modify: `scenes/game_shell/GameShell.cs`

- [ ] **Step 1: Read GameShell.cs to find Person Inspector code**

Read `scenes/game_shell/GameShell.cs` and locate the Person Inspector dialog code that displays location info. Find the exact line where `CurrentAddressId` is displayed.

- [ ] **Step 2: Add sublocation display**

Where the Person Inspector shows the person's current location (address), add sublocation info:

```csharp
// After showing address name, add sublocation if present
if (person.CurrentSublocationId.HasValue &&
    State.Sublocations.TryGetValue(person.CurrentSublocationId.Value, out var subloc))
{
    // Append " → SublocName" to the location display
    locationText += $" → {subloc.Name}";
}
```

The exact code depends on the current Person Inspector implementation — read the file first.

- [ ] **Step 3: Run the game in Godot, open Person Inspector, verify sublocation shows**

Open game, generate a crime or let NPCs move, open Person Inspector for any NPC. Should show something like "123 Main St → Cubicle Area".

- [ ] **Step 4: Commit**

```
git add scenes/game_shell/GameShell.cs
git commit -m "feat: Person Inspector shows current sublocation"
```

---

## Task 18: Graph View Visualization

**Files:**
- Create: `scenes/address/GraphView.cs`
- Create: `scenes/address/GraphView.tscn`
- Modify: `scenes/address/AddressView.cs`

- [ ] **Step 1: Create GraphView scene and script**

Create `scenes/address/GraphView.tscn` as a Control node. Create `scenes/address/GraphView.cs` that:
- Takes an address ID and reads its sublocation graph from SimulationState
- Uses `_Draw()` override to render:
  - Rectangles for each sublocation, colored by primary tag (entrance=blue, work_area=gold, food=green, bedroom=purple, road=gray, etc.)
  - Lines between connected sublocations
  - Labels with sublocation names
  - Dots for NPCs currently in each room (query People by CurrentSublocationId)
- Uses a simple tree layout algorithm: Road at top, BFS down by depth, spread nodes horizontally per depth level
- Groups nodes by floor (if Floor property is set) with a labeled border

- [ ] **Step 2: Create BlueprintView scene and script**

Create `scenes/address/BlueprintView.tscn` and `scenes/address/BlueprintView.cs` similarly, but:
- Rooms as adjacent rectangles (floor-plan style)
- Simple auto-layout: entrance rooms at bottom, connected rooms placed adjacently
- Floor switching via buttons (only show one floor at a time)

- [ ] **Step 3: Add view toggle to AddressView**

Modify `scenes/address/AddressView.cs` to add menu items: "Graph View" and "Blueprint View" that load the respective scenes into the content area (or as child nodes).

- [ ] **Step 4: Run the game, enter a location, toggle between views**

Verify both views render the sublocation graph. NPC dots should update as simulation runs.

- [ ] **Step 5: Commit**

```
git add scenes/address/
git commit -m "feat: add Graph View and Blueprint View for sublocation visualization"
```

---

## Task 19: Load Test Harness

**Files:**
- Create: `stakeout.benchmarks/stakeout.benchmarks.csproj`
- Create: `stakeout.benchmarks/Program.cs`

- [ ] **Step 1: Create benchmark project**

```xml
<!-- stakeout.benchmarks/stakeout.benchmarks.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <RootNamespace>Stakeout.Benchmarks</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="../stakeout.csproj" />
  </ItemGroup>
</Project>
```

Note: This project references the main Godot project. If there are build issues with Godot types in a non-Godot project, you may need to conditionally exclude Godot-dependent code or create a shared library. Cross that bridge when you get there — start simple.

- [ ] **Step 2: Implement benchmark Program.cs**

```csharp
// stakeout.benchmarks/Program.cs
using System;
using System.Diagnostics;
using Stakeout.Simulation;
using Stakeout.Simulation.Sublocations;

namespace Stakeout.Benchmarks;

class Program
{
    static void Main(string[] args)
    {
        SublocationGeneratorRegistry.RegisterAll();
        var npcCounts = new[] { 50, 200, 500, 1000 };
        var simHours = 24;

        Console.WriteLine($"Sublocation Simulation Benchmark");
        Console.WriteLine($"Simulating {simHours} in-game hours");
        Console.WriteLine(new string('-', 60));
        Console.WriteLine($"{"NPCs",-8} {"Avg ms/tick",-14} {"Max ms/tick",-14} {"Ticks/sec",-12} {"Memory MB",-10}");
        Console.WriteLine(new string('-', 60));

        foreach (var count in npcCounts)
        {
            RunBenchmark(count, simHours);
        }
    }

    static void RunBenchmark(int npcCount, int simHours)
    {
        // Setup: generate city with N NPCs, all with sublocation-aware schedules
        var state = new SimulationState();
        LocationGenerator.GenerateCityScaffolding(state);

        var rng = new Random(42);
        for (int i = 0; i < npcCount; i++)
        {
            LocationGenerator.GenerateAddress(state, rng);
            PersonGenerator.GeneratePerson(state, rng);
        }

        // Build schedules for all NPCs
        // (This would need adaptation based on how the schedule pipeline works)

        // Simulate
        var tickCount = 0;
        var totalMs = 0.0;
        var maxMs = 0.0;
        var sw = new Stopwatch();
        var tickDelta = 1.0; // 1 second per tick
        var totalSeconds = simHours * 3600;

        var memBefore = GC.GetTotalMemory(true);

        for (int s = 0; s < totalSeconds; s++)
        {
            sw.Restart();

            state.Clock.Tick(tickDelta);
            // Update all people (PersonBehavior equivalent without Godot)
            // This is where you'd call the behavior update loop

            sw.Stop();
            var ms = sw.Elapsed.TotalMilliseconds;
            totalMs += ms;
            if (ms > maxMs) maxMs = ms;
            tickCount++;
        }

        var memAfter = GC.GetTotalMemory(false);
        var memMb = (memAfter - memBefore) / (1024.0 * 1024.0);
        var avgMs = totalMs / tickCount;
        var ticksPerSec = 1000.0 / avgMs;

        Console.WriteLine($"{npcCount,-8} {avgMs,-14:F4} {maxMs,-14:F4} {ticksPerSec,-12:F0} {memMb,-10:F1}");
    }
}
```

- [ ] **Step 3: Verify it builds and runs**

Run: `dotnet run --project stakeout.benchmarks/`

Note: This may fail if the main project has Godot dependencies that can't resolve outside the Godot editor. If so, you'll need to either:
- Extract simulation code into a separate class library that both Godot and benchmarks reference
- Or mock/stub the Godot types (Vector2, etc.)

Solve this if it comes up. The benchmark logic above is the target — adapt the project structure as needed.

- [ ] **Step 4: Commit**

```
git add stakeout.benchmarks/
git commit -m "feat: add load test harness for simulation benchmarking"
```

---

## Task 20: Integration Smoke Test & Final Verification

**Files:**
- No new files

- [ ] **Step 1: Run all unit tests**

Run: `dotnet test stakeout.tests/ -v minimal`
Expected: All tests pass.

- [ ] **Step 2: Run the game in Godot**

Launch the game. Verify:
- NPCs spawn and move between addresses (existing behavior preserved)
- Person Inspector shows sublocation info (e.g., "123 Main St → Kitchen")
- NPC sublocations change over time as they move through rooms
- Graph View renders when entering a location
- Blueprint View renders when entering a location
- No errors in Godot console

- [ ] **Step 3: Generate a crime and verify killer sublocation movement**

Click "Generate Crime" in the debug panel. Fast-forward time. Watch the killer in Person Inspector. They should:
- Leave their home (sublocation changes: Bedroom → Hallway → Front Door → Road)
- Travel to victim's home
- Enter via covert entry (Back Door or Window, not Front Door)
- Move through rooms to victim's bedroom
- Execute kill

- [ ] **Step 4: Run load test benchmark**

Run: `dotnet run --project stakeout.benchmarks/`
Record baseline numbers for future comparison.

- [ ] **Step 5: Commit any final fixes**

```
git add -A
git commit -m "fix: integration fixes from smoke testing"
```

(Only if there were fixes needed.)

---

## Summary

| Task | Component | Estimated Steps |
|------|-----------|----------------|
| 1 | Sublocation entity & data model | 8 |
| 2 | SublocationGraph (queries + pathfinding) | 6 |
| 3 | AddressType extensions | 6 |
| 4 | ScheduleEntry & Person fields | 4 |
| 5 | Generator interface & registry | 4 |
| 6 | SuburbanHome generator | 6 |
| 7 | Office generator | 5 |
| 8 | Diner, DiveBar, Park, ApartmentBuilding generators | 7 |
| 9 | Integrate generation into LocationGenerator | 6 |
| 10 | Decomposition interface & WorkDay strategy | 6 |
| 11 | Inhabit & Intrude strategies | 6 |
| 12 | Patronize, StaffShift, Visit strategies | 5 |
| 13 | TaskResolver | 6 |
| 14 | Integrate TaskResolver into ScheduleBuilder | 6 |
| 15 | PersonBehavior sublocation tracking | 6 |
| 16 | SimulationManager update | 5 |
| 17 | Person Inspector sublocation display | 4 |
| 18 | Graph View & Blueprint View | 5 |
| 19 | Load test harness | 4 |
| 20 | Integration smoke test | 5 |
