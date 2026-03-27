# Connection Points as Graph Edges — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> **CRITICAL INSTRUCTION FOR ALL AGENTS:** Never prefix shell commands with `cd`. The working directory is already the project root. Run commands directly (e.g., `git add file.cs`, not `cd path && git add file.cs`). Never use `git -C`. Never chain commands with `&&` or `;`. Run each command as a separate Bash call. This breaks permission matching and is strictly prohibited.

**Goal:** Refactor connection points (doors, windows, stairs, gates) from sublocation nodes to graph edges with composable property modifiers.

**Architecture:** Connection points become metadata on `SublocationConnection` edges instead of `Sublocation` nodes. Each connection carries nullable property objects (`LockableProperty`, `ConcealableProperty`, `TransparentProperty`, `BreakableProperty`) for composable capabilities. Pathfinding returns `PathStep` objects containing both the destination node and the edge used. Generators stop creating connection-point nodes and wire rooms directly.

**Tech Stack:** C# / .NET 8 / Godot 4.6 / xUnit

**Spec:** `docs/superpowers/specs/2026-03-26-connection-edges-design.md`

**Test command:** `dotnet test stakeout.tests/ -v minimal`

---

## File Structure

### New files
- `src/simulation/entities/ConnectionProperties.cs` — LockableProperty, ConcealableProperty, TransparentProperty, BreakableProperty classes, plus LockMechanism and ConcealmentMethod enums
- `src/simulation/entities/PathStep.cs` — PathStep class for FindPath results
- `src/simulation/entities/TraversalContext.cs` — TraversalContext class for edge-filtered pathfinding
- `stakeout.tests/Simulation/Entities/ConnectionPropertiesTests.cs` — tests for property classes
- `stakeout.tests/Simulation/Entities/PathStepTests.cs` — tests for new FindPath behavior

### Modified files
- `src/simulation/entities/Sublocation.cs` — revise ConnectionType enum, revise SublocationConnection class
- `src/simulation/entities/SublocationGraph.cs` — store connections, edge-aware BFS, new query methods
- `src/simulation/scheduling/DailySchedule.cs` — add ViaConnectionId to ScheduleEntry
- `src/simulation/sublocations/SuburbanHomeGenerator.cs` — remove connection-point nodes, use typed connections
- `src/simulation/sublocations/DinerGenerator.cs` — same
- `src/simulation/sublocations/DiveBarGenerator.cs` — same
- `src/simulation/sublocations/ParkGenerator.cs` — same, Trail→OpenPassage
- `src/simulation/sublocations/ApartmentBuildingGenerator.cs` — elevator chain→single node, remove stairwells
- `src/simulation/sublocations/OfficeGenerator.cs` — same
- `src/simulation/scheduling/decomposition/InhabitDecomposition.cs` — use FindEntryPoint
- `src/simulation/scheduling/decomposition/IntrudeDecomposition.cs` — use FindEntryPoint, edge-aware paths
- `src/simulation/scheduling/decomposition/WorkDayDecomposition.cs` — use FindEntryPoint
- `src/simulation/scheduling/decomposition/VisitDecomposition.cs` — use FindEntryPoint
- `src/simulation/scheduling/decomposition/PatronizeDecomposition.cs` — use FindEntryPoint
- `src/simulation/scheduling/decomposition/StaffShiftDecomposition.cs` — use FindEntryPoint
- `scenes/game_shell/GameShell.cs` — schedule display with "via" connections
- `scenes/address/GraphView.cs` — remove IsBidirectional, add edge labels
- `scenes/address/BlueprintView.cs` — add connection labels
- All existing test files for the above — update test helper graphs to remove connection-point nodes

---

## Task 1: Connection Properties and Revised Data Model

**Files:**
- Create: `src/simulation/entities/ConnectionProperties.cs`
- Modify: `src/simulation/entities/Sublocation.cs`
- Create: `stakeout.tests/Simulation/Entities/ConnectionPropertiesTests.cs`
- Modify: `stakeout.tests/Simulation/Entities/SublocationTests.cs`

This task changes the foundational data model. All subsequent tasks depend on it.

- [ ] **Step 1: Create ConnectionProperties.cs with enums and property classes**

Create `src/simulation/entities/ConnectionProperties.cs`:

```csharp
using System;

namespace Stakeout.Simulation.Entities;

public enum LockMechanism
{
    Key,
    Combination,
    Keypad,
    Electronic
}

public enum ConcealmentMethod
{
    Rug,
    Bookshelf,
    Leaves,
    FalseWall,
    Bushes
}

public class LockableProperty
{
    public LockMechanism Mechanism { get; set; }
    public bool IsLocked { get; set; }
    public int? KeyItemId { get; set; }
}

public class ConcealableProperty
{
    public ConcealmentMethod Method { get; set; }
    public bool IsDiscovered { get; set; }
    public float DiscoveryDifficulty { get; set; }
}

public class TransparentProperty
{
    public bool CanSeeThrough { get; set; }
    public bool CanShootThrough { get; set; }
    public bool CanHearThrough { get; set; }
}

public class BreakableProperty
{
    public float Durability { get; set; } = 1.0f;
    public bool IsBroken { get; set; }
}
```

- [ ] **Step 2: Revise ConnectionType enum and SublocationConnection in Sublocation.cs**

Replace the entire contents of `src/simulation/entities/Sublocation.cs` with:

```csharp
using System;

namespace Stakeout.Simulation.Entities;

public enum ConnectionType
{
    OpenPassage,
    Door,
    Window,
    Stairs,
    Gate,
    HiddenPath
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
    public int Id { get; set; }
    public int FromSublocationId { get; set; }
    public int ToSublocationId { get; set; }
    public ConnectionType Type { get; set; } = ConnectionType.OpenPassage;
    public string Name { get; set; }
    public string[] Tags { get; set; } = Array.Empty<string>();

    public LockableProperty Lockable { get; set; }
    public ConcealableProperty Concealable { get; set; }
    public TransparentProperty Transparent { get; set; }
    public BreakableProperty Breakable { get; set; }

    public bool HasTag(string tag) => Array.IndexOf(Tags, tag) >= 0;
}
```

Key changes: ConnectionType enum reordered with OpenPassage as default, Elevator and Trail removed. SublocationConnection gains Id, Name, Tags, nullable property objects. IsBidirectional removed (all connections are bidirectional).

- [ ] **Step 3: Write tests for ConnectionProperties**

Create `stakeout.tests/Simulation/Entities/ConnectionPropertiesTests.cs`:

```csharp
using Stakeout.Simulation.Entities;
using Xunit;

namespace Stakeout.Tests.Simulation.Entities;

public class ConnectionPropertiesTests
{
    [Fact]
    public void LockableProperty_Defaults()
    {
        var prop = new LockableProperty();
        Assert.False(prop.IsLocked);
        Assert.Null(prop.KeyItemId);
    }

    [Fact]
    public void ConcealableProperty_Defaults()
    {
        var prop = new ConcealableProperty();
        Assert.False(prop.IsDiscovered);
        Assert.Equal(0f, prop.DiscoveryDifficulty);
    }

    [Fact]
    public void TransparentProperty_Defaults()
    {
        var prop = new TransparentProperty();
        Assert.False(prop.CanSeeThrough);
        Assert.False(prop.CanShootThrough);
        Assert.False(prop.CanHearThrough);
    }

    [Fact]
    public void BreakableProperty_DefaultDurability()
    {
        var prop = new BreakableProperty();
        Assert.Equal(1.0f, prop.Durability);
        Assert.False(prop.IsBroken);
    }

    [Fact]
    public void SublocationConnection_NewDefaults()
    {
        var conn = new SublocationConnection();
        Assert.Equal(0, conn.Id);
        Assert.Equal(ConnectionType.OpenPassage, conn.Type);
        Assert.Null(conn.Name);
        Assert.Empty(conn.Tags);
        Assert.Null(conn.Lockable);
        Assert.Null(conn.Concealable);
        Assert.Null(conn.Transparent);
        Assert.Null(conn.Breakable);
    }

    [Fact]
    public void SublocationConnection_HasTag()
    {
        var conn = new SublocationConnection { Tags = new[] { "entrance", "main" } };
        Assert.True(conn.HasTag("entrance"));
        Assert.False(conn.HasTag("covert_entry"));
    }
}
```

