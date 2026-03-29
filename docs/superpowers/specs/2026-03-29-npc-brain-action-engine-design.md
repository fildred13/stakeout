# Project 3: NPC Brain & Action Engine — Design Spec

## Overview

Replace the old pre-computed 1440-slot daily schedule with a reactive, objective-driven NPC brain. NPCs plan their day each morning by evaluating objectives in priority order, then execute actions throughout the day. The system is built for composition — simple hobby objectives and complex multi-week crime plots use the same machinery.

## Core Model

Three layers, cleanly separated:

```
Objectives (persistent, stateful, cross-day)
  └── produce → Actions (concrete, schedulable, ephemeral)
       └── scheduled into → DayPlan (ordered list for today)
            └── executed by → ActionRunner (per-frame tick loop)
```

## Objectives

An objective is a persistent goal with internal state. It can span days or weeks. It is never something an NPC "does" — it's something they're *pursuing*. The things they *do* are the actions it generates.

### Interface

Every objective implements:

- `Priority` — integer, higher = scheduled first. Determines scheduling order in the daily planner.
- `GetActionsForToday(person, state, currentDate)` — returns zero or more actions to schedule today.
- `OnActionCompleted(action, outcome)` — callback when a spawned action finishes. The objective can advance its internal state.
- `Status` — Active, Completed, Failed.
- `Source` — where the objective came from (Universal, Trait, Job, Crime).

### Priority Bands

```
100  Crime objectives (kill target, execute heist)
 80  Sleep
 60  Work shifts (Project 4)
 40  Eat meals
 20  Hobby drives (go for a run, go to church)
  0  IdleAtHome (implicit gap-filler, not a real objective)
```

### Objective Composition

Objectives can have child objectives. A parent delegates a phase to a child. The child produces daily actions independently. The parent monitors the child's status and advances when it completes.

Multiple children can be active in parallel — e.g., a heist planner scouting a target in the morning and meeting a crew candidate in the evening, both from the same parent HeistObjective.

Example composition for future crime projects (not built in P3, but the infrastructure supports it):

```
HeistObjective
  ├── ScoutLocation (reusable) — "visit target, observe"
  ├── RecruitCrew (reusable) — "find and recruit for each role"
  │     └── FindCandidate (reusable per slot)
  ├── PlanRoute — "plan at hideout"
  ├── ExecuteHeist — group action (P5)
  └── DepositFunds — "deposit at bank"
```

### P3 Objective Library

- **SleepObjective** (Universal, priority 80) — uses `SleepScheduleCalculator` for time window.
- **EatOutObjective** (Trait: foodie, priority 40) — finds a restaurant, ~30min meal.
- **GoForARunObjective** (Trait: runner, priority 20) — finds nearest park, ~45min.
- **IdleAtHome** — not a real objective. The brain uses this as the gap-filler when no objective claims a time slot.

## Actions

An action is a concrete, schedulable activity. "Be at this place, do this thing, for this long." Actions go into the day plan and get executed by the action runner.

### Interface (IAction)

```csharp
string Name { get; }
string DisplayText { get; }           // "running on the trails"
ActionStatus Tick(ActionContext ctx, TimeSpan delta);
void OnStart(ActionContext ctx);
void OnComplete(ActionContext ctx);
void OnSuspend(ActionContext ctx);     // pushed down by interrupt (future)
void OnResume(ActionContext ctx);      // back on top after interrupt resolves (future)
```

`ActionStatus`: Running, Completed, Failed.

### ActionContext

State bag threaded through all action methods:

```csharp
Person Person
SimulationState State
TraceEmitter TraceEmitter
EventJournal EventJournal
Random Random
DateTime CurrentTime
```

### ActionSequence Builder

Fluent API for composing primitives into multi-step activities:

```csharp
ActionSequence.Create("BreakInAndKill")
    .Do(ForcedEntry(backDoor))
    .MoveTo(victimBedroom)
    .Do(Violence(victim))
    .Build();
```

Builder methods: `.Do(action)`, `.MoveTo(location)`, `.Wait(duration)`, `.Maybe(probability, action)`, `.If(condition, action)`.

For P3's simple objectives, actions are typically just a single `Wait` with a display string. The builder infrastructure exists for future complex actions.

