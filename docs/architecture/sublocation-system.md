# Sublocation System

## Purpose
Models the interior layout of each Address as a graph of rooms (sublocation nodes) connected by typed edges (doors, windows, stairs, gates). Provides pathfinding, entry-point discovery, and graph generation for each location type.

## Key Files
| File | Role |
|------|------|
| `src/simulation/entities/Sublocation.cs` | Sublocation node, SublocationConnection edge, ConnectionType enum |
| `src/simulation/entities/ConnectionProperties.cs` | Composable edge modifiers: LockableProperty, ConcealableProperty, TransparentProperty, BreakableProperty, FingerprintSurface (also used on Item) |
| `src/simulation/entities/SublocationGraph.cs` | Graph storage, BFS pathfinding, tag-based queries (FindByTag, FindEntryPoint, FindConnectionByTag) |
| `src/simulation/entities/PathStep.cs` | Pathfinding result: (Location, Via connection) pairs |
| `src/simulation/entities/TraversalContext.cs` | Per-person edge filtering during BFS (locked doors, hidden passages) |
| `src/simulation/sublocations/*Generator.cs` | Six generators: SuburbanHome, Diner, DiveBar, Park, ApartmentBuilding, Office |

## How It Works
Each Address holds a dictionary of Sublocations and a list of SublocationConnections. When a location is generated, a generator creates sublocation nodes (rooms, yards, hallways) and wires them with typed connections. Connections are graph edges, not nodes -- a person is never "at" a door, they pass through it instantly.

Connections carry composable nullable property objects (Lockable, Breakable, etc.) that affect pathfinding and gameplay. BFS in `FindPath` checks `TraversalContext.CanTraverse()` per edge -- locked doors block non-keyholders, undiscovered hidden passages block unaware travelers. The visited set only marks nodes after both visit-check and traversal-check pass, so a locked alternate route doesn't permanently block a node reachable via another path.

Entry-point discovery (`FindEntryPoint`) searches connection tags first, then sublocation tags. This handles both "the door is the entrance" (tag on connection) and "the alley is a covert area" (tag on sublocation node).

## Key Decisions
- **Connections are edges, not nodes:** Doors/stairs/windows are traversal mechanisms, not places. Removes artificial schedule entries like "5 min at Front Door."
- **Nullable property composition:** Each connection carries only relevant modifiers (a window has Breakable + Transparent, an open passage has none). Chosen over flat fields, subclasses, or interfaces because C# lacks mixins.
- **Bidirectional by default:** All connections create a synthetic reverse edge in the graph. One-way connections can be added later if needed.
- **Elevator is a node:** Elevators are rooms you wait in, connected to floors via Door edges. Stairs are edges between hallways.

## Connection Points
- **Scheduling decomposition** uses `FindEntryPoint`/`FindByTag` to place people in rooms; `ViaConnectionId` on ScheduleEntry tracks which edge was used
- **Generators** are called by LocationGenerator during world setup; ApartmentBuildingGenerator supports deferred floor expansion via `ExpandFloor()`
- **Display** (GameShell, GraphView, BlueprintView) reads graph structure and connection names/types for labels and "via" annotations
