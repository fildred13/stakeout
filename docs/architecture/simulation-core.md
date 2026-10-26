# Simulation Core

## Purpose
Owns all game-world state and advances it in time. Everything flows through `SimulationState` as the single source
of truth; `SimulationManager` is the Godot node that builds the world and ticks it each frame.

## Key Files
| File | Role |
|------|------|
| `src/simulation/SimulationState.cs` | Central store - dictionaries of every entity (people, businesses, cities, streets, addresses, locations, sublocations, fixtures, items, traces, crimes, groups, relationships, vehicles), GameClock, EventJournal, ID generation, query helpers |
| `src/simulation/SimulationManager.cs` | Godot Node - builds Boston and NYC, spawns NPCs and the player in `_Ready()`; in `_Process()` ticks the clock, plans/ticks every living NPC, updates door locks and player travel |
| `src/simulation/PersonGenerator.cs` | Creates a Person: job (open Position), home (suburban house or apartment unit), traits, objectives, home phone, keys |
| `src/simulation/LocationGenerator.cs` | Lazy interior generation: `PickAndResolveAddress`, `ResolveAddressInterior` |
| `src/simulation/BusinessResolver.cs` | Spawns staff for every unfilled Position of a business on demand |
| `src/simulation/GameClock.cs` | In-game DateTime and TimeScale |
| `src/simulation/MapConfig.cs` | Map size and distance-based travel time (max 1 h corner to corner) |
| `src/simulation/events/EventJournal.cs` | Append-only event log, indexed globally and per person |
| `src/simulation/entities/*` | Plain entity classes (Person, Player, Address, Location, Business, Group, ...) |
| `src/GameManager.cs` | Autoload that creates SimulationState, SimulationManager and EvidenceBoard |

## How It Works
`SimulationManager._Ready()` registers address templates, generates two cities with `CityGenerator` (seeds 42 and 84),
which also creates every business. It spawns 5 random NPCs plus a dating couple (Jack and Susan, for manual testing
of P5), then the player in an unclaimed suburban home with a key to its front door.

Each `_Process` frame: tick the clock by `delta * TimeScale`; for each living person, run `NpcBrain.PlanDay` if they
have no plan or it is exhausted, then `ActionRunner.Tick`; then `DoorLockingService` locks or unlocks business
entrances by opening hours; then player travel is interpolated.

Generation is lazy ("progressive growth", see `docs/design/simulation/simulation_overview.md`): addresses exist on
the grid from the start, but their Locations are only built when a person, the player, or a business needs them.

## Key Decisions
- **State-primary with an event journal:** entities hold mutable current state; the journal is a parallel log for UI and future replay.
- **Single SimulationState:** one place to query, and later to save.
- **Centralized IDs:** `GenerateEntityId()` gives one monotonically increasing ID space across all entity types.
- **Seeded randomness:** world generation and crimes take a `Random` from the caller; avoid `new Random()` and `DateTime.Now` in simulation code.
- **`Person.CurrentAddressId == null` means in transit** (`Player` still uses 0).

## Connection Points
- **NPC brain** (`npc-brain.md`): planning and action execution per person.
- **City generation** (`city-generation.md`) and **businesses** (`businesses.md`) build the world.
- **Social coordination** (`social-coordination.md`): relationships, phone calls, dates.
- **Crime system** (`crime-system.md`): stub, rebuilt in P7.
- **Game shell / evidence board:** read state through `GameManager.State` and `SimulationManager`.
