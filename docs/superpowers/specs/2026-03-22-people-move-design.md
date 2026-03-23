# People Move: AI, Schedules, and the Event Journal

## Overview

Make the simulation world come alive by giving each Person an AI "brain" that follows a daily schedule derived from prioritized goals. Introduce jobs, sleep schedules, continuous travel, and an event journal that records all state transitions for future replay and retroactive history generation.

## Scope

### In scope (this branch)

- Event journal system (append-only, timestamped, per-person indexed)
- Goal/priority system with pre-computed daily schedules
- Job system (DinerWaiter, OfficeWorker, Bartender) with shifts
- Sleep schedule (adjusted around work hours + commute)
- Activities: AtHome, Working, TravellingByCar, Sleeping
- Continuous 2D travel interpolation
- Spawn 1 person with auto-generated home + workplace
- Remove bulk random address/person generation
- Time controls in SimulationDebug (pause, 1x, 4x, 8x)
- Start date changed to January 1, 1980

### Deferred (design for, don't implement)

- Reactive re-evaluation / triggers (criminal plots, player interactions)
- Retroactive past history generation for NPCs
- Timeline replay UI
- Multiple persons / dynamic NPC spawning beyond the initial 1
- Criminal goal chains (plots)

## Architecture Approach: State-Primary with Event Journal

Current state lives on entities as mutable fields. An event journal runs alongside — every meaningful state transition appends an event, but current state is not derived from the journal. The journal is an audit trail for replay and history queries. Retroactive generation means computing what a schedule would have been and writing those events directly into the journal.

**Key discipline:** All state changes to tracked Person fields go through methods that both update state and append to the journal. No direct field writes for tracked properties.

## Event Journal

### SimulationEvent

Each event record contains:

- **Timestamp** (DateTime) — when it happened in sim-time
- **PersonId** (int) — who it concerns
- **EventType** (SimulationEventType enum) — what happened
- **Data fields** — typed context for the event (address IDs, activity types)

### Event types for this branch

| EventType | Data |
|---|---|
| DepartedAddress | FromAddressId, ToAddressId |
| ArrivedAtAddress | AddressId |
| StartedWorking | AddressId |
| StoppedWorking | AddressId |
| FellAsleep | AddressId |
| WokeUp | AddressId |
| ActivityChanged | OldActivity, NewActivity |

### Storage

- Global `List<SimulationEvent>` on SimulationState, sorted by timestamp
- Per-person index: `Dictionary<int, List<SimulationEvent>>` referencing the same event objects
- Both maintained in lockstep via a single `AppendEvent()` method

### Scale analysis

At 1000 NPCs over 90 days: ~720K events, ~72 MB. Appending is O(1). Per-person lookup is O(1) to get the person's list, then O(n) on that person's events only. Sufficient for the foreseeable scope. Snapshot/archival optimizations can be added later if needed.

## Job System

### Job entity

```
Job
  Id: int
  Type: JobType (DinerWaiter, OfficeWorker, Bartender)
  Title: string
  WorkAddressId: int
  ShiftStart: TimeSpan
  ShiftEnd: TimeSpan
  WorkDays: DayOfWeek[] (all 7 days for now)
```

### Job definitions

| JobType | Title | Shift | Notes |
|---|---|---|---|
| DinerWaiter | "Waiter"/"Waitress" | Random 12hr block | Random start time per person |
| OfficeWorker | "Office Worker" | 09:00–17:00 | Fixed |
| Bartender | "Bartender" | 16:00–02:00 | Overnight |

### Relationship to other entities

- Person gains a `JobId` field. The old `WorkAddressId` on Person is removed — the Job owns the workplace reference.
- Each Job points to an AddressId. Job type must match address type (DinerWaiter → Diner address, OfficeWorker → Office address, Bartender → DiveBar address).
- Jobs stored in `Dictionary<int, Job>` on SimulationState.

## Sleep Schedule

Each Person has `PreferredSleepTime` and `PreferredWakeTime` (both TimeSpan). Default preference: sleep at 22:00, wake at 06:00 (8 hours).

### Adjustment rule

Sleep must not overlap with work shift + commute time. Computed during person generation:

