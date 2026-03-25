# Sublocations & Recursive Task System Design

## Summary

Add a hierarchical sublocation model (rooms/zones within addresses) and refactor the task system to use recursive decomposition. NPCs move through sublocations as part of their daily routines via a precomputed schedule at sublocation granularity. Two visualization prototypes (graph view and blueprint view) let the player enter locations and see the layout. A load test harness validates simulation performance and becomes a standard development SOP.

## Goals

- Model rooms, zones, and connections within every address type
- NPCs move through sublocations realistically, producing traces as natural byproducts of movement
- Unify the task resolution model so the same logic works at city scale (which address?) and address scale (which room?)
- Provide two visualization prototypes for location interiors: graph view and blueprint view
- Person Inspector shows sublocation-level NPC positions
- Establish a load testing baseline for simulation performance

## Non-Goals (deferred to future features)

- Connection access rules, lock state, keys
- Text-based location interaction
- Player click interaction with rooms (search, interview, etc.)
- Trace generation wiring (data model supports it; next feature connects it)
- NPC-to-NPC intentional interactions (phone calls, coordinated actions)
- Realistic architectural floor plan layouts

## Design

### 1. Sublocation Data Model

**Sublocation entity:**

```csharp
public class Sublocation
{
    public int Id { get; set; }
    public int AddressId { get; set; }
    public int? ParentId { get; set; }       // for lazy-generated children (floor → rooms)
    public string Name { get; set; }          // "Lobby", "Bedroom", "Parking Lot"
    public string[] Tags { get; set; }        // ["entrance", "public"], ["work_area"]
    public int? Floor { get; set; }           // null for outdoor/single-floor
    public bool IsGenerated { get; set; }     // false = placeholder for lazy generation
}
```

**Connection entity:**

```csharp
public class SublocationConnection
{
    public int FromSublocationId { get; set; }
    public int ToSublocationId { get; set; }
    public ConnectionType Type { get; set; }  // Door, Window, Elevator, Stairs, OpenPassage, Gate, HiddenPath, Trail
    public bool IsBidirectional { get; set; } // most true; one-way for drop-downs etc.
}
```

**Road node:** Each address gets a special "Road" sublocation tagged `[road]` as the entry point from the outside world. All paths into a location start from Road.

**Storage:** `Dictionary<int, Sublocation>` and `List<SublocationConnection>` added to SimulationState.

**Lazy generation:** ApartmentBuilding floors are initially generated as single `IsGenerated = false` placeholder nodes. When an NPC or the player first needs to enter that floor, the generator expands it into individual rooms using the floor template. Other address types generate fully upfront since they're small.

**Tag vocabulary (initial set):** `entrance`, `staff_entry`, `covert_entry`, `work_area`, `food`, `restroom`, `bedroom`, `kitchen`, `living`, `social`, `service_area`, `storage`, `road`, `public`, `private`, `phone`

### 2. Recursive Task System

**Approach:** Recursive decomposition at schedule build time, flattened to a linear schedule for O(1) per-tick execution.

**SimTask changes:**

```csharp
public class SimTask
{
    // Existing fields
    public int Id { get; set; }
    public int ObjectiveId { get; set; }
    public int StepIndex { get; set; }
    public ActionType ActionType { get; set; }
    public int Priority { get; set; }
    public TimeSpan WindowStart { get; set; }
    public TimeSpan WindowEnd { get; set; }
    public int? TargetAddressId { get; set; }
    public Dictionary<string, object> ActionData { get; set; }

    // New fields
    public int? ParentTaskId { get; set; }
    public int? TargetSublocationId { get; set; }   // specific room target
    public string[] TargetTags { get; set; }          // tag-based target (find nearest [food])
    public List<SimTask> SubTasks { get; set; }       // child tasks (tree structure)
    public TaskStatus Status { get; set; }            // Pending, Active, Completed, Preempted
}
```

**Pipeline:**

