# Time and Scheduling

## Overview

STAKEOUT uses continuous time, not discrete ticks. Godot's `_Process(delta)` fires every frame (~16ms at 60 FPS), and the simulation advances by `delta * TimeScale` seconds of game time. There is no fixed tick rate or step size — the simulation is frame-rate independent.

## Game Clock

`GameClock` is the single source of truth for simulation time. It tracks:

- **CurrentTime** — a `DateTime` starting at 1980-01-01 08:30:00
- **ElapsedSeconds** — total seconds elapsed since simulation start
- **TimeScale** — multiplier on delta (default 1.0, real-time)

At TimeScale 60, one real second equals one game minute. The clock has no upper bound on TimeScale, but gameplay will typically offer a few fixed speeds (1x, 10x, 60x, etc.).

## Schedule Granularity

`ScheduleBuilder` divides each day into a **1440-slot array** — one slot per minute of the day. This is the finest resolution for NPC planning. Each slot maps to an action (Sleep, Work, Travel, Idle, etc.) at a specific address and sublocation.

Schedules are built once per day per NPC when `PersonBehavior` detects a day boundary or a rebuild flag. The builder:

1. Resolves the NPC's objectives into concrete tasks via `TaskResolver`
2. Decomposes tasks into minute-level schedule entries via decomposition strategies
3. Inserts travel entries between locations using `TravelInfo` duration estimates
4. Fills remaining gaps with sleep (22:00–06:00 default) and idle time

## Behavior Loop

Every frame, `SimulationManager._Process()` calls `PersonBehavior.Update()` for each living NPC:

1. Look up the current schedule entry via `schedule.GetEntryAtTime(currentTime.TimeOfDay)`
2. If the entry's action differs from the NPC's `CurrentAction`, fire a **transition**:
   - Log departure/arrival events to `EventJournal`
   - Set up `TravelInfo` with absolute `DepartureTime` and `ArrivalTime`
   - Execute any immediate actions (e.g., `KillPerson`)
3. If the NPC is traveling, **interpolate position** each frame:
   - `progress = (currentTime - departureTime) / totalDuration`
   - Position is lerped from origin to destination
   - On arrival (`currentTime >= ArrivalTime`), snap to destination and log arrival event

## Events

All state changes produce `SimulationEvent` entries appended to `EventJournal`, timestamped with `Clock.CurrentTime`. Events are indexed by person ID for fast lookup. Event types include:

- `DepartedAddress` / `ArrivedAtAddress` — movement
- `StartedWorking` / `StoppedWorking` — work shifts
- `FellAsleep` / `WokeUp` — sleep cycle
- `PersonDied` / `CrimeCommitted` — crime actions

## Key Constants

| Constant | Value | Location |
|----------|-------|----------|
| Schedule granularity | 1440 slots/day (1 per minute) | `ScheduleBuilder` |
| TimeScale default | 1.0 (real-time) | `GameClock` |
| MaxTravelTimeHours | 1.0 | `MapConfig` |
| Default sleep window | 22:00–06:00 (8 hours) | `SleepScheduleCalculator` |
| Simulation start | 1980-01-01 08:30 | `GameClock` |

## Design Rationale

- **Continuous time over discrete ticks**: Avoids artifacts from fixed step sizes, allows smooth travel interpolation, and makes TimeScale trivial — just multiply delta.
- **1-minute schedule resolution**: Fine enough to model realistic daily routines without blowing up memory. A full day for one NPC is a 1440-element array of structs.
- **Absolute DateTimes for travel**: Travel start/end are pinned to game time, not frame counts. This keeps travel duration correct regardless of frame rate or TimeScale changes mid-trip.
- **Event journal over state polling**: Investigation gameplay needs to answer "what happened and when." An append-only log with timestamps is the natural data structure for this.
