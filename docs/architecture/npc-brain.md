# NPC Brain & Action Engine

## Purpose

Drives all NPC behavior through objective-driven daily planning and action execution. NPCs evaluate their objectives each morning, schedule actions into a day plan, and execute them throughout the day.

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
| `src/simulation/objectives/Objective.cs` | Abstract base — Priority, GetActionsForToday, children, status |
| `src/simulation/objectives/PlannedAction.cs` | IAction + target address + time window + duration |
| `src/simulation/objectives/SleepObjective.cs` | Universal, priority 80 |
| `src/simulation/objectives/GoForARunObjective.cs` | Trait: runner, priority 20 |
| `src/simulation/objectives/EatOutObjective.cs` | Trait: foodie, priority 40 |
| `src/simulation/traits/TraitDefinitions.cs` | Registry mapping trait names to objective factories |

## How It Works

1. On first tick (or wake-up), `NpcBrain.PlanDay` collects all active objectives, sorts by priority, and greedily schedules their actions into time slots. Gaps fill with IdleAtHome.
2. `ActionRunner.Tick` runs each frame per person: if traveling, interpolate position; if doing an activity, tick it; if idle, start next plan entry.
3. Inter-address travel is the engine's job — actions only define what to do at a destination.
4. Objectives are persistent and can have child objectives. Simple objectives (Sleep, GoForARun) return a fixed action daily. Complex objectives (future crime plots) track phases across days.
5. Traits on Person map to objectives via TraitDefinitions registry at generation time.

## Key Decisions

- **Unified objective model**: no separate obligation/drive/default layers — everything is an objective with a priority
- **Daily planning, not per-tick**: brain runs at wake-up, not every frame
- **Travel is engine-level**: actions are pure activities, never contain movement logic
- **No fast-forward optimization**: deferred until performance requires it (all NPCs use same path)
- **Job features deferred to P4**: WorkShift, Commute, DoorLockingService

## Connection Points

- **SimulationManager** calls `ActionRunner.Tick` each frame and `NpcBrain.PlanDay` on first tick
- **PersonGenerator** assigns traits and creates objectives at NPC creation time
- **TraceEmitter** (P2) ready for actions to call — currently only light use (sighting traces)
- **EventJournal** logs ActivityStarted, ActivityCompleted, DayPlanned events
- **GameShell inspector** reads DayPlan, CurrentActivity, Objectives for debug display