- [ ] **Step 4: Update SublocationTests.cs**

In `stakeout.tests/Simulation/Entities/SublocationTests.cs`, update the `SublocationConnection_Defaults` test to match the new defaults:

```csharp
[Fact]
public void SublocationConnection_Defaults()
{
    var conn = new SublocationConnection();
    Assert.Equal(0, conn.FromSublocationId);
    Assert.Equal(0, conn.ToSublocationId);
    Assert.Equal(ConnectionType.OpenPassage, conn.Type);
}
```

- [ ] **Step 5: Run tests**

Run: `dotnet test stakeout.tests/ -v minimal`

Expected: Build succeeds. New tests pass. Many existing tests will FAIL because they reference `ConnectionType.Door` as default and `IsBidirectional`. That's expected — we fix those in subsequent tasks.

If the build fails (not test failures, but compile errors), fix any missed references to `IsBidirectional`, `ConnectionType.Elevator`, or `ConnectionType.Trail` across the codebase. These compile errors tell you what else needs updating.

- [ ] **Step 6: Fix all compile errors across the codebase**

The build will have compile errors from:
1. All generators using `IsBidirectional = true` — remove these lines
2. `GraphView.cs:40-41` using `c.IsBidirectional` — remove the check, always add both directions
3. `ApartmentBuildingGenerator.cs` using `ConnectionType.Elevator` — change to `ConnectionType.Door`
4. `ParkGenerator.cs` using `ConnectionType.Trail` — change to `ConnectionType.OpenPassage`
5. Any test files referencing `IsBidirectional` or removed enum values

For each generator's `Connect` helper, remove the `IsBidirectional = true` line. The SublocationConnection no longer has this property.

For `GraphView.cs` line 40-41, change:
```csharp
if (c.IsBidirectional && adjacency.ContainsKey(c.ToSublocationId))
    adjacency[c.ToSublocationId].Add(c.FromSublocationId);
```
to:
```csharp
if (adjacency.ContainsKey(c.ToSublocationId))
    adjacency[c.ToSublocationId].Add(c.FromSublocationId);
```

- [ ] **Step 7: Run tests again to confirm build succeeds**

Run: `dotnet test stakeout.tests/ -v minimal`