### Event Primitives (P3 scope)

- **WaitAction** — stay at current location for a duration. Returns Completed when time elapses.
- **MoveToAction** — intra-address movement between locations/sublocations.

ForcedEntry, Violence, Social, Search are deferred to P5+/P7 when crime actions need them.

### Separation: Travel vs. Activity

Inter-address travel is the **engine's responsibility**, not the action's. Every action resolves to a destination + activity. The engine handles getting the NPC there:

```
Action completes → brain advances to next plan entry →
  engine checks: NPC at destination? → no: travel → yes: start activity
```

This means action templates are pure activity definitions — no movement boilerplate. "Go for a run" is just "be at the park, run for 45 minutes." The engine wraps it in travel.

## NPC Brain (Daily Planner)

The brain runs at specific moments, not per-frame. It produces a DayPlan — an ordered list of actions with time slots.

### When Planning Happens

- **On wake-up** — plan the full day.
- **After an interrupt resolves** (future projects) — re-plan remaining day.

### Planning Algorithm

```
PlanDay(person, currentTime):
  plan = empty timeline

  sort person's objectives by priority (descending)
  for each objective:
    actions = objective.GetActionsForToday(person, today)
    for each action:
      slot = FindSlot(plan, action.TimeWindow, action.Duration + travelTime)
      if slot exists:
        plan.Insert(action, slot)
      // no slot → this objective gets skipped today

  FillGaps(plan, IdleAtHome)
  return plan
```

One algorithm. Everything is an objective with a priority. A runner's hobby and a killer's murder use the same planner — they differ in priority, not in kind.

### DayPlan

An ordered list of time-slotted entries with a current-index tracking which entry is active:

```
06:00 - 06:30  EatOut at Mario's Diner
07:00 - 07:45  GoForARun at Central Park
12:00 - 12:30  EatOut at Mario's Diner
22:00 - 06:00  Sleep at 42 Maple St
       gaps    IdleAtHome at 42 Maple St
```

Each entry: start time, end time, action, target address, display text.

## Action Runner

The per-frame loop that executes the day plan.

```
ActionRunner.Tick(person, delta):
  if person is traveling:
    UpdateTravel(person, delta)
    if arrived:
      LogArrivalEvent()
      StartCurrentActivity()
    return

  if person has active activity:
    status = activity.Tick(context, delta)
    if status == Completed:
      ReportOutcomeToObjective()
      AdvanceToNextPlanEntry()
    return

  // No activity, not traveling — start next plan entry
  nextEntry = person.DayPlan.Next()
  if nextEntry requires travel:
    BeginTravel(person, nextEntry.TargetAddressId)
  else:
    StartActivity(nextEntry)
```

Travel reuses existing infrastructure: `MapConfig` for travel time calculation, `TravelInfo` on Person for state, position interpolation already in `SimulationManager`.

## Traits

Traits are strings on the Person entity ("runner", "foodie"). At generation time, traits map to objectives:

```
TraitDefinitions registry:
  "runner"  → assign GoForARunObjective (priority 20)
  "foodie"  → assign EatOutObjective (priority 40)
```

P3 ships with these two. Future projects add more traits and corresponding objectives.

## Person Entity Changes

### Remove
- `DailySchedule Schedule` — replaced by DayPlan
- `List<Objective> Objectives` — replaced by new Objective list
- `ActionType CurrentAction` — replaced by IAction CurrentActivity
- `bool NeedsScheduleRebuild`

### Add
- `DayPlan DayPlan` — today's ordered action list
- `IAction CurrentActivity` — what they're doing right now (null if traveling)
- `List<string> Traits` — for drive/objective mapping
- `List<Objective> Objectives` — new objective system (reuses field name, new type)

### Keep
- `TravelInfo` — used by action runner for inter-address travel
- `CurrentAddressId`, `CurrentLocationId`, `CurrentSubLocationId`, `CurrentPosition` — runtime location state
- `PreferredSleepTime`, `PreferredWakeTime` — used by SleepObjective
- `InventoryItemIds` — future objectives may check inventory
- `IsAlive` — action runner skips dead NPCs

## Event Log

Extend `SimulationEvent` with new event types:
- `ActivityStarted` — logged when an action begins
- `ActivityCompleted` — logged when an action finishes

