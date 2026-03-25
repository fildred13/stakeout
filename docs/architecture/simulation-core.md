# Simulation Core

## Purpose
The core simulation layer that manages all game world state: entities (people, locations, jobs), world generation, time progression, and event logging. Everything flows through SimulationState as the single source of truth.

## Key Files
| File | Role |
|------|------|
| `src/simulation/SimulationState.cs` | Central data store — dictionaries of all entities, Crimes, Traces, GameClock, EventJournal, ID generation |
| `src/simulation/SimulationManager.cs` | Godot Node orchestrator — initializes world in `_Ready()`, ticks clock and updates people in `_Process()`, rebuilds schedules when flagged, emits events for UI |
| `src/simulation/PersonGenerator.cs` | Creates a Person with home, job, CoreNeed Objectives, built schedule, and initial state |
| `src/simulation/LocationGenerator.cs` | Generates addresses, streets, cities — handles street reuse and realistic address numbering |
| `src/simulation/entities/Person.cs` | Person entity — IsAlive, Objectives list, Schedule, CurrentAction (ActionType), home/job/position/travel state |
| `src/simulation/entities/Player.cs` | Player entity — home address, current address/position, travel info (reuses NPC TravelInfo) |
| `src/simulation/GameClock.cs` | Tracks in-game DateTime with a float TimeScale property (scaling applied by SimulationManager) |
| `src/simulation/MapConfig.cs` | Map bounds and distance-based travel time calculation |
| `src/simulation/events/EventJournal.cs` | Append-only event store, dual-indexed (global list + per-person dictionary) |
| `src/simulation/events/SimulationEvent.cs` | Immutable event record with timestamp, person ID, event type, and contextual IDs |
| `src/GameManager.cs` | Top-level Godot Node — creates SimulationState, EvidenceBoard, SimulationManager; wires them together |

## How It Works
GameManager creates a SimulationState (empty) and SimulationManager, adding it to the scene tree. On `_Ready()`, SimulationManager generates a city scaffold (Boston, USA) via LocationGenerator, spawns 5 NPCs via PersonGenerator, and creates a Player with a home address. It emits C# events (PersonAdded, AddressAdded, PlayerCreated) so UI layers can react.

PersonGenerator creates each Person with CoreNeed Objectives (GetSleep, MaintainJob, DefaultIdle), resolves them into Tasks via ObjectiveResolver, then builds a DailySchedule via ScheduleBuilder. The schedule lives on the Person entity.

Each frame, SimulationManager ticks the GameClock, calls PersonBehavior.Update() for each alive person, then checks for schedule rebuild flags (set when Objectives change). Player travel is updated separately. All state changes are logged to the EventJournal.

## Key Decisions
- **State-primary with event journal:** Entities hold mutable current state; the journal is a parallel append-only log. Chosen over event-sourced because the simulation needs fast current-state reads every frame.
- **Single SimulationState:** All entity dictionaries in one place. Crimes and Traces live here alongside People and Addresses.
- **Schedule lives on Person:** PersonGenerator builds the schedule and stores it on `Person.Schedule`. SimulationManager.RebuildSchedule() re-derives it when Objectives change.
- **ID generation is centralized:** `SimulationState.GenerateEntityId()` provides monotonically increasing IDs across all entity types.

## Connection Points
- **Crime system** injects Objectives onto People via CrimeGenerator, which triggers schedule rebuilds
- **Scheduling system** reads from SimulationState (jobs, addresses) and PersonBehavior writes back to Person entities and EventJournal
- **Evidence board** is created by GameManager alongside SimulationState but currently has no direct data flow from simulation
- **Game shell** accesses simulation through GameManager's public `State` and `SimulationManager` properties