1. Calculate commute time from home to work (distance-based).
2. Define "work block" = (shift start − commute) → (shift end + commute).
3. If default 22:00–06:00 overlaps the work block, shift sleep to fit:
   - Early shift: push wake and sleep times earlier.
   - Late shift (e.g. bartender ends at 02:00): push sleep start to after arrival home, wake 8 hours later.

### Examples

| Job | Shift | Commute | Resulting Sleep |
|---|---|---|---|
| Office Worker | 09:00–17:00 | 30min | 22:00–06:00 (default works) |
| Bartender | 16:00–02:00 | 30min | 02:30–10:30 |
| Diner Waiter (early) | 05:00–17:00 | 20min | 19:00–03:00 |

`ComputeSleepSchedule(Job, commuteTimeHours)` is a pure function — testable and reusable for retroactive generation.

## Goals and Schedule

### GoalType enum

- `BeAtWork` — be at the workplace during shift hours
- `BeAtHome` — be at home (fallback, always active)
- `Sleep` — be asleep during sleep window

### Goal

```
Goal
  Type: GoalType
  Priority: int (higher wins)
  WindowStart: TimeSpan
  WindowEnd: TimeSpan
```

### GoalSet

A person's collection of goals, derived from job + sleep schedule during generation. Example for an office worker:

| Goal | Priority | Window |
|---|---|---|
| Sleep | 30 | 22:00–06:00 |
| BeAtWork | 20 | 09:00–17:00 |
| BeAtHome | 10 | always |

### ScheduleBuilder

Takes a GoalSet, home address, work address, and map config. Produces a `DailySchedule` — an ordered list of `ScheduleEntry` records:

```
ScheduleEntry
  Activity: ActivityType
  StartTime: TimeSpan
  EndTime: TimeSpan
  TargetAddressId: int? (for travel entries)
  FromAddressId: int? (for travel entries)
```

The builder walks the day chronologically, resolves which goal wins at each time, and inserts travel entries where the person needs to move. Travel duration computed from distance.

### Example schedule (office worker, 30min commute)

| Time | Activity | Location |
|---|---|---|
| 00:00–06:00 | Sleeping | Home |
| 06:00–08:30 | AtHome | Home |
| 08:30–09:00 | TravellingByCar | Home → Work |
| 09:00–17:00 | Working | Work |
| 17:00–17:30 | TravellingByCar | Work → Home |
| 17:30–22:00 | AtHome | Home |
| 22:00–00:00 | Sleeping | Home |

### PersonBehavior

A plain C# class (not a Godot node) with an `Update(Person, SimulationState)` method. Called by SimulationManager each tick for each person. Checks the current schedule entry against sim time and transitions when needed — updating state, appending journal events, and beginning position interpolation for travel.

### Future extensibility

When reactive re-evaluation is needed (criminal plots, player interference), the brain gains an `InterruptSchedule()` method that recomputes the schedule from the current moment forward. Add a high-priority goal and rebuild.

## Travel and Position

### Travel time formula

```
TravelTimeHours = EuclideanDistance(from, to) / MapMaxDiagonal * MaxTravelTimeHours
```

- `MapMaxDiagonal` is computed from map dimensions read from a shared config — not hardcoded.
- `MaxTravelTimeHours = 1.0` (max travel time across the full map diagonal).

### Position interpolation

During TravellingByCar activity:

```
progress = (currentTime - departureTime) / (arrivalTime - departureTime)
CurrentPosition = Lerp(fromPosition, toPosition, clamp(progress, 0, 1))
```

When stationary, `CurrentPosition` equals the person's current address position.

### TravelInfo

```
TravelInfo
  FromPosition: Vector2
  ToPosition: Vector2
  DepartureTime: DateTime
  ArrivalTime: DateTime
  FromAddressId: int
  ToAddressId: int
```

Set on Person only during travel. Null when stationary.

## Person Model Updates

```
Person (updated)
  Id: int
  FirstName: string
  LastName: string
  CreatedAt: DateTime
  HomeAddressId: int
  JobId: int (replaces WorkAddressId)
  CurrentAddressId: int? (nullable — null during travel)
  CurrentPosition: Vector2
  CurrentActivity: ActivityType
  TravelInfo: TravelInfo? (null when stationary)
  PreferredSleepTime: TimeSpan
  PreferredWakeTime: TimeSpan
  FullName => FirstName + LastName
```

### ActivityType enum

- AtHome
- Working
- TravellingByCar
- Sleeping

## Generation Overhaul

