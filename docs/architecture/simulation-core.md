# Simulation Core

## Purpose
Runs the city as a discrete-event simulation that writes one append-only ground truth (History). Same seed, same
history, at any time scale. Everything the player learns is read from that history by observers.

## Key Files
| File | Role |
|------|------|
| `src/simulation/World.cs` | The running world: State, Scheduler, History, roads, every Agent and VehicleRuntime, subsystems; `AdvanceTo(t)` |
| `src/simulation/core/Scheduler.cs` | Priority queue of events on (time, sequence); `RunUntil(t)` |
| `src/simulation/core/Rng.cs` | Keyed deterministic randomness (`Rng.For/Unit/Chance(seed, keys...)`) and `Purpose` constants |
| `src/simulation/SimulationState.cs` | The static world: places, people, households, businesses, items, fixtures, relationships, tensions, vehicles |
| `src/simulation/space/RoadGraph.cs` | Road cells of the CityGrid as a graph; each address's access cell |
| `src/simulation/space/Router.cs` | A* with a route cache; `Route` = cells + per-cell time offsets; Drive / Walk / Taxi |
| `src/simulation/history/Facts.cs` | Typed facts: Presence, Parked, Trip, Call, Touch, Wipe, Noise, Death, ForcedEntry, ItemMoved, Purchase |
| `src/simulation/history/HistoryStore.cs` | Fact lists with indexes by person, address, vehicle, surface, phone; live events for stakeouts |
| `src/simulation/agents/Movement.cs` | The only code that moves people and cars; writes presence, trips and parking |
| `src/simulation/diagnostics/InvariantChecker.cs` | Checks a history against physics (one place at a time, cars, phones, staffing, dates) |
| `src/simulation/diagnostics/Timeline.cs` | Readable reconstruction of someone's day from History |
| `src/simulation/generation/WorldBuilder.cs` | Seed to complete world (city, interiors, roads, population) |

## How It Works
1. `WorldBuilder.Build(seed)` generates the city, resolves every interior, builds the road graph, populates the city
   (`population.md`) and places everyone at home. `World.Start()` gives each agent an agenda.
2. Godot (or a test) calls `World.AdvanceTo(t)`; the scheduler runs every event due by then. Nothing ticks per frame.
3. A person is always in one room (open `PresenceFact`) or in one trip (`TripFact`). Passengers ride inside the
   driver's vehicle trip, so people who travel together are together by construction. Lobbies take a minute to cross.
4. Positions between events are computed from the route (`World.PositionOnTrip`) for the map.

## Key Decisions
- **Events, not ticks:** determinism at any speed, and a week of a 4,600-person city runs in about 2 seconds.
- **Keyed RNG:** each decision draws from its own stream, so adding a behavior doesn't reshuffle others.
- **History is append-only and typed:** replaying a case is a query; evidence can never contradict it.
- **Residents' fingerprints are implied** (not recorded touch by touch); only visitors' touches are facts.
- **Taxis are anonymous:** people without a car go far by cab; the cab is visible but not a simulated vehicle.

## Connection Points
- **Agents** (`agents.md`) and **social coordination** (`social-coordination.md`) schedule all events.
- **Observers** (`observers.md`) read History; **investigation** (`investigation.md`) drives the player agent.
- `InvariantChecker` runs in `WorldWeekTests` and after the scripted detective in `InvestigationTests`.
