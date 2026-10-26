# Crime System

## Purpose
Creates crimes as structured scenarios: casts NPCs into roles and records the crime in `SimulationState`. **Currently
a stub** - the old objective/scheduler pipeline it drove was removed in the NPC brain rewrite (P3), and Project 7
("Crime Templates & Scenarios" in `docs/future_plans/simulation_overhaul_plan.md`) rebuilds it on the brain,
groups and trace systems.

## Key Files
| File | Role |
|------|------|
| `src/simulation/crimes/ICrimeTemplate.cs` | Interface - Type, Name, `Instantiate(state, random)` returns a Crime |
| `src/simulation/crimes/SerialKillerTemplate.cs` | Only template - picks a random living Killer; Victim role stays null; injects no objectives yet |
| `src/simulation/crimes/CrimeGenerator.cs` | Template registry; `Generate(type, state)` instantiates with the simulation's seeded RNG |
| `src/simulation/crimes/Crime.cs` | Crime record - roles (name to personId), status, related trace/objective IDs; enums |
| `src/simulation/traces/Trace.cs` | Evidence record - TraceType (Mark/Item/Sighting/Record/Fingerprint/Condition), RecordKind, where/who/decay |
| `src/simulation/traces/TraceEmitter.cs` | Static helpers that create Traces; objectives call it from `EmitTraces` |
| `src/simulation/traces/InvestigationQuery.cs` | Read-side helper for finding traces at a location (future investigation UI) |

## How It Works
1. The debug sidebar's "Generate Now" button calls `SimulationManager.CrimeGenerator.Generate(SerialKiller, state)`.
2. The template casts a Killer from living people, stores the Crime in `state.Crimes`, and returns it.
3. Nothing else happens yet: no objectives are injected, so the killer's behavior is unchanged.

Traces are already produced by ordinary NPC life (sightings while running, phone call logs and messages,
fingerprints), so the evidence side exists independently of crimes.

## Key Decisions
- **Templates are C# classes, not data files:** keeps casting logic next to the objective tree it will build.
- **Crime.Roles is `Dictionary<string, int?>`:** roles can be filled later (Victim chosen when the plan runs).
- **Seeded RNG and game clock only:** crimes must be reproducible from the world seed for replay/backfill (P6).
- **One crime at a time** for the first scenario.

## Connection Points
- **NPC brain (P7):** crimes will inject Objectives (`ObjectiveSource.Crime`) that `NpcBrain.PlanDay` schedules like any other.
- **Groups (P5):** multi-person crimes will reuse `Group` (`GroupType.CriminalMeeting` already exists).
- **SimulationState:** `Crimes` and `Traces` dictionaries.
- **Game shell:** debug sidebar shows the generated killer/victim names.
