# Day Planner Overhaul — Design Spec

## Problem

`NpcBrain.PlanDay` frames scheduling as a `TimeSpan` window from `wakeTime` to `sleepTime`. This breaks for NPCs whose waking hours cross midnight (night-shift workers with `wakeTime > sleepTime`):

1. `FindSlot` clamps activity windows to `[wakeTime, sleepTime]`, which produces a negative range when wrapped — no activities can be scheduled.
2. Gap-filling (`currentSlotTime < sleepTime`) is false when wakeTime > sleepTime — no idle entries are added.
3. The plan contains only a sleep entry. On plan exhaustion, replanning produces the identical sleep-only plan. The NPC sleeps forever.

Secondary issue: `DayPlanEntry` uses `TimeSpan` for start/end times, which can't cleanly represent entries that cross midnight.

## Design

### Planning Model

The planner answers: **"What should this person do for the next 24 hours?"**

```
PlanDay(person, state, DateTime now) → DayPlan
```

- **Planning horizon** is `[now, now + 24 hours]`. Fixed window, no special boundaries.
- **All objectives compete equally by priority**, including sleep. There is no structural special-casing of sleep — it's an objective at priority 80 like any other.
- **DayPlanEntry times are `DateTime`** (absolute moments). No wrapping ambiguity. A night-shift worker's plan naturally crosses midnight without special handling.
- **Plan exhaustion triggers replan.** When the last entry completes, `SimulationManager` calls `PlanDay` with the new current time.
- **The planner is a pure function** of `(person, time, state)`. No side effects on the person. This supports future backfill: call repeatedly with historical timestamps, fast-forward the resulting plans for trace generation.

### Planning Algorithm

```
PlanDay(person, state, DateTime now) → DayPlan:

  planStart = now
  planEnd = now + 24 hours

  sort person's objectives by priority (descending)

  scheduled = []
  for each objective:
    actions = objective.GetActions(person, state, planStart, planEnd)
    for each action:
      slot = FindSlot(scheduled, action.TimeWindow, action.Duration + travelTime)
      if slot exists:
        scheduled.Add(slot, action)

  sort scheduled by start time
  fill gaps with IdleAtHome
  return plan
```

`FindSlot` works entirely in `DateTime` — find a gap in the scheduled list where the action fits. No clamping to wake/sleep boundaries. Straightforward comparisons.

### Example Plans

**Day-shift worker** (wakes 06:00, sleeps 22:00), plan created at 06:00:
```
2026-03-29 06:00  Idle at home           → 2026-03-29 12:00
2026-03-29 12:00  Eat out at Mario's     → 2026-03-29 12:30
2026-03-29 12:30  Go for a run           → 2026-03-29 13:15
2026-03-29 13:15  Idle at home           → 2026-03-29 22:00
2026-03-29 22:00  Sleep                  → 2026-03-30 06:00
```

**Night-shift worker** (wakes 15:30, sleeps 07:30), plan created at 15:30:
```
2026-03-29 15:30  Idle at home           → 2026-03-29 18:00
2026-03-29 18:00  Eat out at Mario's     → 2026-03-29 18:30
2026-03-29 18:30  Idle at home           → 2026-03-30 07:30
2026-03-30 07:30  Sleep                  → 2026-03-30 15:30
```

**Night-shift worker**, plan created at 08:30 (game start, mid-sleep):
```
2026-03-29 08:30  Sleep (remaining 7h)   → 2026-03-29 15:30
2026-03-29 15:30  Idle at home           → 2026-03-30 07:30
2026-03-30 07:30  Sleep                  → 2026-03-30 08:30
```

### Sleep Handling

Sleep is not structurally special. `SleepObjective` (priority 80) competes with all other objectives:

- It finds the next occurrence of `PreferredSleepTime` within `[planStart, planEnd]` and returns a sleep action at that time.
- **Currently sleeping edge case:** If `planStart` falls within the sleep window (between `PreferredSleepTime` and `PreferredWakeTime`), it returns a sleep action starting at `planStart` for the remaining duration.
- Higher-priority objectives (e.g., priority-100 crime) can claim time that overlaps the sleep window. The crime template is responsible for any recovery behavior (e.g., scheduling post-heist rest).
- For P3's scope, no objective outranks sleep, so this case cannot occur yet.

### Objective Interface

```csharp
// Old
List<PlannedAction> GetActionsForToday(Person person, SimulationState state, DateTime currentDate)

// New
List<PlannedAction> GetActions(Person person, SimulationState state, DateTime planStart, DateTime planEnd)
```

Objectives receive the concrete planning window as `DateTime` and return actions with `DateTime` time windows. They schedule relative to the window rather than wall-clock hours:

- **SleepObjective** — positions sleep at next preferred sleep time, or immediately if mid-sleep.
- **EatOutObjective** — picks a time relative to the waking portion of the window.
- **GoForARunObjective** — picks a time relative to the waking portion of the window.

### PlannedAction Changes

```csharp
// Old
TimeSpan TimeWindowStart
TimeSpan TimeWindowEnd
TimeSpan Duration

// New
DateTime TimeWindowStart
DateTime TimeWindowEnd
TimeSpan Duration          // stays TimeSpan — it's a length, not a moment
```

### DayPlanEntry Changes

```csharp
// Old
TimeSpan StartTime
TimeSpan EndTime

// New
DateTime StartTime
DateTime EndTime
```

## Scope of Changes

**Rewritten:**
- `NpcBrain.PlanDay` — new algorithm with 24-hour DateTime window
- `NpcBrain.FindSlot` — simplified, all DateTime parameters
- `SleepObjective.GetActions` — sleep positioning logic
- `EatOutObjective.GetActions` — DateTime-relative scheduling
- `GoForARunObjective.GetActions` — DateTime-relative scheduling

**Modified:**
- `DayPlanEntry` — `StartTime`/`EndTime` from `TimeSpan` to `DateTime`
- `PlannedAction` — `TimeWindowStart`/`TimeWindowEnd` from `TimeSpan` to `DateTime`
- `Objective` base class — method signature change
- Debug inspector — display `DateTime` instead of `TimeSpan`

**Unchanged:**
- `ActionRunner` — executes sequentially, doesn't use entry times
- `SimulationManager` — replan trigger logic identical
- `WaitAction`, `MoveToAction` — duration-based, unaffected
- `ActionSequence`, `ActionContext` — unaffected
- Trait system, `PersonGenerator` — unaffected

**Tests:**
- Update existing NpcBrain and objective tests for new signatures
- Add test: night-shift NPC gets a valid plan with activities and sleep
- Add test: plan created mid-sleep starts with remaining sleep, then waking activities
- Add test: objectives respect priority ordering across the 24-hour window

## Backfill Compatibility

This design supports future history backfill (P6). The planner is a pure function — call it with a historical `DateTime`, execute the resulting plan in fast-forward for trace generation. Repeated calls with successive timestamps produce a multi-day history. Batch-spawned NPCs can coordinate with each other while blacklisting coordination with pre-existing NPCs.