Expected: Build succeeds. Tests that relied on `ConnectionType.Door` as the default will now fail (they'll get `OpenPassage`). Some generator tests may also fail. Note the failures — we address them in subsequent tasks.

- [ ] **Step 8: Commit**

```
git add src/simulation/entities/ConnectionProperties.cs
git add src/simulation/entities/Sublocation.cs
git add stakeout.tests/Simulation/Entities/ConnectionPropertiesTests.cs
git add stakeout.tests/Simulation/Entities/SublocationTests.cs
git add scenes/address/GraphView.cs
```

Then add any other files that were fixed for compile errors (generators, etc).

```
git commit -m "refactor: connection points data model — edges with composable properties

ConnectionType enum revised (OpenPassage default, Elevator/Trail removed).
SublocationConnection gains Id, Name, Tags, nullable property objects.
IsBidirectional removed (all connections bidirectional).
New ConnectionProperties.cs with LockableProperty, ConcealableProperty,
TransparentProperty, BreakableProperty."
```

---

## Task 2: SublocationGraph — Edge-Aware Storage and Pathfinding

**Files:**
- Modify: `src/simulation/entities/SublocationGraph.cs`
- Create: `src/simulation/entities/PathStep.cs`
- Create: `src/simulation/entities/TraversalContext.cs`
- Modify: `stakeout.tests/Simulation/Entities/SublocationGraphTests.cs`

**Depends on:** Task 1

- [ ] **Step 1: Create PathStep.cs**

Create `src/simulation/entities/PathStep.cs`:

```csharp
namespace Stakeout.Simulation.Entities;

public class PathStep
{
    public Sublocation Location { get; set; }
    public SublocationConnection Via { get; set; }
}
```

- [ ] **Step 2: Create TraversalContext.cs**

Create `src/simulation/entities/TraversalContext.cs`:

```csharp
namespace Stakeout.Simulation.Entities;

public class TraversalContext
{
    public Person Traveler { get; set; }

    public bool CanTraverse(SublocationConnection conn)
    {
        if (conn.Lockable?.IsLocked == true)
            return false;
        if (conn.Concealable != null && !conn.Concealable.IsDiscovered)
            return false;
        return true;
    }
}
```

Note: This is a minimal first implementation. `CanTraverse` will be extended later to check keys, skills, etc. For now, locked doors block everyone and undiscovered concealed passages block everyone.

- [ ] **Step 3: Rewrite SublocationGraph.cs**

Replace the entire contents of `src/simulation/entities/SublocationGraph.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;

namespace Stakeout.Simulation.Entities;

public class SublocationGraph
{
    private readonly Dictionary<int, Sublocation> _sublocations;
    private readonly Dictionary<int, List<SublocationConnection>> _edges;
    private readonly List<SublocationConnection> _allConnections;

    public SublocationGraph(
        Dictionary<int, Sublocation> sublocations,
        List<SublocationConnection> connections)
    {
        _sublocations = sublocations;
        _allConnections = connections;
        _edges = new Dictionary<int, List<SublocationConnection>>();

        foreach (var sub in sublocations.Keys)
            _edges[sub] = new List<SublocationConnection>();

        foreach (var conn in connections)
        {
            if (_edges.ContainsKey(conn.FromSublocationId))
                _edges[conn.FromSublocationId].Add(conn);
            if (_edges.ContainsKey(conn.ToSublocationId))
                _edges[conn.ToSublocationId].Add(conn);
        }
    }

    public Sublocation Get(int id)
    {
        return _sublocations.TryGetValue(id, out var sub) ? sub : null;
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

    public List<Sublocation> GetNeighbors(int sublocationId)
    {
        if (!_edges.TryGetValue(sublocationId, out var connections))
            return new List<Sublocation>();

        return connections
            .Select(c => c.FromSublocationId == sublocationId ? c.ToSublocationId : c.FromSublocationId)
            .Distinct()
            .Where(id => _sublocations.ContainsKey(id))
            .Select(id => _sublocations[id])
            .ToList();
    }

    /// <summary>
    /// Find a connection by tag. Returns the connection and the interior (To) sublocation.
    /// </summary>
    public (SublocationConnection conn, Sublocation target)? FindConnectionByTag(string tag)
    {
        foreach (var conn in _allConnections)
        {
            if (conn.HasTag(tag) && _sublocations.TryGetValue(conn.ToSublocationId, out var target))
                return (conn, target);
        }
        return null;
    }

    /// <summary>
    /// Find all connections with a given tag.
    /// </summary>
    public List<(SublocationConnection conn, Sublocation target)> FindAllConnectionsByTag(string tag)
    {
        var results = new List<(SublocationConnection, Sublocation)>();
        foreach (var conn in _allConnections)
        {
            if (conn.HasTag(tag) && _sublocations.TryGetValue(conn.ToSublocationId, out var target))
                results.Add((conn, target));
        }
        return results;
    }

    /// <summary>
    /// Unified entry-point search: checks connection tags first, then sublocation tags.
    /// Returns the connection (if entry is via a connection) and the target sublocation.
    /// </summary>
    public (SublocationConnection conn, Sublocation target)? FindEntryPoint(string tag)
    {
        var connResult = FindConnectionByTag(tag);
        if (connResult.HasValue)
            return connResult;

        var sub = FindByTag(tag);
        if (sub != null)
            return (null, sub);

        return null;
    }

    /// <summary>
    /// Get the connection between two adjacent sublocations.
    /// </summary>
    public SublocationConnection GetConnectionBetween(int fromId, int toId)
    {
        if (!_edges.TryGetValue(fromId, out var connections))
            return null;

        return connections.FirstOrDefault(c =>
            (c.FromSublocationId == fromId && c.ToSublocationId == toId) ||
            (c.FromSublocationId == toId && c.ToSublocationId == fromId));
    }

    /// <summary>
    /// BFS pathfinding. Returns list of PathStep (location + via edge).
    /// When context is provided, edges are filtered by CanTraverse.
    /// </summary>
    public List<PathStep> FindPath(int fromId, int toId, TraversalContext context = null)
    {
        if (fromId == toId)
            return new List<PathStep> { new PathStep { Location = _sublocations[fromId], Via = null } };

        var visited = new HashSet<int> { fromId };
        var queue = new Queue<int>();
        var parentNode = new Dictionary<int, int>();
        var parentEdge = new Dictionary<int, SublocationConnection>();
        queue.Enqueue(fromId);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current == toId)
            {
                var path = new List<PathStep>();
                var node = toId;
                while (node != fromId)
                {
                    path.Add(new PathStep
                    {
                        Location = _sublocations[node],
                        Via = parentEdge[node]
                    });
                    node = parentNode[node];
                }
                path.Add(new PathStep { Location = _sublocations[fromId], Via = null });
                path.Reverse();
                return path;
            }

            if (!_edges.TryGetValue(current, out var connections))
                continue;

            foreach (var conn in connections)
            {
                var neighbor = conn.FromSublocationId == current
                    ? conn.ToSublocationId
                    : conn.FromSublocationId;

                if (visited.Contains(neighbor))
                    continue;

                if (context != null && !context.CanTraverse(conn))
                    continue;

                visited.Add(neighbor);
                parentNode[neighbor] = current;
                parentEdge[neighbor] = conn;
                queue.Enqueue(neighbor);
            }
        }

        return new List<PathStep>();
    }

    public IReadOnlyDictionary<int, Sublocation> AllSublocations => _sublocations;
    public IReadOnlyList<SublocationConnection> AllConnections => _allConnections;
}
```

- [ ] **Step 4: Update SublocationGraphTests.cs**

Replace the entire contents of `stakeout.tests/Simulation/Entities/SublocationGraphTests.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using Stakeout.Simulation.Entities;
using Xunit;

namespace Stakeout.Tests.Simulation.Entities;

public class SublocationGraphTests
{
    private static SublocationGraph CreateSimpleOffice()
    {
        var subs = new Dictionary<int, Sublocation>
        {
            { 1, new Sublocation { Id = 1, AddressId = 10, Name = "Road", Tags = new[] { "road" } } },
            { 2, new Sublocation { Id = 2, AddressId = 10, Name = "Lobby", Tags = new[] { "work_area", "public" } } },
            { 3, new Sublocation { Id = 3, AddressId = 10, Name = "Cubicle Area", Tags = new[] { "work_area" } } },
            { 4, new Sublocation { Id = 4, AddressId = 10, Name = "Break Room", Tags = new[] { "food" } } },
        };
        var conns = new List<SublocationConnection>
        {
            new() { Id = 100, FromSublocationId = 1, ToSublocationId = 2, Type = ConnectionType.Door, Name = "Front Door", Tags = new[] { "entrance" } },
            new() { Id = 101, FromSublocationId = 2, ToSublocationId = 3, Type = ConnectionType.Door },
            new() { Id = 102, FromSublocationId = 3, ToSublocationId = 4, Type = ConnectionType.OpenPassage },
        };
        return new SublocationGraph(subs, conns);
    }

    [Fact]
    public void FindByTag_ExistingTag_ReturnsSublocation()
    {
        var graph = CreateSimpleOffice();
        var result = graph.FindByTag("road");
        Assert.NotNull(result);
        Assert.Equal("Road", result.Name);
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
        var path = graph.FindPath(2, 3);
        Assert.Equal(2, path.Count);
        Assert.Equal(2, path[0].Location.Id);
        Assert.Null(path[0].Via);
        Assert.Equal(3, path[1].Location.Id);
        Assert.NotNull(path[1].Via);
        Assert.Equal(101, path[1].Via.Id);
    }

    [Fact]
    public void FindPath_TwoHops_ReturnsFullPath()
    {
        var graph = CreateSimpleOffice();
        var path = graph.FindPath(1, 3);
        Assert.Equal(3, path.Count);
        Assert.Equal(1, path[0].Location.Id);
        Assert.Equal(2, path[1].Location.Id);
        Assert.Equal(3, path[2].Location.Id);
    }

    [Fact]
    public void FindPath_SameRoom_ReturnsSingleElement()
    {
        var graph = CreateSimpleOffice();
        var path = graph.FindPath(2, 2);
        Assert.Single(path);
        Assert.Equal(2, path[0].Location.Id);
        Assert.Null(path[0].Via);
    }

    [Fact]
    public void FindPath_Unreachable_ReturnsEmptyList()
    {
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
    public void FindPath_LockedDoor_BlocksTraversal()
    {
        var subs = new Dictionary<int, Sublocation>
        {
            { 1, new Sublocation { Id = 1, AddressId = 10, Name = "Room A", Tags = new[] { "road" } } },
            { 2, new Sublocation { Id = 2, AddressId = 10, Name = "Room B", Tags = new[] { "work_area" } } },
        };
        var conns = new List<SublocationConnection>
        {
            new() { Id = 100, FromSublocationId = 1, ToSublocationId = 2, Type = ConnectionType.Door,
                Lockable = new LockableProperty { IsLocked = true } },
        };
        var graph = new SublocationGraph(subs, conns);
        var path = graph.FindPath(1, 2, new TraversalContext());
        Assert.Empty(path);
    }

    [Fact]
    public void FindPath_UnlockedDoor_AllowsTraversal()
    {
        var subs = new Dictionary<int, Sublocation>
        {
            { 1, new Sublocation { Id = 1, AddressId = 10, Name = "Room A", Tags = new[] { "road" } } },
            { 2, new Sublocation { Id = 2, AddressId = 10, Name = "Room B", Tags = new[] { "work_area" } } },
        };
        var conns = new List<SublocationConnection>
        {
            new() { Id = 100, FromSublocationId = 1, ToSublocationId = 2, Type = ConnectionType.Door,
                Lockable = new LockableProperty { IsLocked = false } },
        };
        var graph = new SublocationGraph(subs, conns);
        var path = graph.FindPath(1, 2, new TraversalContext());
        Assert.Equal(2, path.Count);
    }

    [Fact]
    public void GetNeighbors_ReturnsConnectedRooms()
    {
        var graph = CreateSimpleOffice();
        var neighbors = graph.GetNeighbors(2);
        Assert.Equal(2, neighbors.Count);
        Assert.Contains(neighbors, n => n.Id == 1);
        Assert.Contains(neighbors, n => n.Id == 3);
    }

    [Fact]
    public void FindConnectionByTag_ReturnsConnectionAndTarget()
    {
        var graph = CreateSimpleOffice();
        var result = graph.FindConnectionByTag("entrance");
        Assert.NotNull(result);
        Assert.Equal("Front Door", result.Value.conn.Name);
        Assert.Equal("Lobby", result.Value.target.Name);
    }

    [Fact]
    public void FindConnectionByTag_NoMatch_ReturnsNull()
    {
        var graph = CreateSimpleOffice();
        var result = graph.FindConnectionByTag("covert_entry");
        Assert.Null(result);
    }

    [Fact]
    public void FindEntryPoint_ConnectionTag_ReturnsConnection()
    {
        var graph = CreateSimpleOffice();
        var result = graph.FindEntryPoint("entrance");
        Assert.NotNull(result);
        Assert.NotNull(result.Value.conn);
        Assert.Equal("Lobby", result.Value.target.Name);
    }

    [Fact]
    public void FindEntryPoint_SublocationTag_ReturnsSubWithNullConnection()
    {
        var graph = CreateSimpleOffice();
        var result = graph.FindEntryPoint("food");
        Assert.NotNull(result);
        Assert.Null(result.Value.conn);
        Assert.Equal("Break Room", result.Value.target.Name);
    }

    [Fact]
    public void GetConnectionBetween_ReturnsConnection()
    {
        var graph = CreateSimpleOffice();
        var conn = graph.GetConnectionBetween(1, 2);
        Assert.NotNull(conn);
        Assert.Equal(ConnectionType.Door, conn.Type);
    }

    [Fact]
    public void GetConnectionBetween_ReverseDirection_StillFinds()
    {
        var graph = CreateSimpleOffice();
        var conn = graph.GetConnectionBetween(2, 1);
        Assert.NotNull(conn);
    }
}
```

Note: The `CreateSimpleOffice` helper no longer has "entrance" as a sublocation tag on Lobby. Instead, "entrance" is a tag on the Door connection between Road and Lobby. Lobby now has "work_area" and "public" tags. This matches the new model.

- [ ] **Step 5: Run tests**

Run: `dotnet test stakeout.tests/ -v minimal`

Expected: New SublocationGraph tests pass. Other tests across the codebase may still fail (generators, decomposition tests) — those are addressed in subsequent tasks.

- [ ] **Step 6: Commit**

```
git add src/simulation/entities/PathStep.cs
git add src/simulation/entities/TraversalContext.cs
git add src/simulation/entities/SublocationGraph.cs
git add stakeout.tests/Simulation/Entities/SublocationGraphTests.cs
git commit -m "refactor: edge-aware SublocationGraph with PathStep and TraversalContext

SublocationGraph now stores SublocationConnection objects instead of
plain adjacency IDs. FindPath returns List<PathStep> with via-edge info.
New methods: FindConnectionByTag, FindAllConnectionsByTag, FindEntryPoint,
GetConnectionBetween. TraversalContext filters edges during BFS."
```

---

## Task 3: ScheduleEntry ViaConnectionId

**Files:**
- Modify: `src/simulation/scheduling/DailySchedule.cs`

**Depends on:** Task 1

- [ ] **Step 1: Add ViaConnectionId to ScheduleEntry**

In `src/simulation/scheduling/DailySchedule.cs`, add one line to the ScheduleEntry class after `TargetSublocationId`:

```csharp
public int? ViaConnectionId { get; set; }
```

- [ ] **Step 2: Run tests to confirm nothing breaks**

Run: `dotnet test stakeout.tests/ -v minimal`

Expected: Adding an optional property doesn't break anything.

- [ ] **Step 3: Commit**

```
git add src/simulation/scheduling/DailySchedule.cs
git commit -m "feat: add ViaConnectionId to ScheduleEntry for edge tracking"
```

---

## Task 4: Update All Generators

**Files:**
- Modify: `src/simulation/sublocations/SuburbanHomeGenerator.cs`
- Modify: `src/simulation/sublocations/DinerGenerator.cs`
- Modify: `src/simulation/sublocations/DiveBarGenerator.cs`
- Modify: `src/simulation/sublocations/ParkGenerator.cs`
- Modify: `src/simulation/sublocations/ApartmentBuildingGenerator.cs`
- Modify: `src/simulation/sublocations/OfficeGenerator.cs`
- Modify: All generator test files

**Depends on:** Task 1

Each generator needs: (a) remove connection-point sublocation nodes, (b) use new `Connect` overload that accepts a `SublocationConnection`, (c) move entry-point tags to connections.

- [ ] **Step 1: Rewrite SuburbanHomeGenerator.cs**

Replace the full contents of `src/simulation/sublocations/SuburbanHomeGenerator.cs`:

```csharp
using System;
using System.Collections.Generic;
using Stakeout.Simulation.Entities;

namespace Stakeout.Simulation.Sublocations;

public class SuburbanHomeGenerator : ISublocationGenerator
{
    public SublocationGraph Generate(Address address, SimulationState state, Random rng)
    {
        var subs = new Dictionary<int, Sublocation>();
        var conns = new List<SublocationConnection>();

        Sublocation Make(string name, string[] tags, int floor)
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

        // Ground floor layout
        var road = Make("Road", new[] { "road" }, 0);
        var frontYard = Make("Front Yard", new[] { "yard", "front" }, 0);
        var hallway = Make("Ground Floor Hallway", new[] { "hallway" }, 0);
        var kitchen = Make("Kitchen", new[] { "kitchen", "food" }, 0);
        var livingRoom = Make("Living Room", new[] { "living", "social" }, 0);
        var groundBathroom = Make("Ground Floor Bathroom", new[] { "restroom" }, 0);
        var backyard = Make("Backyard", new[] { "yard", "back" }, 0);

        // Upstairs layout
        var upstairsHallway = Make("Upstairs Hallway", new[] { "hallway" }, 1);
        var upstairsBathroom = Make("Upstairs Bathroom", new[] { "restroom" }, 1);

        // Ground floor connections
        Connect(road, frontYard);
        Connect(frontYard, hallway, new SublocationConnection
        {
            Type = ConnectionType.Door,
            Name = "Front Door",
            Tags = new[] { "entrance" },
            Lockable = new LockableProperty { Mechanism = LockMechanism.Key },
            Breakable = new BreakableProperty(),
        });
        Connect(hallway, kitchen);
        Connect(hallway, livingRoom);
        Connect(hallway, groundBathroom, new SublocationConnection
        {
            Type = ConnectionType.Door,
        });
        Connect(hallway, backyard, new SublocationConnection
        {
            Type = ConnectionType.Door,
            Name = "Back Door",
            Tags = new[] { "covert_entry", "staff_entry" },
            Lockable = new LockableProperty { Mechanism = LockMechanism.Key },
            Breakable = new BreakableProperty(),
        });
        Connect(backyard, road);
        Connect(frontYard, livingRoom, new SublocationConnection
        {
            Type = ConnectionType.Window,
            Name = "Ground Floor Window",
            Tags = new[] { "covert_entry" },
            Lockable = new LockableProperty { Mechanism = LockMechanism.Key, IsLocked = true },
            Transparent = new TransparentProperty { CanSeeThrough = true, CanShootThrough = true },
            Breakable = new BreakableProperty(),
        });

        // Stairs to upstairs
        Connect(hallway, upstairsHallway, new SublocationConnection
        {
            Type = ConnectionType.Stairs,
            Name = "Stairs",
        });
        Connect(upstairsHallway, upstairsBathroom, new SublocationConnection
        {
            Type = ConnectionType.Door,
        });

        // Bedrooms (2-3)
        int bedroomCount = rng.Next(2, 4);
        for (int i = 1; i <= bedroomCount; i++)
        {
            var bedroom = Make($"Bedroom {i}", new[] { "bedroom", "private" }, 1);
            Connect(upstairsHallway, bedroom, new SublocationConnection
            {
                Type = ConnectionType.Door,
            });
        }

        return new SublocationGraph(subs, conns);
    }
}
```

- [ ] **Step 2: Rewrite DinerGenerator.cs**

Replace the full contents of `src/simulation/sublocations/DinerGenerator.cs`:

```csharp
using System;
using System.Collections.Generic;
using Stakeout.Simulation.Entities;

namespace Stakeout.Simulation.Sublocations;

public class DinerGenerator : ISublocationGenerator
{
    public SublocationGraph Generate(Address address, SimulationState state, Random rng)
    {
        var subs = new Dictionary<int, Sublocation>();
        var conns = new List<SublocationConnection>();

        Sublocation Make(string name, string[] tags, int floor)
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
        var diningArea = Make("Dining Area", new[] { "service_area", "social" }, 0);
        var counter = Make("Counter", new[] { "service_area" }, 0);
        var kitchen = Make("Kitchen", new[] { "work_area", "food" }, 0);
        var storage = Make("Storage", new[] { "storage" }, 0);
        var managerOffice = Make("Manager Office", new[] { "work_area", "private" }, 0);
        var restrooms = Make("Restrooms", new[] { "restroom" }, 0);

        Connect(road, diningArea, new SublocationConnection
        {
            Type = ConnectionType.Door,
            Name = "Front Door",
            Tags = new[] { "entrance" },
            Lockable = new LockableProperty { Mechanism = LockMechanism.Key },
            Breakable = new BreakableProperty(),
        });
        Connect(diningArea, counter);
        Connect(road, kitchen, new SublocationConnection
        {
            Type = ConnectionType.Door,
            Name = "Back Door",
            Tags = new[] { "staff_entry" },
            Lockable = new LockableProperty { Mechanism = LockMechanism.Key },
            Breakable = new BreakableProperty(),
        });
        Connect(kitchen, storage);
        Connect(kitchen, managerOffice, new SublocationConnection
        {
            Type = ConnectionType.Door,
        });
        Connect(diningArea, restrooms, new SublocationConnection
        {
            Type = ConnectionType.Door,
        });

        return new SublocationGraph(subs, conns);
    }
}
```

- [ ] **Step 3: Rewrite DiveBarGenerator.cs**

Replace the full contents of `src/simulation/sublocations/DiveBarGenerator.cs`:

```csharp
using System;
using System.Collections.Generic;
using Stakeout.Simulation.Entities;

namespace Stakeout.Simulation.Sublocations;

public class DiveBarGenerator : ISublocationGenerator
{
    public SublocationGraph Generate(Address address, SimulationState state, Random rng)
    {
        var subs = new Dictionary<int, Sublocation>();
        var conns = new List<SublocationConnection>();

        Sublocation Make(string name, string[] tags, int floor)
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
        var barArea = Make("Bar Area", new[] { "service_area", "social" }, 0);
        var alley = Make("Alley", new[] { "covert_entry" }, 0);
        var backHallway = Make("Back Hallway", new[] { "hallway" }, 0);
        var storage = Make("Storage", new[] { "storage" }, 0);
        var managerOffice = Make("Manager Office", new[] { "work_area", "private" }, 0);
        var restrooms = Make("Restrooms", new[] { "restroom" }, 0);

        Connect(road, barArea, new SublocationConnection
        {
            Type = ConnectionType.Door,
            Name = "Front Door",
            Tags = new[] { "entrance" },
            Lockable = new LockableProperty { Mechanism = LockMechanism.Key },
            Breakable = new BreakableProperty(),
        });
        Connect(road, backHallway, new SublocationConnection
        {
            Type = ConnectionType.Door,
            Name = "Back Door",
            Tags = new[] { "staff_entry" },
            Lockable = new LockableProperty { Mechanism = LockMechanism.Key },
            Breakable = new BreakableProperty(),
        });
        Connect(backHallway, alley);
        Connect(alley, road);
        Connect(backHallway, storage);
        Connect(backHallway, managerOffice, new SublocationConnection
        {
            Type = ConnectionType.Door,
        });
        Connect(barArea, restrooms, new SublocationConnection
        {
            Type = ConnectionType.Door,
        });

        return new SublocationGraph(subs, conns);
    }
}
```

Note: "Alley" keeps its `covert_entry` sublocation tag — it's a real place, not a connection point.

- [ ] **Step 4: Rewrite ParkGenerator.cs**

Replace the full contents of `src/simulation/sublocations/ParkGenerator.cs`:

```csharp
using System;
using System.Collections.Generic;
using Stakeout.Simulation.Entities;

namespace Stakeout.Simulation.Sublocations;

public class ParkGenerator : ISublocationGenerator
{
    public SublocationGraph Generate(Address address, SimulationState state, Random rng)
    {
        var subs = new Dictionary<int, Sublocation>();
        var conns = new List<SublocationConnection>();

        Sublocation Make(string name, string[] tags, int floor)
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
        var parkingLot = Make("Parking Lot", new[] { "parking" }, 0);
        var joggingPath = Make("Jogging Path", new[] { "outdoor" }, 0);
        var picnicArea = Make("Picnic Area", new[] { "food", "social" }, 0);
        var playground = Make("Playground", new[] { "outdoor", "social" }, 0);
        var woodedArea = Make("Wooded Area", new[] { "outdoor", "covert_entry" }, 0);
        var shoreLine = Make("Shore/Beach", new[] { "outdoor" }, 0);
        var restroomBuilding = Make("Restroom Building", new[] { "restroom" }, 0);

        Connect(road, parkingLot);
        Connect(parkingLot, joggingPath, new SublocationConnection
        {
            Type = ConnectionType.Gate,
            Name = "Main Entrance",
            Tags = new[] { "entrance" },
        });
        Connect(road, joggingPath, new SublocationConnection
        {
            Type = ConnectionType.Gate,
            Name = "Side Gate",
            Tags = new[] { "covert_entry" },
        });
        Connect(joggingPath, picnicArea);
        Connect(joggingPath, playground);
        Connect(joggingPath, woodedArea);
        Connect(joggingPath, shoreLine);
        Connect(picnicArea, playground);
        Connect(picnicArea, woodedArea);
        Connect(picnicArea, shoreLine);
        Connect(playground, woodedArea);
        Connect(playground, shoreLine);
        Connect(woodedArea, shoreLine);
        Connect(picnicArea, restroomBuilding, new SublocationConnection
        {
            Type = ConnectionType.Door,
        });

        return new SublocationGraph(subs, conns);
    }
}
```

Note: "Main Entrance" and "Side Gate" are now connection tags, not sublocation nodes. Trail connections become OpenPassage (default). "Wooded Area" keeps its `covert_entry` sublocation tag.

- [ ] **Step 5: Rewrite ApartmentBuildingGenerator.cs**

Replace the full contents of `src/simulation/sublocations/ApartmentBuildingGenerator.cs`:

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

        Sublocation Make(string name, string[] tags, int? floor, bool isGenerated = true)
        {
            var sub = new Sublocation
            {
                Id = state.GenerateEntityId(),
                AddressId = address.Id,
                Name = name,
                Tags = tags,
                Floor = floor,
                IsGenerated = isGenerated
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
            Breakable = new BreakableProperty(),
        });
        Connect(lobby, elevator, new SublocationConnection
        {
            Type = ConnectionType.Door,
            Name = "Elevator Doors (Lobby)",
        });

        int floorCount = rng.Next(4, 21);
        Sublocation prevHallway = null;

        for (int n = 1; n <= floorCount; n++)
        {
            var floorPlaceholder = Make($"Floor {n}", new[] { "floor_placeholder" }, n, isGenerated: false);
            floorPlaceholder.ParentId = null;

            var hallway = Make($"Floor {n} Hallway", new[] { "hallway" }, n);

            Connect(elevator, hallway, new SublocationConnection
            {
                Type = ConnectionType.Door,
                Name = $"Elevator Doors (Floor {n})",
            });

            if (prevHallway != null)
            {
                Connect(prevHallway, hallway, new SublocationConnection
                {
                    Type = ConnectionType.Stairs,
                    Name = $"Stairs (Floor {n - 1} to {n})",
                });
            }
            else
            {
                Connect(lobby, hallway, new SublocationConnection
                {
                    Type = ConnectionType.Stairs,
                    Name = "Stairs (Lobby to Floor 1)",
                });
            }

            prevHallway = hallway;
        }

        return new SublocationGraph(subs, conns);
    }

    public static SublocationGraph ExpandFloor(Sublocation floorPlaceholder, SimulationState state, Random rng)
    {
        var subs = new Dictionary<int, Sublocation>();
        var conns = new List<SublocationConnection>();
        var address = state.Addresses[floorPlaceholder.AddressId];
        int floor = floorPlaceholder.Floor ?? 1;

        Sublocation Make(string name, string[] tags)
        {
            var sub = new Sublocation
            {
                Id = state.GenerateEntityId(),
                AddressId = address.Id,
                Name = name,
                Tags = tags,
                Floor = floor,
                ParentId = floorPlaceholder.Id
            };
            subs[sub.Id] = sub;
            address.Sublocations[sub.Id] = sub;
            return sub;
        }

        void Connect(Sublocation from, Sublocation to, SublocationConnection template = null)
        {
            var conn = template ?? new SublocationConnection { Type = ConnectionType.Door };
            conn.Id = state.GenerateEntityId();
            conn.FromSublocationId = from.Id;
            conn.ToSublocationId = to.Id;
            conns.Add(conn);
            address.Connections.Add(conn);
        }

        var hallway = Make($"Floor {floor} Hallway", new[] { "hallway" });

        int unitCount = rng.Next(4, 9);
        for (int i = 1; i <= unitCount; i++)
        {
            var bedroom = Make($"Apt {i} Bedroom", new[] { "bedroom", "private" });
            var kitchen = Make($"Apt {i} Kitchen", new[] { "kitchen", "food" });
            var living = Make($"Apt {i} Living Room", new[] { "living", "social" });
            var bathroom = Make($"Apt {i} Bathroom", new[] { "restroom" });

            Connect(hallway, living);
            Connect(living, bedroom);
            Connect(living, kitchen);
            Connect(living, bathroom);
        }

        floorPlaceholder.IsGenerated = true;

        return new SublocationGraph(subs, conns);
    }
}
```

Key changes: Single elevator node connected to all floors via Door edges. Stairwells removed — stairs are edges between consecutive floor hallways.

- [ ] **Step 6: Rewrite OfficeGenerator.cs**

Replace the full contents of `src/simulation/sublocations/OfficeGenerator.cs`:

```csharp
using System;
using System.Collections.Generic;
using Stakeout.Simulation.Entities;

