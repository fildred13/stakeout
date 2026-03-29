# NPC Brain & Action Engine

## Purpose

Drives all NPC behavior through objective-driven planning and action execution. NPCs plan a 24-hour window of activities, execute the plan, and replan when it's exhausted or interrupted.

## Key Files

| File | Role |
|------|------|
| `src/simulation/brain/NpcBrain.cs` | PlanDay algorithm — sorts objectives by priority, schedules greedily, fills gaps with idle |
| `src/simulation/brain/DayPlan.cs` | Ordered list of DayPlanEntry with current-index |
| `src/simulation/brain/DayPlanEntry.cs` | Time slot + PlannedAction + status |
| `src/simulation/actions/ActionRunner.cs` | Per-frame tick loop — travel, tick activity, advance plan |
| `src/simulation/actions/IAction.cs` | Action interface — Name, DisplayText, Tick, OnStart, OnComplete |
| `src/simulation/actions/ActionSequence.cs` | Fluent builder for multi-step actions |
| `src/simulation/actions/ActionContext.cs` | State bag threaded through action methods |
| `src/simulation/actions/primitives/WaitAction.cs` | Stay at location for a duration |
| `src/simulation/objectives/Objective.cs` | Abstract base — Priority, GetActions(planStart, planEnd), children, status |
| `src/simulation/objectives/PlannedAction.cs` | IAction + target address + DateTime time window + duration |
| `src/simulation/objectives/SleepObjective.cs` | Universal, priority 80 |
| `src/simulation/objectives/GoForARunObjective.cs` | Trait: runner, priority 20 |
| `src/simulation/objectives/EatOutObjective.cs` | Trait: foodie, priority 40 |
| `src/simulation/traits/TraitDefinitions.cs` | Registry mapping trait names to objective factories |

## How It Works

1. On first tick (or plan exhaustion), `NpcBrain.PlanDay(person, state, currentTime)` plans a 24-hour window from `currentTime`. All objectives compete by priority — sleep is not special-cased. Plan entries use absolute `DateTime` timestamps.
2. Objectives implement `GetActions(person, state, planStart, planEnd)` and return actions with `DateTime` time windows relative to the planning window (not wall-clock hours). SleepObjective detects mid-sleep starts and returns remaining sleep.
3. `ActionRunner.Tick` runs each frame per person: if traveling, interpolate position; if doing an activity, tick it; if idle, start next plan entry.
4. Inter-address travel is the engine's job — actions only define what to do at a destination.
5. Objectives are persistent and can have child objectives. Simple objectives (Sleep, GoForARun) return a fixed action per plan cycle. Complex objectives (future crime plots) track phases across days.
6. Traits on Person map to objectives via TraitDefinitions registry at generation time.

## Key Decisions

- **Unified objective model**: no separate obligation/drive/default layers — everything is an objective with a priority, including sleep
- **24-hour DateTime window**: planner uses absolute DateTime, not TimeSpan — eliminates midnight-crossing bugs for night-shift workers
- **Plan-relative scheduling**: objectives schedule relative to the planning window, not wall-clock hours — a night worker's "meal" naturally lands at the right time
- **Travel is engine-level**: actions are pure activities, never contain movement logic
- **No fast-forward optimization**: deferred until performance requires it (all NPCs use same path)
- **Job features deferred to P4**: WorkShift, Commute, DoorLockingService

## Connection Points

- **SimulationManager** calls `ActionRunner.Tick` each frame and `NpcBrain.PlanDay` on first tick or plan exhaustion
- **PersonGenerator** assigns traits and creates objectives at NPC creation time
- **TraceEmitter** (P2) ready for actions to call — currently only light use (sighting traces)
- **EventJournal** logs ActivityStarted, ActivityCompleted, DayPlanned events
- **GameShell inspector** reads DayPlan, CurrentActivity, Objectives for debug display
