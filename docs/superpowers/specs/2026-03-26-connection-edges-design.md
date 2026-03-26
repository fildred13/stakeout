# Connection Points as Graph Edges

## Problem

Connection points (doors, windows, stairs, gates) are currently modeled as sublocation nodes in the location graph. This means a person is scheduled to physically occupy "Front Door" for several minutes, which is nonsensical. It also clutters schedules and inflates the graph with non-locations.

Example of current model: `Road -> FrontDoor -> EntranceHall`

The fix: connection points become edges of the graph, not nodes. Traversal through them is instantaneous. The schedule display uses "via" to indicate which edge was used.

Target model: `Road --[Front Door]--> EntranceHall`

## Decisions

### What becomes an edge vs stays a node

**Edges (connection types):** Door, Window, Stairs, Gate, OpenPassage, HiddenPath

**Stays as nodes:**
- Trail — an actual location where events can happen (hiking trail)
- Elevator — a room you wait in, connected to other floors via Door edges
- All real rooms (yards, hallways, kitchens, bedrooms, etc.)

**Rule of thumb:** if a person can meaningfully *be there doing something*, it's a node. If it's purely a traversal mechanism, it's an edge.

### Traversal is instantaneous

No travel time on edges. A person moves from one node to the next with zero delay. The time previously spent "in" a door was artificial and the simulation doesn't need sub-minute fidelity for traversal.

### Connection state model: nullable property composition

Connections use nullable property objects for composable capabilities. Each property class encapsulates one concern (locking, concealment, transparency, breakability). A connection only carries the properties relevant to it — an OpenPassage has none, a window has Lockable + Transparent + Breakable.

