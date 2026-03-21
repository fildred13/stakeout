# Plan: Simulation Basics

## Language Choice: C# All the Way

The project is already configured for C# with .NET (project.godot has `[dotnet]` config, MainMenu is C#). This is the right call for the simulation layer and I'd recommend staying with it throughout. Here's why:

- **GDScript is wrong for this.** GDScript is dynamically typed, has no proper data structures beyond Dictionary/Array, lacks generics, and has poor tooling for large codebases. For a simulation with thousands of entities, relationships, and query patterns, you'd hit a wall fast. Refactoring becomes dangerous without static types, and performance for tight loops over entity collections is measurably worse.
- **C/C++ is overkill right now.** You *could* write a GDExtension in C++ for the simulation, and if we hit performance walls with thousands of entities later, that's an option. But C# with .NET 8 is remarkably fast — struct-based entity storage, LINQ for queries, and the JIT compiler mean we're unlikely to need native code for the scale described (low thousands of entities). The complexity cost of a C++ GDExtension (build toolchain, marshaling, debugging) isn't justified at this stage.
- **C# gives us the best balance**: static typing, generics, records, pattern matching, excellent collections (Dictionary, HashSet, SortedList), easy serialization, and seamless Godot integration. If we ever need C++ for a hot path, we can extract just that piece later.

## Data Architecture: Entity-Component-Inspired Approach

The simulation description calls for progressive growth — entities are created on-demand, with varying levels of detail. Rather than a full ECS framework (overkill), I recommend a **centralized registry with typed entity collections**:

```
SimulationState (the "world")
├── GameClock (current DateTime, tick management)
├── EntityRegistry
│   ├── People: Dictionary<int, Person>
│   ├── (future: Locations, Organizations, Items, Events...)
│   └── NextEntityId: int (monotonic ID generator)
└── (future: RelationshipGraph, EventLog, etc.)
```

**Key design decisions:**

1. **Plain C# objects, not Nodes.** Simulation entities are *data*, not scene tree objects. A `Person` is a C# class stored in a Dictionary, not a Godot Node. This keeps the simulation decoupled from rendering and makes it easy to serialize/save, query efficiently, and test independently.

2. **Centralized state object.** One `SimulationState` holds everything. This makes save/load straightforward (serialize one object), debugging easy (inspect one object), and avoids scattered global state.

3. **Integer IDs for references.** Entities reference each other by ID, not object reference. This makes serialization trivial and avoids circular reference issues. Example: `Person.FriendIds: List<int>` rather than `Person.Friends: List<Person>`.

4. **Separation of data and behavior.** Entity classes hold data. "Systems" (manager classes) hold behavior — e.g., `PersonGenerator` creates people, future `MovementSystem` handles pathfinding. This keeps entity classes clean and makes it easy to add new behaviors without bloating data classes.

5. **Scalability path.** Dictionary<int, T> gives O(1) lookup by ID. When we need spatial queries ("who is near this location?"), we add a spatial index alongside. When we need relationship queries ("who knows this person?"), we add a graph structure alongside. The core entity storage stays simple.

## What We're Building

For this first iteration, minimal scope — just enough to prove the pipeline works:

### New Files

| File | Purpose |
|------|---------|
| `src/simulation/SimulationState.cs` | Central world state container |
| `src/simulation/GameClock.cs` | In-game DateTime tracking, tick logic |
| `src/simulation/entities/Person.cs` | Person data class |
| `src/simulation/PersonGenerator.cs` | Generates Person entities with random names |
| `src/simulation/SimulationManager.cs` | Godot Node that owns and ticks the simulation |
| `src/simulation/data/NameData.cs` | Static pools of first/last names |
| `scenes/simulation_debug/SimulationDebug.tscn` | Debug UI scene |
| `scenes/simulation_debug/SimulationDebug.cs` | Debug UI script |

### Modified Files

| File | Change |
|------|--------|
| `scenes/main_menu/MainMenu.cs` | "New Career" button loads SimulationDebug scene |

## Implementation Steps

### Step 1: Core simulation data structures

Create `src/simulation/` folder and implement:

**`GameClock.cs`** — Tracks in-game time as a `DateTime` starting at a configurable date (e.g., Jan 1 1984, 00:00:00). Exposes `Tick(double deltaSec)` to advance time. The clock runs at 1:1 real-time (1 real second = 1 game second). Emits no signals — it's pure data.

**`Person.cs`** — Simple data class:
```csharp
public class Person
{
    public int Id { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public DateTime CreatedAt { get; set; }  // in-game time of creation
}
```

**`SimulationState.cs`** — Holds the world:
```csharp
public class SimulationState
{
    public GameClock Clock { get; } = new();
    public Dictionary<int, Person> People { get; } = new();
    private int _nextEntityId = 1;

    public int GenerateEntityId() => _nextEntityId++;
}
```

### Step 2: Person generation

**`NameData.cs`** — Static arrays of ~50 first names and ~50 last names, 1980s-America-flavored.

**`PersonGenerator.cs`** — Has a `GeneratePerson(SimulationState state)` method that creates a Person with a random name from the pool, assigns an ID, sets `CreatedAt` to current game clock time, and adds it to `state.People`.

### Step 3: SimulationManager (Godot bridge)

**`SimulationManager.cs`** — A Godot `Node` that:
- Creates and owns a `SimulationState` instance
- In `_Process(double delta)`, calls `state.Clock.Tick(delta)` to advance game time
- On the first tick (when clock crosses second 1), calls `PersonGenerator.GeneratePerson(state)`
- Emits a C# event `PersonAdded(Person person)` so the UI can react

This is the *only* Godot Node in the simulation layer. Everything else is plain C#.

### Step 4: Simulation Debug UI

**`SimulationDebug.tscn`** — A Control scene with:
- A Label showing the 24-hour clock (HH:MM:SS format)
- A VBoxContainer/ItemList showing the list of people (starts empty, updates when person is added)
- Uses the same fonts as MainMenu for visual consistency

**`SimulationDebug.cs`** — Script that:
- Creates a `SimulationManager` as a child node on `_Ready()`
- Every frame, updates the clock label from `SimulationManager.State.Clock.CurrentTime`
- Subscribes to `SimulationManager.PersonAdded` to add entries to the people list

### Step 5: Wire up New Career button

Modify `MainMenu.cs` so `_OnNewCareerPressed()` calls `GetTree().ChangeSceneToFile("res://scenes/simulation_debug/SimulationDebug.tscn")`.

## Folder Structure Rationale

Putting simulation code in `src/simulation/` (not `scenes/`) emphasizes that this is pure logic, not scene-bound code. Only Godot-facing scripts live in `scenes/`. As the simulation grows, `src/simulation/` will expand:

```
src/simulation/
├── SimulationState.cs
├── SimulationManager.cs      (Godot bridge)
├── GameClock.cs
├── entities/
│   ├── Person.cs
│   └── (future: Location.cs, Organization.cs, Item.cs...)
├── generators/               (future: rename from flat files)
│   ├── PersonGenerator.cs
│   └── (future: LocationGenerator.cs, CrimeGenerator.cs...)
├── systems/                  (future)
│   └── (MovementSystem.cs, RelationshipSystem.cs...)
└── data/
    ├── NameData.cs
    └── (future: crime templates, location templates...)
```

## Out of Scope

- Save/load (but the architecture supports it trivially)
- Any simulation behavior beyond generating one person
- Crime generation, motives, movement, relationships
- Any real gameplay screens

These will come in future plans layered on this foundation.
