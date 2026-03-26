# Benchmark Improvements Design

## Problem

The current benchmark simulates 24 game-hours for each NPC count (50, 200, 500, 1000) sequentially. At higher NPC counts, this means 86,400 iterations with no output until completion — appearing to hang. There's no way to get quick frame-budget feedback or to observe how load changes over the course of a simulated day.

## Design

Split the benchmark into two modes that run sequentially in a single invocation.

### Mode 1: Frame Budget Test

**Purpose:** Answer "can the game maintain 60 FPS at N NPCs?" quickly.

- Runs for every NPC count in the array (50, 200, 500, 1000)
- Simulates 5 game-minutes (300 ticks at 1-second delta, representing ~60x fast-forward)
- Starts at the default clock time (08:30), which is a transition-heavy period (NPCs waking, starting travel)
- Prints one summary row per NPC count with: avg ms/tick, max ms/tick, ticks/sec, memory delta

**Output format:**
```
Frame Budget (5 game-minutes, 1s tick delta)
------------------------------------------------------------
NPCs     Avg ms/tick    Max ms/tick    Ticks/sec    Memory MB
------------------------------------------------------------
50       ...
200      ...
500      ...
1000     ...
```

### Mode 2: Day Profile

**Purpose:** Show how simulation load varies hour-by-hour over a full day, revealing degradation from event/trace accumulation.

- Runs for a single NPC count (default 200, overridable via CLI arg)
- Simulates a full 24-hour day (86,400 ticks at 1-second delta)
- Prints one metrics row per game-hour as it completes (streaming output)
- Each row shows: avg ms/tick for that hour, max ms/tick for that hour, event count for that hour, cumulative memory
- Prints a totals row at the end

The Day Profile resets the game clock to midnight (00:00) so that hour labels correspond to in-game wall-clock time. This differs from the frame budget test which uses the default 08:30 start.

**Output format:**
```
Day Profile (200 NPCs, 24 game-hours, 1s tick delta)
------------------------------------------------------------
Hour     Avg ms/tick    Max ms/tick    Events       Memory MB
------------------------------------------------------------
00:00    ...
01:00    ...
...
23:00    ...
------------------------------------------------------------
Total    ...
```

The "Events" column counts EventJournal entries appended during that hour, measured by snapshotting `journal.AllEvents.Count` before and after each hourly batch. This correlates event accumulation with performance changes.

### CLI Interface

```
dotnet run --project stakeout.benchmarks/ -c Release           # frame budget all counts + day profile at 200
dotnet run --project stakeout.benchmarks/ -c Release -- 500    # day profile at 500 instead
```

The first positional argument overrides the day profile NPC count. Frame budget always runs all counts.

## Implementation

This is a single-file change to `stakeout.benchmarks/Program.cs`:

1. Parse optional CLI arg for day profile NPC count
2. `RunFrameBudget(int npcCount)` — creates simulation state, runs 300 ticks, prints summary row
3. `RunDayProfile(int npcCount, int simHours)` — creates simulation state, runs full day, prints row per hour
4. `Main` calls frame budget for each count, then day profile once

Shared setup logic (creating SimulationState, generators, NPCs) can be extracted to a helper method.

## Simulation Fidelity

Both modes use a 1-second tick delta, matching ~60x fast-forward gameplay. This preserves:
- Per-frame schedule lookup overhead (happens every tick even when nothing changes)
- Travel interpolation on every tick during travel
- Transition detection and event logging at realistic frequency

No sampling or tick-skipping is used — every tick is simulated and measured.

## Key Metrics

| Metric | What it measures |
|--------|-----------------|
| Avg ms/tick | Typical per-frame cost at this NPC count |
| Max ms/tick | Worst-case frame (schedule transitions, travel starts) |
| Ticks/sec | Inverse of avg — how many frames per second the sim alone could sustain |
| Events | Journal entries per hour — proxy for accumulation pressure |
| Memory MB | Heap growth — tracks whether state is leaking or accumulating |