namespace Stakeout.Simulation.Sublocations;

public class OfficeGenerator : ISublocationGenerator
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

        // Ground floor layout
        var road = Make("Road", new[] { "road" }, 0);
        var lobby = Make("Lobby", new[] { "entrance", "public" }, 0);
        var securityRoom = Make("Security Room", new[] { "security" }, 0);
        var elevator = Make("Elevator", new[] { "elevator" }, null);

        Connect(road, lobby, new SublocationConnection
        {
            Type = ConnectionType.Door,
            Name = "Front Door",
            Tags = new[] { "entrance" },
            Lockable = new LockableProperty { Mechanism = LockMechanism.Key },
            Breakable = new BreakableProperty(),
        });
        Connect(lobby, securityRoom, new SublocationConnection
        {
            Type = ConnectionType.Door,
        });
        Connect(lobby, elevator, new SublocationConnection
        {
            Type = ConnectionType.Door,
            Name = "Elevator Doors (Lobby)",
        });

        // Upper floors
        int floorCount = rng.Next(1, 6);
        Sublocation prevHallway = null;

        for (int floor = 1; floor <= floorCount; floor++)
        {
            var reception = Make($"Floor {floor} Reception", new[] { "public" }, floor);
            var cubicleArea = Make($"Floor {floor} Cubicle Area", new[] { "work_area" }, floor);
            var managerOffice = Make($"Floor {floor} Manager Office", new[] { "work_area", "private" }, floor);
            var breakRoom = Make($"Floor {floor} Break Room", new[] { "food", "social" }, floor);
            var restroom = Make($"Floor {floor} Restroom", new[] { "restroom" }, floor);

            Connect(elevator, reception, new SublocationConnection
            {
                Type = ConnectionType.Door,
                Name = $"Elevator Doors (Floor {floor})",
            });

            if (prevHallway != null)
            {
                Connect(prevHallway, reception, new SublocationConnection
                {
                    Type = ConnectionType.Stairs,
                    Name = $"Stairs (Floor {floor - 1} to {floor})",
                });
            }
            else
            {
                Connect(lobby, reception, new SublocationConnection
                {
                    Type = ConnectionType.Stairs,
                    Name = "Stairs (Lobby to Floor 1)",
                });
            }

            Connect(reception, cubicleArea);
            Connect(cubicleArea, managerOffice, new SublocationConnection
            {
                Type = ConnectionType.Door,
            });
            Connect(cubicleArea, breakRoom);
            Connect(reception, restroom, new SublocationConnection
            {
                Type = ConnectionType.Door,
            });

            prevHallway = reception;
        }

        return new SublocationGraph(subs, conns);
    }
}
```

- [ ] **Step 7: Update all generator test files**

For each generator test file, update the helper graphs and assertions to remove connection-point sublocation nodes and use the new connection model. The key changes in each test file:

1. Remove sublocations that were connection points (Front Door, Back Door, Stairs, etc.)
2. Update sublocation count assertions (fewer nodes)
3. Update connection count assertions if needed
4. Update any assertions that checked for connection-point sublocations by tag
5. **IMPORTANT:** `graph.FindPath()` now returns `List<PathStep>` instead of `List<Sublocation>`. Any test that calls `FindPath` must update: use `path[i].Location` to access the sublocation, and `path[i].Via` to access the edge. For example, change `Assert.Equal("Lobby", path[1].Name)` to `Assert.Equal("Lobby", path[1].Location.Name)`.
6. Tests that check `FindByTag("entrance")` on the graph will now return `null` for buildings where "entrance" is a connection tag (not a sublocation tag). Update these to use `FindConnectionByTag("entrance")` or `FindEntryPoint("entrance")` as appropriate.

Read each test file, update it to match the new generator output, and ensure assertions are correct. The test files are:
- `stakeout.tests/Simulation/Sublocations/SuburbanHomeGeneratorTests.cs`
- `stakeout.tests/Simulation/Sublocations/DinerGeneratorTests.cs`
- `stakeout.tests/Simulation/Sublocations/DiveBarGeneratorTests.cs`
- `stakeout.tests/Simulation/Sublocations/ParkGeneratorTests.cs`
- `stakeout.tests/Simulation/Sublocations/ApartmentBuildingGeneratorTests.cs`
- `stakeout.tests/Simulation/Sublocations/OfficeGeneratorTests.cs`

- [ ] **Step 8: Run tests**

Run: `dotnet test stakeout.tests/ -v minimal`

Expected: All generator tests pass. Decomposition tests may still fail (addressed in Task 5).

- [ ] **Step 9: Commit**

```
git add src/simulation/sublocations/SuburbanHomeGenerator.cs
git add src/simulation/sublocations/DinerGenerator.cs
git add src/simulation/sublocations/DiveBarGenerator.cs
git add src/simulation/sublocations/ParkGenerator.cs
git add src/simulation/sublocations/ApartmentBuildingGenerator.cs
git add src/simulation/sublocations/OfficeGenerator.cs
```

Then add all modified test files.

```
git commit -m "refactor: generators use connection edges instead of connection-point nodes

