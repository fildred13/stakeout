# Crime System Design Spec

## Overview

Add the foundational crime system to STAKEOUT. This refactors the NPC behavior model from hardcoded Goals into a general Objective → Task → Action → Trace pipeline, then uses that pipeline to implement a serial killer murder as the first crime template. Debug UI provides crime generation and full observability.

## Core Model

### Objectives

An Objective is a high-level goal an NPC wants to achieve. Objectives live on the Person (`Person.Objectives`).

**Properties:**
- `Id: int`
- `Type: ObjectiveType` enum (e.g., MaintainJob, GetSleep, DefaultIdle, CommitMurder)
- `Source: ObjectiveSource` enum (CoreNeed, Trait, CrimeTemplate, Assignment)
- `SourceEntityId: int?` — reference to the crime/assignment/trait that created this
- `Priority: int` — inherited by Tasks this objective spawns
- `Status: ObjectiveStatus` enum (Active, Completed, Blocked, Cancelled)
- `Steps: List<ObjectiveStep>` — ordered steps for sequential objectives
- `CurrentStepIndex: int` — which step is active
- `IsRecurring: bool` — whether this regenerates Tasks daily (sleep, work) vs. one-shot (murder)
- `Data: Dictionary<string, object>` — flexible storage for step results (e.g., chosen victim ID)

**ObjectiveStep:**
- `Description: string`
- `Status: StepStatus` enum (Pending, Active, Completed, Failed)
- `ActionType: ActionType` — what action to execute for this step
- `ResolveTaskFunc` — logic to generate a Task from this step (may depend on prior step results in `Data`)

### Tasks

A Task is a concrete, schedulable item. Replaces the current `Goal` class. Tasks are what the ScheduleBuilder consumes.

**Properties:**
- `Id: int`
- `ObjectiveId: int` — which objective spawned this
- `StepIndex: int` — which step of that objective
- `ActionType: ActionType` — what the person does during this time slot
- `Priority: int` — for schedule conflict resolution
- `WindowStart: TimeSpan` — time-of-day start
- `WindowEnd: TimeSpan` — time-of-day end
- `TargetAddressId: int?` — where to be (null = current location / home)
- `ActionData: Dictionary<string, object>` — passed to the action when executing

### Actions

An Action is an atomic behavior that a person performs. Actions replace the current `ActivityType` enum. A Person's `CurrentAction` is what they're physically doing right now.

**ActionType enum:**
- `Idle` — at home, doing nothing (replaces `AtHome`, priority 10 default filler)
- `Work` — working at job address (replaces `Working`)
- `Sleep` — sleeping at home (replaces `Sleeping`)
- `TravelByCar` — in transit between locations (replaces `TravellingByCar`)
- `KillPerson` — committing murder at target location
- `ChooseVictim` — internal/instant: pick a target (no scheduled duration)