This was chosen over:
- Flat fields (too many unused fields per connection, no structure)
- Separate definition/state objects (unnecessary given replay strategy reconstructs from action history)
- Interface/subclass composition (C# lacks mixins, leading to duplicated implementations across classes)

Factories may be extracted later when generators show repetition, but are not built upfront.

### Connection state is fully reactive

Connection state (locked, broken, discovered) changes in response to simulation events — a person locks a door behind them, an intruder breaks a window, the player picks a lock. This is the full reactive model, not static-per-generation or schedule-driven.

## Data Model

### ConnectionType enum

```csharp
public enum ConnectionType
{
    OpenPassage,    // default, unlabeled in views
    Door,
    Window,
    Stairs,
    Gate,
    HiddenPath      // always paired with ConcealableProperty
}
```

Removed from current enum:
- Elevator — now a sublocation node
- Trail — now a sublocation node

### Composable modifier properties

```csharp
public class LockableProperty
{
    public LockMechanism Mechanism { get; set; }  // Key, Combination, Keypad, Electronic
    public bool IsLocked { get; set; }
    public int? KeyItemId { get; set; }
}

public class ConcealableProperty
{
    public ConcealmentMethod Method { get; set; }  // Rug, Bookshelf, Leaves, FalseWall, Bushes, etc.
    public bool IsDiscovered { get; set; }
    public float DiscoveryDifficulty { get; set; } // 0.0-1.0
}

public class TransparentProperty
{
    public bool CanSeeThrough { get; set; }
    public bool CanShootThrough { get; set; }
    public bool CanHearThrough { get; set; }
}

public class BreakableProperty
{
    public float Durability { get; set; }          // 0.0 = destroyed, 1.0 = pristine
    public bool IsBroken { get; set; }
}
```

### SublocationConnection (revised)

```csharp
public class SublocationConnection
{
    public int Id { get; set; }
    public int FromSublocationId { get; set; }
    public int ToSublocationId { get; set; }
    public ConnectionType Type { get; set; } = ConnectionType.OpenPassage;
    public string? Name { get; set; }              // "Front Door", "Kitchen Window", null for unnamed
    public string[] Tags { get; set; } = Array.Empty<string>();  // "entrance", "covert_entry", "staff_entry"

    // Composable modifiers — null means the connection doesn't have this capability
    public LockableProperty? Lockable { get; set; }
    public ConcealableProperty? Concealable { get; set; }
    public TransparentProperty? Transparent { get; set; }
    public BreakableProperty? Breakable { get; set; }
}
```

Key changes from current model:
- `Id` — connections get an ID so game events and schedule entries can reference them
- `Name` — display name for the connection ("Front Door"), since this is no longer a sublocation name
- `Tags` — entry-point semantics move here from sublocation tags (the *door* is the entrance, not the room behind it)
- `IsBidirectional` removed — all connections are bidirectional for now (add back if one-way connections become needed)

### ScheduleEntry changes

```csharp
public class ScheduleEntry
{
    public ActionType Action { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public int? TargetAddressId { get; set; }
    public int? FromAddressId { get; set; }
    public int? TargetSublocationId { get; set; }
    public int? ViaConnectionId { get; set; }      // NEW — which edge was used to enter this sublocation
}
```

## Pathfinding

### FindPath returns nodes + edges

```csharp
public class PathStep
{
    public Sublocation Location { get; set; }
    public SublocationConnection? Via { get; set; }  // null for the starting node
}

public class TraversalContext
{
    public Person Traveler { get; set; }
    // Extensible: person's keys, skills, knowledge of hidden passages, etc.
}

// New signature
public List<PathStep> FindPath(int fromId, int toId, TraversalContext context)
```

Example result for an intruder entering through a concealed basement window:

```
[0] Road           (via: null)
[1] Back Yard      (via: Gate connection)
[2] Basement       (via: "Basement Window" connection)
[3] Bedroom        (via: OpenPassage connection)
```

### Edge filtering in BFS

BFS skips edges the traveler can't use:

```csharp
bool CanTraverse(SublocationConnection conn, TraversalContext ctx)
{
    if (conn.Lockable?.IsLocked == true && !ctx.HasKeyFor(conn))
        return false;
    if (conn.Concealable != null && !conn.Concealable.IsDiscovered && !ctx.KnowsAbout(conn))
        return false;
    return true;
}
```

Different people traverse differently — a resident has keys and knows about hidden passages, an intruder might pick locks or break windows. The pathfinding delegates to TraversalContext.

### Performance

Fewer nodes (connection points removed) means BFS is cheaper. The per-edge CanTraverse check is a few null checks — negligible.

### New SublocationGraph query methods

```csharp
// Find a connection by tag, return the connection + the interior node it leads to
public (SublocationConnection conn, Sublocation target)? FindConnectionByTag(string tag)

// Get the connection between two adjacent nodes (for "via" display)
public SublocationConnection? GetConnectionBetween(int fromId, int toId)
```

## Schedule Display

### Debug inspector format

**Before:**
```
▼ [09:00-17:00] Work @ 123 Main St (House)
    [09:00-09:05] Work → Front Door
    [09:05-12:00] Work → Kitchen
```

**After:**
```
▼ [09:00-17:00] Work @ 123 Main St (House)
    [09:00-12:00] Work → Kitchen (via Front Door)
    [12:00-12:30] Eat → Break Room
```

Rules:
- "via" is shown only for non-OpenPassage connections that have a name
- OpenPassage connections are unlabeled (they're the default)
- No schedule entries target connection points (they don't exist as sublocations anymore)

### Formatting logic

```csharp
var sublocationName = ResolveSublocationName(e.TargetSublocationId, e.TargetAddressId, state);
if (sublocationName != null)
{
    text += $" -> {sublocationName}";
    var conn = ResolveConnection(e.ViaConnectionId, e.TargetAddressId, state);
    if (conn?.Name != null && conn.Type != ConnectionType.OpenPassage)
        text += $" (via {conn.Name})";
}
```

## Generator Changes

All generators stop creating sublocation nodes for connection points and wire rooms directly with typed connections. Entry-point tags move from sublocation tags to connection tags.

### SuburbanHomeGenerator example

**Before:**
```csharp
var road = Make("Road", new[] { "road" }, 0);
var frontYard = Make("Front Yard", new[] { "yard", "front" }, 0);
var frontDoor = Make("Front Door", new[] { "entrance" }, 0);
var hallway = Make("Hallway", new[] { "hallway" }, 0);
var stairs = Make("Stairs", new[] { "stairs" }, 0);
var upstairsHallway = Make("Upstairs Hallway", new[] { "hallway" }, 1);

Connect(road, frontYard);
Connect(frontYard, frontDoor, ConnectionType.Door);
Connect(frontDoor, hallway);
Connect(hallway, stairs);
Connect(stairs, upstairsHallway, ConnectionType.Stairs);
```

**After:**
```csharp
var road = Make("Road", new[] { "road" }, 0);
var frontYard = Make("Front Yard", new[] { "yard", "front" }, 0);
var hallway = Make("Hallway", new[] { "hallway" }, 0);
var upstairsHallway = Make("Upstairs Hallway", new[] { "hallway" }, 1);

Connect(road, frontYard);  // OpenPassage by default
Connect(frontYard, hallway, new SublocationConnection
{
    Type = ConnectionType.Door,
    Name = "Front Door",
    Tags = new[] { "entrance" },
    Lockable = new LockableProperty { Mechanism = LockMechanism.Key },
    Breakable = new BreakableProperty(),
});
Connect(hallway, upstairsHallway, new SublocationConnection
{
    Type = ConnectionType.Stairs,
    Name = "Stairs",
});
```

### Decomposition strategy changes

Strategies that query entry-point sublocations by tag switch to querying connections by tag:

- `graph.FindByTag("entrance")` becomes `graph.FindConnectionByTag("entrance")` which returns the connection and its adjacent real sublocation
- `graph.FindByTag("covert_entry")` same pattern
- IntrudeDecomposition paths are shorter since door/stairs nodes are gone
- All path results are real locations; "via" info comes from PathStep.Via

## Elevator Handling

Elevators are sublocation nodes (rooms you wait in), connected to floors via Door edges:

```csharp
var elevator = Make("Elevator", new[] { "elevator" }, null);  // floor=null, spans floors

Connect(lobby, elevator, new SublocationConnection
{
    Type = ConnectionType.Door,
    Name = "Elevator Doors (Ground)",
    Lockable = new LockableProperty { Mechanism = LockMechanism.Electronic },
});
Connect(elevator, secondFloorHallway, new SublocationConnection
{
    Type = ConnectionType.Door,
    Name = "Elevator Doors (2nd Floor)",
});
```

Elevator doors can be locked (keycard access to certain floors), handled naturally by CanTraverse in pathfinding. A person's schedule shows them entering and waiting in the elevator as a real activity.

## Files Affected

| Area | Files |
|------|-------|
| Data model | `Sublocation.cs`, `SublocationConnection.cs` (new property classes), `Address.cs` |
| Graph | `SublocationGraph.cs` (new PathStep, TraversalContext, FindConnectionByTag, edge-aware BFS) |
| Schedule | `DailySchedule.cs` (ViaConnectionId on ScheduleEntry) |
| Generators | `SuburbanHomeGenerator.cs`, `DinerGenerator.cs`, `DiveBarGenerator.cs`, `ParkGenerator.cs`, `LocationGenerator.cs` |
| Decomposition | `TaskResolver.cs`, `IntrudeDecomposition.cs`, `WorkDayDecomposition.cs`, `VisitDecomposition.cs`, `PatronizeDecomposition.cs`, `StaffShiftDecomposition.cs`, `InhabitDecomposition.cs` |
| Display | `GameShell.cs` (schedule formatting), `GraphView.cs` (edge labels), `BlueprintView.cs` (connection labels) |