All generators updated: doors, windows, stairs, gates are now edges
with typed SublocationConnection objects. Connection-point sublocation
nodes removed. Entry-point tags moved to connections. Elevator is a
single node connected to floors via Door edges. Stairwells removed."
```

---

## Task 5: Update Decomposition Strategies

**Files:**
- Modify: `src/simulation/scheduling/decomposition/InhabitDecomposition.cs`
- Modify: `src/simulation/scheduling/decomposition/WorkDayDecomposition.cs`
- Modify: `src/simulation/scheduling/decomposition/VisitDecomposition.cs`
- Modify: `src/simulation/scheduling/decomposition/PatronizeDecomposition.cs`
- Modify: `src/simulation/scheduling/decomposition/StaffShiftDecomposition.cs`
- Modify: `src/simulation/scheduling/decomposition/IntrudeDecomposition.cs`
- Modify: All decomposition test files

**Depends on:** Tasks 1, 2, 3

Each decomposition strategy that uses `graph.FindByTag("entrance")`, `graph.FindByTag("staff_entry")`, or `graph.FindByTag("covert_entry")` to find an entry-point sublocation must switch to `graph.FindEntryPoint(tag)` which returns the interior sublocation.

- [ ] **Step 1: Update InhabitDecomposition.cs**

The key change: replace `graph.FindByTag("entrance")` with `graph.FindEntryPoint("entrance")`. The entrance is now a connection tag, so FindEntryPoint returns the interior sublocation (e.g., Hallway behind the front door).

In `src/simulation/scheduling/decomposition/InhabitDecomposition.cs`, change line 19 from:
```csharp
var entrance = graph.FindByTag("entrance");
```
to:
```csharp
var entryResult = graph.FindEntryPoint("entrance");
var entrance = entryResult?.target;
```

- [ ] **Step 2: Update WorkDayDecomposition.cs**

In `src/simulation/scheduling/decomposition/WorkDayDecomposition.cs`, change line 19 from:
```csharp
var entrance = graph.FindByTag("entrance");
```
to:
```csharp
var entryResult = graph.FindEntryPoint("entrance");
var entrance = entryResult?.target;
```

- [ ] **Step 3: Update VisitDecomposition.cs**

In `src/simulation/scheduling/decomposition/VisitDecomposition.cs`, change line 17 from:
```csharp
var entrance = graph.FindByTag("entrance");
```
to:
```csharp
var entryResult = graph.FindEntryPoint("entrance");
var entrance = entryResult?.target;
```

- [ ] **Step 4: Update PatronizeDecomposition.cs**

In `src/simulation/scheduling/decomposition/PatronizeDecomposition.cs`, change line 18 from:
```csharp
var entrance = graph.FindByTag("entrance");
```
to:
```csharp
var entryResult = graph.FindEntryPoint("entrance");
var entrance = entryResult?.target;
```

- [ ] **Step 5: Update StaffShiftDecomposition.cs**

In `src/simulation/scheduling/decomposition/StaffShiftDecomposition.cs`, change line 20 from:
```csharp
var staffEntry = graph.FindByTag("staff_entry") ?? graph.FindByTag("entrance");
```
to:
```csharp
var staffResult = graph.FindEntryPoint("staff_entry") ?? graph.FindEntryPoint("entrance");
var staffEntry = staffResult?.target;
```

- [ ] **Step 6: Update IntrudeDecomposition.cs**

In `src/simulation/scheduling/decomposition/IntrudeDecomposition.cs`, change line 16 from:
```csharp
var entryPoint = graph.FindByTag("covert_entry") ?? graph.FindByTag("entrance");
```
to:
```csharp
var covertResult = graph.FindEntryPoint("covert_entry") ?? graph.FindEntryPoint("entrance");
var entryPoint = covertResult?.target;
```

Also update `FindPath` usage to use the new PathStep-based API. Change lines 42-47 from:
```csharp
var entryPath = graph.FindPath(entryPoint.Id, targetRoom.Id);
sublocationSequence.AddRange(entryPath);
var exitPath = graph.FindPath(targetRoom.Id, entryPoint.Id);
sublocationSequence.AddRange(exitPath.Skip(1));
```
to:
```csharp
var entryPath = graph.FindPath(entryPoint.Id, targetRoom.Id);
sublocationSequence.AddRange(entryPath.Select(s => s.Location));
var exitPath = graph.FindPath(targetRoom.Id, entryPoint.Id);
sublocationSequence.AddRange(exitPath.Skip(1).Select(s => s.Location));
```

Add `using System.Linq;` at the top if not already present.

- [ ] **Step 7: Update decomposition test files**

Update all test helper graphs in decomposition tests to remove connection-point sublocation nodes. The tests need to:
1. Remove "Front Door" and similar sublocation nodes from test graph helpers
2. Put "entrance" tag on the connection between Road and the first real room
3. Update sublocation ID assertions to match the new graph structure

Test files to update:
- `stakeout.tests/Simulation/Scheduling/Decomposition/InhabitDecompositionTests.cs`
- `stakeout.tests/Simulation/Scheduling/Decomposition/IntrudeDecompositionTests.cs`
- `stakeout.tests/Simulation/Scheduling/Decomposition/WorkDayDecompositionTests.cs`

For InhabitDecompositionTests, update `CreateHomeGraph`:
```csharp
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
```

Update assertions: "entrance" now resolves to Hallway (id=3) instead of Front Door (id=2). So:
- `MorningRoutine_EndsAtEntrance`: expect `entries[^1].TargetSublocationId == 3`
- `EveningRoutine_StartsAtEntrance`: expect `entries[0].TargetSublocationId == 3`

Apply similar patterns to the other decomposition test files.

- [ ] **Step 8: Run tests**

Run: `dotnet test stakeout.tests/ -v minimal`

Expected: All decomposition tests pass.

- [ ] **Step 9: Commit**

```
git add src/simulation/scheduling/decomposition/InhabitDecomposition.cs
git add src/simulation/scheduling/decomposition/WorkDayDecomposition.cs
git add src/simulation/scheduling/decomposition/VisitDecomposition.cs
git add src/simulation/scheduling/decomposition/PatronizeDecomposition.cs
git add src/simulation/scheduling/decomposition/StaffShiftDecomposition.cs
git add src/simulation/scheduling/decomposition/IntrudeDecomposition.cs
```

Then add all modified test files.

```
git commit -m "refactor: decomposition strategies use FindEntryPoint for edge-based entries