1. **Objectives → root SimTasks** — ObjectiveResolver stays mostly unchanged. Produces address-level tasks as today.
2. **Root SimTasks → task tree** — TaskResolver (new) decomposes each root task into sublocation-level sub-tasks using decomposition strategies and pathfinding.
3. **Task tree → flat schedule** — the tree is flattened into a linear `List<ScheduleEntry>` (extended with `TargetSublocationId`). This is the runtime execution structure.
4. **Per-tick execution** — identical to today. Compare current time to next entry, transition if needed. O(1).
5. **Interrupts** — inject a high-priority task, rebuild from the current point forward, re-flatten. Most NPCs on most days are never interrupted.

The task tree is optionally retained on Person for the debug inspector but is not used for runtime execution.

**Targeting modes:**

| Mode | When used | Example |
|---|---|---|
| Fixed ID | Task has a specific target | Go home, go to work, kill victim at their house |
| Tag search | Task needs any matching location | Find somewhere to eat, find a restroom |

This distinction applies at both address level and sublocation level.

**Person entity changes:**

- Add `CurrentSublocationId` (nullable int)
- `DailySchedule` entries gain `TargetSublocationId`
- Task tree optionally retained for debugging

### 3. TaskResolver

Takes a root SimTask and decomposes it into sub-tasks:

- Dispatches to a decomposition strategy based on ActionType
- Strategy produces abstract sub-tasks with tag-based targets
- Pathfinder resolves concrete routes between rooms (BFS on sublocation graph)
- Movement sub-tasks are inserted for each room traversed
- Output: a task tree, then flattened to schedule entries

### 4. Decomposition Strategies

Each strategy is a separate class defining the pattern of behavior within a location:

- **WorkDayDecomposition** — Enter via [entrance] → go to [work_area] → periodic [food] (every 1-3hrs, 50%) and [restroom] (every 2-4hrs, 70%) → exit via [entrance]. Used for `ActionType.Work`.
- **InhabitDecomposition** — Time at home. Morning: [bedroom] → [restroom] → [kitchen] → [entrance]. Evening: [entrance] → [kitchen] → [living] → [restroom] → [bedroom]. Used for Idle/Sleep at home.
- **PatronizeDecomposition** — Enter [entrance] → [service_area] or [social] → optionally [restroom] → exit [entrance]. For customers at diners, bars, etc.
- **StaffShiftDecomposition** — Enter [staff_entry] → [work_area] → periodic [food]/[restroom] → exit [staff_entry]. For employees using staff entrances.
- **IntrudeDecomposition** — Enter [covert_entry] → find room containing target person → pathfind → execute action → exit. For KillPerson and future break-ins.
- **VisitDecomposition** — Generic fallback. Enter [entrance] → go to specific or tagged room → do thing → exit.

