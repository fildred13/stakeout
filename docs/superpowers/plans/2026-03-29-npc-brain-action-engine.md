# NPC Brain & Action Engine Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> **CRITICAL: Never prefix shell commands with `cd`. The working directory is already the project root. Run commands directly (e.g., `git add file.cs`, not `cd path && git add file.cs`). This breaks permission matching and is strictly prohibited.**

**Goal:** Replace the old pre-computed 1440-slot daily schedule with an objective-driven NPC brain that plans each day from prioritized objectives, then executes actions via an action runner.

**Architecture:** Everything is an objective with a priority. On wake-up, the brain sorts objectives by priority and greedily schedules their actions into a DayPlan. The ActionRunner ticks the plan each frame, handling inter-address travel automatically. Actions are pure activities at a destination — they never contain travel logic.

**Tech Stack:** C# / .NET 8.0 / Godot 4.6 / xUnit

**Design Spec:** `docs/superpowers/specs/2026-03-29-npc-brain-action-engine-design.md`

---

## File Map

### New Files

| File | Responsibility |
|------|---------------|
| `src/simulation/actions/IAction.cs` | Action interface: Name, DisplayText, Tick, OnStart, OnComplete |
| `src/simulation/actions/ActionStatus.cs` | Enum: Running, Completed, Failed |
| `src/simulation/actions/ActionContext.cs` | State bag: Person, SimulationState, EventJournal, Random, CurrentTime (TraceEmitter is static, called directly) |
| `src/simulation/actions/ActionRunner.cs` | Per-frame tick loop: travel, tick activity, advance plan |
| `src/simulation/actions/ActionSequence.cs` | Fluent builder + sequential step executor implementing IAction |
| `src/simulation/actions/primitives/WaitAction.cs` | Stay at location for a duration |
| `src/simulation/actions/primitives/MoveToAction.cs` | Intra-address movement between locations/sublocations |
| `src/simulation/objectives/Objective.cs` | Rewrite: abstract base class with Priority, GetActionsForToday, OnActionCompleted, Status, children |
| `src/simulation/objectives/ObjectiveStatus.cs` | Enum: Active, Completed, Failed |
| `src/simulation/objectives/ObjectiveSource.cs` | Enum: Universal, Trait, Job, Crime |
| `src/simulation/objectives/PlannedAction.cs` | Data class: IAction + target address + time window + duration + display text |
| `src/simulation/objectives/SleepObjective.cs` | Universal, priority 80, uses SleepScheduleCalculator |
| `src/simulation/objectives/EatOutObjective.cs` | Trait: foodie, priority 40, finds restaurant |
| `src/simulation/objectives/GoForARunObjective.cs` | Trait: runner, priority 20, finds park |
| `src/simulation/brain/NpcBrain.cs` | PlanDay algorithm: sort objectives, schedule greedily, fill gaps with IdleAtHome |
| `src/simulation/brain/DayPlan.cs` | Ordered list of DayPlanEntry with current-index |
| `src/simulation/brain/DayPlanEntry.cs` | Start time, end time, PlannedAction, status (pending/active/completed) |
| `src/simulation/traits/TraitDefinitions.cs` | Static registry: trait name -> list of objective factories |
| `stakeout.tests/Simulation/Actions/WaitActionTests.cs` | Tests for WaitAction |
| `stakeout.tests/Simulation/Actions/ActionSequenceTests.cs` | Tests for ActionSequence builder |
| `stakeout.tests/Simulation/Actions/ActionRunnerTests.cs` | Tests for ActionRunner |
| `stakeout.tests/Simulation/Objectives/SleepObjectiveTests.cs` | Tests for SleepObjective |
| `stakeout.tests/Simulation/Objectives/EatOutObjectiveTests.cs` | Tests for EatOutObjective |
| `stakeout.tests/Simulation/Objectives/GoForARunObjectiveTests.cs` | Tests for GoForARunObjective |
| `stakeout.tests/Simulation/Brain/NpcBrainTests.cs` | Tests for PlanDay algorithm |
| `stakeout.tests/Simulation/Brain/DayPlanTests.cs` | Tests for DayPlan data structure |
| `stakeout.tests/Simulation/Traits/TraitDefinitionsTests.cs` | Tests for trait registry |

### Modified Files

| File | Changes |
|------|---------|
| `src/simulation/entities/Person.cs` | Remove old fields (Schedule, CurrentAction, NeedsScheduleRebuild), add DayPlan, CurrentActivity, Traits, new Objectives list |
| `src/simulation/events/SimulationEvent.cs` | Add ActivityStarted, ActivityCompleted event types; add Description field |
| `src/simulation/SimulationManager.cs` | Rewire _Process to use ActionRunner, trigger PlanDay on wake-up |
| `src/simulation/PersonGenerator.cs` | Assign traits, create objectives, gut job-related scheduling with TODO: Project 4 |
| `scenes/game_shell/GameShell.cs` | Update debug inspector: Current State, Objectives, Day Plan sections; gut Job section |

### Deleted Files

| File | Reason |
|------|--------|
| `src/simulation/scheduling/ScheduleBuilder.cs` | Replaced by NpcBrain.PlanDay |
| `src/simulation/scheduling/DailySchedule.cs` | Replaced by DayPlan |
| `src/simulation/scheduling/TaskResolver.cs` | Replaced by objective -> action pipeline |
| `src/simulation/scheduling/PersonBehavior.cs` | Replaced by ActionRunner |
| `src/simulation/scheduling/decomposition/*` | Entire directory, replaced by objectives |
| `src/simulation/objectives/ObjectiveResolver.cs` | Replaced by NpcBrain |
| `src/simulation/objectives/Task.cs` | SimTask replaced by PlannedAction |
| `src/simulation/actions/ActionExecutor.cs` | Replaced by ActionRunner |
| `src/simulation/actions/ActionType.cs` | Replaced by IAction interface |
| `stakeout.tests/Simulation/Objectives/TaskTests.cs` | Old SimTask tests |

### Gutted Files (TODO markers)

| File | Marker |
|------|--------|
| `src/simulation/scheduling/DoorLockingService.cs` | `TODO: Project 4` |

---

## Task 1: Delete Old Systems & Fix Build

Remove the old scheduling, objective, and action systems. Update all references so the project builds.

**Files:**
- Delete: `src/simulation/scheduling/ScheduleBuilder.cs`, `src/simulation/scheduling/DailySchedule.cs`, `src/simulation/scheduling/TaskResolver.cs`, `src/simulation/scheduling/PersonBehavior.cs`, `src/simulation/scheduling/decomposition/` (entire directory), `src/simulation/objectives/ObjectiveResolver.cs`, `src/simulation/objectives/Task.cs`, `src/simulation/actions/ActionExecutor.cs`, `src/simulation/actions/ActionType.cs`
- Delete: `stakeout.tests/Simulation/Objectives/TaskTests.cs` and any `.uid` files for deleted source files
- Modify: `src/simulation/scheduling/DoorLockingService.cs` (update TODO marker)
- Modify: `src/simulation/entities/Person.cs` (remove old fields, add new ones)
- Modify: `src/simulation/events/SimulationEvent.cs` (remove ActionType references, add new event types)
- Modify: `src/simulation/SimulationManager.cs` (remove commented-out code, gut _Process loop)
- Modify: `src/simulation/PersonGenerator.cs` (remove ActionType/Schedule references, gut job scheduling)
- Modify: `scenes/game_shell/GameShell.cs` (remove Schedule/Objective/ActionType references temporarily)

- [ ] **Step 1: Delete old scheduling files**

Delete these files and their `.uid` sidecars:
```
src/simulation/scheduling/ScheduleBuilder.cs
src/simulation/scheduling/DailySchedule.cs
src/simulation/scheduling/TaskResolver.cs
src/simulation/scheduling/PersonBehavior.cs
src/simulation/scheduling/decomposition/  (entire directory)
```

- [ ] **Step 2: Delete old objective and action files**

Delete these files and their `.uid` sidecars:
```
src/simulation/objectives/ObjectiveResolver.cs
src/simulation/objectives/Task.cs
src/simulation/actions/ActionExecutor.cs
src/simulation/actions/ActionType.cs
stakeout.tests/Simulation/Objectives/TaskTests.cs
```

- [ ] **Step 3: Update DoorLockingService.cs TODO marker**

Replace the file content with:

```csharp
using Stakeout.Simulation.Entities;

namespace Stakeout.Simulation.Scheduling;

// TODO: Project 4 — lock/unlock doors based on business hours and NPC presence
public static class DoorLockingService
{
    public static void LockEntrances(SimulationState state, Person person)
    {
        throw new System.NotImplementedException();
    }

    public static void UnlockEntrances(SimulationState state, Person person)
    {
        throw new System.NotImplementedException();
    }
}
```

- [ ] **Step 4: Create new objective enums and base class (stubs)**

Create `src/simulation/objectives/ObjectiveStatus.cs`:

```csharp
namespace Stakeout.Simulation.Objectives;

public enum ObjectiveStatus
{
    Active,
    Completed,
    Failed
}
```

Create `src/simulation/objectives/ObjectiveSource.cs`:

```csharp
namespace Stakeout.Simulation.Objectives;

public enum ObjectiveSource
{
    Universal,
    Trait,
    Job,
    Crime
}
```

Rewrite `src/simulation/objectives/Objective.cs` — replace entire file:

```csharp
using System;
using System.Collections.Generic;

namespace Stakeout.Simulation.Objectives;

public abstract class Objective
{
    public int Id { get; set; }
    public abstract int Priority { get; }
    public abstract ObjectiveSource Source { get; }
    public ObjectiveStatus Status { get; set; } = ObjectiveStatus.Active;
    public List<Objective> Children { get; } = new();

    public abstract List<PlannedAction> GetActionsForToday(
        Simulation.Entities.Person person,
        SimulationState state,
        DateTime currentDate);

    public virtual void OnActionCompleted(PlannedAction action, bool success) { }

    /// <summary>
    /// Called by ActionRunner after an action completes successfully.
    /// Override to emit traces at the action's location.
    /// </summary>
    public virtual void EmitTraces(PlannedAction action, Person person, SimulationState state) { }
}
```

- [ ] **Step 5: Create action infrastructure stubs**

Create `src/simulation/actions/ActionStatus.cs`:

```csharp
namespace Stakeout.Simulation.Actions;

public enum ActionStatus
{
    Running,
    Completed,
    Failed
}
```

Create `src/simulation/actions/IAction.cs`:

```csharp
using System;

namespace Stakeout.Simulation.Actions;

public interface IAction
{
    string Name { get; }
    string DisplayText { get; }
    ActionStatus Tick(ActionContext ctx, TimeSpan delta);
    void OnStart(ActionContext ctx);
    void OnComplete(ActionContext ctx);
}
```

Create `src/simulation/actions/ActionContext.cs`:

```csharp
using System;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Events;

namespace Stakeout.Simulation.Actions;

// Note: TraceEmitter is a static class. Actions call TraceEmitter.EmitSighting(ctx.State, ...) directly.
public class ActionContext
{
    public Person Person { get; init; }
    public SimulationState State { get; init; }
    public EventJournal EventJournal { get; init; }
    public Random Random { get; init; }
    public DateTime CurrentTime { get; init; }
}
```

Create `src/simulation/objectives/PlannedAction.cs`:

```csharp
using System;
using Stakeout.Simulation.Actions;

namespace Stakeout.Simulation.Objectives;

public class PlannedAction
{
    public IAction Action { get; init; }
    public int TargetAddressId { get; init; }
    public TimeSpan TimeWindowStart { get; init; }
    public TimeSpan TimeWindowEnd { get; init; }
    public TimeSpan Duration { get; init; }
    public string DisplayText { get; init; }
    public Objective SourceObjective { get; init; }
}
```

- [ ] **Step 6: Update Person.cs**

Replace `src/simulation/entities/Person.cs` entirely:

```csharp
using System;
using System.Collections.Generic;
using Godot;
using Stakeout.Simulation.Actions;
using Stakeout.Simulation.Brain;
using Stakeout.Simulation.Objectives;

namespace Stakeout.Simulation.Entities;

public class Person
{
    public int Id { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public DateTime CreatedAt { get; set; }
    public int? CurrentCityId { get; set; }
    public int HomeAddressId { get; set; }
    public int? HomeLocationId { get; set; }
    public int JobId { get; set; }
    public int? CurrentAddressId { get; set; }
    public int? CurrentLocationId { get; set; }
    public int? CurrentSubLocationId { get; set; }
    public Vector2 CurrentPosition { get; set; }
    public TravelInfo TravelInfo { get; set; }
    public TimeSpan PreferredSleepTime { get; set; }
    public TimeSpan PreferredWakeTime { get; set; }
    public bool IsAlive { get; set; } = true;
    public List<int> InventoryItemIds { get; set; } = new();

    // New P3 fields
    public List<Objective> Objectives { get; set; } = new();
    public DayPlan DayPlan { get; set; }
    public IAction CurrentActivity { get; set; }
    public List<string> Traits { get; set; } = new();

    public string FullName => $"{FirstName} {LastName}";
}
```

- [ ] **Step 7: Create DayPlan and DayPlanEntry stubs**

Create `src/simulation/brain/DayPlanEntry.cs`:

```csharp
using System;
using Stakeout.Simulation.Objectives;

namespace Stakeout.Simulation.Brain;

public enum DayPlanEntryStatus
{
    Pending,
    Active,
    Completed,
    Skipped
}

public class DayPlanEntry
{
    public TimeSpan StartTime { get; init; }
    public TimeSpan EndTime { get; init; }
    public PlannedAction PlannedAction { get; init; }
    public DayPlanEntryStatus Status { get; set; } = DayPlanEntryStatus.Pending;
}
```

Create `src/simulation/brain/DayPlan.cs`:

```csharp
using System.Collections.Generic;

namespace Stakeout.Simulation.Brain;

public class DayPlan
{
    public List<DayPlanEntry> Entries { get; } = new();
    public int CurrentIndex { get; set; }

    public DayPlanEntry Current =>
        CurrentIndex < Entries.Count ? Entries[CurrentIndex] : null;

    public DayPlanEntry AdvanceToNext()
    {
        if (CurrentIndex < Entries.Count)
            Entries[CurrentIndex].Status = DayPlanEntryStatus.Completed;
        CurrentIndex++;
        return Current;
    }
}
```

- [ ] **Step 8: Update SimulationEvent.cs**

Replace `src/simulation/events/SimulationEvent.cs`:

```csharp
using System;

namespace Stakeout.Simulation.Events;

public enum SimulationEventType
{
    DepartedAddress,
    ArrivedAtAddress,
    FellAsleep,
    WokeUp,
    PersonDied,
    CrimeCommitted,
    ActivityStarted,
    ActivityCompleted,
    DayPlanned
}

public class SimulationEvent
{
    public DateTime Timestamp { get; set; }
    public int PersonId { get; set; }
    public SimulationEventType EventType { get; set; }
    public int? FromAddressId { get; set; }
    public int? ToAddressId { get; set; }
    public int? AddressId { get; set; }
    public string Description { get; set; }
}
```

- [ ] **Step 9: Update SimulationManager.cs**

Gut the `_Process` loop and `RebuildSchedule`:

```csharp
// In _Process, replace the TODO comments (lines 102-117) with:
// TODO: Project 3 — ActionRunner.Tick will be wired here in Task 6

// Remove the RebuildSchedule method entirely (lines 122-125)
```