Actions know:
- Where they happen (derived from Task.TargetAddressId)
- What traces they produce (defined per ActionType)
- What world-state changes they cause (e.g., KillPerson sets victim's IsAlive = false)

**Action execution:** When PersonBehavior detects a Task is active and the person is at the right location, it calls the action's Execute method. The action modifies world state and returns a list of Traces to record. Some actions are instant (ChooseVictim completes immediately and advances the objective) while others have duration (KillPerson occupies a time window).

### Traces

A Trace is an observable artifact left by an Action. Stored in `SimulationState.Traces`.

**Properties:**
- `Id: int`
- `TraceType: TraceType` enum (Item, Sighting, Mark, Condition, Record)
- `CreatedAt: DateTime`
- `CreatedByPersonId: int` — who caused this trace
- `LocationId: int?` — where it exists (for Items, Marks)
- `AttachedToPersonId: int?` — who it's on (for Conditions)
- `Description: string` — human-readable label
- `Data: Dictionary<string, object>` — flexible payload (cause of death, evidence details, etc.)

**For the serial killer prototype, actions produce:**
- `KillPerson` → Condition on victim (cause of death), Mark at location (crime scene evidence)

The five trace categories (Item, Sighting, Mark, Condition, Record) are defined now. Only Condition and Mark are produced in this iteration. The others exist as enum values ready for future use.

## Person & Entity Changes

### Person

**New fields:**
- `bool IsAlive` (default true) — when false, PersonBehavior skips this person entirely
- `List<Objective> Objectives` — all active and completed objectives
- `DailySchedule Schedule` — moved from SimulationManager's external `_schedules` dictionary onto the Person

**Removed external state:**
- `SimulationManager._schedules` dictionary is eliminated; schedules live on Person

**CurrentActivity → CurrentAction:**
- `Person.CurrentActivity` (ActivityType) becomes `Person.CurrentAction` (ActionType)
- All references throughout the codebase update accordingly

### SimulationState

**New collections:**
- `Dictionary<int, Crime> Crimes` — active and completed crimes
- `Dictionary<int, Trace> Traces` — all traces in the world

### Crime

**Properties:**
- `Id: int`
- `TemplateType: CrimeTemplateType` enum (SerialKiller, plus future values)
- `CreatedAt: DateTime`
- `Status: CrimeStatus` enum (InProgress, Completed, Failed)
- `Roles: Dictionary<string, int>` — role name → person ID (e.g., "Killer" → 7, "Victim" → 3)
- `RelatedTraceIds: List<int>` — all traces produced by this crime
- `ObjectiveIds: List<int>` — objectives injected by this crime

### SimulationEventType

**New values:**
- `PersonDied`
- `CrimeCommitted`
- `ObjectiveStarted`
- `ObjectiveCompleted`
- `TaskStarted`
- `TaskCompleted`

## Schedule Building Refactoring

### Current flow (being replaced)
1. `PersonGenerator` creates Person
2. `GoalSetBuilder.Build(job, sleepTime, wakeTime)` → `GoalSet` (3 hardcoded goals)
3. `ScheduleBuilder.Build(goalSet, home, work, mapConfig)` → `DailySchedule`
4. Schedule stored in `SimulationManager._schedules[personId]`
5. `PersonBehavior.Update()` reads schedule, transitions person

### New flow
1. `PersonGenerator` creates Person with CoreNeed Objectives (MaintainJob, GetSleep, DefaultIdle)
2. `ObjectiveResolver.ResolveTasks(person, state)` — iterates Objectives, generates current Tasks
3. `ScheduleBuilder.Build(tasks, addresses, mapConfig)` — same priority-based minute-by-minute resolution, but takes Tasks instead of GoalSet. Each Task carries its own TargetAddressId, so address resolution is data-driven instead of hardcoded.
4. Schedule stored on `person.Schedule`
5. `PersonBehavior.Update()` reads schedule, transitions person, **executes actions on task boundaries** (new)

### Schedule recalculation
When `Person.Objectives` changes (new objective added, step completes, objective cancelled):
1. Re-run `ObjectiveResolver.ResolveTasks()` to get updated Task list
2. Re-run `ScheduleBuilder.Build()` with new Tasks
3. Store new schedule on Person
4. PersonBehavior detects mismatch on next `Update()` and transitions

### ScheduleBuilder changes
- Input changes from `GoalSet` to `List<Task>`
- `GetWinningGoal()` → `GetWinningTask()` — same priority resolution logic
- `GetAddressForGoal()` eliminated — each Task has its own `TargetAddressId`; null means home
- `GoalTypeToActivity()` eliminated — each Task has its own `ActionType`
- Travel insertion logic stays the same, just reads addresses from Tasks instead of hardcoded home/work mapping

## Crime Template System

### Structure
Templates are C# classes implementing a common interface. Each template defines roles, casting logic, and the objective tree for each role.

```
ICrimeTemplate
  - CrimeTemplateType Type
  - string Name
  - Crime Instantiate(SimulationState state) — picks NPCs, creates Crime record, injects Objectives
```

### SerialKiller Template

**Casting:** Pick one random NPC as Killer (must be alive).

**Objective tree for Killer:**
```
Objective: CommitMurder
  Source: CrimeTemplate
  Priority: 40
  Sequential steps:
    1. ChooseVictim
       - Action: ChooseVictim (instant)
       - Picks a random alive NPC (not self) as victim
       - Stores victimId in Objective.Data
       - Immediately advances to step 2
    2. TravelToVictimHome
       - Action: TravelByCar
       - Task: be at victim's HomeAddressId at 1:00 AM, priority 40
       - Completes when person arrives at destination
    3. KillVictim
       - Action: KillPerson
       - Task: at victim's home, 1:00-1:30 AM, priority 40
       - Execution: sets victim.IsAlive = false, produces Traces
       - Produces: Condition trace (cause of death on victim), Mark trace (crime scene at address)
    4. ReturnHome
       - Action: TravelByCar
       - Task: go to killer's home, ~1:30 AM
       - Completes when person arrives home
       - On completion: Objective status → Completed
```

**Crime record created on instantiation:**
- Roles: { "Killer": killerId, "Victim": victimId }  (victim assigned after step 1)
- Status: InProgress → Completed when step 4 finishes

## Debug UI

### Crime Generator (in existing debug sidebar)

Added to the top of the debug sidebar panel:
- Section header: "— Crime Generator —"
- Dropdown or label: template name ("Serial Killer")
- "Generate Now" button
- Result label: shows outcome ("Viktor → murder → Sarah. Crime in progress.") or "No crime active"

**On click:** calls `CrimeGenerator.Generate(selectedTemplate, state)` which instantiates the template, returns the Crime record, and updates the result label.

### City View Tooltip Enhancement

Current: shows address info and occupant names on hover.

**Enhanced person tooltips:**
- `"John Smith: Sleep"` — action name for stationary actions
- `"John Smith: TravelByCar → 123 Fantasy Ln"` — travel shows destination address
- `"John Smith: Work at Joe's Diner"` — work shows workplace name/address
- `"Sarah Jones: Dead"` — for dead NPCs

**Dead NPC dots:** rendered in red (Color(1, 0, 0)) instead of the current white/grey.

### Person Inspector Dialog

Clicking a person name in the debug sidebar opens a `Window` node (Godot built-in modal dialog) displaying:

- **Identity:** Name, ID, IsAlive status
- **Location:** Current address name/number or "In transit to [address]", position coordinates
- **Current State:** Current action, current task description
- **Job:** Type, title, work address, shift times
- **Objectives:** Each objective listed with:
  - Type, source, priority, status
  - Steps with completion state (checkmarks for done, arrow for current)
- **Schedule:** Today's entries as a list: "[08:00-09:00] TravelByCar → Joe's Diner" / "[09:00-17:00] Work"
- **Recent Events:** Last 10 event journal entries for this person, with timestamps

Implemented as a scrollable VBoxContainer inside a Window node. Text-based, using Labels. No rich formatting needed — this is a debug tool.

## File Organization

### New files
```
src/simulation/objectives/
  Objective.cs          — Objective, ObjectiveStep, ObjectiveType, ObjectiveSource, ObjectiveStatus
  ObjectiveResolver.cs  — decomposes Objectives into Tasks
  Task.cs               — Task class (replaces Goal)

src/simulation/actions/
  ActionType.cs         — ActionType enum (replaces ActivityType)
  ActionExecutor.cs     — executes actions, produces traces, modifies world state

src/simulation/crimes/
  Crime.cs              — Crime record, CrimeStatus, CrimeTemplateType
  ICrimeTemplate.cs     — interface for crime templates
  SerialKillerTemplate.cs — first template implementation
  CrimeGenerator.cs     — instantiates templates, wired to debug UI

src/simulation/traces/
  Trace.cs              — Trace class, TraceType enum
```

### Modified files
```
src/simulation/entities/Person.cs          — add IsAlive, Objectives, Schedule, CurrentAction
src/simulation/SimulationState.cs          — add Crimes, Traces dictionaries
src/simulation/SimulationManager.cs        — remove _schedules, add schedule rebuild logic
src/simulation/PersonGenerator.cs          — create CoreNeed Objectives instead of GoalSet
src/simulation/scheduling/ScheduleBuilder.cs — take Tasks instead of GoalSet
src/simulation/scheduling/PersonBehavior.cs  — execute actions, handle dead NPCs
src/simulation/events/SimulationEvent.cs   — new event types, ActionType fields
scenes/game_shell/GameShell.cs             — crime generator UI, person inspector dialog
scenes/city/CityView.cs                    — enhanced tooltips, dead NPC rendering
```

### Removed/replaced files
```
src/simulation/scheduling/Goal.cs          — replaced by objectives/Task.cs and objectives/Objective.cs
src/simulation/entities/ActivityType.cs    — replaced by actions/ActionType.cs
```

### Preserved files (unchanged or minimal changes)
```
src/simulation/scheduling/DailySchedule.cs        — ScheduleEntry updates ActionType reference
src/simulation/scheduling/SleepScheduleCalculator.cs — unchanged (still computes sleep times)
src/simulation/GameClock.cs                        — unchanged
src/simulation/MapConfig.cs                        — unchanged
src/simulation/LocationGenerator.cs                — unchanged
```

## What's NOT in Scope

- Item lifecycle (inventory, trash cans, decay)
- Sighting generation (NPCs perceiving nearby events)
- Traits as objective sources
- Assignments between NPCs
- Non-crime objectives (bar visits, vacations)
- Data-driven template files (JSON/YAML)
- Player-facing investigation UI
- Multiple crimes running simultaneously
- Crime template options/parameters in the generator UI