Objectives can query the journal to check state (e.g., "when did I last eat?" to avoid scheduling meals too close together).

## Trace Integration

P2's `TraceEmitter` API is ready. P3's simple actions emit minimal traces:
- **GoForARun**: sighting trace at the park
- **EatOut**: sighting trace at the diner, receipt trace in trash fixture
- **Sleep / IdleAtHome**: no traces (residency traces come from backfill in P6)

## Debug Inspector Updates

The person inspector in GameShell.cs updates to reflect the new model:

**"— Current State —" becomes:**
```
— Current State —
Activity: running on the trails (GoForARun)
Status: Traveling to Central Park [ETA: 12:15]
  -or-
Status: Active [28min remaining]
```

**"— Objectives —" becomes:**
```
— Objectives —
[P80] Sleep (Universal) — Active
[P40] EatOut (Trait: foodie) — Active
[P20] GoForARun (Trait: runner) — Active, executing
  └─ state: phase=Running, park=Central Park
```

**"— Schedule —" becomes:**
```
— Day Plan —
06:00 - 06:30  EatOut at Mario's Diner                          ✓
07:00 - 07:45  GoForARun at Central Park                        ← current
12:00 - 12:30  EatOut at Mario's Diner
22:00 - 06:00  Sleep at Home
       gaps    IdleAtHome
```

**"— Job —" section:** Comment out with `TODO: Project 4`.

## Files Removed

- `scheduling/ScheduleBuilder.cs`
- `scheduling/DailySchedule.cs`
- `scheduling/TaskResolver.cs`
- `scheduling/PersonBehavior.cs` (replaced by ActionRunner)
- `scheduling/decomposition/` (entire directory)
- `objectives/ObjectiveResolver.cs`
- `objectives/Task.cs` (SimTask)

## Files Gutted (TODO markers)

- `scheduling/DoorLockingService.cs` → `TODO: Project 4`
- `PersonGenerator.cs` → comment out job assignment, `TODO: Project 4`

## New Files

```
src/simulation/
  brain/
    NpcBrain.cs              — PlanDay algorithm
    DayPlan.cs               — Ordered action list with time slots

  objectives/
    Objective.cs             — Base class: priority, state, phases, children, GetActionsForToday()
    SleepObjective.cs        — Universal, priority 80
    IdleAtHomeObjective.cs   — Gap-filler, priority 0
    GoForARunObjective.cs    — Trait: runner, priority 20
    EatOutObjective.cs       — Trait: foodie, priority 40
    ObjectiveSource.cs       — Enum: Universal, Trait, Job, Crime

  actions/
    IAction.cs               — Interface: Tick, OnStart, OnComplete, DisplayText
    ActionSequence.cs        — Fluent builder + sequential step executor
    ActionRunner.cs          — Per-frame tick loop
    ActionContext.cs         — State bag
    ActionStatus.cs          — Enum: Running, Completed, Failed
    primitives/
      WaitAction.cs          — Stay for duration
      MoveToAction.cs        — Intra-address movement

  traits/
    TraitDefinitions.cs      — Registry: trait name → objectives to assign
```

## Modified Files

```
  entities/Person.cs         — Remove old fields, add DayPlan/CurrentActivity/Traits
  SimulationManager.cs       — Rewire to ActionRunner, trigger PlanDay on wake-up
  PersonGenerator.cs         — Assign traits, create objectives, gut job assignment
  events/SimulationEvent.cs  — Add ActivityStarted, ActivityCompleted
  scenes/game_shell/GameShell.cs — Update debug inspector sections
```

## Deferred to Future Projects

- **Project 4**: WorkShift/Commute actions, Business entities, job obligation integration, DoorLockingService, job assignment in PersonGenerator
- **Project 5**: Group actions, Split/Regroup, OnSuspend/OnResume for interrupts, re-planning after interrupts
- **Project 6**: History backfill (residency traces, routine-based trace generation)
- **Project 7**: Crime objectives (HeistObjective, ScoutLocation, RecruitCrew), crime event primitives (ForcedEntry, Violence, Social, Search), crime trace emission
- **Fast-forward optimization**: Deferred until performance requires it. All NPCs use the same action stack path for now.
