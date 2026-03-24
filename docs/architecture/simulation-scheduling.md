# Simulation Scheduling

## Purpose
Gives each Person an AI "brain" that follows a daily schedule derived from prioritized goals. Handles goal resolution, schedule building, sleep computation, and per-frame activity execution with state transitions.

## Key Files
| File | Role |
|------|------|
| `src/simulation/scheduling/Goal.cs` | GoalType enum (BeAtWork, BeAtHome, Sleep), Goal class (type, priority, time window), GoalSet, GoalSetBuilder |
| `src/simulation/scheduling/SleepScheduleCalculator.cs` | Pure function: computes sleep/wake times from job shift + commute, handles midnight wrapping |
| `src/simulation/scheduling/ScheduleBuilder.cs` | Converts (GoalSet, addresses, MapConfig) → DailySchedule — minute-by-minute priority resolution, block merging, travel insertion |
| `src/simulation/scheduling/DailySchedule.cs` | DailySchedule (list of ScheduleEntry) and ScheduleEntry (activity, time window, target/from address IDs) |
| `src/simulation/scheduling/PersonBehavior.cs` | Per-frame updater: compares current activity to schedule, triggers transitions, interpolates travel, logs events to journal |

## How It Works
When a Person is created, PersonGenerator orchestrates the full pipeline:
1. GoalSetBuilder creates 3 goals: Sleep (priority 30), Work (priority 20), Home (priority 10, always active)
2. SleepScheduleCalculator computes sleep/wake times, adjusting around job shifts and commute
3. ScheduleBuilder resolves goals minute-by-minute — highest priority wins — then merges into contiguous blocks and inserts travel entries between location changes

Each frame, PersonBehavior.Update() checks the person's current activity against what the schedule says they should be doing. On mismatch, it triggers a transition: logging the end of the old activity, starting travel if a location change is needed, or switching directly. During travel, position is interpolated between origin and destination based on elapsed time.

## Key Decisions
- **Priority-based goal resolution:** Each minute has a winning goal. This is simple, deterministic, and easy to extend with new goal types (criminal plots, player interactions) by just adding goals with appropriate priorities.
- **Pre-computed daily schedules:** Schedules are built once per day (currently once at creation), not evaluated reactively. Reactive re-evaluation is a designed-for future extension.
- **Travel as explicit schedule entries:** ScheduleBuilder inserts TravellingByCar entries into the schedule, so PersonBehavior doesn't need to decide when to travel — it just follows the schedule.
- **Sleep priority is highest (30):** Ensures people sleep even if other goals overlap. Work (20) beats Home (10).

## Connection Points
- **Reads from:** SimulationState (Job shift hours, Address positions for commute calculation, MapConfig for travel times)
- **Writes to:** Person entity (CurrentActivity, CurrentPosition, CurrentAddressId, TravelInfo) and EventJournal (all transitions)
- **Called by:** SimulationManager._Process() invokes PersonBehavior.Update() each frame
- **Built by:** PersonGenerator orchestrates the full Goal → Sleep → Schedule pipeline during NPC creation