### Removed

- Bulk address generation via LocationGenerator (the class is removed or gutted)
- 5-person spawn after 1 second delay

### New flow

`PersonGenerator.GeneratePerson()` becomes the single entry point that creates everything a person needs:

1. Pick a random JobType.
2. Generate a commercial address matching the job type (create street if needed).
3. Generate a residential address for the person's home (create street if needed).
4. Create the Job with appropriate shift.
5. Compute commute time from home to work.
6. Compute sleep schedule from job + commute.
7. Build GoalSet and compute DailySchedule.
8. Set initial state (CurrentActivity, CurrentPosition based on schedule + current sim time).
9. Append initial journal events.
10. Return the fully-formed Person.

Addresses are placed at random positions within map bounds. Streets are created or reused as needed.

`SimulationManager._Ready()` creates city/country scaffolding, generates 1 person, and creates the player.

## Simulation Loop

### SimulationManager._Process() (updated)

1. Compute `scaledDelta = delta * Clock.TimeScale`.
2. `State.Clock.Tick(scaledDelta)`.
3. For each person: `PersonBehavior.Update(person, state)`.

### GameClock changes

- Start time changes to January 1, 1980.
- Add `TimeScale` property (float): 0 (paused), 1, 4, or 8.
- Scaling applied by the manager at the call site — the clock stays simple.

## SimulationDebug UI Changes

### Time controls

Four buttons in an HBoxContainer next to the clock:

| Button | Label | TimeScale |
|---|---|---|
| Pause | ⏸ | 0 |
| Play | ▶ | 1 |
| Fast | ▶▶ | 4 |
| Super Fast | ▶▶▶ | 8 |

Active button is visually highlighted. Default state is Play (1x).

### Clock display

Updated from `HH:mm:ss` to include date, e.g. `Mon Jan 01, 1980 00:00`.

### Map rendering

- Person dots read `CurrentPosition` directly (already a Vector2), enabling smooth movement during travel.
- Tooltip for person shows current activity (e.g. "John Smith — Travelling to Work").
- Address tooltip only lists people whose `CurrentAddressId` matches (excludes travellers).
- Map starts sparse (only 2 addresses for the 1 generated person) and grows as NPCs are added.

### Visual activity cues

- Sleeping: dimmed/grey dot
- AtHome/Working/Travelling: white dot (current behavior)

## State Change Discipline

All mutations to tracked Person fields (CurrentActivity, CurrentPosition, CurrentAddressId, TravelInfo) go through a centralized method (e.g. `SimulationState.TransitionActivity()` or similar) that:

1. Updates the mutable field on the Person.
2. Appends the corresponding SimulationEvent to both the global journal and the per-person index.

This is the core invariant that keeps state and journal in sync. No direct writes to these fields outside this pathway.

## File Organization

New files:

- `src/simulation/entities/Job.cs` — Job entity, JobType enum
- `src/simulation/entities/ActivityType.cs` — ActivityType enum
- `src/simulation/entities/TravelInfo.cs` — TravelInfo struct
- `src/simulation/events/SimulationEvent.cs` — event record and SimulationEventType enum
- `src/simulation/scheduling/Goal.cs` — Goal, GoalType, GoalSet
- `src/simulation/scheduling/ScheduleBuilder.cs` — builds DailySchedule from GoalSet
- `src/simulation/scheduling/DailySchedule.cs` — ScheduleEntry list
- `src/simulation/scheduling/SleepScheduleCalculator.cs` — pure function for sleep adjustment
- `src/simulation/scheduling/PersonBehavior.cs` — executes schedule each tick
- `src/simulation/MapConfig.cs` — map dimensions, travel constants

Modified files:

- `src/simulation/entities/Person.cs` — new fields, remove WorkAddressId
- `src/simulation/SimulationState.cs` — add Jobs dict, event journal, AppendEvent method
- `src/simulation/SimulationManager.cs` — new sim loop, time scaling, generation changes
- `src/simulation/PersonGenerator.cs` — complete overhaul for on-demand generation
- `src/simulation/LocationGenerator.cs` — removed or gutted
- `src/simulation/GameClock.cs` — TimeScale, start date change
- `scenes/simulation_debug/SimulationDebug.cs` — time controls, updated rendering
- `scenes/simulation_debug/SimulationDebug.tscn` — time control buttons