All strategies updated to use graph.FindEntryPoint() instead of
graph.FindByTag() for entrance/staff_entry/covert_entry lookups.
Entry points now resolve to the interior sublocation behind the
connection edge."
```

---

## Task 6: Schedule Display and UI Views

**Files:**
- Modify: `scenes/game_shell/GameShell.cs` (lines 553-558, 593-601, 612-618)
- Modify: `scenes/address/GraphView.cs` (lines 90-100)
- Modify: `scenes/address/BlueprintView.cs` (lines 99-107)

**Depends on:** Tasks 1, 2, 3

- [ ] **Step 1: Add ResolveConnection helper to GameShell.cs**

After the `ResolveSublocationName` method (around line 618), add:

```csharp
private SublocationConnection ResolveConnection(int? connectionId, int? addressId, SimulationState state)
{
    if (!connectionId.HasValue || !addressId.HasValue) return null;
    if (!state.Addresses.TryGetValue(addressId.Value, out var addr)) return null;
    return addr.Connections.FirstOrDefault(c => c.Id == connectionId.Value);
}
```

Add `using System.Linq;` at the top of GameShell.cs if not already present.

- [ ] **Step 2: Update FormatScheduleEntry in GameShell.cs**

Change the `FormatScheduleEntry` method (lines 593-601) to include "via" information:

```csharp
private string FormatScheduleEntry(ScheduleEntry e, SimulationState state)
{
    var text = $"[{e.StartTime:hh\\:mm}-{e.EndTime:hh\\:mm}] {e.Action}";
    text += FormatAddressString(e.TargetAddressId, e.FromAddressId, state);
    var sublocationName = ResolveSublocationName(e.TargetSublocationId, e.TargetAddressId, state);
    if (sublocationName != null)
    {
        text += $" → {sublocationName}";
        var conn = ResolveConnection(e.ViaConnectionId, e.TargetAddressId, state);
        if (conn?.Name != null && conn.Type != ConnectionType.OpenPassage)
            text += $" (via {conn.Name})";
    }
    return text;
}
```

Add `using Stakeout.Simulation.Entities;` at the top if not already present.

- [ ] **Step 3: Update child schedule display in AddScheduleTree**

In `AddScheduleTree` (around line 555-558), update the child display to also show "via":

Change:
```csharp
var sublocationName = ResolveSublocationName(child.TargetSublocationId, child.TargetAddressId, state);
var childText = $"    [{child.StartTime:hh\\:mm}-{child.EndTime:hh\\:mm}] {child.Action}";
if (sublocationName != null)
    childText += $" → {sublocationName}";
