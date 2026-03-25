# Simulation Scheduling

## Purpose
Gives each Person an AI "brain" that follows a daily schedule derived from prioritized Objectives. Handles objective resolution into Tasks, schedule building, sleep computation, and per-frame action execution with state transitions.

## Key Files
| File | Role |
|------|------|
| `src/simulation/objectives/Objective.cs` | Objective, ObjectiveStep, enums (ObjectiveType, ObjectiveSource, ObjectiveStatus, StepStatus) |
| `src/simulation/objectives/ObjectiveResolver.cs` | Decomposes Objectives → Tasks; executes instant steps (e.g., ChooseVictim); factory methods for CoreNeed objectives |
| `src/simulation/objectives/Task.cs` | SimTask class — schedulable item with ActionType, priority, time window, target address |
| `src/simulation/actions/ActionType.cs` | ActionType enum (Idle, Work, Sleep, TravelByCar, KillPerson) |
| `src/simulation/actions/ActionExecutor.cs` | Executes actions on task boundaries — modifies world state, produces Traces |
| `src/simulation/scheduling/ScheduleBuilder.cs` | Converts List\<SimTask\> → DailySchedule — minute-by-minute priority resolution, block merging, travel insertion |
| `src/simulation/scheduling/DailySchedule.cs` | DailySchedule (list of ScheduleEntry) and ScheduleEntry (ActionType, time window, target/from address IDs) |
| `src/simulation/scheduling/SleepScheduleCalculator.cs` | Pure function: computes sleep/wake times from job shift + commute |
| `src/simulation/scheduling/PersonBehavior.cs` | Per-frame updater: compares current action to schedule, triggers transitions, executes actions (e.g., KillPerson), skips dead NPCs |

## How It Works
The pipeline flows: **Objectives → Tasks → Schedule → Actions → Traces**.

1. **Objectives** live on `Person.Objectives`. Sources include CoreNeed (sleep, work, idle), CrimeTemplate (murder), and future sources (Trait, Assignment).
2. **ObjectiveResolver.ResolveTasks()** iterates objectives, executes any instant steps (side effects computed at resolution time), then generates SimTasks for the current non-instant step.
3. **ScheduleBuilder.BuildFromTasks()** takes the task list and resolves minute-by-minute — highest priority wins — then merges contiguous blocks and inserts TravelByCar entries between location changes.
4. **PersonBehavior.Update()** each frame checks the person's current action against schedule. On mismatch, triggers transitions. On KillPerson action boundary, finds the matching objective and calls ActionExecutor.Execute().
5. **ActionExecutor** modifies world state (e.g., victim.IsAlive = false) and produces Traces (Condition on victim, Mark at location).

When Objectives change (crime injected, step advances), `Person.NeedsScheduleRebuild` is flagged. SimulationManager detects this and re-runs the ObjectiveResolver → ScheduleBuilder pipeline.

## Key Decisions
- **Priority-based task resolution:** Each minute has a winning task. Crime tasks at priority 40 override sleep (30) and work (20). Simple, deterministic, extensible.
- **Instant steps:** Some objective steps (like ChooseVictim) compute data at resolution time rather than being scheduled. They execute in a while-loop during ResolveTasks() and advance the objective immediately.
- **Travel auto-insertion:** ScheduleBuilder inserts TravelByCar between blocks with different target addresses, computing per-pair travel times from MapConfig.
- **SimTask (not Task):** Named to avoid collision with System.Threading.Tasks.Task.

## Connection Points
- **Reads from:** SimulationState (Jobs, Addresses, People, MapConfig, Crimes)
- **Writes to:** Person (CurrentAction, Position, AddressId, TravelInfo, IsAlive), SimulationState (Traces), EventJournal
- **Called by:** SimulationManager._Process() invokes PersonBehavior.Update() each frame; SimulationManager.RebuildSchedule() re-derives schedules
- **Built by:** PersonGenerator creates CoreNeed Objectives and builds initial schedule; CrimeGenerator injects crime Objectives
