# Crime System

## Purpose
Generates and executes crimes as structured scenarios that inject Objectives onto NPCs, producing observable Traces in the world. Crime templates define the cast, objectives, and steps; the scheduling system handles execution.

## Key Files
| File | Role |
|------|------|
| `src/simulation/crimes/ICrimeTemplate.cs` | Interface — Type, Name, Instantiate(state) → Crime |
| `src/simulation/crimes/SerialKillerTemplate.cs` | First template — casts a killer, creates 3-step CommitMurder objective (ChooseVictim → KillPerson → GoHome) |
| `src/simulation/crimes/CrimeGenerator.cs` | Registry of templates by CrimeTemplateType; Generate() instantiates and returns a Crime |
| `src/simulation/crimes/Crime.cs` | Crime record — roles (name→personId), status, related trace/objective IDs; CrimeTemplateType, CrimeStatus enums |
| `src/simulation/traces/Trace.cs` | Trace record — type (Item/Sighting/Mark/Condition/Record), location or person attachment, description, data; TraceType enum |

## How It Works
1. **CrimeGenerator.Generate()** looks up the template by type and calls `Instantiate(state)`.
2. **Template.Instantiate()** casts NPCs into roles (e.g., picks a random alive person as Killer), creates a Crime record in `state.Crimes`, and injects Objectives onto cast members.
3. **ObjectiveResolver** processes the new Objectives during the next resolution pass. Instant steps (like ChooseVictim) execute immediately — picking a victim, storing the ID in `Objective.Data`, and updating `Crime.Roles`.
4. **ScheduleBuilder** incorporates the crime's Tasks (e.g., KillPerson at 1:00 AM, priority 40) into the daily schedule, auto-inserting travel.
5. **PersonBehavior** detects the KillPerson action boundary and calls **ActionExecutor.Execute()**, which sets `victim.IsAlive = false` and produces Traces (Condition on victim, Mark at location).

## Key Decisions
- **Templates are C# classes, not data files:** Keeps logic co-located with casting and objective tree definitions. Designed to become data-driven later.
- **Crime.Roles uses Dictionary\<string, int?\>:** Flexible role mapping (Killer, Victim, etc.). Victim starts null, populated when ChooseVictim resolves.
- **Trace taxonomy defined upfront:** Five types (Item, Sighting, Mark, Condition, Record) cover all planned evidence categories. Only Condition and Mark are produced in this iteration.
- **One crime at a time:** Multiple simultaneous crimes are out of scope for the initial implementation.

## Connection Points
- **Scheduling system:** Crime Objectives flow through the same ObjectiveResolver → ScheduleBuilder → PersonBehavior pipeline as CoreNeed objectives
- **SimulationState:** Crimes and Traces dictionaries store all crime records and evidence
- **Debug UI:** GameShell's Crime Generator button triggers CrimeGenerator.Generate(); Person Inspector shows objectives and traces
- **Future:** Trace system will expand with Item lifecycle, Sighting generation, and player investigation mechanics