```

To:
```csharp
var sublocationName = ResolveSublocationName(child.TargetSublocationId, child.TargetAddressId, state);
var childText = $"    [{child.StartTime:hh\\:mm}-{child.EndTime:hh\\:mm}] {child.Action}";
if (sublocationName != null)
{
    childText += $" → {sublocationName}";
    var conn = ResolveConnection(child.ViaConnectionId, child.TargetAddressId, state);
    if (conn?.Name != null && conn.Type != ConnectionType.OpenPassage)
        childText += $" (via {conn.Name})";
}
```

- [ ] **Step 4: Update GraphView.cs to show edge labels**

In `scenes/address/GraphView.cs`, update the `_Draw` method to add connection type labels on edges. Change the connection drawing loop (lines 93-100) to:

```csharp
foreach (var conn in _connections)
{
    if (_nodePositions.TryGetValue(conn.FromSublocationId, out var from) &&
        _nodePositions.TryGetValue(conn.ToSublocationId, out var to))
    {
        DrawLine(from, to, Colors.Gray, 2);

        // Label non-OpenPassage connections
        if (conn.Type != ConnectionType.OpenPassage)
        {
            var mid = (from + to) / 2;
            var label = conn.Name ?? conn.Type.ToString();
            var labelSize = font.GetStringSize(label, HorizontalAlignment.Center, -1, 9);
            DrawString(font, new Vector2(mid.X - labelSize.X / 2, mid.Y - 2), label,
                HorizontalAlignment.Left, -1, 9, new Color(0.7f, 0.7f, 0.5f));
        }
    }
}
```

Note: `font` and `fontSize` are declared on line 103-104. Move the `var font = ThemeDB.FallbackFont;` line before the connection drawing loop, or declare it at the top of `_Draw`.

- [ ] **Step 5: Update BlueprintView.cs to show connection labels**

In `scenes/address/BlueprintView.cs`, update the connection drawing loop (lines 99-107) to:

```csharp
foreach (var conn in _connections)
{
    if (_roomRects.TryGetValue(conn.FromSublocationId, out var fromRect) &&
        _roomRects.TryGetValue(conn.ToSublocationId, out var toRect))
    {
        var from = fromRect.GetCenter();
        var to = toRect.GetCenter();
        DrawLine(from, to, new Color(0.5f, 0.5f, 0.5f, 0.5f), 1);

        if (conn.Type != ConnectionType.OpenPassage && conn.Name != null)
        {
            var font = ThemeDB.FallbackFont;
            var mid = (from + to) / 2;
            DrawString(font, new Vector2(mid.X, mid.Y - 2), conn.Name,
                HorizontalAlignment.Left, -1, 8, new Color(0.6f, 0.6f, 0.4f, 0.7f));
        }
    }
}
```

- [ ] **Step 6: Run tests**

Run: `dotnet test stakeout.tests/ -v minimal`

Expected: All tests pass. UI changes are visual-only and don't affect unit tests.

- [ ] **Step 7: Commit**

```
git add scenes/game_shell/GameShell.cs
git add scenes/address/GraphView.cs
git add scenes/address/BlueprintView.cs
git commit -m "feat: schedule display shows 'via' connections, graph views label edges

