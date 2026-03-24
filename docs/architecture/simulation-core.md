# Simulation Core

## Purpose
The core simulation layer that manages all game world state: entities (people, locations, jobs), world generation, time progression, and event logging. Everything flows through SimulationState as the single source of truth.

## Key Files
| File | Role |
|------|------|
| `src/simulation/SimulationState.cs` | Central data store — dictionaries of all entities, GameClock, EventJournal, ID generation |
| `src/simulation/SimulationManager.cs` | Godot Node orchestrator — initializes world and Player in `_Ready()`, ticks clock and updates people in `_Process()`, emits events (PersonAdded, AddressAdded, PlayerCreated) for UI notification |
| `src/simulation/PersonGenerator.cs` | Creates a Person with home, job, work address, sleep schedule, daily schedule, and initial state |
| `src/simulation/LocationGenerator.cs` | Generates addresses, streets, cities — handles street reuse and realistic address numbering |
| `src/simulation/entities/Player.cs` | Player entity — home address, current address/position, travel info (reuses NPC TravelInfo) |
| `src/simulation/GameClock.cs` | Tracks in-game DateTime with a float TimeScale property (scaling applied by SimulationManager) |
| `src/simulation/MapConfig.cs` | Map bounds and distance-based travel time calculation |
| `src/simulation/events/EventJournal.cs` | Append-only event store, dual-indexed (global list + per-person dictionary) |
| `src/simulation/events/SimulationEvent.cs` | Immutable event record with timestamp, person ID, event type, and contextual IDs |
| `src/simulation/data/NameData.cs` | First/last name arrays for random person generation |
| `src/simulation/data/StreetData.cs` | Street name/suffix arrays for random address generation |
| `src/GameManager.cs` | Top-level Godot Node — creates SimulationState, EvidenceBoard, SimulationManager; wires them together |

## How It Works
GameManager creates a SimulationState (empty) and SimulationManager, adding it to the scene tree. On `_Ready()`, SimulationManager generates a city scaffold (Boston, USA) via LocationGenerator, spawns 5 NPCs via PersonGenerator, and creates a Player with a home address. It emits C# events (PersonAdded, AddressAdded, PlayerCreated) so UI layers can react to initialization.

Each frame, SimulationManager ticks the GameClock (applying TimeScale to delta), then calls PersonBehavior.Update() for each person with their schedule, and UpdatePlayerTravel() to interpolate player movement. Player travel reuses the NPC TravelInfo class and MapConfig travel time formula. All state changes (including player departures/arrivals) are logged to the EventJournal.

All systems read/write through SimulationState — there is no direct system-to-system communication.

## Key Decisions
- **State-primary with event journal:** Entities hold mutable current state; the journal is a parallel append-only log for future replay/retroactive history. Chosen over event-sourced because the simulation needs fast current-state reads every frame.
- **Single SimulationState:** All entity dictionaries in one place for simple access patterns. No separate repositories per entity type.
- **PersonGenerator returns (Person, DailySchedule):** Schedule is stored in SimulationManager, not on Person, because schedules will eventually be rebuilt daily when reactive re-evaluation is added.
- **ID generation is centralized:** `SimulationState.GenerateEntityId()` provides monotonically increasing IDs across all entity types.

## Connection Points
- **Scheduling system** reads from SimulationState (jobs, addresses) and PersonBehavior writes back to Person entities and EventJournal
- **Evidence board** is created by GameManager alongside SimulationState but currently has no direct data flow from simulation (UI layer will bridge this)
- **Game shell** accesses simulation through GameManager's public `State` and `SimulationManager` properties; content views trigger player travel via `SimulationManager.StartPlayerTravel()`