**Strategy selection:** ActionType maps to a default strategy. Tasks can override via ActionData (e.g., a bartender's Work task specifies StaffShiftDecomposition).

**Graceful degradation:** If tag search finds no matching room, that sub-task is skipped. NPC stays in current room.

### 5. Sublocation Generation

One generator class per AddressType:

- **SuburbanHomeGenerator** — Road → Front Yard → Front Door → Hallway → Kitchen, Living Room, Bathroom (ground). Stairs → Bedrooms, Bathroom (second floor). Back Door → Backyard. Windows as covert_entry. Varies by RNG: 2-3 bedrooms, optional garage, optional basement.
- **OfficeGenerator** — Road → Lobby → Elevator/Stairwell → per-floor: Reception, Cubicle Area, Offices, Break Room, Restrooms. 1-5 floors.
- **DinerGenerator** — Road → Front Door → Dining Area, Counter. Back Door → Kitchen, Storage, Manager Office. Restrooms off dining area.
- **DiveBarGenerator** — Road → Front Door → Bar Area. Back Door → Alley. Storage, Manager Office off back hallway. Restrooms off bar area.
- **ApartmentBuildingGenerator** — Road → Lobby → Elevator/Stairwell → Floor placeholders (lazy). Floor template on demand: Hallway → individual units with simplified home layouts.
- **ParkGenerator** — Road → Parking Lot, Main Entrance, Side Gate. Inner zones: Jogging Path, Picnic Area, Playground, Wooded Area, Shore/Beach. Restrooms with door. Connections mostly OpenPassage.

New AddressTypes `ApartmentBuilding` and `Park` added to the AddressType enum.

Generators use seeded RNG for variety. Tags assigned during generation. Called by LocationGenerator after creating an Address.

### 6. Pathfinding

BFS or Dijkstra on the sublocation connection graph. Used during task decomposition (build time), not per-tick.

- Input: source sublocation, target sublocation (or target tag)
- Output: ordered list of sublocations to traverse
- Each traversal step becomes a Move sub-task in the flattened schedule

### 7. Visualization

**Graph View** — node-and-edge diagram of the sublocation graph. Nodes are labeled rectangles colored by tag category. Edges show connection type. Floor grouping as bordered regions. Player's current sublocation highlighted. NPCs shown as dots in their current room.

**Blueprint View** — overhead floor plan. Rooms as rectangles, simple auto-layout algorithm (entrance rooms near bottom, connected rooms adjacent, floors separated vertically). Same coloring and NPC dots. Floor switching via buttons.

Both views accessible from AddressView when the player enters a location. Toggle between them. No click interaction in this pass.

**Person Inspector update** — shows sublocation: "Location: 456 Fantasy Lane → Cubicle Area (Floor 3)".

**Task tree inspector** (debug) — optional addition to Person Inspector showing the flattened schedule with sublocation entries. Renders as a list for debugging decomposition.

### 8. Load Testing

Separate benchmark project (`stakeout.benchmarks`), not run as part of normal unit tests. Must be explicitly invoked.

- Generates a city with configurable NPC counts (default tiers: 50, 200, 500, 1000)
- Each NPC has a full day schedule decomposed to sublocation granularity
- Runs simulation loop for configurable in-game hours (no rendering)
- Measures: average ms per tick, worst-case ms per tick, ticks per second, memory usage
- Outputs results as a table to console
- Repeatable: same command, same format, comparable numbers across features

## Key Architectural Decisions

- **Recursive decomposition, flat execution** — task tree is an intermediate representation at build time. Runtime uses a flat schedule for O(1) per-tick cost. Tree optionally retained for debugging.
- **Same model for indoor and outdoor** — parks use the same sublocation graph as offices. Connection types (OpenPassage vs Door) capture the difference.
- **Tag-based room finding** — decomposition strategies specify needs via tags, not room names. Strategies are reusable across address types. ~6 strategies cover all current behaviors.
- **Lazy generation for apartments** — floor placeholders expand on demand. Keeps the graph small for large buildings.
- **Approach 3 (Recursive Tasks) over Approach 2 (Layered)** — chosen because the game's design requires cross-scale interactions (interrupts, phone calls, emergent reactions). A layered approach would create a boundary that needs constant drilling-through. Recursive tasks handle preemption uniformly via priority at any scale.

## Files Affected

**Modified:**
- `SimTask` — new fields (ParentTaskId, TargetSublocationId, TargetTags, SubTasks, Status)
- `Person` — add CurrentSublocationId, retain task tree for debug
- `AddressType` — add ApartmentBuilding, Park
- `ScheduleEntry` — add TargetSublocationId
- `ScheduleBuilder` — reworked to produce sublocation-aware schedule from task tree
- `PersonBehavior` — slimmed to orchestration, delegates to TaskExecutor
- `SimulationState` — add Sublocation and SublocationConnection storage
- `LocationGenerator` — call sublocation generators after address creation
- `SimulationManager` — wire up sublocation state
- `AddressView` — add graph/blueprint view toggle
- `Person Inspector` — show sublocation, optional task tree

**New:**
- `Sublocation` entity
- `SublocationConnection` entity
- `ConnectionType` enum
- `TaskResolver` — decomposes tasks using strategies + pathfinder
- `TaskExecutor` — walks flattened schedule (replaces PersonBehavior core loop)
- `Pathfinder` — BFS on sublocation graph
- Decomposition strategies: WorkDay, Inhabit, Patronize, StaffShift, Intrude, Visit
- Sublocation generators: SuburbanHome, Office, Diner, DiveBar, ApartmentBuilding, Park
- Graph view scene/script
- Blueprint view scene/script
- `stakeout.benchmarks` project