Debug inspector shows 'via Front Door' for non-OpenPassage connections.
GraphView draws connection type labels on edges. BlueprintView shows
named connection labels on edge lines."
```

---

## Task 7: Fix Remaining Test Failures and Final Verification

**Files:**
- Any remaining test files with failures

**Depends on:** Tasks 1-6

- [ ] **Step 1: Run full test suite**

Run: `dotnet test stakeout.tests/ -v minimal`

- [ ] **Step 2: Fix any remaining test failures**

Common remaining failures will be:
- Tests that create `SublocationConnection` without an `Id` (add `Id = N`)
- Tests that expect `FindPath` to return `List<Sublocation>` (it now returns `List<PathStep>`)
- Tests that reference removed sublocation nodes in their helper graphs
- Tests that reference `ConnectionType.Elevator` or `ConnectionType.Trail`

For each failure, read the test, understand what it's testing, and update it to match the new model. The fix pattern is always the same: remove connection-point nodes from test graphs, add entry-point tags to connections, update ID assertions.

- [ ] **Step 3: Run full test suite again**

Run: `dotnet test stakeout.tests/ -v minimal`

Expected: ALL tests pass.

- [ ] **Step 4: Commit**

```
git add -A
git commit -m "fix: resolve remaining test failures from connection-edges refactor"
```

---

## Task 8: Integration Smoke Test

**Depends on:** Tasks 1-7

- [ ] **Step 1: Run the full test suite one final time**

Run: `dotnet test stakeout.tests/ -v minimal`

Expected: ALL tests pass with 0 failures.

- [ ] **Step 2: Verify build succeeds**

Run: `dotnet build stakeout.sln`

Expected: Build succeeds with 0 errors.

- [ ] **Step 3: Final commit if any loose ends**

If any files were missed, stage and commit them.

---

## Dependency Graph

```
Task 1 (Data Model) ─┬─→ Task 2 (Graph/Pathfinding)
                      ├─→ Task 3 (ScheduleEntry)
                      └─→ Task 4 (Generators)
                                    │
Task 2 + Task 3 ──────→ Task 5 (Decomposition Strategies)
                                    │
Task 1 + Task 2 + Task 3 → Task 6 (UI/Display)
                                    │
Tasks 1-6 ──────────────→ Task 7 (Fix Remaining Tests)
                                    │
Task 7 ─────────────────→ Task 8 (Integration Smoke Test)
```

Tasks 2, 3, and 4 can run in parallel after Task 1.
Task 5 depends on Tasks 2 and 3.
Task 6 depends on Tasks 1, 2, and 3.
Tasks 7 and 8 are sequential cleanup.