Remove the `using Stakeout.Simulation.Scheduling;` import if no longer needed (DoorLockingService is still in that namespace, but SimulationManager doesn't reference it).

- [ ] **Step 10: Update PersonGenerator.cs**

In `PersonGenerator.cs`, update the person creation (around line 69-84):
- Remove `CurrentAction = ActionType.Idle` line
- Remove `using Stakeout.Simulation.Actions;` import
- Keep job creation (still needed for P4), but add a comment: `// TODO: Project 4 — job objectives will be created here`
- Update the journal event to not reference ActionType:

```csharp
state.Journal.Append(new SimulationEvent
{
    Timestamp = state.Clock.CurrentTime,
    PersonId = person.Id,
    EventType = SimulationEventType.ActivityStarted,
    Description = "Spawned"
});
```

- [ ] **Step 11: Update GameShell.cs inspector temporarily**

In `PopulateInspectorContent` (around line 400-510):
- Replace the "Current State" section (lines 434-437) to show `person.CurrentActivity?.DisplayText ?? "idle"` instead of `person.CurrentAction`
- Comment out the "Objectives" section (lines 452-467) with `// TODO: P3 — will be restored with new objective display`
- Comment out the "Schedule" section (lines 469-473) with `// TODO: P3 — will be restored as Day Plan display`
- Comment out the "Job" section (lines 440-450) with `// TODO: Project 4 — will be restored with Business entities`
- Remove any `using Stakeout.Simulation.Actions;` or `using Stakeout.Simulation.Scheduling;` imports that are no longer needed. Keep any that are still referenced.

- [ ] **Step 12: Fix remaining build errors**

Search the entire project for references to deleted types (`ActionType`, `DailySchedule`, `ScheduleBuilder`, `PersonBehavior`, `TaskResolver`, `ObjectiveResolver`, `SimTask`, `ActionExecutor`, `ObjectiveType`, `ObjectiveStep`, `StepStatus`) and fix each one.

Check `stakeout.tests/` for any test files that reference deleted types — update or remove them.

Run: `dotnet build stakeout.csproj`
Run: `dotnet build stakeout.tests/stakeout.tests.csproj`

Expected: Both build with 0 errors.

- [ ] **Step 13: Run all tests**

Run: `dotnet test stakeout.tests/ -v minimal`

Expected: All existing tests pass. Some tests may have been removed alongside deleted files.

- [ ] **Step 14: Commit**

```
git add -A
git commit -m "feat(p3): remove old scheduling system, add action/objective infrastructure stubs"
```

---

## Task 2: WaitAction & Action Primitives

Build the first action primitive and prove the IAction interface works.

**Files:**
- Create: `src/simulation/actions/primitives/WaitAction.cs`
- Test: `stakeout.tests/Simulation/Actions/WaitActionTests.cs`

- [ ] **Step 1: Write failing tests for WaitAction**

Create `stakeout.tests/Simulation/Actions/WaitActionTests.cs`:

```csharp
using System;
using Stakeout.Simulation;
using Stakeout.Simulation.Actions;
using Stakeout.Simulation.Actions.Primitives;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Events;
using Xunit;

namespace Stakeout.Tests.Simulation.Actions;

public class WaitActionTests
{
    private static ActionContext CreateContext(Person person = null)
    {
        var state = new SimulationState();
        person ??= new Person { Id = 1 };
        state.People[person.Id] = person;
        return new ActionContext
        {
            Person = person,
            State = state,
            EventJournal = state.Journal,
            Random = new Random(42),
            CurrentTime = state.Clock.CurrentTime
        };
    }

    [Fact]
    public void WaitAction_HasCorrectName()
    {
        var action = new WaitAction(TimeSpan.FromMinutes(30), "resting");
        Assert.Equal("Wait", action.Name);
    }

    [Fact]
    public void WaitAction_HasCorrectDisplayText()
    {
        var action = new WaitAction(TimeSpan.FromMinutes(30), "running on the trails");
        Assert.Equal("running on the trails", action.DisplayText);
    }

    [Fact]
    public void WaitAction_ReturnsRunning_WhileTimeRemains()
    {
        var action = new WaitAction(TimeSpan.FromMinutes(30), "resting");
        var ctx = CreateContext();
        action.OnStart(ctx);

        var status = action.Tick(ctx, TimeSpan.FromMinutes(10));
        Assert.Equal(ActionStatus.Running, status);
    }

    [Fact]
    public void WaitAction_ReturnsCompleted_WhenTimeElapses()
    {
        var action = new WaitAction(TimeSpan.FromMinutes(30), "resting");
        var ctx = CreateContext();
        action.OnStart(ctx);

        action.Tick(ctx, TimeSpan.FromMinutes(20));
        var status = action.Tick(ctx, TimeSpan.FromMinutes(15));
        Assert.Equal(ActionStatus.Completed, status);
    }

    [Fact]
    public void WaitAction_ReturnsCompleted_OnExactDuration()
    {
        var action = new WaitAction(TimeSpan.FromMinutes(30), "resting");
        var ctx = CreateContext();
        action.OnStart(ctx);

        var status = action.Tick(ctx, TimeSpan.FromMinutes(30));
        Assert.Equal(ActionStatus.Completed, status);
    }

    [Fact]
    public void WaitAction_RemainingTime_DecreasesEachTick()
    {
        var action = new WaitAction(TimeSpan.FromMinutes(30), "resting");
        var ctx = CreateContext();
        action.OnStart(ctx);

        action.Tick(ctx, TimeSpan.FromMinutes(10));
        Assert.Equal(TimeSpan.FromMinutes(20), action.RemainingTime);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test stakeout.tests/ --filter "WaitActionTests" -v minimal`

Expected: FAIL — `WaitAction` class does not exist yet.

- [ ] **Step 3: Implement WaitAction**

Create `src/simulation/actions/primitives/WaitAction.cs`:

```csharp
using System;

namespace Stakeout.Simulation.Actions.Primitives;

public class WaitAction : IAction
{
    private readonly TimeSpan _duration;
    private TimeSpan _elapsed;

    public string Name => "Wait";
    public string DisplayText { get; }
    public TimeSpan RemainingTime => _duration - _elapsed;

    public WaitAction(TimeSpan duration, string displayText)
    {
        _duration = duration;
        DisplayText = displayText;
    }

    public void OnStart(ActionContext ctx) { _elapsed = TimeSpan.Zero; }

    public ActionStatus Tick(ActionContext ctx, TimeSpan delta)
    {
        _elapsed += delta;
        return _elapsed >= _duration ? ActionStatus.Completed : ActionStatus.Running;
    }

    public void OnComplete(ActionContext ctx) { }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test stakeout.tests/ --filter "WaitActionTests" -v minimal`

Expected: All 6 tests PASS.

- [ ] **Step 5: Commit**

```
git add src/simulation/actions/primitives/WaitAction.cs stakeout.tests/Simulation/Actions/WaitActionTests.cs
git commit -m "feat(p3): add WaitAction primitive with tests"
```

- [ ] **Step 6: Write failing tests for MoveToAction**

Add to a new file `stakeout.tests/Simulation/Actions/MoveToActionTests.cs`:

```csharp
using System;
using Stakeout.Simulation;
using Stakeout.Simulation.Actions;
using Stakeout.Simulation.Actions.Primitives;
using Stakeout.Simulation.Entities;
using Xunit;

namespace Stakeout.Tests.Simulation.Actions;

public class MoveToActionTests
{
    private static ActionContext CreateContext(Person person = null)
    {
        var state = new SimulationState();
        person ??= new Person { Id = 1, CurrentLocationId = 10 };
        state.People[person.Id] = person;
        return new ActionContext
        {
            Person = person,
            State = state,
            EventJournal = state.Journal,
            Random = new Random(42),
            CurrentTime = state.Clock.CurrentTime
        };
    }

    [Fact]
    public void MoveToAction_HasCorrectName()
    {
        var action = new MoveToAction(5, null, "heading to bedroom");
        Assert.Equal("MoveTo", action.Name);
    }

    [Fact]
    public void MoveToAction_SetsPersonLocation_OnComplete()
    {
        var person = new Person { Id = 1, CurrentLocationId = 10 };
        var action = new MoveToAction(20, null, "heading to bedroom");
        var ctx = CreateContext(person);
        action.OnStart(ctx);
        action.Tick(ctx, TimeSpan.FromSeconds(1)); // completes immediately (intra-address)
        Assert.Equal(20, person.CurrentLocationId);
    }

    [Fact]
    public void MoveToAction_SetsSubLocation_WhenProvided()
    {
        var person = new Person { Id = 1, CurrentLocationId = 10 };
        var action = new MoveToAction(20, 30, "heading to kitchen");
        var ctx = CreateContext(person);
        action.OnStart(ctx);
        action.Tick(ctx, TimeSpan.FromSeconds(1));
        Assert.Equal(20, person.CurrentLocationId);
        Assert.Equal(30, person.CurrentSubLocationId);
    }

    [Fact]
    public void MoveToAction_CompletesImmediately()
    {
        var action = new MoveToAction(20, null, "moving");
        var ctx = CreateContext();
        action.OnStart(ctx);
        var status = action.Tick(ctx, TimeSpan.FromSeconds(1));
        Assert.Equal(ActionStatus.Completed, status);
    }
}
```

- [ ] **Step 7: Implement MoveToAction**

Create `src/simulation/actions/primitives/MoveToAction.cs`:

```csharp
using System;
using Stakeout.Simulation.Entities;

namespace Stakeout.Simulation.Actions.Primitives;

/// <summary>
/// Intra-address movement — changes the person's current Location/SubLocation.
/// Completes immediately (no travel time for room-to-room movement).
/// </summary>
public class MoveToAction : IAction
{
    private readonly int _targetLocationId;
    private readonly int? _targetSubLocationId;

    public string Name => "MoveTo";
    public string DisplayText { get; }

    public MoveToAction(int targetLocationId, int? targetSubLocationId, string displayText)
    {
        _targetLocationId = targetLocationId;
        _targetSubLocationId = targetSubLocationId;
        DisplayText = displayText;
    }

    public void OnStart(ActionContext ctx) { }

    public ActionStatus Tick(ActionContext ctx, TimeSpan delta)
    {
        ctx.Person.CurrentLocationId = _targetLocationId;
        ctx.Person.CurrentSubLocationId = _targetSubLocationId;
        return ActionStatus.Completed;
    }

    public void OnComplete(ActionContext ctx) { }
}
```

- [ ] **Step 8: Run MoveToAction tests**

Run: `dotnet test stakeout.tests/ --filter "MoveToActionTests" -v minimal`

Expected: All 4 tests PASS.

- [ ] **Step 9: Commit**

```
git add src/simulation/actions/primitives/MoveToAction.cs stakeout.tests/Simulation/Actions/MoveToActionTests.cs
git commit -m "feat(p3): add MoveToAction primitive for intra-address movement"
```

---

## Task 3: ActionSequence Builder

Build the fluent API for composing multi-step actions.

**Files:**
- Create: `src/simulation/actions/ActionSequence.cs`
- Test: `stakeout.tests/Simulation/Actions/ActionSequenceTests.cs`

- [ ] **Step 1: Write failing tests for ActionSequence**

Create `stakeout.tests/Simulation/Actions/ActionSequenceTests.cs`:

```csharp
using System;
using Stakeout.Simulation;
using Stakeout.Simulation.Actions;
using Stakeout.Simulation.Actions.Primitives;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Events;
using Xunit;

namespace Stakeout.Tests.Simulation.Actions;

public class ActionSequenceTests
{
    private static ActionContext CreateContext(Person person = null)
    {
        var state = new SimulationState();
        person ??= new Person { Id = 1 };
        state.People[person.Id] = person;
        return new ActionContext
        {
            Person = person,
            State = state,
            EventJournal = state.Journal,
            Random = new Random(42),
            CurrentTime = state.Clock.CurrentTime
        };
    }

    [Fact]
    public void Create_SetsName()
    {
        var seq = ActionSequence.Create("TestAction").Wait(TimeSpan.FromMinutes(10), "waiting").Build();
        Assert.Equal("TestAction", seq.Name);
    }

    [Fact]
    public void SingleWait_RunsThenCompletes()
    {
        var seq = ActionSequence.Create("Test")
            .Wait(TimeSpan.FromMinutes(10), "waiting")
            .Build();
        var ctx = CreateContext();
        seq.OnStart(ctx);

        Assert.Equal(ActionStatus.Running, seq.Tick(ctx, TimeSpan.FromMinutes(5)));
        Assert.Equal(ActionStatus.Completed, seq.Tick(ctx, TimeSpan.FromMinutes(6)));
    }

    [Fact]
    public void MultipleSteps_ExecuteSequentially()
    {
        var seq = ActionSequence.Create("TwoStep")
            .Wait(TimeSpan.FromMinutes(10), "step one")
            .Wait(TimeSpan.FromMinutes(5), "step two")
            .Build();
        var ctx = CreateContext();
        seq.OnStart(ctx);

        // Step 1 in progress
        Assert.Equal("step one", seq.DisplayText);
        Assert.Equal(ActionStatus.Running, seq.Tick(ctx, TimeSpan.FromMinutes(10)));

        // Step 2 in progress
        Assert.Equal("step two", seq.DisplayText);
        Assert.Equal(ActionStatus.Running, seq.Tick(ctx, TimeSpan.FromMinutes(3)));

        // Step 2 completes
        Assert.Equal(ActionStatus.Completed, seq.Tick(ctx, TimeSpan.FromMinutes(3)));
    }

    [Fact]
    public void DisplayText_ReflectsCurrentStep()
    {
        var seq = ActionSequence.Create("Test")
            .Wait(TimeSpan.FromMinutes(5), "first")
            .Wait(TimeSpan.FromMinutes(5), "second")
            .Build();
        var ctx = CreateContext();
        seq.OnStart(ctx);

        Assert.Equal("first", seq.DisplayText);
        seq.Tick(ctx, TimeSpan.FromMinutes(5)); // completes first step
        Assert.Equal("second", seq.DisplayText);
    }

    [Fact]
    public void Do_RunsCustomAction()
    {
        var customRan = false;
        var seq = ActionSequence.Create("Test")
            .Do(new TestAction(() => customRan = true))
            .Build();
        var ctx = CreateContext();
        seq.OnStart(ctx);
        seq.Tick(ctx, TimeSpan.FromSeconds(1));

        Assert.True(customRan);
    }

    [Fact]
    public void If_ConditionTrue_RunsStep()
    {
        var seq = ActionSequence.Create("Test")
            .If(_ => true, b => b.Wait(TimeSpan.FromMinutes(5), "ran"))
            .Build();
        var ctx = CreateContext();
        seq.OnStart(ctx);

        Assert.Equal("ran", seq.DisplayText);
        Assert.Equal(ActionStatus.Running, seq.Tick(ctx, TimeSpan.FromMinutes(3)));
    }

    [Fact]
    public void If_ConditionFalse_SkipsStep()
    {
        var seq = ActionSequence.Create("Test")
            .If(_ => false, b => b.Wait(TimeSpan.FromMinutes(5), "skipped"))
            .Wait(TimeSpan.FromMinutes(5), "ran instead")
            .Build();
        var ctx = CreateContext();
        seq.OnStart(ctx);

        Assert.Equal("ran instead", seq.DisplayText);
    }

    [Fact]
    public void Maybe_ProbabilityZero_SkipsStep()
    {
        // Seed 42 Random — but probability 0 always skips
        var seq = ActionSequence.Create("Test")
            .Maybe(0.0, b => b.Wait(TimeSpan.FromMinutes(5), "skipped"))
            .Wait(TimeSpan.FromMinutes(5), "ran")
            .Build();
        var ctx = CreateContext();
        seq.OnStart(ctx);

        Assert.Equal("ran", seq.DisplayText);
    }

    [Fact]
    public void Maybe_ProbabilityOne_RunsStep()
    {
        var seq = ActionSequence.Create("Test")
            .Maybe(1.0, b => b.Wait(TimeSpan.FromMinutes(5), "ran"))
            .Build();
        var ctx = CreateContext();
        seq.OnStart(ctx);

        Assert.Equal("ran", seq.DisplayText);
    }

    // Helper: an IAction that completes immediately and calls a callback
    private class TestAction : IAction
    {
        private readonly Action _onTick;
        public string Name => "Test";
        public string DisplayText => "testing";

        public TestAction(Action onTick) { _onTick = onTick; }
        public void OnStart(ActionContext ctx) { }
        public ActionStatus Tick(ActionContext ctx, TimeSpan delta) { _onTick(); return ActionStatus.Completed; }
        public void OnComplete(ActionContext ctx) { }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test stakeout.tests/ --filter "ActionSequenceTests" -v minimal`

Expected: FAIL — `ActionSequence` class does not exist.

- [ ] **Step 3: Implement ActionSequence**

Create `src/simulation/actions/ActionSequence.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace Stakeout.Simulation.Actions;

public class ActionSequence : IAction
{
    private readonly List<IStep> _steps = new();
    private int _currentStepIndex;
    private IAction _currentAction;

    public string Name { get; }
    public string DisplayText => _currentAction?.DisplayText ?? Name;

    private ActionSequence(string name)
    {
        Name = name;
    }

    public static ActionSequenceBuilder Create(string name) => new(name);

    public void OnStart(ActionContext ctx)
    {
        _currentStepIndex = 0;
        AdvanceToNextRunnableStep(ctx);
    }

    public ActionStatus Tick(ActionContext ctx, TimeSpan delta)
    {
        if (_currentAction == null) return ActionStatus.Completed;

        var status = _currentAction.Tick(ctx, delta);
        if (status == ActionStatus.Completed)
        {
            _currentAction.OnComplete(ctx);
            _currentStepIndex++;
            AdvanceToNextRunnableStep(ctx);
            if (_currentAction == null) return ActionStatus.Completed;
        }
        else if (status == ActionStatus.Failed)
        {
            return ActionStatus.Failed;
        }
        return ActionStatus.Running;
    }

    public void OnComplete(ActionContext ctx) { }

    private void AdvanceToNextRunnableStep(ActionContext ctx)
    {
        while (_currentStepIndex < _steps.Count)
        {
            var step = _steps[_currentStepIndex];
            var action = step.Resolve(ctx);
            if (action != null)
            {
                _currentAction = action;
                _currentAction.OnStart(ctx);
                return;
            }
            _currentStepIndex++;
        }
        _currentAction = null;
    }

    private interface IStep
    {
        IAction Resolve(ActionContext ctx);
    }

    private class DirectStep : IStep
    {
        private readonly IAction _action;
        public DirectStep(IAction action) { _action = action; }
        public IAction Resolve(ActionContext ctx) => _action;
    }

    private class ConditionalStep : IStep
    {
        private readonly Func<ActionContext, bool> _condition;
        private readonly IAction _action;
        public ConditionalStep(Func<ActionContext, bool> condition, IAction action)
        {
            _condition = condition;
            _action = action;
        }
        public IAction Resolve(ActionContext ctx) => _condition(ctx) ? _action : null;
    }

    private class MaybeStep : IStep
    {
        private readonly double _probability;
        private readonly IAction _action;
        public MaybeStep(double probability, IAction action)
        {
            _probability = probability;
            _action = action;
        }
        public IAction Resolve(ActionContext ctx) =>
            ctx.Random.NextDouble() < _probability ? _action : null;
    }

    public class ActionSequenceBuilder
    {
        private readonly ActionSequence _sequence;

        internal ActionSequenceBuilder(string name)
        {
            _sequence = new ActionSequence(name);
        }

        public ActionSequenceBuilder Wait(TimeSpan duration, string displayText)
        {
            _sequence._steps.Add(new DirectStep(
                new Primitives.WaitAction(duration, displayText)));
            return this;
        }

        public ActionSequenceBuilder Do(IAction action)
        {
            _sequence._steps.Add(new DirectStep(action));
            return this;
        }

        public ActionSequenceBuilder If(Func<ActionContext, bool> condition,
            Func<ActionSequenceBuilder, ActionSequenceBuilder> buildInner)
        {
            var inner = new ActionSequenceBuilder("if-branch");
            buildInner(inner);
            var innerSeq = inner.Build();
            _sequence._steps.Add(new ConditionalStep(condition, innerSeq));
            return this;
        }

        public ActionSequenceBuilder Maybe(double probability,
            Func<ActionSequenceBuilder, ActionSequenceBuilder> buildInner)
        {
            var inner = new ActionSequenceBuilder("maybe-branch");
            buildInner(inner);
            var innerSeq = inner.Build();
            _sequence._steps.Add(new MaybeStep(probability, innerSeq));
            return this;
        }

        public ActionSequence Build() => _sequence;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test stakeout.tests/ --filter "ActionSequenceTests" -v minimal`

Expected: All 9 tests PASS.

- [ ] **Step 5: Commit**

```
git add src/simulation/actions/ActionSequence.cs stakeout.tests/Simulation/Actions/ActionSequenceTests.cs
git commit -m "feat(p3): add ActionSequence fluent builder with tests"
```

---

## Task 4: DayPlan & NpcBrain

Build the daily planning algorithm.

**Files:**
- Modify: `src/simulation/brain/DayPlan.cs` (add scheduling logic)
- Create: `src/simulation/brain/NpcBrain.cs`
- Test: `stakeout.tests/Simulation/Brain/DayPlanTests.cs`
- Test: `stakeout.tests/Simulation/Brain/NpcBrainTests.cs`

- [ ] **Step 1: Write failing tests for DayPlan**

Create `stakeout.tests/Simulation/Brain/DayPlanTests.cs`:

```csharp
using System;
using Stakeout.Simulation.Actions;
using Stakeout.Simulation.Actions.Primitives;
using Stakeout.Simulation.Brain;
using Stakeout.Simulation.Objectives;
using Xunit;

namespace Stakeout.Tests.Simulation.Brain;

public class DayPlanTests
{
    private static PlannedAction MakeAction(string name, TimeSpan windowStart, TimeSpan windowEnd, TimeSpan duration)
    {
        return new PlannedAction
        {
            Action = new WaitAction(duration, name),
            TargetAddressId = 1,
            TimeWindowStart = windowStart,
            TimeWindowEnd = windowEnd,
            Duration = duration,
            DisplayText = name
        };
    }

    [Fact]
    public void Empty_DayPlan_CurrentIsNull()
    {
        var plan = new DayPlan();
        Assert.Null(plan.Current);
    }

    [Fact]
    public void Current_ReturnsFirstEntry()
    {
        var plan = new DayPlan();
        plan.Entries.Add(new DayPlanEntry
        {
            StartTime = TimeSpan.FromHours(6),
            EndTime = TimeSpan.FromHours(7),
            PlannedAction = MakeAction("test", TimeSpan.FromHours(6), TimeSpan.FromHours(7), TimeSpan.FromHours(1))
        });
        Assert.NotNull(plan.Current);
        Assert.Equal("test", plan.Current.PlannedAction.DisplayText);
    }

    [Fact]
    public void AdvanceToNext_MovesToSecondEntry()
    {
        var plan = new DayPlan();
        plan.Entries.Add(new DayPlanEntry
        {
            StartTime = TimeSpan.FromHours(6),
            EndTime = TimeSpan.FromHours(7),
            PlannedAction = MakeAction("first", TimeSpan.FromHours(6), TimeSpan.FromHours(7), TimeSpan.FromHours(1))
        });
        plan.Entries.Add(new DayPlanEntry
        {
            StartTime = TimeSpan.FromHours(8),
            EndTime = TimeSpan.FromHours(9),
            PlannedAction = MakeAction("second", TimeSpan.FromHours(8), TimeSpan.FromHours(9), TimeSpan.FromHours(1))
        });

        var next = plan.AdvanceToNext();
        Assert.Equal("second", next.PlannedAction.DisplayText);
        Assert.Equal(DayPlanEntryStatus.Completed, plan.Entries[0].Status);
    }

    [Fact]
    public void AdvanceToNext_PastEnd_ReturnsNull()
    {
        var plan = new DayPlan();
        plan.Entries.Add(new DayPlanEntry
        {
            StartTime = TimeSpan.FromHours(6),
            EndTime = TimeSpan.FromHours(7),
            PlannedAction = MakeAction("only", TimeSpan.FromHours(6), TimeSpan.FromHours(7), TimeSpan.FromHours(1))
        });

        var next = plan.AdvanceToNext();
        Assert.Null(next);
    }
}
```

- [ ] **Step 2: Run tests to verify they pass (DayPlan already implemented in Task 1)**

Run: `dotnet test stakeout.tests/ --filter "DayPlanTests" -v minimal`

Expected: All 4 tests PASS (DayPlan was created as a stub in Task 1, core logic is already there).

- [ ] **Step 3: Write failing tests for NpcBrain**

Create `stakeout.tests/Simulation/Brain/NpcBrainTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using Stakeout.Simulation;
using Stakeout.Simulation.Actions;
using Stakeout.Simulation.Actions.Primitives;
using Stakeout.Simulation.Brain;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Objectives;
using Xunit;

namespace Stakeout.Tests.Simulation.Brain;

public class NpcBrainTests
{
    private static SimulationState CreateState()
    {
        var state = new SimulationState();
        var home = new Address { Id = 1, Position = new Godot.Vector2(0, 0) };
        state.Addresses[home.Id] = home;
        return state;
    }

    private static Person CreatePerson(SimulationState state, TimeSpan wakeTime, TimeSpan sleepTime)
    {
        var person = new Person
        {
            Id = state.GenerateEntityId(),
            HomeAddressId = 1,
            CurrentAddressId = 1,
            PreferredWakeTime = wakeTime,
            PreferredSleepTime = sleepTime,
            CurrentPosition = new Godot.Vector2(0, 0)
        };
        state.People[person.Id] = person;
        return person;
    }

    [Fact]
    public void PlanDay_NoObjectives_OnlyIdleAtHome()
    {
        var state = CreateState();
        var person = CreatePerson(state, TimeSpan.FromHours(6), TimeSpan.FromHours(22));

        var plan = NpcBrain.PlanDay(person, state, state.Clock.CurrentTime);

        Assert.NotEmpty(plan.Entries);
        Assert.All(plan.Entries, e => Assert.Equal("relaxing at home", e.PlannedAction.DisplayText));
    }

    [Fact]
    public void PlanDay_HigherPriorityScheduledFirst()
    {
        var state = CreateState();
        var park = new Address { Id = 2, Position = new Godot.Vector2(10, 10) };
        state.Addresses[park.Id] = park;

        var person = CreatePerson(state, TimeSpan.FromHours(6), TimeSpan.FromHours(22));
        person.Objectives.Add(new TestObjective(80, new PlannedAction
        {
            Action = new WaitAction(TimeSpan.FromHours(1), "high priority"),
            TargetAddressId = 1,
            TimeWindowStart = TimeSpan.FromHours(8),
            TimeWindowEnd = TimeSpan.FromHours(12),
            Duration = TimeSpan.FromHours(1),
            DisplayText = "high priority"
        }));
        person.Objectives.Add(new TestObjective(20, new PlannedAction
        {
            Action = new WaitAction(TimeSpan.FromHours(1), "low priority"),
            TargetAddressId = 1,
            TimeWindowStart = TimeSpan.FromHours(8),
            TimeWindowEnd = TimeSpan.FromHours(12),
            Duration = TimeSpan.FromHours(1),
            DisplayText = "low priority"
        }));

        var plan = NpcBrain.PlanDay(person, state, state.Clock.CurrentTime);

        // Find the two non-idle entries
        var nonIdle = plan.Entries.FindAll(e => e.PlannedAction.DisplayText != "relaxing at home");
        Assert.Equal(2, nonIdle.Count);
        // High priority should be scheduled first (earlier time)
        Assert.True(nonIdle[0].StartTime <= nonIdle[1].StartTime);
        Assert.Equal("high priority", nonIdle[0].PlannedAction.DisplayText);
    }

    [Fact]
    public void PlanDay_GapsFilledWithIdleAtHome()
    {
        var state = CreateState();
        var person = CreatePerson(state, TimeSpan.FromHours(6), TimeSpan.FromHours(22));
        person.Objectives.Add(new TestObjective(40, new PlannedAction
        {
            Action = new WaitAction(TimeSpan.FromHours(1), "eating"),
            TargetAddressId = 1,
            TimeWindowStart = TimeSpan.FromHours(12),
            TimeWindowEnd = TimeSpan.FromHours(13),
            Duration = TimeSpan.FromHours(1),
            DisplayText = "eating"
        }));

        var plan = NpcBrain.PlanDay(person, state, state.Clock.CurrentTime);

        // Should have idle before eating, eating, idle after eating
        Assert.True(plan.Entries.Count >= 3);
        var eatingEntry = plan.Entries.Find(e => e.PlannedAction.DisplayText == "eating");
        Assert.NotNull(eatingEntry);
    }

    [Fact]
    public void PlanDay_ObjectiveThatDoesntFit_IsSkipped()
    {
        var state = CreateState();
        var person = CreatePerson(state, TimeSpan.FromHours(6), TimeSpan.FromHours(22));
        // Fill the whole day with high priority
        person.Objectives.Add(new TestObjective(80, new PlannedAction
        {
            Action = new WaitAction(TimeSpan.FromHours(16), "all day"),
            TargetAddressId = 1,
            TimeWindowStart = TimeSpan.FromHours(6),
            TimeWindowEnd = TimeSpan.FromHours(22),
            Duration = TimeSpan.FromHours(16),
            DisplayText = "all day"
        }));
        // Low priority can't fit
        person.Objectives.Add(new TestObjective(20, new PlannedAction
        {
            Action = new WaitAction(TimeSpan.FromHours(1), "cant fit"),
            TargetAddressId = 1,
            TimeWindowStart = TimeSpan.FromHours(6),
            TimeWindowEnd = TimeSpan.FromHours(22),
            Duration = TimeSpan.FromHours(1),
            DisplayText = "cant fit"
        }));

        var plan = NpcBrain.PlanDay(person, state, state.Clock.CurrentTime);

        Assert.DoesNotContain(plan.Entries, e => e.PlannedAction.DisplayText == "cant fit");
    }

    // Helper: a test objective that returns a fixed PlannedAction
    private class TestObjective : Objective
    {
        private readonly int _priority;
        private readonly PlannedAction _action;

        public override int Priority => _priority;
        public override ObjectiveSource Source => ObjectiveSource.Universal;

        public TestObjective(int priority, PlannedAction action)
        {
            _priority = priority;
            _action = action;
        }

        public override List<PlannedAction> GetActionsForToday(Person person, SimulationState state, DateTime currentDate)
        {
            return new List<PlannedAction> { _action };
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they fail**

Run: `dotnet test stakeout.tests/ --filter "NpcBrainTests" -v minimal`

Expected: FAIL — `NpcBrain` class does not exist.

- [ ] **Step 5: Implement NpcBrain**

Create `src/simulation/brain/NpcBrain.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Stakeout.Simulation.Actions;
using Stakeout.Simulation.Actions.Primitives;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Objectives;

namespace Stakeout.Simulation.Brain;

public static class NpcBrain
{
    public static DayPlan PlanDay(Person person, SimulationState state, DateTime currentTime, MapConfig mapConfig = null)
    {
        var plan = new DayPlan();
        var wakeTime = person.PreferredWakeTime;
        var sleepTime = person.PreferredSleepTime;

        // Separate sleep from other objectives — sleep is appended at the end, not slot-found
        var objectives = person.Objectives
            .Where(o => o.Status == ObjectiveStatus.Active)
            .OrderByDescending(o => o.Priority)
            .ToList();

        PlannedAction sleepAction = null;
        var scheduled = new List<(TimeSpan start, TimeSpan end, PlannedAction action)>();

        foreach (var objective in objectives)
        {
            var actions = objective.GetActionsForToday(person, state, currentTime.Date);
            foreach (var action in actions)
            {
                // Sleep is special — it goes at the end of the day, not in a waking-hours slot
                if (objective is SleepObjective)
                {
                    sleepAction = action;
                    continue;
                }

                var travelHours = EstimateTravelTime(person, action.TargetAddressId, state, mapConfig);
                var totalDuration = action.Duration + TimeSpan.FromHours(travelHours);

                var slot = FindSlot(scheduled, action.TimeWindowStart, action.TimeWindowEnd,
                    totalDuration, wakeTime, sleepTime);
                if (slot.HasValue)
                {
                    scheduled.Add((slot.Value, slot.Value + totalDuration, action));
                }
            }
        }

        // Sort scheduled actions by start time
        scheduled.Sort((a, b) => a.start.CompareTo(b.start));

        // Build plan entries, filling gaps with IdleAtHome
        var currentSlotTime = wakeTime;
        foreach (var (start, end, action) in scheduled)
        {
            if (start > currentSlotTime)
            {
                AddIdleEntry(plan, currentSlotTime, start, person.HomeAddressId);
            }
            plan.Entries.Add(new DayPlanEntry
            {
                StartTime = start,
                EndTime = end,
                PlannedAction = action
            });
            currentSlotTime = end;
        }

        // Fill remaining waking time with idle
        if (currentSlotTime < sleepTime)
        {
            AddIdleEntry(plan, currentSlotTime, sleepTime, person.HomeAddressId);
        }

        // Append sleep at the end of the day
        if (sleepAction != null)
        {
            plan.Entries.Add(new DayPlanEntry
            {
                StartTime = sleepTime,
                EndTime = sleepTime + sleepAction.Duration,
                PlannedAction = sleepAction
            });
        }

        return plan;
    }

    private static TimeSpan? FindSlot(
        List<(TimeSpan start, TimeSpan end, PlannedAction action)> scheduled,
        TimeSpan windowStart, TimeSpan windowEnd,
        TimeSpan totalDuration,
        TimeSpan wakeTime, TimeSpan sleepTime)
    {
        // Clamp window to waking hours
        var effectiveStart = windowStart < wakeTime ? wakeTime : windowStart;
        var effectiveEnd = windowEnd > sleepTime ? sleepTime : windowEnd;

        if (effectiveEnd - effectiveStart < totalDuration)
            return null;

        // Try to fit starting from effectiveStart, skipping over existing slots
        var candidate = effectiveStart;
        foreach (var (start, end, _) in scheduled.OrderBy(s => s.start))
        {
            if (candidate + totalDuration <= start)
                return candidate;

            if (candidate < end)
                candidate = end;
        }

        if (candidate + totalDuration <= effectiveEnd)
            return candidate;

        return null;
    }

    private static void AddIdleEntry(DayPlan plan, TimeSpan start, TimeSpan end, int homeAddressId)
    {
        var duration = end - start;
        if (duration <= TimeSpan.Zero) return;

        plan.Entries.Add(new DayPlanEntry
        {
            StartTime = start,
            EndTime = end,
            PlannedAction = new PlannedAction
            {
                Action = new WaitAction(duration, "relaxing at home"),
                TargetAddressId = homeAddressId,
                TimeWindowStart = start,
                TimeWindowEnd = end,
                Duration = duration,
                DisplayText = "relaxing at home"
            }
        });
    }

    private static float EstimateTravelTime(Person person, int targetAddressId, SimulationState state, MapConfig mapConfig)
    {
        if (person.CurrentAddressId == targetAddressId) return 0f;
        if (!state.Addresses.TryGetValue(targetAddressId, out var target)) return 0f;
        if (mapConfig != null)
            return mapConfig.ComputeTravelTimeHours(person.CurrentPosition, target.Position);
        // Fallback: use same formula as MapConfig defaults
        var diagonal = new Godot.Vector2(4800, 4800).Length();
        return person.CurrentPosition.DistanceTo(target.Position) / diagonal * 1.0f;
    }
}
```

- [ ] **Step 6: Run tests to verify they pass**

Run: `dotnet test stakeout.tests/ --filter "NpcBrainTests" -v minimal`

Expected: All 4 tests PASS.

- [ ] **Step 7: Run all tests**

Run: `dotnet test stakeout.tests/ -v minimal`

Expected: All tests PASS.

- [ ] **Step 8: Commit**

```
git add src/simulation/brain/NpcBrain.cs stakeout.tests/Simulation/Brain/NpcBrainTests.cs stakeout.tests/Simulation/Brain/DayPlanTests.cs
git commit -m "feat(p3): add NpcBrain daily planner and DayPlan with tests"
```

---

## Task 5: Objective Library (Sleep, EatOut, GoForARun)

Build the concrete objectives that produce PlannedActions for the brain to schedule.

**Files:**
- Create: `src/simulation/objectives/SleepObjective.cs`
- Create: `src/simulation/objectives/EatOutObjective.cs`
- Create: `src/simulation/objectives/GoForARunObjective.cs`
- Test: `stakeout.tests/Simulation/Objectives/SleepObjectiveTests.cs`
- Test: `stakeout.tests/Simulation/Objectives/EatOutObjectiveTests.cs`
- Test: `stakeout.tests/Simulation/Objectives/GoForARunObjectiveTests.cs`

- [ ] **Step 1: Write failing tests for SleepObjective**

Create `stakeout.tests/Simulation/Objectives/SleepObjectiveTests.cs`:

```csharp
using System;
using System.Linq;
using Stakeout.Simulation;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Objectives;
using Xunit;

namespace Stakeout.Tests.Simulation.Objectives;

public class SleepObjectiveTests
{
    [Fact]
    public void Priority_Is80()
    {
        var obj = new SleepObjective();
        Assert.Equal(80, obj.Priority);
    }

    [Fact]
    public void Source_IsUniversal()
    {
        var obj = new SleepObjective();
        Assert.Equal(ObjectiveSource.Universal, obj.Source);
    }

    [Fact]
    public void GetActionsForToday_ReturnsSleepAction()
    {
        var state = new SimulationState();
        state.Addresses[1] = new Address { Id = 1 };
        var person = new Person
        {
            Id = 1,
            HomeAddressId = 1,
            PreferredSleepTime = TimeSpan.FromHours(22),
            PreferredWakeTime = TimeSpan.FromHours(6)
        };

        var obj = new SleepObjective();
        var actions = obj.GetActionsForToday(person, state, DateTime.Today);

        Assert.Single(actions);
        Assert.Equal("sleeping", actions[0].DisplayText);
        Assert.Equal(1, actions[0].TargetAddressId);
    }

    [Fact]
    public void GetActionsForToday_SleepWindowMatchesPreferences()
    {
        var state = new SimulationState();
        state.Addresses[1] = new Address { Id = 1 };
        var person = new Person
        {
            Id = 1,
            HomeAddressId = 1,
            PreferredSleepTime = TimeSpan.FromHours(22),
            PreferredWakeTime = TimeSpan.FromHours(6)
        };

        var obj = new SleepObjective();
        var actions = obj.GetActionsForToday(person, state, DateTime.Today);

        Assert.Equal(TimeSpan.FromHours(22), actions[0].TimeWindowStart);
    }

    [Fact]
    public void GetActionsForToday_DurationIs8Hours()
    {
        var state = new SimulationState();
        state.Addresses[1] = new Address { Id = 1 };
        var person = new Person
        {
            Id = 1,
            HomeAddressId = 1,
            PreferredSleepTime = TimeSpan.FromHours(22),
            PreferredWakeTime = TimeSpan.FromHours(6)
        };

        var obj = new SleepObjective();
        var actions = obj.GetActionsForToday(person, state, DateTime.Today);

        Assert.Equal(TimeSpan.FromHours(8), actions[0].Duration);
    }
}
```

- [ ] **Step 2: Implement SleepObjective**

Create `src/simulation/objectives/SleepObjective.cs`:

```csharp
using System;
using System.Collections.Generic;
using Stakeout.Simulation.Actions.Primitives;
using Stakeout.Simulation.Entities;

namespace Stakeout.Simulation.Objectives;

/// <summary>
/// Sleep is special: it marks the END of the waking day, not a slot within it.
/// The brain handles sleep by appending it after all other scheduled activities.
/// GetActionsForToday returns the sleep action with its timing info so the brain
/// knows when to end the day and start sleep.
/// </summary>
public class SleepObjective : Objective
{
    public override int Priority => 80;
    public override ObjectiveSource Source => ObjectiveSource.Universal;

    public override List<PlannedAction> GetActionsForToday(Person person, SimulationState state, DateTime currentDate)
    {
        var sleepTime = person.PreferredSleepTime;
        var wakeTime = person.PreferredWakeTime;

        var duration = wakeTime - sleepTime;
        if (duration < TimeSpan.Zero)
            duration += TimeSpan.FromHours(24);

        return new List<PlannedAction>
        {
            new()
            {
                Action = new WaitAction(duration, "sleeping"),
                TargetAddressId = person.HomeAddressId,
                TimeWindowStart = sleepTime,
                TimeWindowEnd = sleepTime, // Exact time, not a window
                Duration = duration,
                DisplayText = "sleeping",
                SourceObjective = this
            }
        };
    }
}
```

- [ ] **Step 3: Run SleepObjective tests**

Run: `dotnet test stakeout.tests/ --filter "SleepObjectiveTests" -v minimal`

Expected: All 5 tests PASS.

- [ ] **Step 4: Write failing tests for GoForARunObjective**

Create `stakeout.tests/Simulation/Objectives/GoForARunObjectiveTests.cs`:

```csharp
using System;
using System.Linq;
using Stakeout.Simulation;
using Stakeout.Simulation.Addresses;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Objectives;
using Xunit;

namespace Stakeout.Tests.Simulation.Objectives;

public class GoForARunObjectiveTests
{
    private static SimulationState CreateStateWithPark()
    {
        var state = new SimulationState();
        state.Addresses[1] = new Address { Id = 1, Type = AddressType.SuburbanHome };
        state.Addresses[2] = new Address { Id = 2, Type = AddressType.Park, CityId = 1 };
        state.Cities[1] = new Simulation.Entities.City { Id = 1, AddressIds = new List<int> { 1, 2 } };
        return state;
    }

    [Fact]
    public void Priority_Is20()
    {
        var obj = new GoForARunObjective();
        Assert.Equal(20, obj.Priority);
    }

    [Fact]
    public void Source_IsTrait()
    {
        var obj = new GoForARunObjective();
        Assert.Equal(ObjectiveSource.Trait, obj.Source);
    }

    [Fact]
    public void GetActionsForToday_ReturnsRunAction()
    {
        var state = CreateStateWithPark();
        var person = new Person { Id = 1, HomeAddressId = 1, CurrentCityId = 1 };

        var obj = new GoForARunObjective();
        var actions = obj.GetActionsForToday(person, state, DateTime.Today);

        Assert.Single(actions);
        Assert.Equal("running on the trails", actions[0].DisplayText);
        Assert.Equal(2, actions[0].TargetAddressId); // park
    }

    [Fact]
    public void GetActionsForToday_NoPark_ReturnsEmpty()
    {
        var state = new SimulationState();
        state.Addresses[1] = new Address { Id = 1, Type = AddressType.SuburbanHome };
        state.Cities[1] = new Simulation.Entities.City { Id = 1, AddressIds = new List<int> { 1 } };
        var person = new Person { Id = 1, HomeAddressId = 1, CurrentCityId = 1 };

        var obj = new GoForARunObjective();
        var actions = obj.GetActionsForToday(person, state, DateTime.Today);

        Assert.Empty(actions);
    }

    [Fact]
    public void GetActionsForToday_Duration_Is45Minutes()
    {
        var state = CreateStateWithPark();
        var person = new Person { Id = 1, HomeAddressId = 1, CurrentCityId = 1 };

        var obj = new GoForARunObjective();
        var actions = obj.GetActionsForToday(person, state, DateTime.Today);

        Assert.Equal(TimeSpan.FromMinutes(45), actions[0].Duration);
    }
}
```

- [ ] **Step 5: Implement GoForARunObjective**

Create `src/simulation/objectives/GoForARunObjective.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Stakeout.Simulation.Actions.Primitives;
using Stakeout.Simulation.Entities;

namespace Stakeout.Simulation.Objectives;

public class GoForARunObjective : Objective
{
    private static readonly TimeSpan RunDuration = TimeSpan.FromMinutes(45);

    public override int Priority => 20;
    public override ObjectiveSource Source => ObjectiveSource.Trait;

    public override List<PlannedAction> GetActionsForToday(Person person, SimulationState state, DateTime currentDate)
    {
        var parkId = FindPark(person, state);
        if (parkId == null) return new List<PlannedAction>();

        return new List<PlannedAction>
        {
            new()
            {
                Action = new WaitAction(RunDuration, "running on the trails"),
                TargetAddressId = parkId.Value,
                TimeWindowStart = TimeSpan.FromHours(6),
                TimeWindowEnd = TimeSpan.FromHours(20),
                Duration = RunDuration,
                DisplayText = "running on the trails",
                SourceObjective = this
            }
        };
    }

    public override void EmitTraces(PlannedAction action, Person person, SimulationState state)
    {
        if (person.CurrentAddressId.HasValue)
        {
            // Find any location at this address to attach the sighting to
            var locations = state.GetLocationsForAddress(person.CurrentAddressId.Value);
            if (locations.Count > 0)
            {
                Traces.TraceEmitter.EmitSighting(state, person.Id,
                    locations[0].Id, $"{person.FullName} was seen running", decayDays: 3);
            }
        }
    }

    private static int? FindPark(Person person, SimulationState state)
    {
        if (!person.CurrentCityId.HasValue) return null;
        var city = state.Cities[person.CurrentCityId.Value];
        return city.AddressIds
            .Select(id => state.Addresses[id])
            .FirstOrDefault(a => a.Type == Addresses.AddressType.Park)?.Id;
    }
}
```

- [ ] **Step 6: Run GoForARunObjective tests**

Run: `dotnet test stakeout.tests/ --filter "GoForARunObjectiveTests" -v minimal`

Expected: All 5 tests PASS.

- [ ] **Step 7: Write failing tests for EatOutObjective**

Create `stakeout.tests/Simulation/Objectives/EatOutObjectiveTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using Stakeout.Simulation;
using Stakeout.Simulation.Addresses;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Objectives;
using Xunit;

namespace Stakeout.Tests.Simulation.Objectives;

public class EatOutObjectiveTests
{
    private static SimulationState CreateStateWithDiner()
    {
        var state = new SimulationState();
        state.Addresses[1] = new Address { Id = 1, Type = AddressType.SuburbanHome };
        state.Addresses[2] = new Address { Id = 2, Type = AddressType.Diner, CityId = 1 };
        state.Cities[1] = new Simulation.Entities.City { Id = 1, AddressIds = new List<int> { 1, 2 } };
        return state;
    }

    [Fact]
    public void Priority_Is40()
    {
        var obj = new EatOutObjective();
        Assert.Equal(40, obj.Priority);
    }

    [Fact]
    public void Source_IsTrait()
    {
        var obj = new EatOutObjective();
        Assert.Equal(ObjectiveSource.Trait, obj.Source);
    }

    [Fact]
    public void GetActionsForToday_ReturnsEatAction()
    {
        var state = CreateStateWithDiner();
        var person = new Person { Id = 1, HomeAddressId = 1, CurrentCityId = 1 };

        var obj = new EatOutObjective();
        var actions = obj.GetActionsForToday(person, state, DateTime.Today);

        Assert.Single(actions);
        Assert.Contains("eating", actions[0].DisplayText);
        Assert.Equal(2, actions[0].TargetAddressId);
    }

    [Fact]
    public void GetActionsForToday_NoDiner_ReturnsEmpty()
    {
        var state = new SimulationState();
        state.Addresses[1] = new Address { Id = 1, Type = AddressType.SuburbanHome };
        state.Cities[1] = new Simulation.Entities.City { Id = 1, AddressIds = new List<int> { 1 } };
        var person = new Person { Id = 1, HomeAddressId = 1, CurrentCityId = 1 };

        var obj = new EatOutObjective();
        var actions = obj.GetActionsForToday(person, state, DateTime.Today);

        Assert.Empty(actions);
    }

    [Fact]
    public void GetActionsForToday_Duration_Is30Minutes()
    {
        var state = CreateStateWithDiner();
        var person = new Person { Id = 1, HomeAddressId = 1, CurrentCityId = 1 };

        var obj = new EatOutObjective();
        var actions = obj.GetActionsForToday(person, state, DateTime.Today);

        Assert.Equal(TimeSpan.FromMinutes(30), actions[0].Duration);
    }
}
```

- [ ] **Step 8: Implement EatOutObjective**

Create `src/simulation/objectives/EatOutObjective.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Stakeout.Simulation.Actions.Primitives;
using Stakeout.Simulation.Entities;

namespace Stakeout.Simulation.Objectives;

public class EatOutObjective : Objective
{
    private static readonly TimeSpan MealDuration = TimeSpan.FromMinutes(30);

    public override int Priority => 40;
    public override ObjectiveSource Source => ObjectiveSource.Trait;

    public override List<PlannedAction> GetActionsForToday(Person person, SimulationState state, DateTime currentDate)
    {
        var dinerId = FindRestaurant(person, state);
        if (dinerId == null) return new List<PlannedAction>();

        return new List<PlannedAction>
        {
            new()
            {
                Action = new WaitAction(MealDuration, "eating at the counter"),
                TargetAddressId = dinerId.Value,
                TimeWindowStart = TimeSpan.FromHours(11),
                TimeWindowEnd = TimeSpan.FromHours(14),
                Duration = MealDuration,
                DisplayText = "eating at the counter",
                SourceObjective = this
            }
        };
    }

    public override void EmitTraces(PlannedAction action, Person person, SimulationState state)
    {
        if (!person.CurrentAddressId.HasValue) return;
        var locations = state.GetLocationsForAddress(person.CurrentAddressId.Value);
        if (locations.Count > 0)
        {
            // Sighting at diner
            Traces.TraceEmitter.EmitSighting(state, person.Id,
                locations[0].Id, $"{person.FullName} was seen eating", decayDays: 3);

            // Receipt in trash fixture (if one exists)
            var fixtures = state.GetFixturesForLocation(locations[0].Id);
            var trash = fixtures.FirstOrDefault(f => f.FixtureType == Fixtures.FixtureType.TrashCan);
            if (trash != null)
            {
                Traces.TraceEmitter.EmitItem(state, person.Id,
                    locations[0].Id, trash.Id, "crumpled receipt", decayDays: 7);
            }
        }
    }

    private static int? FindRestaurant(Person person, SimulationState state)
    {
        if (!person.CurrentCityId.HasValue) return null;
        var city = state.Cities[person.CurrentCityId.Value];
        return city.AddressIds
            .Select(id => state.Addresses[id])
            .FirstOrDefault(a => a.Type == Addresses.AddressType.Diner)?.Id;
    }
}
```

- [ ] **Step 9: Run all objective tests**

Run: `dotnet test stakeout.tests/ --filter "ObjectiveTests" -v minimal`

Expected: All 15 tests PASS.

- [ ] **Step 10: Commit**

```
git add src/simulation/objectives/SleepObjective.cs src/simulation/objectives/EatOutObjective.cs src/simulation/objectives/GoForARunObjective.cs stakeout.tests/Simulation/Objectives/
git commit -m "feat(p3): add SleepObjective, EatOutObjective, GoForARunObjective with tests"
```

---

## Task 6: ActionRunner & SimulationManager Integration

Wire the action runner into the frame loop so NPCs actually execute their day plans.

**Files:**
- Create: `src/simulation/actions/ActionRunner.cs`
- Modify: `src/simulation/SimulationManager.cs`
- Test: `stakeout.tests/Simulation/Actions/ActionRunnerTests.cs`

- [ ] **Step 1: Write failing tests for ActionRunner**

Create `stakeout.tests/Simulation/Actions/ActionRunnerTests.cs`:

```csharp
using System;
using Stakeout.Simulation;
using Stakeout.Simulation.Actions;
using Stakeout.Simulation.Actions.Primitives;
using Stakeout.Simulation.Brain;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Events;
using Stakeout.Simulation.Objectives;
using Xunit;

namespace Stakeout.Tests.Simulation.Actions;

public class ActionRunnerTests
{
    private static (SimulationState state, Person person) Setup()
    {
        var state = new SimulationState(new GameClock(new DateTime(1980, 1, 1, 8, 0, 0)));
        var home = new Address { Id = 1, Position = new Godot.Vector2(0, 0) };
        var park = new Address { Id = 2, Position = new Godot.Vector2(100, 100) };
        state.Addresses[home.Id] = home;
        state.Addresses[park.Id] = park;

        var person = new Person
        {
            Id = state.GenerateEntityId(),
            HomeAddressId = 1,
            CurrentAddressId = 1,
            CurrentPosition = new Godot.Vector2(0, 0),
            PreferredWakeTime = TimeSpan.FromHours(6),
            PreferredSleepTime = TimeSpan.FromHours(22)
        };
        state.People[person.Id] = person;

        // Create a simple day plan
        person.DayPlan = new DayPlan();
        person.DayPlan.Entries.Add(new DayPlanEntry
        {
            StartTime = TimeSpan.FromHours(8),
            EndTime = TimeSpan.FromHours(9),
            PlannedAction = new PlannedAction
            {
                Action = new WaitAction(TimeSpan.FromHours(1), "relaxing at home"),
                TargetAddressId = 1,
                Duration = TimeSpan.FromHours(1),
                DisplayText = "relaxing at home"
            }
        });

        return (state, person);
    }

    [Fact]
    public void Tick_StartsFirstActivity_WhenAtTargetAddress()
    {
        var (state, person) = Setup();
        var runner = new ActionRunner(new MapConfig());

        runner.Tick(person, state, TimeSpan.FromSeconds(1));

        Assert.NotNull(person.CurrentActivity);
        Assert.Equal("relaxing at home", person.CurrentActivity.DisplayText);
    }

    [Fact]
    public void Tick_StartsTraveling_WhenNotAtTargetAddress()
    {
        var (state, person) = Setup();
        // Change plan to target park (address 2), person is at home (address 1)
        person.DayPlan.Entries[0] = new DayPlanEntry
        {
            StartTime = TimeSpan.FromHours(8),
            EndTime = TimeSpan.FromHours(9),
            PlannedAction = new PlannedAction
            {
                Action = new WaitAction(TimeSpan.FromHours(1), "running"),
                TargetAddressId = 2,
                Duration = TimeSpan.FromHours(1),
                DisplayText = "running"
            }
        };

        var runner = new ActionRunner(new MapConfig());
        runner.Tick(person, state, TimeSpan.FromSeconds(1));

        Assert.NotNull(person.TravelInfo);
        Assert.Equal(2, person.TravelInfo.ToAddressId);
        Assert.Null(person.CurrentActivity);
    }

    [Fact]
    public void Tick_CompletesActivity_AdvancesPlan()
    {
        var (state, person) = Setup();
        // Add a second entry
        person.DayPlan.Entries.Add(new DayPlanEntry
        {
            StartTime = TimeSpan.FromHours(9),
            EndTime = TimeSpan.FromHours(10),
            PlannedAction = new PlannedAction
            {
                Action = new WaitAction(TimeSpan.FromHours(1), "second activity"),
                TargetAddressId = 1,
                Duration = TimeSpan.FromHours(1),
                DisplayText = "second activity"
            }
        });

        var runner = new ActionRunner(new MapConfig());
        // Start first activity
        runner.Tick(person, state, TimeSpan.FromSeconds(1));
        Assert.Equal("relaxing at home", person.CurrentActivity.DisplayText);

        // Complete first activity
        runner.Tick(person, state, TimeSpan.FromHours(1.1));
        Assert.Equal(1, person.DayPlan.CurrentIndex);
    }

    [Fact]
    public void Tick_LogsActivityStartedEvent()
    {
        var (state, person) = Setup();
        var runner = new ActionRunner(new MapConfig());

        runner.Tick(person, state, TimeSpan.FromSeconds(1));

        var events = state.Journal.GetEventsForPerson(person.Id);
        Assert.Contains(events, e => e.EventType == SimulationEventType.ActivityStarted);
    }

    [Fact]
    public void Tick_NoPlan_DoesNothing()
    {
        var (state, person) = Setup();
        person.DayPlan = null;
        var runner = new ActionRunner(new MapConfig());

        runner.Tick(person, state, TimeSpan.FromSeconds(1));

        Assert.Null(person.CurrentActivity);
        Assert.Null(person.TravelInfo);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test stakeout.tests/ --filter "ActionRunnerTests" -v minimal`

Expected: FAIL — `ActionRunner` class does not exist.

- [ ] **Step 3: Implement ActionRunner**

Create `src/simulation/actions/ActionRunner.cs`:

```csharp
using System;
using Stakeout.Simulation.Brain;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Events;

namespace Stakeout.Simulation.Actions;

public class ActionRunner
{
    private readonly MapConfig _mapConfig;

    public ActionRunner(MapConfig mapConfig)
    {
        _mapConfig = mapConfig;
    }

    public void Tick(Person person, SimulationState state, TimeSpan delta)
    {
        if (person.DayPlan == null) return;

        // If traveling, update travel
        if (person.TravelInfo != null)
        {
            UpdateTravel(person, state);
            return;
        }

        // If currently doing an activity, tick it
        if (person.CurrentActivity != null)
        {
            var ctx = CreateContext(person, state);
            var status = person.CurrentActivity.Tick(ctx, delta);
            if (status == ActionStatus.Completed || status == ActionStatus.Failed)
            {
                person.CurrentActivity.OnComplete(ctx);
                LogActivityCompleted(person, state);

                var entry = person.DayPlan.Current;
                if (entry?.PlannedAction?.SourceObjective != null)
                {
                    var obj = entry.PlannedAction.SourceObjective;
                    obj.OnActionCompleted(entry.PlannedAction, status == ActionStatus.Completed);
                    if (status == ActionStatus.Completed)
                        obj.EmitTraces(entry.PlannedAction, person, state);
                }

                person.CurrentActivity = null;
                person.DayPlan.AdvanceToNext();
                // Try to start next entry immediately
                StartNextEntry(person, state);
            }
            return;
        }

        // No activity, not traveling — start next plan entry
        StartNextEntry(person, state);
    }

    private void StartNextEntry(Person person, SimulationState state)
    {
        var entry = person.DayPlan.Current;
        if (entry == null) return;

        var targetAddressId = entry.PlannedAction.TargetAddressId;

        if (person.CurrentAddressId != targetAddressId)
        {
            BeginTravel(person, state, targetAddressId);
        }
        else
        {
            StartActivity(person, state, entry);
        }
    }

    private void StartActivity(Person person, SimulationState state, DayPlanEntry entry)
    {
        var action = entry.PlannedAction.Action;
        var ctx = CreateContext(person, state);
        action.OnStart(ctx);
        person.CurrentActivity = action;
        entry.Status = DayPlanEntryStatus.Active;

        state.Journal.Append(new SimulationEvent
        {
            Timestamp = state.Clock.CurrentTime,
            PersonId = person.Id,
            EventType = SimulationEventType.ActivityStarted,
            AddressId = person.CurrentAddressId,
            Description = action.DisplayText
        });
    }

    private void BeginTravel(Person person, SimulationState state, int destinationAddressId)
    {
        var destAddress = state.Addresses[destinationAddressId];
        var currentTime = state.Clock.CurrentTime;

        // Log departure
        if (person.CurrentAddressId.HasValue && person.CurrentAddressId != 0)
        {
            state.Journal.Append(new SimulationEvent
            {
                Timestamp = currentTime,
                PersonId = person.Id,
                EventType = SimulationEventType.DepartedAddress,
                FromAddressId = person.CurrentAddressId,
                ToAddressId = destinationAddressId
            });
        }

        var fromPosition = person.CurrentPosition;
        var travelHours = _mapConfig.ComputeTravelTimeHours(fromPosition, destAddress.Position);
        var arrivalTime = currentTime.AddHours(travelHours);

        person.TravelInfo = new TravelInfo
        {
            FromPosition = fromPosition,
            ToPosition = destAddress.Position,
            DepartureTime = currentTime,
            ArrivalTime = arrivalTime,
            FromAddressId = person.CurrentAddressId ?? 0,
            ToAddressId = destinationAddressId
        };

        person.CurrentAddressId = 0; // In transit
    }

    private void UpdateTravel(Person person, SimulationState state)
    {
        var travel = person.TravelInfo;
        var currentTime = state.Clock.CurrentTime;

        if (currentTime >= travel.ArrivalTime)
        {
            person.CurrentPosition = travel.ToPosition;
            person.CurrentAddressId = travel.ToAddressId;
            person.TravelInfo = null;

            state.Journal.Append(new SimulationEvent
            {
                Timestamp = currentTime,
                PersonId = person.Id,
                EventType = SimulationEventType.ArrivedAtAddress,
                AddressId = travel.ToAddressId
            });

            // Start the activity now that we've arrived
            StartNextEntry(person, state);
        }
        else
        {
            var totalSeconds = (travel.ArrivalTime - travel.DepartureTime).TotalSeconds;
            var elapsedSeconds = (currentTime - travel.DepartureTime).TotalSeconds;
            var progress = Math.Clamp(elapsedSeconds / totalSeconds, 0.0, 1.0);
            person.CurrentPosition = travel.FromPosition.Lerp(travel.ToPosition, (float)progress);
        }
    }

    private void LogActivityCompleted(Person person, SimulationState state)
    {
        state.Journal.Append(new SimulationEvent
        {
            Timestamp = state.Clock.CurrentTime,
            PersonId = person.Id,
            EventType = SimulationEventType.ActivityCompleted,
            AddressId = person.CurrentAddressId,
            Description = person.CurrentActivity?.DisplayText
        });
    }

    private static ActionContext CreateContext(Person person, SimulationState state)
    {
        return new ActionContext
        {
            Person = person,
            State = state,
            EventJournal = state.Journal,
            Random = new Random(person.Id), // deterministic per-person
            CurrentTime = state.Clock.CurrentTime
        };
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test stakeout.tests/ --filter "ActionRunnerTests" -v minimal`

Expected: All 5 tests PASS.

- [ ] **Step 5: Wire ActionRunner into SimulationManager**

In `src/simulation/SimulationManager.cs`:

Add a field: `private readonly ActionRunner _actionRunner;`

In the constructor, initialize: `_actionRunner = new ActionRunner(_mapConfig);`

Add `using Stakeout.Simulation.Actions;` and `using Stakeout.Simulation.Brain;` imports.

Replace the TODO comments in `_Process` (lines 102-117) with:

```csharp
foreach (var person in State.People.Values)
{
    if (!person.IsAlive) continue;

    // Plan day on wake-up (first tick or when plan is null)
    if (person.DayPlan == null)
    {
        person.DayPlan = NpcBrain.PlanDay(person, State, State.Clock.CurrentTime);
        State.Journal.Append(new SimulationEvent
        {
            Timestamp = State.Clock.CurrentTime,
            PersonId = person.Id,
            EventType = SimulationEventType.DayPlanned,
            Description = $"Planned {person.DayPlan.Entries.Count} activities"
        });
    }

    _actionRunner.Tick(person, State, TimeSpan.FromSeconds(scaledDelta));
}
```

Remove the empty `RebuildSchedule` method.

- [ ] **Step 6: Run all tests**

Run: `dotnet test stakeout.tests/ -v minimal`

Expected: All tests PASS.

- [ ] **Step 7: Commit**

```
git add src/simulation/actions/ActionRunner.cs stakeout.tests/Simulation/Actions/ActionRunnerTests.cs src/simulation/SimulationManager.cs
git commit -m "feat(p3): add ActionRunner, wire into SimulationManager frame loop"
```

---

## Task 7: Traits & PersonGenerator Integration

Wire traits to objectives and update PersonGenerator to assign them.

**Files:**
- Create: `src/simulation/traits/TraitDefinitions.cs`
- Modify: `src/simulation/PersonGenerator.cs`
- Test: `stakeout.tests/Simulation/Traits/TraitDefinitionsTests.cs`

- [ ] **Step 1: Write failing tests for TraitDefinitions**

Create `stakeout.tests/Simulation/Traits/TraitDefinitionsTests.cs`:

```csharp
using System.Linq;
using Stakeout.Simulation.Objectives;
using Stakeout.Simulation.Traits;
using Xunit;

namespace Stakeout.Tests.Simulation.Traits;

public class TraitDefinitionsTests
{
    [Fact]
    public void Runner_CreatesGoForARunObjective()
    {
        var objectives = TraitDefinitions.CreateObjectivesForTrait("runner");
        Assert.Single(objectives);
        Assert.IsType<GoForARunObjective>(objectives[0]);
    }

    [Fact]
    public void Foodie_CreatesEatOutObjective()
    {
        var objectives = TraitDefinitions.CreateObjectivesForTrait("foodie");
        Assert.Single(objectives);
        Assert.IsType<EatOutObjective>(objectives[0]);
    }

    [Fact]
    public void UnknownTrait_ReturnsEmpty()
    {
        var objectives = TraitDefinitions.CreateObjectivesForTrait("unknown");
        Assert.Empty(objectives);
    }

    [Fact]
    public void GetAllTraitNames_ContainsRunnerAndFoodie()
    {
        var names = TraitDefinitions.GetAllTraitNames();
        Assert.Contains("runner", names);
        Assert.Contains("foodie", names);
    }
}
```

- [ ] **Step 2: Implement TraitDefinitions**

Create `src/simulation/traits/TraitDefinitions.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Stakeout.Simulation.Objectives;

namespace Stakeout.Simulation.Traits;

public static class TraitDefinitions
{
    private static readonly Dictionary<string, Func<List<Objective>>> Registry = new()
    {
        ["runner"] = () => new List<Objective> { new GoForARunObjective() },
        ["foodie"] = () => new List<Objective> { new EatOutObjective() },
    };

    public static List<Objective> CreateObjectivesForTrait(string traitName)
    {
        return Registry.TryGetValue(traitName, out var factory)
            ? factory()
            : new List<Objective>();
    }

    public static IReadOnlyList<string> GetAllTraitNames() => Registry.Keys.ToList();
}
```

- [ ] **Step 3: Run trait tests**

Run: `dotnet test stakeout.tests/ --filter "TraitDefinitionsTests" -v minimal`

Expected: All 4 tests PASS.

- [ ] **Step 4: Update PersonGenerator to assign traits and objectives**

In `src/simulation/PersonGenerator.cs`:

Add `using Stakeout.Simulation.Objectives;` and `using Stakeout.Simulation.Traits;` imports.

After the person is created (after line 84 `};`), before `state.People[person.Id] = person;`, add:

```csharp
// Assign traits (random selection, 0-2 traits per person)
var allTraits = TraitDefinitions.GetAllTraitNames();
var traitCount = _random.Next(0, 3); // 0, 1, or 2 traits
var shuffled = allTraits.OrderBy(_ => _random.Next()).Take(traitCount);
foreach (var trait in shuffled)
{
    person.Traits.Add(trait);
}

// Create objectives: universal + trait-based
person.Objectives.Add(new SleepObjective { Id = state.GenerateEntityId() });
foreach (var trait in person.Traits)
{
    foreach (var obj in TraitDefinitions.CreateObjectivesForTrait(trait))
    {
        obj.Id = state.GenerateEntityId();
        person.Objectives.Add(obj);
    }
}
```

Also comment out job-related scheduling code. Add a comment before the job creation section (line 59-61):

```csharp
// TODO: Project 4 — job creation will be wired into Business entities and WorkShiftObjective
```

Keep the job creation code itself — it's still needed for data, just not for scheduling.

- [ ] **Step 5: Update PersonGenerator tests**

In `stakeout.tests/Simulation/PersonGeneratorTests.cs`:

Update `GeneratePerson_SetsInitialActivity` test — remove or replace it since `CurrentAction` no longer exists. Replace with:

```csharp
[Fact]
public void GeneratePerson_HasObjectives()
{
    var state = CreateState();
    var person = CreateGenerator().GeneratePerson(state);
    // At minimum, every person has a SleepObjective
    Assert.NotEmpty(person.Objectives);
    Assert.Contains(person.Objectives, o => o is SleepObjective);
}

[Fact]
public void GeneratePerson_HasTraits()
{
    var state = CreateState();
    var gen = CreateGenerator();
    // Generate enough people that some have traits
    var hasTrait = false;
    for (int i = 0; i < 50; i++)
    {
        var person = gen.GeneratePerson(state);
        if (person.Traits.Count > 0) hasTrait = true;
    }
    Assert.True(hasTrait);
}
```

- [ ] **Step 6: Run all tests**

Run: `dotnet test stakeout.tests/ -v minimal`

Expected: All tests PASS.

- [ ] **Step 7: Commit**

```
git add src/simulation/traits/TraitDefinitions.cs src/simulation/PersonGenerator.cs stakeout.tests/Simulation/Traits/TraitDefinitionsTests.cs stakeout.tests/Simulation/PersonGeneratorTests.cs
git commit -m "feat(p3): add trait system, wire traits and objectives into PersonGenerator"
```

---

## Task 8: Debug Inspector Updates

Update the person inspector to show the new model: current activity, objectives, and day plan.

**Files:**
- Modify: `scenes/game_shell/GameShell.cs`

- [ ] **Step 1: Update the Current State section**

In `PopulateInspectorContent` method, replace the "Current State" section (around line 434):

```csharp
// Current State
var activityLines = new List<string>();
if (person.TravelInfo != null)
{
    var toAddr = state.Addresses.GetValueOrDefault(person.TravelInfo.ToAddressId);
    var street = toAddr != null ? state.Streets.GetValueOrDefault(toAddr.StreetId) : null;
    var eta = person.TravelInfo.ArrivalTime;
    activityLines.Add($"Status: Traveling to {toAddr?.Number} {street?.Name ?? "Unknown"}");
    activityLines.Add($"ETA: {eta:HH:mm}");
}
else if (person.CurrentActivity != null)
{
    activityLines.Add($"Activity: {person.CurrentActivity.DisplayText}");
    activityLines.Add("Status: Active");
}
else
{
    activityLines.Add("Activity: idle");
}
AddInspectorSection(vbox, font, "— Current State —", activityLines.ToArray());
```

- [ ] **Step 2: Update the Objectives section**

Replace the old objectives section (around line 452-467):

```csharp
// Objectives
var objLines = new List<string>();
foreach (var obj in person.Objectives)
{
    var executing = person.CurrentActivity != null &&
        person.DayPlan?.Current?.PlannedAction?.SourceObjective == obj;
    var statusStr = executing ? "Active, executing" : obj.Status.ToString();
    objLines.Add($"[P{obj.Priority}] {obj.GetType().Name} ({obj.Source}) — {statusStr}");
}
if (objLines.Count > 0)
    AddInspectorSection(vbox, font, "— Objectives —", objLines.ToArray());
```

- [ ] **Step 3: Replace Schedule section with Day Plan**

Replace the old schedule tree section (around line 469-473) with:

```csharp
// Day Plan
if (person.DayPlan != null)
{
    var planLines = new List<string>();
    for (int i = 0; i < person.DayPlan.Entries.Count; i++)
    {
        var entry = person.DayPlan.Entries[i];
        var marker = entry.Status == DayPlanEntryStatus.Completed ? " ✓"
            : i == person.DayPlan.CurrentIndex ? " ← current"
            : "";
        var targetAddr = state.Addresses.GetValueOrDefault(entry.PlannedAction.TargetAddressId);
        var street = targetAddr != null ? state.Streets.GetValueOrDefault(targetAddr.StreetId) : null;
        var location = targetAddr != null ? $"at {targetAddr.Number} {street?.Name ?? "Unknown"}" : "";
        planLines.Add($"{entry.StartTime:hh\\:mm} - {entry.EndTime:hh\\:mm}  {entry.PlannedAction.DisplayText} {location}{marker}");
    }
    AddInspectorSection(vbox, font, "— Day Plan —", planLines.ToArray());
}
```

- [ ] **Step 4: Comment out Job section**

Replace the job section (around line 440-450) with:

```csharp
// TODO: Project 4 — Job section will be restored with Business entities
```

- [ ] **Step 5: Remove old imports and AddScheduleTree method**

Remove `using Stakeout.Simulation.Scheduling;` if no longer needed.
Remove the `AddScheduleTree` method and the `FormatScheduleEntry` / `FormatAddressString` helper methods if they only served the old schedule display.
Add any needed new imports: `using Stakeout.Simulation.Brain;`, `using Stakeout.Simulation.Actions;`.

- [ ] **Step 6: Build the project**

Run: `dotnet build stakeout.csproj`

Expected: Build succeeds with 0 errors.

- [ ] **Step 7: Commit**

```
git add scenes/game_shell/GameShell.cs
git commit -m "feat(p3): update debug inspector for new brain/action system"
```

---

## Task 9: Integration Test & Final Cleanup

Verify the full pipeline works end-to-end and clean up any loose ends.

**Files:**
- Create: `stakeout.tests/Simulation/Brain/IntegrationTests.cs`
- Modify: any remaining files with stale references

- [ ] **Step 1: Write integration test**

Create `stakeout.tests/Simulation/Brain/IntegrationTests.cs`:

```csharp
using System;
using System.Linq;
using Stakeout.Simulation;
using Stakeout.Simulation.Actions;
using Stakeout.Simulation.Addresses;
using Stakeout.Simulation.Brain;
using Stakeout.Simulation.City;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Events;
using Stakeout.Simulation.Objectives;
using Xunit;
using CityEntity = Stakeout.Simulation.Entities.City;

namespace Stakeout.Tests.Simulation.Brain;

public class IntegrationTests
{
    private static (SimulationState state, Person person) Setup()
    {
        AddressTemplateRegistry.RegisterAll();
        var state = new SimulationState(new GameClock(new DateTime(1980, 1, 1, 6, 0, 0)));

        var city = new CityEntity { Id = state.GenerateEntityId(), Name = "Boston", CountryName = "US" };
        state.Cities[city.Id] = city;
        var cityGen = new CityGenerator(seed: 42);
        state.CityGrids[city.Id] = cityGen.Generate(state, city);

        var gen = new PersonGenerator(new MapConfig());
        var person = gen.GeneratePerson(state);

        return (state, person);
    }

    [Fact]
    public void PersonGetsPlannedDay()
    {
        var (state, person) = Setup();

        var plan = NpcBrain.PlanDay(person, state, state.Clock.CurrentTime);

        Assert.NotNull(plan);
        Assert.NotEmpty(plan.Entries);
    }

    [Fact]
    public void PersonHasSleepInPlan()
    {
        var (state, person) = Setup();

        var plan = NpcBrain.PlanDay(person, state, state.Clock.CurrentTime);

        Assert.Contains(plan.Entries, e => e.PlannedAction.DisplayText == "sleeping");
    }

    [Fact]
    public void ActionRunnerExecutesPlan()
    {
        var (state, person) = Setup();
        person.DayPlan = NpcBrain.PlanDay(person, state, state.Clock.CurrentTime);

        var runner = new ActionRunner(new MapConfig());

        // Tick for a bit — first entry should start
        runner.Tick(person, state, TimeSpan.FromSeconds(1));

        // Person should have an activity or be traveling
        Assert.True(person.CurrentActivity != null || person.TravelInfo != null);
    }

    [Fact]
    public void FullDaySimulation_ProducesEvents()
    {
        var (state, person) = Setup();
        person.DayPlan = NpcBrain.PlanDay(person, state, state.Clock.CurrentTime);

        var runner = new ActionRunner(new MapConfig());

        // Simulate 18 hours in 1-minute increments
        for (int i = 0; i < 18 * 60; i++)
        {
            state.Clock.Tick(60); // 1 minute
            runner.Tick(person, state, TimeSpan.FromMinutes(1));
        }

        // Should have generated some events
        var events = state.Journal.GetEventsForPerson(person.Id);
        Assert.True(events.Count > 1, $"Expected multiple events, got {events.Count}");
        Assert.Contains(events, e => e.EventType == SimulationEventType.ActivityStarted);
    }

    [Fact]
    public void MultiplePersons_EachGetOwnPlan()
    {
        AddressTemplateRegistry.RegisterAll();
        var state = new SimulationState(new GameClock(new DateTime(1980, 1, 1, 6, 0, 0)));
        var city = new CityEntity { Id = state.GenerateEntityId(), Name = "Boston", CountryName = "US" };
        state.Cities[city.Id] = city;
        var cityGen = new CityGenerator(seed: 42);
        state.CityGrids[city.Id] = cityGen.Generate(state, city);

        var gen = new PersonGenerator(new MapConfig());
        for (int i = 0; i < 5; i++)
        {
            var person = gen.GeneratePerson(state);
            person.DayPlan = NpcBrain.PlanDay(person, state, state.Clock.CurrentTime);
            Assert.NotNull(person.DayPlan);
            Assert.NotEmpty(person.DayPlan.Entries);
        }
    }
}
```

- [ ] **Step 2: Run integration tests**

Run: `dotnet test stakeout.tests/ --filter "IntegrationTests" -v minimal`

Expected: All 5 tests PASS.

- [ ] **Step 3: Run full test suite**

Run: `dotnet test stakeout.tests/ -v minimal`

Expected: All tests PASS.

- [ ] **Step 4: Clean up any stale .uid files and empty directories**

Check for orphaned `.uid` files in test directories for deleted test files:
```bash
find stakeout.tests/ -name "*.uid" | grep -E "(TaskTests|PersonBehavior|ScheduleBuilder|TaskResolver|ObjectiveResolver|ActionExecutor)"
```

Delete any found. Also check `stakeout.tests/Simulation/Scheduling/Decomposition/` — if empty, delete it.

- [ ] **Step 5: Run full test suite one more time**

Run: `dotnet test stakeout.tests/ -v minimal`

Expected: All tests PASS, clean build.

- [ ] **Step 6: Commit**

```
git add -A
git commit -m "feat(p3): add integration tests, final cleanup"
```

---

## Task 10: Architecture Documentation

Update the architecture docs to reflect the new system.

**Files:**
- Modify or create: `docs/architecture/npc-brain.md`
- Modify: `docs/architecture/simulation-scheduling.md` (if it still exists — may need to be replaced)

- [ ] **Step 1: Check what architecture docs exist**

Read `docs/architecture/` directory listing and the existing `simulation-scheduling.md` file.

- [ ] **Step 2: Replace simulation-scheduling.md with npc-brain.md**

Delete or rename `docs/architecture/simulation-scheduling.md` since the old system no longer exists.

Create `docs/architecture/npc-brain.md` following the standard format (Purpose, Key Files, How It Works, Key Decisions, Connection Points):

```markdown
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
```

- [ ] **Step 3: Update simulation-core.md if needed**

Check if `docs/architecture/simulation-core.md` references the old scheduling system and update any stale references.

- [ ] **Step 4: Commit**

```
git add docs/architecture/
git commit -m "docs: update architecture docs for P3 NPC brain and action engine"
```
