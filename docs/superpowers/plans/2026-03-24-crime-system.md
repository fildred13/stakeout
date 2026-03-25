# Crime System Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Refactor the NPC behavior model into an Objective → Task → Action → Trace pipeline and implement a serial killer crime template as the first crime.

**Architecture:** Replace the hardcoded Goal/GoalSet/GoalSetBuilder with a layered system where Objectives decompose into Tasks, Tasks are resolved into a DailySchedule (reusing the existing priority-based minute-by-minute resolution), and Actions execute when Tasks become active — producing Traces as observable artifacts. The existing scheduling engine's core algorithm is preserved; we change what feeds into it and what happens when scheduled items activate.

**Tech Stack:** Godot 4.6, C# (.NET 8), xunit for tests

**Spec:** `docs/superpowers/specs/2026-03-24-crime-system-design.md`

---

## File Structure

### New files

| File | Responsibility |
|------|---------------|
| `src/simulation/actions/ActionType.cs` | ActionType enum (replaces ActivityType) |
| `src/simulation/actions/ActionExecutor.cs` | Executes actions, modifies world state, produces traces |
| `src/simulation/objectives/Objective.cs` | Objective, ObjectiveStep, enums (ObjectiveType, ObjectiveSource, ObjectiveStatus, StepStatus) |
| `src/simulation/objectives/ObjectiveResolver.cs` | Decomposes Objectives → Tasks; handles instant steps |
| `src/simulation/objectives/Task.cs` | Task class (replaces Goal) |
| `src/simulation/crimes/Crime.cs` | Crime record, CrimeStatus, CrimeTemplateType |
| `src/simulation/crimes/ICrimeTemplate.cs` | Crime template interface |
| `src/simulation/crimes/SerialKillerTemplate.cs` | First crime template |
| `src/simulation/crimes/CrimeGenerator.cs` | Instantiates templates, wired to UI |
| `src/simulation/traces/Trace.cs` | Trace class, TraceType enum |
| `stakeout.tests/Simulation/Objectives/ObjectiveResolverTests.cs` | Tests for objective → task resolution |
| `stakeout.tests/Simulation/Objectives/TaskTests.cs` | Tests for Task data model |
| `stakeout.tests/Simulation/Actions/ActionExecutorTests.cs` | Tests for action execution and trace production |
| `stakeout.tests/Simulation/Crimes/SerialKillerTemplateTests.cs` | Tests for crime template instantiation |
| `stakeout.tests/Simulation/Crimes/CrimeGeneratorTests.cs` | Tests for crime generation end-to-end |
| `stakeout.tests/Simulation/Traces/TraceTests.cs` | Tests for trace creation |

### Modified files

| File | Changes |
|------|---------|
| `src/simulation/entities/Person.cs` | Add IsAlive, Objectives, Schedule, CurrentAction (replace CurrentActivity) |
| `src/simulation/SimulationState.cs` | Add Crimes, Traces dictionaries |
| `src/simulation/SimulationManager.cs` | Remove _schedules dict, add schedule rebuild, wire crime generator |
| `src/simulation/PersonGenerator.cs` | Create CoreNeed Objectives instead of GoalSet |
| `src/simulation/scheduling/ScheduleBuilder.cs` | Take List\<Task\> instead of GoalSet, data-driven address resolution |
| `src/simulation/scheduling/PersonBehavior.cs` | Skip dead NPCs, execute actions, use ActionType |
| `src/simulation/scheduling/DailySchedule.cs` | ScheduleEntry uses ActionType instead of ActivityType |
| `src/simulation/events/SimulationEvent.cs` | New event types, OldAction/NewAction fields |
| `scenes/game_shell/GameShell.cs` | Crime generator UI section, person inspector dialog |
| `scenes/city/CityView.cs` | Enhanced tooltips, dead NPC red dots |
| `stakeout.tests/Simulation/Scheduling/ScheduleBuilderTests.cs` | Use Task instead of GoalSet |
| `stakeout.tests/Simulation/Scheduling/PersonBehaviorTests.cs` | Use ActionType, test dead NPC skipping |
| `stakeout.tests/Simulation/PersonGeneratorTests.cs` | Adapt to Objective-based generation |
| `stakeout.tests/Simulation/SimulationStateTests.cs` | Test new collections |

### Removed files

| File | Replaced by |
|------|------------|
| `src/simulation/entities/ActivityType.cs` | `src/simulation/actions/ActionType.cs` |
| `src/simulation/scheduling/Goal.cs` | `src/simulation/objectives/Task.cs` + `src/simulation/objectives/Objective.cs` |

---

## Task 1: ActionType enum — replace ActivityType

This is a mechanical rename that touches many files. Do it first to establish the new foundation.

**Files:**
- Create: `src/simulation/actions/ActionType.cs`
- Delete: `src/simulation/entities/ActivityType.cs`
- Modify: `src/simulation/entities/Person.cs`
- Modify: `src/simulation/scheduling/DailySchedule.cs`
- Modify: `src/simulation/scheduling/PersonBehavior.cs`
- Modify: `src/simulation/scheduling/ScheduleBuilder.cs`
- Modify: `src/simulation/events/SimulationEvent.cs`
- Modify: `scenes/city/CityView.cs`
- Modify: all test files referencing ActivityType

- [ ] **Step 1: Create ActionType.cs**

```csharp
// src/simulation/actions/ActionType.cs
namespace Stakeout.Simulation.Actions;

public enum ActionType
{
    Idle,           // was AtHome
    Work,           // was Working
    TravelByCar,    // was TravellingByCar
    Sleep,          // was Sleeping
    KillPerson
}
```

- [ ] **Step 2: Update Person.cs**

Replace `CurrentActivity` (ActivityType) with `CurrentAction` (ActionType). Update the `using` to reference the new namespace.

```csharp
// In Person.cs:
// Remove: using Stakeout.Simulation.Entities; (for ActivityType)
// Add: using Stakeout.Simulation.Actions;
// Change: public ActivityType CurrentActivity → public ActionType CurrentAction
```

- [ ] **Step 3: Update DailySchedule.cs**

Change `ScheduleEntry.Activity` from `ActivityType` to `ActionType`. Update using.

```csharp
// In ScheduleEntry:
// Change: public ActivityType Activity → public ActionType Action
```

- [ ] **Step 4: Update SimulationEvent.cs**

Rename `OldActivity`/`NewActivity` to `OldAction`/`NewAction`, change type to `ActionType?`. Add new event types.

```csharp
public enum SimulationEventType
{
    DepartedAddress,
    ArrivedAtAddress,
    StartedWorking,
    StoppedWorking,
    FellAsleep,
    WokeUp,
    ActionChanged,     // was ActivityChanged
    PersonDied,
    CrimeCommitted,
    ObjectiveStarted,
    ObjectiveCompleted,
    TaskStarted,
    TaskCompleted
}

public class SimulationEvent
{
    // ... existing fields ...
    public ActionType? OldAction { get; set; }   // was OldActivity
    public ActionType? NewAction { get; set; }    // was NewActivity
}
```

- [ ] **Step 5: Update ScheduleBuilder.cs**

Change all `GoalType` → `ActionType` references in the mapping methods. For now, keep the `GoalSet` input — we'll change that in Task 7. Focus only on the output side:

- `GoalTypeToActivity()` → returns `ActionType` instead of `ActivityType`
- Travel entries use `ActionType.TravelByCar`
- Return values use new enum values (Idle, Work, Sleep)

```csharp
// Mapping changes:
// GoalType.BeAtWork → ActionType.Work
// GoalType.Sleep → ActionType.Sleep
// GoalType.BeAtHome → ActionType.Idle
// ActivityType.TravellingByCar → ActionType.TravelByCar
```

- [ ] **Step 6: Update PersonBehavior.cs**

Replace all `ActivityType` references with `ActionType`:
- `person.CurrentActivity` → `person.CurrentAction`
- `ActivityType.TravellingByCar` → `ActionType.TravelByCar`
- `ActivityType.Working` → `ActionType.Work`
- `ActivityType.Sleeping` → `ActionType.Sleep`
- `entry.Activity` → `entry.Action`

- [ ] **Step 7: Update CityView.cs**

Replace `ActivityType` references in the `_Process` color logic and `UpdateHoverLabel` switch:

```csharp
// Color logic:
// person.CurrentActivity == ActivityType.Sleeping → person.CurrentAction == ActionType.Sleep

// Hover label switch:
// ActionType.Work => "Working",
// ActionType.Sleep => "Sleeping",
// ActionType.TravelByCar => "Travelling",
// ActionType.Idle => "At Home",
```

- [ ] **Step 8: Update PersonGenerator.cs**

Change `ActivityType` references in initial state assignment:
- `ActivityType.TravellingByCar` → `ActionType.TravelByCar`
- `ActivityType.AtHome` → `ActionType.Idle`
- `ActivityType.Working` → `ActionType.Work`
- `person.CurrentActivity` → `person.CurrentAction`

- [ ] **Step 9: Delete ActivityType.cs**

Remove `src/simulation/entities/ActivityType.cs`.

- [ ] **Step 10: Update all test files**

Search and replace across all test files:
- `ActivityType.Sleeping` → `ActionType.Sleep`
- `ActivityType.Working` → `ActionType.Work`
- `ActivityType.AtHome` → `ActionType.Idle`
- `ActivityType.TravellingByCar` → `ActionType.TravelByCar`
- `person.CurrentActivity` → `person.CurrentAction`
- `entry.Activity` → `entry.Action`
- Update `using` statements

- [ ] **Step 11: Run all tests to verify rename is clean**

Run: `dotnet test stakeout.tests/ -v minimal`
Expected: all existing tests pass (same behavior, just renamed types)

- [ ] **Step 12: Commit**

```
git add -A
git commit -m "refactor: replace ActivityType with ActionType enum

Rename ActivityType → ActionType, CurrentActivity → CurrentAction,
and move enum from entities/ to actions/ namespace. Add KillPerson
and Idle action types. Functional behavior unchanged."
```

---

## Task 2: Task class (replaces Goal)

**Files:**
- Create: `src/simulation/objectives/Task.cs`
- Create: `stakeout.tests/Simulation/Objectives/TaskTests.cs`

- [ ] **Step 1: Write failing test**

```csharp
// stakeout.tests/Simulation/Objectives/TaskTests.cs
using System;
using Stakeout.Simulation.Actions;
using Stakeout.Simulation.Objectives;
using Xunit;

namespace Stakeout.Tests.Simulation.Objectives;

public class TaskTests
{
    [Fact]
    public void Task_DefaultValues_AreCorrect()
    {
        var task = new SimTask
        {
            Id = 1,
            ObjectiveId = 10,
            StepIndex = 0,
            ActionType = ActionType.Work,
            Priority = 20,
            WindowStart = new TimeSpan(9, 0, 0),
            WindowEnd = new TimeSpan(17, 0, 0),
            TargetAddressId = 5
        };

        Assert.Equal(1, task.Id);
        Assert.Equal(10, task.ObjectiveId);
        Assert.Equal(ActionType.Work, task.ActionType);
        Assert.Equal(20, task.Priority);
        Assert.Equal(new TimeSpan(9, 0, 0), task.WindowStart);
        Assert.Null(task.ActionData);
    }

    [Fact]
    public void Task_NullTargetAddress_DefaultsToNull()
    {
        var task = new SimTask
        {
            Id = 1,
            ActionType = ActionType.Idle,
            Priority = 10,
            WindowStart = TimeSpan.Zero,
            WindowEnd = TimeSpan.Zero
        };

        Assert.Null(task.TargetAddressId);
    }
}
```

Note: We name the class `SimTask` to avoid collision with `System.Threading.Tasks.Task`.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test stakeout.tests/ --filter "FullyQualifiedName~TaskTests" -v minimal`
Expected: FAIL — SimTask class doesn't exist

- [ ] **Step 3: Write implementation**

```csharp
// src/simulation/objectives/Task.cs
using System;
using System.Collections.Generic;
using Stakeout.Simulation.Actions;

namespace Stakeout.Simulation.Objectives;

public class SimTask
{
    public int Id { get; set; }
    public int ObjectiveId { get; set; }
    public int StepIndex { get; set; }
    public ActionType ActionType { get; set; }
    public int Priority { get; set; }
    public TimeSpan WindowStart { get; set; }
    public TimeSpan WindowEnd { get; set; }
    public int? TargetAddressId { get; set; }
    public Dictionary<string, object> ActionData { get; set; }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test stakeout.tests/ --filter "FullyQualifiedName~TaskTests" -v minimal`
Expected: PASS

- [ ] **Step 5: Commit**

```
git add src/simulation/objectives/Task.cs stakeout.tests/Simulation/Objectives/TaskTests.cs
git commit -m "feat: add SimTask class replacing Goal for schedule input"
```

---

## Task 3: Objective and ObjectiveStep classes

**Files:**
- Create: `src/simulation/objectives/Objective.cs`

- [ ] **Step 1: Write Objective.cs**

```csharp
// src/simulation/objectives/Objective.cs
using System;
using System.Collections.Generic;
using Stakeout.Simulation.Actions;

namespace Stakeout.Simulation.Objectives;

public enum ObjectiveType
{
    MaintainJob,
    GetSleep,
    DefaultIdle,
    CommitMurder
}

public enum ObjectiveSource
{
    CoreNeed,
    Trait,
    CrimeTemplate,
    Assignment
}

public enum ObjectiveStatus
{
    Active,
    Completed,
    Blocked,
    Cancelled
}

public enum StepStatus
{
    Pending,
    Active,
    Completed,
    Failed
}

public class ObjectiveStep
{
    public string Description { get; set; }
    public StepStatus Status { get; set; } = StepStatus.Pending;
    public ActionType? ActionType { get; set; }
    public bool IsInstant { get; set; }
    public Func<Objective, SimulationState, SimTask> ResolveFunc { get; set; }
}

public class Objective
{
    public int Id { get; set; }
    public ObjectiveType Type { get; set; }
    public ObjectiveSource Source { get; set; }
    public int? SourceEntityId { get; set; }
    public int Priority { get; set; }
    public ObjectiveStatus Status { get; set; } = ObjectiveStatus.Active;
    public List<ObjectiveStep> Steps { get; set; } = new();
    public int CurrentStepIndex { get; set; }
    public bool IsRecurring { get; set; }
    public Dictionary<string, object> Data { get; set; } = new();

    public ObjectiveStep CurrentStep =>
        CurrentStepIndex < Steps.Count ? Steps[CurrentStepIndex] : null;

    public bool AdvanceStep()
    {
        if (CurrentStepIndex < Steps.Count)
            Steps[CurrentStepIndex].Status = StepStatus.Completed;

        CurrentStepIndex++;

        if (CurrentStepIndex >= Steps.Count)
        {
            Status = ObjectiveStatus.Completed;
            return false;
        }

        Steps[CurrentStepIndex].Status = StepStatus.Active;
        return true;
    }
}
```

Note: `ResolveFunc` references `SimulationState` — add `using Stakeout.Simulation;`. This creates a forward reference that compiles fine since both are in the same project.

- [ ] **Step 2: Build to verify it compiles**

Run: `dotnet build stakeout.tests/ -v minimal`
Expected: Build succeeded

- [ ] **Step 3: Commit**

```
git add src/simulation/objectives/Objective.cs
git commit -m "feat: add Objective and ObjectiveStep classes"
```

---

## Task 4: Trace and Crime data models

**Files:**
- Create: `src/simulation/traces/Trace.cs`
- Create: `src/simulation/crimes/Crime.cs`
- Create: `stakeout.tests/Simulation/Traces/TraceTests.cs`

- [ ] **Step 1: Write failing test for Trace**

```csharp
// stakeout.tests/Simulation/Traces/TraceTests.cs
using System;
using Stakeout.Simulation.Traces;
using Xunit;

namespace Stakeout.Tests.Simulation.Traces;

public class TraceTests
{
    [Fact]
    public void Trace_ConditionType_AttachedToPerson()
    {
        var trace = new Trace
        {
            Id = 1,
            TraceType = TraceType.Condition,
            CreatedAt = new DateTime(1980, 1, 2, 1, 15, 0),
            CreatedByPersonId = 5,
            AttachedToPersonId = 3,
            Description = "Cause of death: stabbing"
        };

        Assert.Equal(TraceType.Condition, trace.TraceType);
        Assert.Equal(3, trace.AttachedToPersonId);
        Assert.Null(trace.LocationId);
    }

    [Fact]
    public void Trace_MarkType_BoundToLocation()
    {
        var trace = new Trace
        {
            Id = 2,
            TraceType = TraceType.Mark,
            CreatedAt = new DateTime(1980, 1, 2, 1, 15, 0),
            CreatedByPersonId = 5,
            LocationId = 10,
            Description = "Signs of forced entry"
        };

        Assert.Equal(TraceType.Mark, trace.TraceType);
        Assert.Equal(10, trace.LocationId);
        Assert.Null(trace.AttachedToPersonId);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test stakeout.tests/ --filter "FullyQualifiedName~TraceTests" -v minimal`
Expected: FAIL

- [ ] **Step 3: Write Trace.cs**

```csharp
// src/simulation/traces/Trace.cs
using System;
using System.Collections.Generic;

namespace Stakeout.Simulation.Traces;

public enum TraceType
{
    Item,
    Sighting,
    Mark,
    Condition,
    Record
}

public class Trace
{
    public int Id { get; set; }
    public TraceType TraceType { get; set; }
    public DateTime CreatedAt { get; set; }
    public int CreatedByPersonId { get; set; }
    public int? LocationId { get; set; }
    public int? AttachedToPersonId { get; set; }
    public string Description { get; set; }
    public Dictionary<string, object> Data { get; set; }
}
```

- [ ] **Step 4: Write Crime.cs**

```csharp
// src/simulation/crimes/Crime.cs
using System;
using System.Collections.Generic;

namespace Stakeout.Simulation.Crimes;

public enum CrimeTemplateType
{
    SerialKiller
}

public enum CrimeStatus
{
    InProgress,
    Completed,
    Failed
}

public class Crime
{
    public int Id { get; set; }
    public CrimeTemplateType TemplateType { get; set; }
    public DateTime CreatedAt { get; set; }
    public CrimeStatus Status { get; set; } = CrimeStatus.InProgress;
    public Dictionary<string, int?> Roles { get; set; } = new();
    public List<int> RelatedTraceIds { get; set; } = new();
    public List<int> ObjectiveIds { get; set; } = new();
}
```

- [ ] **Step 5: Run tests**

Run: `dotnet test stakeout.tests/ --filter "FullyQualifiedName~TraceTests" -v minimal`
Expected: PASS

- [ ] **Step 6: Commit**

```
git add src/simulation/traces/Trace.cs src/simulation/crimes/Crime.cs stakeout.tests/Simulation/Traces/TraceTests.cs
git commit -m "feat: add Trace and Crime data models"
```

---

## Task 5: Update SimulationState and Person

**Files:**
- Modify: `src/simulation/SimulationState.cs`
- Modify: `src/simulation/entities/Person.cs`

- [ ] **Step 1: Add Crimes and Traces to SimulationState**

```csharp
// Add to SimulationState.cs:
// using Stakeout.Simulation.Crimes;
// using Stakeout.Simulation.Traces;

public Dictionary<int, Crime> Crimes { get; } = new();
public Dictionary<int, Trace> Traces { get; } = new();
```

- [ ] **Step 2: Add IsAlive, Objectives, Schedule to Person**

```csharp
// Add to Person.cs:
// using Stakeout.Simulation.Objectives;
// using Stakeout.Simulation.Scheduling;

public bool IsAlive { get; set; } = true;
public List<Objective> Objectives { get; set; } = new();
public DailySchedule Schedule { get; set; }
```

`CurrentAction` was already renamed in Task 1.

- [ ] **Step 3: Run all tests**

Run: `dotnet test stakeout.tests/ -v minimal`
Expected: all pass (additive changes only)

- [ ] **Step 4: Commit**

```
git add src/simulation/SimulationState.cs src/simulation/entities/Person.cs
git commit -m "feat: add IsAlive, Objectives, Schedule to Person; Crimes, Traces to SimulationState"
```

---

## Task 6: Refactor ScheduleBuilder to use Tasks

This is the core scheduling refactor. We change the input from GoalSet to List\<SimTask\> and make address resolution data-driven.

**Files:**
- Modify: `src/simulation/scheduling/ScheduleBuilder.cs`
- Modify: `stakeout.tests/Simulation/Scheduling/ScheduleBuilderTests.cs`

- [ ] **Step 1: Write new tests for Task-based ScheduleBuilder**

Add new tests alongside existing ones. We'll update the existing tests afterward.

```csharp
// Add to ScheduleBuilderTests.cs:

using Stakeout.Simulation.Actions;
using Stakeout.Simulation.Objectives;
using System.Collections.Generic;

// Helper to create tasks for testing
private static List<SimTask> CreateOfficerWorkerTasks(Address home, Address work)
{
    return new List<SimTask>
    {
        new SimTask
        {
            Id = 1, ActionType = ActionType.Sleep, Priority = 30,
            WindowStart = new TimeSpan(22, 0, 0), WindowEnd = new TimeSpan(6, 0, 0),
            TargetAddressId = home.Id
        },
        new SimTask
        {
            Id = 2, ActionType = ActionType.Work, Priority = 20,
            WindowStart = new TimeSpan(9, 0, 0), WindowEnd = new TimeSpan(17, 0, 0),
            TargetAddressId = work.Id
        },
        new SimTask
        {
            Id = 3, ActionType = ActionType.Idle, Priority = 10,
            WindowStart = TimeSpan.Zero, WindowEnd = TimeSpan.Zero,
            TargetAddressId = home.Id
        }
    };
}

[Fact]
public void BuildFromTasks_OfficeWorker_HasCorrectActionSequence()
{
    var (home, work) = CreateAddresses();
    var tasks = CreateOfficerWorkerTasks(home, work);
    var addresses = new Dictionary<int, Address> { { home.Id, home }, { work.Id, work } };

    var schedule = ScheduleBuilder.BuildFromTasks(tasks, addresses, DefaultConfig);

    var actions = schedule.Entries.Select(e => e.Action).ToList();
    Assert.Contains(ActionType.Sleep, actions);
    Assert.Contains(ActionType.Idle, actions);
    Assert.Contains(ActionType.Work, actions);
    Assert.Contains(ActionType.TravelByCar, actions);
}

[Fact]
public void BuildFromTasks_ScheduleCovers24Hours()
{
    var (home, work) = CreateAddresses();
    var tasks = CreateOfficerWorkerTasks(home, work);
    var addresses = new Dictionary<int, Address> { { home.Id, home }, { work.Id, work } };

    var schedule = ScheduleBuilder.BuildFromTasks(tasks, addresses, DefaultConfig);

    double totalHours = 0;
    foreach (var entry in schedule.Entries)
    {
        var duration = (entry.EndTime - entry.StartTime).TotalHours;
        if (duration < 0) duration += 24;
        totalHours += duration;
    }
    Assert.Equal(24.0, totalHours, precision: 1);
}

[Fact]
public void BuildFromTasks_ThirdAddress_ComputesTravelPerPair()
{
    var home = new Address { Id = 1, Position = new Vector2(100, 100), Type = AddressType.SuburbanHome };
    var work = new Address { Id = 2, Position = new Vector2(600, 100), Type = AddressType.Office };
    var crimeScene = new Address { Id = 3, Position = new Vector2(1000, 500), Type = AddressType.SuburbanHome };
    var addresses = new Dictionary<int, Address>
    {
        { home.Id, home }, { work.Id, work }, { crimeScene.Id, crimeScene }
    };

    var tasks = new List<SimTask>
    {
        new SimTask { Id = 1, ActionType = ActionType.Sleep, Priority = 30,
            WindowStart = new TimeSpan(22, 0, 0), WindowEnd = new TimeSpan(6, 0, 0),
            TargetAddressId = home.Id },
        new SimTask { Id = 2, ActionType = ActionType.Work, Priority = 20,
            WindowStart = new TimeSpan(9, 0, 0), WindowEnd = new TimeSpan(17, 0, 0),
            TargetAddressId = work.Id },
        new SimTask { Id = 3, ActionType = ActionType.KillPerson, Priority = 40,
            WindowStart = new TimeSpan(1, 0, 0), WindowEnd = new TimeSpan(1, 30, 0),
            TargetAddressId = crimeScene.Id },
        new SimTask { Id = 4, ActionType = ActionType.Idle, Priority = 10,
            WindowStart = TimeSpan.Zero, WindowEnd = TimeSpan.Zero,
            TargetAddressId = home.Id }
    };

    var schedule = ScheduleBuilder.BuildFromTasks(tasks, addresses, DefaultConfig);

    // KillPerson should appear in the schedule at 1 AM
    var killEntry = schedule.Entries.FirstOrDefault(e => e.Action == ActionType.KillPerson);
    Assert.NotNull(killEntry);

    // There should be travel to the crime scene
    var travelEntries = schedule.Entries.Where(e => e.Action == ActionType.TravelByCar).ToList();
    Assert.True(travelEntries.Count >= 2); // at least: to crime scene and from crime scene
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test stakeout.tests/ --filter "FullyQualifiedName~ScheduleBuilderTests" -v minimal`
Expected: FAIL — `BuildFromTasks` method doesn't exist

- [ ] **Step 3: Implement BuildFromTasks**

Add a new `BuildFromTasks` method to `ScheduleBuilder`. Keep the old `Build` method for now (we'll remove it in Task 11 when PersonGenerator switches over).

Key changes from the old `Build`:
- Input: `List<SimTask>` instead of `GoalSet`
- Each task carries its own `TargetAddressId` — no more `GetAddressForGoal()`
- Travel time computed per-transition pair using `addresses` dictionary, not a single precomputed value
- `GetWinningGoal()` → `GetWinningTask()` — same priority logic, returns `SimTask`

```csharp
public static DailySchedule BuildFromTasks(List<SimTask> tasks, Dictionary<int, Address> addresses, MapConfig config)
{
    // Step 1: For each minute, find the winning task by priority
    var minuteWinners = new SimTask[1440];
    // Need a fallback task (Idle at home) — find it or create a default
    SimTask fallback = tasks.FirstOrDefault(t => t.ActionType == ActionType.Idle)
        ?? tasks.FirstOrDefault();

    for (int m = 0; m < 1440; m++)
    {
        var time = TimeSpan.FromMinutes(m);
        minuteWinners[m] = GetWinningTask(tasks, time) ?? fallback;
    }

    // Step 2: Merge consecutive minutes with the same winning task into blocks
    var blocks = MergeIntoTaskBlocks(minuteWinners);

    // Step 3: Handle midnight wrapping
    if (blocks.Count > 1 && blocks[0].Task.Id == blocks[^1].Task.Id)
    {
        blocks[^1] = blocks[^1] with { EndMinute = blocks[0].EndMinute };
        blocks.RemoveAt(0);
    }

    // Step 4: Convert blocks to schedule entries with per-pair travel
    var schedule = new DailySchedule();
    for (int i = 0; i < blocks.Count; i++)
    {
        var block = blocks[i];
        var prevBlock = blocks[(i - 1 + blocks.Count) % blocks.Count];

        var currentAddressId = block.Task.TargetAddressId;
        var prevAddressId = prevBlock.Task.TargetAddressId;

        var blockStart = block.StartMinute;
        var blockEnd = block.EndMinute;

        // Insert travel if location changes
        if (currentAddressId.HasValue && prevAddressId.HasValue
            && currentAddressId.Value != prevAddressId.Value
            && addresses.ContainsKey(currentAddressId.Value)
            && addresses.ContainsKey(prevAddressId.Value))
        {
            var fromAddr = addresses[prevAddressId.Value];
            var toAddr = addresses[currentAddressId.Value];
            var travelMinutes = (int)Math.Ceiling(
                config.ComputeTravelTimeHours(fromAddr.Position, toAddr.Position) * 60);

            var travelEnd = Mod1440(blockStart + travelMinutes);

            schedule.Entries.Add(new ScheduleEntry
            {
                Action = ActionType.TravelByCar,
                StartTime = TimeSpan.FromMinutes(blockStart),
                EndTime = TimeSpan.FromMinutes(travelEnd),
                FromAddressId = prevAddressId.Value,
                TargetAddressId = currentAddressId.Value
            });

            blockStart = travelEnd;
        }

        schedule.Entries.Add(new ScheduleEntry
        {
            Action = block.Task.ActionType,
            StartTime = TimeSpan.FromMinutes(blockStart),
            EndTime = TimeSpan.FromMinutes(blockEnd),
            TargetAddressId = currentAddressId
        });
    }

    schedule.Entries.Sort((a, b) => a.StartTime.CompareTo(b.StartTime));
    return schedule;
}
```

Add the helper types/methods:

```csharp
private record TaskBlock(SimTask Task, int StartMinute, int EndMinute);

private static SimTask GetWinningTask(List<SimTask> tasks, TimeSpan time)
{
    SimTask winner = null;
    int winnerPriority = int.MinValue;
    foreach (var task in tasks)
    {
        if (!IsTaskActive(task, time))
            continue;
        if (task.Priority > winnerPriority)
        {
            winner = task;
            winnerPriority = task.Priority;
        }
    }
    return winner;
}

private static bool IsTaskActive(SimTask task, TimeSpan time)
{
    var start = task.WindowStart;
    var end = task.WindowEnd;
    if (start == end) return true; // always active
    if (start <= end) return time >= start && time < end;
    return time >= start || time < end; // wraps midnight
}

private static List<TaskBlock> MergeIntoTaskBlocks(SimTask[] minuteWinners)
{
    var blocks = new List<TaskBlock>();
    int blockStart = 0;
    var current = minuteWinners[0];

    for (int m = 1; m < 1440; m++)
    {
        if (minuteWinners[m].Id != current.Id)
        {
            blocks.Add(new TaskBlock(current, blockStart, m));
            blockStart = m;
            current = minuteWinners[m];
        }
    }
    blocks.Add(new TaskBlock(current, blockStart, 1440));
    return blocks;
}
```

- [ ] **Step 4: Run tests**

Run: `dotnet test stakeout.tests/ --filter "FullyQualifiedName~ScheduleBuilderTests" -v minimal`
Expected: all pass (old and new tests)

- [ ] **Step 5: Commit**

```
git add src/simulation/scheduling/ScheduleBuilder.cs stakeout.tests/Simulation/Scheduling/ScheduleBuilderTests.cs
git commit -m "feat: add BuildFromTasks to ScheduleBuilder

Task-based schedule building with per-transition travel computation
and data-driven address resolution. Old Build method preserved for
migration."
```

---

## Task 7: ObjectiveResolver

Decomposes Objectives into Tasks. Handles both recurring CoreNeed objectives and sequential crime objectives.

**Files:**
- Create: `src/simulation/objectives/ObjectiveResolver.cs`
- Create: `stakeout.tests/Simulation/Objectives/ObjectiveResolverTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
// stakeout.tests/Simulation/Objectives/ObjectiveResolverTests.cs
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Stakeout.Simulation;
using Stakeout.Simulation.Actions;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Objectives;
using Xunit;

namespace Stakeout.Tests.Simulation.Objectives;

public class ObjectiveResolverTests
{
    private static SimulationState CreateTestState()
    {
        var state = new SimulationState(new GameClock(new DateTime(1980, 1, 1, 0, 0, 0)));
        var home = new Address { Id = state.GenerateEntityId(), Position = new Vector2(100, 100), Type = AddressType.SuburbanHome };
        var work = new Address { Id = state.GenerateEntityId(), Position = new Vector2(600, 100), Type = AddressType.Office };
        state.Addresses[home.Id] = home;
        state.Addresses[work.Id] = work;
        return state;
    }

    [Fact]
    public void ResolveTasks_CoreNeeds_ProducesThreeTasks()
    {
        var state = CreateTestState();
        var homeId = state.Addresses.Keys.First();
        var workId = state.Addresses.Keys.Last();

        var objectives = new List<Objective>
        {
            ObjectiveResolver.CreateGetSleepObjective(
                new TimeSpan(22, 0, 0), new TimeSpan(6, 0, 0), homeId),
            ObjectiveResolver.CreateMaintainJobObjective(
                new TimeSpan(9, 0, 0), new TimeSpan(17, 0, 0), workId),
            ObjectiveResolver.CreateDefaultIdleObjective(homeId)
        };

        var tasks = ObjectiveResolver.ResolveTasks(objectives, state);

        Assert.Equal(3, tasks.Count);
        Assert.Contains(tasks, t => t.ActionType == ActionType.Sleep);
        Assert.Contains(tasks, t => t.ActionType == ActionType.Work);
        Assert.Contains(tasks, t => t.ActionType == ActionType.Idle);
    }

    [Fact]
    public void ResolveTasks_CoreNeeds_PrioritiesMatch()
    {
        var state = CreateTestState();
        var homeId = state.Addresses.Keys.First();
        var workId = state.Addresses.Keys.Last();

        var objectives = new List<Objective>
        {
            ObjectiveResolver.CreateGetSleepObjective(
                new TimeSpan(22, 0, 0), new TimeSpan(6, 0, 0), homeId),
            ObjectiveResolver.CreateMaintainJobObjective(
                new TimeSpan(9, 0, 0), new TimeSpan(17, 0, 0), workId),
            ObjectiveResolver.CreateDefaultIdleObjective(homeId)
        };

        var tasks = ObjectiveResolver.ResolveTasks(objectives, state);

        Assert.Equal(30, tasks.First(t => t.ActionType == ActionType.Sleep).Priority);
        Assert.Equal(20, tasks.First(t => t.ActionType == ActionType.Work).Priority);
        Assert.Equal(10, tasks.First(t => t.ActionType == ActionType.Idle).Priority);
    }

    [Fact]
    public void ResolveTasks_InstantStep_ExecutesAndAdvances()
    {
        var state = CreateTestState();
        var homeId = state.Addresses.Keys.First();

        // Create a person to be the potential victim
        var victim = new Person
        {
            Id = state.GenerateEntityId(),
            FirstName = "Vic",
            LastName = "Tim",
            IsAlive = true,
            HomeAddressId = homeId
        };
        state.People[victim.Id] = victim;

        var objective = new Objective
        {
            Id = 100,
            Type = ObjectiveType.CommitMurder,
            Source = ObjectiveSource.CrimeTemplate,
            Priority = 40,
            Steps = new List<ObjectiveStep>
            {
                new ObjectiveStep
                {
                    Description = "Choose victim",
                    IsInstant = true,
                    Status = StepStatus.Active,
                    ResolveFunc = (obj, st) =>
                    {
                        // Pick first alive person
                        var target = st.People.Values.First(p => p.IsAlive);
                        obj.Data["VictimId"] = target.Id;
                        return null; // instant, no task
                    }
                },
                new ObjectiveStep
                {
                    Description = "Kill victim",
                    ActionType = ActionType.KillPerson,
                    Status = StepStatus.Pending,
                    ResolveFunc = (obj, st) =>
                    {
                        var victimId = (int)obj.Data["VictimId"];
                        var victimPerson = st.People[victimId];
                        return new SimTask
                        {
                            Id = st.GenerateEntityId(),
                            ObjectiveId = obj.Id,
                            StepIndex = 1,
                            ActionType = ActionType.KillPerson,
                            Priority = obj.Priority,
                            WindowStart = new TimeSpan(1, 0, 0),
                            WindowEnd = new TimeSpan(1, 30, 0),
                            TargetAddressId = victimPerson.HomeAddressId
                        };
                    }
                }
            }
        };

        var tasks = ObjectiveResolver.ResolveTasks(new List<Objective> { objective }, state);

        // Instant step should have executed
        Assert.True(objective.Data.ContainsKey("VictimId"));
        Assert.Equal(StepStatus.Completed, objective.Steps[0].Status);
        Assert.Equal(1, objective.CurrentStepIndex);

        // KillPerson task should be generated
        Assert.Contains(tasks, t => t.ActionType == ActionType.KillPerson);
    }

    [Fact]
    public void ResolveTasks_CompletedObjective_ProducesNoTasks()
    {
        var state = CreateTestState();
        var objective = new Objective
        {
            Id = 1,
            Type = ObjectiveType.CommitMurder,
            Status = ObjectiveStatus.Completed,
            Steps = new List<ObjectiveStep>()
        };

        var tasks = ObjectiveResolver.ResolveTasks(new List<Objective> { objective }, state);

        Assert.Empty(tasks);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test stakeout.tests/ --filter "FullyQualifiedName~ObjectiveResolverTests" -v minimal`
Expected: FAIL

- [ ] **Step 3: Implement ObjectiveResolver**

```csharp
// src/simulation/objectives/ObjectiveResolver.cs
using System;
using System.Collections.Generic;
using Stakeout.Simulation.Actions;

namespace Stakeout.Simulation.Objectives;

public static class ObjectiveResolver
{
    public static List<SimTask> ResolveTasks(List<Objective> objectives, SimulationState state)
    {
        var tasks = new List<SimTask>();

        foreach (var objective in objectives)
        {
            if (objective.Status != ObjectiveStatus.Active)
                continue;

            if (objective.IsRecurring)
            {
                // Recurring objectives: call the single step's ResolveFunc
                var step = objective.CurrentStep;
                if (step?.ResolveFunc != null)
                {
                    var task = step.ResolveFunc(objective, state);
                    if (task != null)
                        tasks.Add(task);
                }
                continue;
            }

            // Sequential objectives: process instant steps, then resolve current step
            while (objective.CurrentStep != null && objective.CurrentStep.IsInstant)
            {
                var step = objective.CurrentStep;
                step.Status = StepStatus.Active;
                step.ResolveFunc?.Invoke(objective, state);
                objective.AdvanceStep();

                if (objective.Status == ObjectiveStatus.Completed)
                    break;
            }

            if (objective.Status == ObjectiveStatus.Completed)
                continue;

            // Resolve the current (non-instant) step into a task
            var currentStep = objective.CurrentStep;
            if (currentStep?.ResolveFunc != null)
            {
                currentStep.Status = StepStatus.Active;
                var task = currentStep.ResolveFunc(objective, state);
                if (task != null)
                    tasks.Add(task);
            }
        }

        return tasks;
    }

    public static Objective CreateGetSleepObjective(TimeSpan sleepTime, TimeSpan wakeTime, int homeAddressId)
    {
        var obj = new Objective
        {
            Type = ObjectiveType.GetSleep,
            Source = ObjectiveSource.CoreNeed,
            Priority = 30,
            IsRecurring = true,
            Steps = new List<ObjectiveStep>
            {
                new ObjectiveStep
                {
                    Description = "Sleep",
                    ActionType = ActionType.Sleep,
                    Status = StepStatus.Active,
                    ResolveFunc = (o, _) => new SimTask
                    {
                        ActionType = ActionType.Sleep,
                        Priority = o.Priority,
                        WindowStart = sleepTime,
                        WindowEnd = wakeTime,
                        TargetAddressId = homeAddressId
                    }
                }
            }
        };
        return obj;
    }

    public static Objective CreateMaintainJobObjective(TimeSpan shiftStart, TimeSpan shiftEnd, int workAddressId)
    {
        var obj = new Objective
        {
            Type = ObjectiveType.MaintainJob,
            Source = ObjectiveSource.CoreNeed,
            Priority = 20,
            IsRecurring = true,
            Steps = new List<ObjectiveStep>
            {
                new ObjectiveStep
                {
                    Description = "Work",
                    ActionType = ActionType.Work,
                    Status = StepStatus.Active,
                    ResolveFunc = (o, _) => new SimTask
                    {
                        ActionType = ActionType.Work,
                        Priority = o.Priority,
                        WindowStart = shiftStart,
                        WindowEnd = shiftEnd,
                        TargetAddressId = workAddressId
                    }
                }
            }
        };
        return obj;
    }

    public static Objective CreateDefaultIdleObjective(int homeAddressId)
    {
        var obj = new Objective
        {
            Type = ObjectiveType.DefaultIdle,
            Source = ObjectiveSource.CoreNeed,
            Priority = 10,
            IsRecurring = true,
            Steps = new List<ObjectiveStep>
            {
                new ObjectiveStep
                {
                    Description = "Idle at home",
                    ActionType = ActionType.Idle,
                    Status = StepStatus.Active,
                    ResolveFunc = (o, _) => new SimTask
                    {
                        ActionType = ActionType.Idle,
                        Priority = o.Priority,
                        WindowStart = TimeSpan.Zero,
                        WindowEnd = TimeSpan.Zero,
                        TargetAddressId = homeAddressId
                    }
                }
            }
        };
        return obj;
    }
}
```

- [ ] **Step 4: Run tests**

Run: `dotnet test stakeout.tests/ --filter "FullyQualifiedName~ObjectiveResolverTests" -v minimal`
Expected: PASS

- [ ] **Step 5: Commit**

```
git add src/simulation/objectives/ObjectiveResolver.cs stakeout.tests/Simulation/Objectives/ObjectiveResolverTests.cs
git commit -m "feat: add ObjectiveResolver — decomposes Objectives into Tasks

Handles recurring CoreNeed objectives (sleep, work, idle) and
sequential objectives with instant step execution."
```

---

## Task 8: ActionExecutor

Executes actions when tasks become active, modifies world state, and produces traces.

**Files:**
- Create: `src/simulation/actions/ActionExecutor.cs`
- Create: `stakeout.tests/Simulation/Actions/ActionExecutorTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
// stakeout.tests/Simulation/Actions/ActionExecutorTests.cs
using System;
using System.Linq;
using Godot;
using Stakeout.Simulation;
using Stakeout.Simulation.Actions;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Objectives;
using Stakeout.Simulation.Traces;
using Xunit;

namespace Stakeout.Tests.Simulation.Actions;

public class ActionExecutorTests
{
    private static SimulationState CreateStateWithKillerAndVictim(
        out Person killer, out Person victim, out Objective objective)
    {
        var state = new SimulationState(new GameClock(new DateTime(1980, 1, 2, 1, 0, 0)));
        var homeA = new Address { Id = state.GenerateEntityId(), Position = new Vector2(100, 100),
            Type = AddressType.SuburbanHome, Number = 10, StreetId = 1 };
        var homeB = new Address { Id = state.GenerateEntityId(), Position = new Vector2(500, 500),
            Type = AddressType.SuburbanHome, Number = 20, StreetId = 1 };
        state.Addresses[homeA.Id] = homeA;
        state.Addresses[homeB.Id] = homeB;

        killer = new Person
        {
            Id = state.GenerateEntityId(), FirstName = "Kill", LastName = "Er",
            IsAlive = true, HomeAddressId = homeA.Id,
            CurrentAddressId = homeB.Id, CurrentPosition = homeB.Position
        };
        state.People[killer.Id] = killer;

        victim = new Person
        {
            Id = state.GenerateEntityId(), FirstName = "Vic", LastName = "Tim",
            IsAlive = true, HomeAddressId = homeB.Id,
            CurrentAddressId = homeB.Id, CurrentPosition = homeB.Position
        };
        state.People[victim.Id] = victim;

        objective = new Objective
        {
            Id = state.GenerateEntityId(),
            Type = ObjectiveType.CommitMurder,
            Priority = 40
        };
        objective.Data["VictimId"] = victim.Id;

        return state;
    }

    [Fact]
    public void ExecuteKillPerson_SetsVictimDead()
    {
        var state = CreateStateWithKillerAndVictim(out var killer, out var victim, out var objective);
        var task = new SimTask
        {
            Id = 1, ObjectiveId = objective.Id, ActionType = ActionType.KillPerson,
            ActionData = new() { { "VictimId", victim.Id } }
        };

        ActionExecutor.Execute(task, killer, objective, state);

        Assert.False(victim.IsAlive);
    }

    [Fact]
    public void ExecuteKillPerson_ProducesConditionTrace()
    {
        var state = CreateStateWithKillerAndVictim(out var killer, out var victim, out var objective);
        var task = new SimTask
        {
            Id = 1, ObjectiveId = objective.Id, ActionType = ActionType.KillPerson,
            ActionData = new() { { "VictimId", victim.Id } }
        };

        ActionExecutor.Execute(task, killer, objective, state);

        var condition = state.Traces.Values.FirstOrDefault(t => t.TraceType == TraceType.Condition);
        Assert.NotNull(condition);
        Assert.Equal(victim.Id, condition.AttachedToPersonId);
        Assert.Equal(killer.Id, condition.CreatedByPersonId);
    }

    [Fact]
    public void ExecuteKillPerson_ProducesMarkTrace()
    {
        var state = CreateStateWithKillerAndVictim(out var killer, out var victim, out var objective);
        var task = new SimTask
        {
            Id = 1, ObjectiveId = objective.Id, ActionType = ActionType.KillPerson,
            ActionData = new() { { "VictimId", victim.Id } }
        };

        ActionExecutor.Execute(task, killer, objective, state);

        var mark = state.Traces.Values.FirstOrDefault(t => t.TraceType == TraceType.Mark);
        Assert.NotNull(mark);
        Assert.Equal(killer.CurrentAddressId, mark.LocationId);
    }

    [Fact]
    public void ExecuteKillPerson_LogsPersonDiedEvent()
    {
        var state = CreateStateWithKillerAndVictim(out var killer, out var victim, out var objective);
        var task = new SimTask
        {
            Id = 1, ObjectiveId = objective.Id, ActionType = ActionType.KillPerson,
            ActionData = new() { { "VictimId", victim.Id } }
        };

        ActionExecutor.Execute(task, killer, objective, state);

        var deathEvent = state.Journal.AllEvents.FirstOrDefault(
            e => e.EventType == SimulationEventType.PersonDied);
        Assert.NotNull(deathEvent);
        Assert.Equal(victim.Id, deathEvent.PersonId);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test stakeout.tests/ --filter "FullyQualifiedName~ActionExecutorTests" -v minimal`
Expected: FAIL

- [ ] **Step 3: Implement ActionExecutor**

```csharp
// src/simulation/actions/ActionExecutor.cs
using System;
using System.Collections.Generic;
using Stakeout.Simulation.Events;
using Stakeout.Simulation.Objectives;
using Stakeout.Simulation.Traces;

namespace Stakeout.Simulation.Actions;

public static class ActionExecutor
{
    public static void Execute(SimTask task, Entities.Person actor, Objective objective, SimulationState state)
    {
        switch (task.ActionType)
        {
            case ActionType.KillPerson:
                ExecuteKillPerson(task, actor, objective, state);
                break;
            // Work, Sleep, Idle, TravelByCar have no special execution logic
        }
    }

    private static void ExecuteKillPerson(SimTask task, Entities.Person killer, Objective objective, SimulationState state)
    {
        var victimId = task.ActionData != null && task.ActionData.ContainsKey("VictimId")
            ? (int)task.ActionData["VictimId"]
            : (int)objective.Data["VictimId"];

        var victim = state.People[victimId];
        victim.IsAlive = false;
        victim.CurrentAction = ActionType.Idle; // Dead people do nothing

        // Log death event
        state.Journal.Append(new SimulationEvent
        {
            Timestamp = state.Clock.CurrentTime,
            PersonId = victim.Id,
            EventType = SimulationEventType.PersonDied,
            AddressId = victim.CurrentAddressId
        });

        // Produce condition trace (cause of death)
        var conditionTrace = new Trace
        {
            Id = state.GenerateEntityId(),
            TraceType = TraceType.Condition,
            CreatedAt = state.Clock.CurrentTime,
            CreatedByPersonId = killer.Id,
            AttachedToPersonId = victim.Id,
            Description = "Cause of death: homicide"
        };
        state.Traces[conditionTrace.Id] = conditionTrace;

        // Produce mark trace (crime scene evidence)
        var markTrace = new Trace
        {
            Id = state.GenerateEntityId(),
            TraceType = TraceType.Mark,
            CreatedAt = state.Clock.CurrentTime,
            CreatedByPersonId = killer.Id,
            LocationId = killer.CurrentAddressId,
            Description = "Signs of violent struggle"
        };
        state.Traces[markTrace.Id] = markTrace;

        // Log crime committed event
        state.Journal.Append(new SimulationEvent
        {
            Timestamp = state.Clock.CurrentTime,
            PersonId = killer.Id,
            EventType = SimulationEventType.CrimeCommitted,
            AddressId = killer.CurrentAddressId
        });
    }
}
```

- [ ] **Step 4: Run tests**

Run: `dotnet test stakeout.tests/ --filter "FullyQualifiedName~ActionExecutorTests" -v minimal`
Expected: PASS

- [ ] **Step 5: Commit**

```
git add src/simulation/actions/ActionExecutor.cs stakeout.tests/Simulation/Actions/ActionExecutorTests.cs
git commit -m "feat: add ActionExecutor with KillPerson action

Modifies world state (victim dies) and produces Condition and Mark
traces at the crime scene."
```

---

## Task 9: SerialKiller crime template + CrimeGenerator

**Files:**
- Create: `src/simulation/crimes/ICrimeTemplate.cs`
- Create: `src/simulation/crimes/SerialKillerTemplate.cs`
- Create: `src/simulation/crimes/CrimeGenerator.cs`
- Create: `stakeout.tests/Simulation/Crimes/SerialKillerTemplateTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
// stakeout.tests/Simulation/Crimes/SerialKillerTemplateTests.cs
using System;
using System.Linq;
using Godot;
using Stakeout.Simulation;
using Stakeout.Simulation.Actions;
using Stakeout.Simulation.Crimes;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Objectives;
using Xunit;

namespace Stakeout.Tests.Simulation.Crimes;

public class SerialKillerTemplateTests
{
    private static SimulationState CreateStateWithPeople(int count)
    {
        var state = new SimulationState(new GameClock(new DateTime(1980, 1, 1, 8, 0, 0)));
        for (int i = 0; i < count; i++)
        {
            var home = new Address
            {
                Id = state.GenerateEntityId(),
                Position = new Vector2(100 + i * 100, 100),
                Type = AddressType.SuburbanHome, Number = i + 1, StreetId = 1
            };
            state.Addresses[home.Id] = home;

            var person = new Person
            {
                Id = state.GenerateEntityId(),
                FirstName = $"Person{i}",
                LastName = "Test",
                IsAlive = true,
                HomeAddressId = home.Id,
                CurrentAddressId = home.Id,
                CurrentPosition = home.Position
            };
            state.People[person.Id] = person;
        }
        return state;
    }

    [Fact]
    public void Instantiate_CreatesCrimeWithKillerRole()
    {
        var state = CreateStateWithPeople(3);
        var template = new SerialKillerTemplate();

        var crime = template.Instantiate(state);

        Assert.NotNull(crime);
        Assert.Equal(CrimeTemplateType.SerialKiller, crime.TemplateType);
        Assert.Equal(CrimeStatus.InProgress, crime.Status);
        Assert.True(crime.Roles.ContainsKey("Killer"));
        Assert.NotNull(crime.Roles["Killer"]);
    }

    [Fact]
    public void Instantiate_VictimStartsNull()
    {
        var state = CreateStateWithPeople(3);
        var template = new SerialKillerTemplate();

        var crime = template.Instantiate(state);

        Assert.True(crime.Roles.ContainsKey("Victim"));
        Assert.Null(crime.Roles["Victim"]);
    }

    [Fact]
    public void Instantiate_InjectsObjectiveIntoKiller()
    {
        var state = CreateStateWithPeople(3);
        var template = new SerialKillerTemplate();

        var crime = template.Instantiate(state);

        var killerId = crime.Roles["Killer"].Value;
        var killer = state.People[killerId];
        Assert.Contains(killer.Objectives, o => o.Type == ObjectiveType.CommitMurder);
    }

    [Fact]
    public void Instantiate_AddsCrimeToState()
    {
        var state = CreateStateWithPeople(3);
        var template = new SerialKillerTemplate();

        var crime = template.Instantiate(state);

        Assert.Contains(crime.Id, state.Crimes.Keys);
    }

    [Fact]
    public void Instantiate_WithOnePerson_ReturnsNull()
    {
        var state = CreateStateWithPeople(1);
        var template = new SerialKillerTemplate();

        var crime = template.Instantiate(state);

        Assert.Null(crime); // need at least 2 people (killer + victim)
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test stakeout.tests/ --filter "FullyQualifiedName~SerialKillerTemplateTests" -v minimal`
Expected: FAIL

- [ ] **Step 3: Implement ICrimeTemplate**

```csharp
// src/simulation/crimes/ICrimeTemplate.cs
namespace Stakeout.Simulation.Crimes;

public interface ICrimeTemplate
{
    CrimeTemplateType Type { get; }
    string Name { get; }
    Crime Instantiate(SimulationState state);
}
```

- [ ] **Step 4: Implement SerialKillerTemplate**

```csharp
// src/simulation/crimes/SerialKillerTemplate.cs
using System;
using System.Collections.Generic;
using System.Linq;
using Stakeout.Simulation.Actions;
using Stakeout.Simulation.Objectives;

namespace Stakeout.Simulation.Crimes;

public class SerialKillerTemplate : ICrimeTemplate
{
    private readonly Random _random = new();

    public CrimeTemplateType Type => CrimeTemplateType.SerialKiller;
    public string Name => "Serial Killer";

    public Crime Instantiate(SimulationState state)
    {
        var alivePeople = state.People.Values.Where(p => p.IsAlive).ToList();
        if (alivePeople.Count < 2)
            return null;

        // Pick killer
        var killer = alivePeople[_random.Next(alivePeople.Count)];

        // Create crime record
        var crime = new Crime
        {
            Id = state.GenerateEntityId(),
            TemplateType = CrimeTemplateType.SerialKiller,
            CreatedAt = state.Clock.CurrentTime,
            Roles = new Dictionary<string, int?>
            {
                { "Killer", killer.Id },
                { "Victim", null }
            }
        };

        // Build the CommitMurder objective
        var objective = new Objective
        {
            Id = state.GenerateEntityId(),
            Type = ObjectiveType.CommitMurder,
            Source = ObjectiveSource.CrimeTemplate,
            SourceEntityId = crime.Id,
            Priority = 40,
            Steps = new List<ObjectiveStep>
            {
                new ObjectiveStep
                {
                    Description = "Choose victim",
                    IsInstant = true,
                    Status = StepStatus.Active,
                    ResolveFunc = (obj, st) =>
                    {
                        var candidates = st.People.Values
                            .Where(p => p.IsAlive && p.Id != killer.Id)
                            .ToList();
                        if (candidates.Count == 0) return null;
                        var victim = candidates[_random.Next(candidates.Count)];
                        obj.Data["VictimId"] = victim.Id;
                        crime.Roles["Victim"] = victim.Id;
                        return null;
                    }
                },
                new ObjectiveStep
                {
                    Description = "Kill victim",
                    ActionType = ActionType.KillPerson,
                    Status = StepStatus.Pending,
                    ResolveFunc = (obj, st) =>
                    {
                        var victimId = (int)obj.Data["VictimId"];
                        var victim = st.People[victimId];
                        return new SimTask
                        {
                            Id = st.GenerateEntityId(),
                            ObjectiveId = obj.Id,
                            StepIndex = 1,
                            ActionType = ActionType.KillPerson,
                            Priority = obj.Priority,
                            WindowStart = new TimeSpan(1, 0, 0),
                            WindowEnd = new TimeSpan(1, 30, 0),
                            TargetAddressId = victim.HomeAddressId,
                            ActionData = new Dictionary<string, object>
                            {
                                { "VictimId", victimId }
                            }
                        };
                    }
                },
                new ObjectiveStep
                {
                    Description = "Go home",
                    ActionType = ActionType.Idle,
                    Status = StepStatus.Pending,
                    ResolveFunc = (obj, _) => new SimTask
                    {
                        Id = state.GenerateEntityId(),
                        ObjectiveId = obj.Id,
                        StepIndex = 2,
                        ActionType = ActionType.Idle,
                        Priority = obj.Priority,
                        WindowStart = new TimeSpan(1, 30, 0),
                        WindowEnd = new TimeSpan(3, 0, 0),
                        TargetAddressId = killer.HomeAddressId
                    }
                }
            }
        };

        crime.ObjectiveIds.Add(objective.Id);
        killer.Objectives.Add(objective);
        state.Crimes[crime.Id] = crime;

        return crime;
    }
}
```

- [ ] **Step 5: Implement CrimeGenerator**

```csharp
// src/simulation/crimes/CrimeGenerator.cs
using System.Collections.Generic;

namespace Stakeout.Simulation.Crimes;

public class CrimeGenerator
{
    private readonly Dictionary<CrimeTemplateType, ICrimeTemplate> _templates = new()
    {
        { CrimeTemplateType.SerialKiller, new SerialKillerTemplate() }
    };

    public Crime Generate(CrimeTemplateType templateType, SimulationState state)
    {
        if (!_templates.TryGetValue(templateType, out var template))
            return null;

        return template.Instantiate(state);
    }

    public IEnumerable<ICrimeTemplate> GetAvailableTemplates() => _templates.Values;
}
```

- [ ] **Step 6: Run tests**

Run: `dotnet test stakeout.tests/ --filter "FullyQualifiedName~SerialKillerTemplateTests" -v minimal`
Expected: PASS

- [ ] **Step 7: Commit**

```
git add src/simulation/crimes/ stakeout.tests/Simulation/Crimes/
git commit -m "feat: add SerialKiller crime template and CrimeGenerator

Template picks a killer, creates CommitMurder objective with
ChooseVictim (instant) → KillPerson → GoHome steps."
```

---

## Task 10: Refactor PersonBehavior, PersonGenerator, SimulationManager

Wire everything together. PersonBehavior skips dead NPCs and executes actions. PersonGenerator creates Objectives. SimulationManager manages schedule rebuilds.

**Files:**
- Modify: `src/simulation/scheduling/PersonBehavior.cs`
- Modify: `src/simulation/PersonGenerator.cs`
- Modify: `src/simulation/SimulationManager.cs`
- Modify: `stakeout.tests/Simulation/Scheduling/PersonBehaviorTests.cs`
- Modify: `stakeout.tests/Simulation/PersonGeneratorTests.cs`

- [ ] **Step 1: Update PersonBehavior to skip dead NPCs and execute actions**

Key changes:
- `Update()` signature changes: takes `Person` and `SimulationState` only (reads `person.Schedule`)
- Early return if `!person.IsAlive`
- After a task boundary transition, check if the new entry has an associated action to execute (KillPerson)
- Call `ActionExecutor.Execute()` for action tasks
- After action execution, advance the objective and trigger schedule rebuild

```csharp
// PersonBehavior.cs changes:
// - Update signature: Update(Person person, SimulationState state)
//   reads schedule from person.Schedule
// - Add at top of Update: if (!person.IsAlive) return;
// - In Transition: after switching to new activity, check if action needs executing
//   if entry.Action == ActionType.KillPerson (or other executable actions):
//     find the objective/task and call ActionExecutor.Execute()
//     advance objective step
//     set person.NeedsScheduleRebuild = true (or call rebuild directly)
```

Add a `NeedsScheduleRebuild` flag to Person (simple bool, checked by SimulationManager).

- [ ] **Step 2: Update PersonGenerator to create Objectives**

Replace `GoalSetBuilder.Build()` call with Objective creation:

```csharp
// Instead of:
//   var goalSet = GoalSetBuilder.Build(job, sleepTime, wakeTime);
//   var schedule = ScheduleBuilder.Build(goalSet, homeAddress, workAddress, _mapConfig);
// Do:
//   person.Objectives = new List<Objective>
//   {
//       ObjectiveResolver.CreateGetSleepObjective(sleepTime, wakeTime, homeAddress.Id),
//       ObjectiveResolver.CreateMaintainJobObjective(job.ShiftStart, job.ShiftEnd, workAddress.Id),
//       ObjectiveResolver.CreateDefaultIdleObjective(homeAddress.Id)
//   };
//   RebuildSchedule(person, state);
```

Change `GeneratePerson` return type from `(Person, DailySchedule)` to just `Person` since schedule now lives on Person.

- [ ] **Step 3: Update SimulationManager**

- Remove `_schedules` dictionary
- Add `RebuildSchedule(Person, SimulationState)` static method:
  1. `ObjectiveResolver.ResolveTasks(person.Objectives, state)`
  2. Build address dictionary from task target IDs
  3. `ScheduleBuilder.BuildFromTasks(tasks, addresses, mapConfig)`
  4. Store on `person.Schedule`
- In `_Process`: call `_personBehavior.Update(person, State)` (no schedule param)
- After all updates, check for persons needing schedule rebuild
- Add `CrimeGenerator` field, exposed for debug UI

```csharp
// In _Process loop:
foreach (var person in State.People.Values)
{
    if (!person.IsAlive) continue;
    _personBehavior.Update(person, State);
}

// After person updates, check for rebuilds
foreach (var person in State.People.Values)
{
    if (person.NeedsScheduleRebuild)
    {
        RebuildSchedule(person, State);
        person.NeedsScheduleRebuild = false;
    }
}
```

- [ ] **Step 4: Update PersonBehaviorTests**

- Change `CreateTestScenario` to put schedule on person instead of returning it separately
- Change `behavior.Update(person, schedule, state)` → `behavior.Update(person, state)`
- Add test for dead NPC skipping:

```csharp
[Fact]
public void Update_DeadPerson_DoesNothing()
{
    var (state, person, _) = CreateTestScenario();
    person.IsAlive = false;
    person.CurrentAction = ActionType.Idle;
    state.Clock.Tick(12 * 3600);
    var behavior = new PersonBehavior(new MapConfig());

    behavior.Update(person, state);

    Assert.Equal(ActionType.Idle, person.CurrentAction); // unchanged
}
```

- [ ] **Step 5: Update PersonGeneratorTests**

- Change `var (person, _) = CreateGenerator().GeneratePerson(state)` → `var person = CreateGenerator().GeneratePerson(state)`
- Remove tests that referenced the returned schedule; replace with tests that check `person.Schedule` and `person.Objectives`:

```csharp
[Fact]
public void GeneratePerson_HasCoreNeedObjectives()
{
    var state = CreateState();
    var person = CreateGenerator().GeneratePerson(state);
    Assert.Equal(3, person.Objectives.Count);
    Assert.Contains(person.Objectives, o => o.Type == ObjectiveType.GetSleep);
    Assert.Contains(person.Objectives, o => o.Type == ObjectiveType.MaintainJob);
    Assert.Contains(person.Objectives, o => o.Type == ObjectiveType.DefaultIdle);
}

[Fact]
public void GeneratePerson_HasSchedule()
{
    var state = CreateState();
    var person = CreateGenerator().GeneratePerson(state);
    Assert.NotNull(person.Schedule);
    Assert.True(person.Schedule.Entries.Count > 0);
}
```

- [ ] **Step 6: Remove old Goal.cs**

Delete `src/simulation/scheduling/Goal.cs`. Remove the old `Build` method from ScheduleBuilder (keep only `BuildFromTasks`). Update any remaining references.

- [ ] **Step 7: Run all tests**

Run: `dotnet test stakeout.tests/ -v minimal`
Expected: all pass

- [ ] **Step 8: Commit**

```
git add -A
git commit -m "refactor: wire Objective → Task → Action pipeline end-to-end

PersonGenerator creates CoreNeed Objectives. ObjectiveResolver produces
Tasks. ScheduleBuilder.BuildFromTasks builds schedules. PersonBehavior
executes actions and skips dead NPCs. Remove old Goal/GoalSet system."
```

---

## Task 11: Debug UI — Crime Generator

**Files:**
- Modify: `scenes/game_shell/GameShell.cs`

- [ ] **Step 1: Add crime generator fields**

Add fields to GameShell:
```csharp
private Button _generateCrimeButton;
private Label _crimeResultLabel;
private CrimeGenerator _crimeGenerator;
```

- [ ] **Step 2: Add crime generator UI to SetupDebugPanel**

After the people list header, add:
- A "— Crime Generator —" section header
- A "Serial Killer" label
- A "Generate Now" button
- A result label

```csharp
// In SetupDebugPanel, add to the debug sidebar VBox:
var crimeHeader = new Label { Text = "— Crime Generator —" };
// style it like the people header

var templateLabel = new Label { Text = "Template: Serial Killer" };
// style it

_generateCrimeButton = new Button { Text = "Generate Now" };
// style it
_generateCrimeButton.Pressed += OnGenerateCrimePressed;

_crimeResultLabel = new Label { Text = "No crime active" };
// style it, autowrap

_crimeGenerator = new CrimeGenerator();
```

- [ ] **Step 3: Implement OnGenerateCrimePressed**

```csharp
private void OnGenerateCrimePressed()
{
    var crime = _crimeGenerator.Generate(CrimeTemplateType.SerialKiller, _simulationManager.State);
    if (crime == null)
    {
        _crimeResultLabel.Text = "Failed: not enough people";
        return;
    }

    var killerId = crime.Roles["Killer"].Value;
    var killer = _simulationManager.State.People[killerId];

    // Rebuild killer's schedule to include crime tasks
    _simulationManager.RebuildSchedule(killer);

    // Resolve objectives to pick victim
    ObjectiveResolver.ResolveTasks(killer.Objectives, _simulationManager.State);
    var victimId = crime.Roles["Victim"];
    var victimName = victimId.HasValue
        ? _simulationManager.State.People[victimId.Value].FullName
        : "unknown";

    _crimeResultLabel.Text = $"{killer.FullName} → murder → {victimName}";
}
```

- [ ] **Step 4: Build and manually test in Godot**

Run: `dotnet build -v minimal`
Expected: compiles. Then test in Godot editor: open debug sidebar, click Generate Now, observe result label updates.

- [ ] **Step 5: Commit**

```
git add scenes/game_shell/GameShell.cs
git commit -m "feat: add crime generator UI to debug sidebar"
```

---

## Task 12: CityView tooltip enhancement + dead NPC rendering

**Files:**
- Modify: `scenes/city/CityView.cs`

- [ ] **Step 1: Update _Process to render dead NPCs in red**

In the person dot color logic:
```csharp
Color color;
if (!person.IsAlive)
    color = new Color(1f, 0f, 0f); // red for dead
else if (person.CurrentAction == ActionType.Sleep)
    color = SleepingPersonColor;
else
    color = PersonColor;
```

Add a `DeadPersonColor` constant:
```csharp
private static readonly Color DeadPersonColor = new(1f, 0f, 0f);
```

- [ ] **Step 2: Update UpdateHoverLabel for enhanced tooltips**

Replace the hover label switch with action-aware tooltips:

```csharp
// In the person hover section:
var person = _simulationManager.State.People[personId];

string label;
if (!person.IsAlive)
{
    label = $"{person.FullName}: Dead";
}
else
{
    var actionLabel = person.CurrentAction switch
    {
        ActionType.Work => FormatWorkLabel(person),
        ActionType.Sleep => "Sleep",
        ActionType.TravelByCar => FormatTravelLabel(person),
        ActionType.Idle => "Idle",
        ActionType.KillPerson => "KillPerson",
        _ => person.CurrentAction.ToString()
    };
    label = $"{person.FullName}: {actionLabel}";
}
```

Add helper methods:
```csharp
private string FormatTravelLabel(Person person)
{
    if (person.TravelInfo == null) return "TravelByCar";
    var toAddr = _simulationManager.State.Addresses.GetValueOrDefault(person.TravelInfo.ToAddressId);
    if (toAddr == null) return "TravelByCar";
    var street = _simulationManager.State.Streets.GetValueOrDefault(toAddr.StreetId);
    return $"TravelByCar → {toAddr.Number} {street?.Name ?? "Unknown"}";
}

private string FormatWorkLabel(Person person)
{
    var job = _simulationManager.State.Jobs.GetValueOrDefault(person.JobId);
    if (job == null) return "Work";
    var workAddr = _simulationManager.State.Addresses.GetValueOrDefault(job.WorkAddressId);
    if (workAddr == null) return "Work";
    var street = _simulationManager.State.Streets.GetValueOrDefault(workAddr.StreetId);
    return $"Work at {workAddr.Number} {street?.Name ?? "Unknown"}";
}
```

- [ ] **Step 3: Build and verify**

Run: `dotnet build -v minimal`
Expected: compiles

- [ ] **Step 4: Commit**

```
git add scenes/city/CityView.cs
git commit -m "feat: enhanced city view tooltips and dead NPC red dots

Tooltips now show current action with destination/workplace details.
Dead NPCs render as red dots with 'Dead' tooltip."
```

---

## Task 13: Person Inspector Dialog

**Files:**
- Modify: `scenes/game_shell/GameShell.cs`

- [ ] **Step 1: Add click handler to debug people list**

In `PopulateDebugPeopleList`, add a left-click handler to each person button:

```csharp
btn.Pressed += () => ShowPersonInspector(personId);
```

- [ ] **Step 2: Implement ShowPersonInspector**

Create a `Window` dialog that shows all person data:

```csharp
private void ShowPersonInspector(int personId)
{
    var person = _simulationManager.State.People[personId];
    var state = _simulationManager.State;

    var window = new Window
    {
        Title = $"Inspector: {person.FullName}",
        Size = new Vector2I(500, 700),
        Position = new Vector2I(200, 100),
        Exclusive = false
    };

    var scroll = new ScrollContainer();
    scroll.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

    var vbox = new VBoxContainer();
    vbox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
    var font = _clockLabel.GetThemeFont("font");

    // Identity
    AddInspectorSection(vbox, font, "— Identity —", new[]
    {
        $"Name: {person.FullName}",
        $"ID: {person.Id}",
        $"Alive: {person.IsAlive}"
    });

    // Location
    var locationLines = new List<string>();
    if (person.TravelInfo != null)
    {
        var toAddr = state.Addresses.GetValueOrDefault(person.TravelInfo.ToAddressId);
        var street = toAddr != null ? state.Streets.GetValueOrDefault(toAddr.StreetId) : null;
        locationLines.Add($"In transit to: {toAddr?.Number} {street?.Name ?? "Unknown"}");
    }
    else if (person.CurrentAddressId.HasValue)
    {
        var addr = state.Addresses.GetValueOrDefault(person.CurrentAddressId.Value);
        var street = addr != null ? state.Streets.GetValueOrDefault(addr.StreetId) : null;
        locationLines.Add($"At: {addr?.Number} {street?.Name ?? "Unknown"} ({addr?.Type})");
    }
    locationLines.Add($"Position: ({person.CurrentPosition.X:F0}, {person.CurrentPosition.Y:F0})");
    AddInspectorSection(vbox, font, "— Location —", locationLines.ToArray());

    // Current State
    AddInspectorSection(vbox, font, "— Current State —", new[]
    {
        $"Action: {person.CurrentAction}"
    });

    // Job
    if (state.Jobs.TryGetValue(person.JobId, out var job))
    {
        var workAddr = state.Addresses.GetValueOrDefault(job.WorkAddressId);
        var workStreet = workAddr != null ? state.Streets.GetValueOrDefault(workAddr.StreetId) : null;
        AddInspectorSection(vbox, font, "— Job —", new[]
        {
            $"Title: {job.Title}",
            $"Work: {workAddr?.Number} {workStreet?.Name ?? "Unknown"}",
            $"Shift: {job.ShiftStart:hh\\:mm} - {job.ShiftEnd:hh\\:mm}"
        });
    }

    // Objectives
    var objLines = new List<string>();
    foreach (var obj in person.Objectives)
    {
        objLines.Add($"[{obj.Status}] {obj.Type} (pri:{obj.Priority}, src:{obj.Source})");
        for (int i = 0; i < obj.Steps.Count; i++)
        {
            var step = obj.Steps[i];
            var marker = step.Status == StepStatus.Completed ? "✓"
                : i == obj.CurrentStepIndex ? "→"
                : " ";
            objLines.Add($"  {marker} {step.Description} [{step.Status}]");
        }
    }
    if (objLines.Count > 0)
        AddInspectorSection(vbox, font, "— Objectives —", objLines.ToArray());

    // Schedule
    if (person.Schedule != null)
    {
        var schedLines = person.Schedule.Entries.Select(e =>
        {
            var targetStr = e.TargetAddressId.HasValue
                ? $" @ addr {e.TargetAddressId.Value}"
                : "";
            return $"[{e.StartTime:hh\\:mm}-{e.EndTime:hh\\:mm}] {e.Action}{targetStr}";
        }).ToArray();
        AddInspectorSection(vbox, font, "— Schedule —", schedLines);
    }

    // Recent Events
    var events = state.Journal.GetEventsForPerson(person.Id);
    var recentEvents = events.TakeLast(10).Reverse().Select(e =>
        $"{e.Timestamp:HH:mm:ss} {e.EventType}"
    ).ToArray();
    if (recentEvents.Length > 0)
        AddInspectorSection(vbox, font, "— Recent Events —", recentEvents);

    scroll.AddChild(vbox);
    window.AddChild(scroll);
    window.CloseRequested += () => window.QueueFree();
    AddChild(window);
    window.Show();
}

private static void AddInspectorSection(VBoxContainer vbox, Font font, string header, string[] lines)
{
    var headerLabel = new Label { Text = header };
    headerLabel.AddThemeFontOverride("font", font);
    headerLabel.AddThemeFontSizeOverride("font_size", 14);
    headerLabel.AddThemeColorOverride("font_color", new Color(0.3f, 0.6f, 1.0f));
    vbox.AddChild(headerLabel);

    foreach (var line in lines)
    {
        var label = new Label { Text = line };
        label.AddThemeFontOverride("font", font);
        label.AddThemeFontSizeOverride("font_size", 12);
        label.AddThemeColorOverride("font_color", new Color(0.9f, 0.9f, 0.9f));
        vbox.AddChild(label);
    }

    // Spacer
    vbox.AddChild(new Control { CustomMinimumSize = new Vector2(0, 10) });
}
```

- [ ] **Step 3: Build and verify**

Run: `dotnet build -v minimal`
Expected: compiles

- [ ] **Step 4: Commit**

```
git add scenes/game_shell/GameShell.cs
git commit -m "feat: add Person Inspector debug dialog

Click a person's name in the debug sidebar to view their identity,
location, current action, job, objectives with step status, schedule
timeline, and recent events."
```

---

## Task 14: End-to-end integration test

Write a test that exercises the full pipeline: generate crime → resolve objectives → build schedule → execute action → verify traces.

**Files:**
- Create: `stakeout.tests/Simulation/Crimes/CrimeIntegrationTests.cs`

- [ ] **Step 1: Write integration test**

```csharp
// stakeout.tests/Simulation/Crimes/CrimeIntegrationTests.cs
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Stakeout.Simulation;
using Stakeout.Simulation.Actions;
using Stakeout.Simulation.Crimes;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Objectives;
using Stakeout.Simulation.Scheduling;
using Stakeout.Simulation.Traces;
using Xunit;

namespace Stakeout.Tests.Simulation.Crimes;

public class CrimeIntegrationTests
{
    [Fact]
    public void FullCrimePipeline_SerialKiller_VictimDiesAndTracesProduced()
    {
        // Setup: state with 2 people
        var state = new SimulationState(new GameClock(new DateTime(1980, 1, 1, 0, 0, 0)));
        var street = new Street { Id = state.GenerateEntityId(), Name = "Oak St", CityId = 1 };
        state.Streets[street.Id] = street;

        var homeA = new Address { Id = state.GenerateEntityId(), Position = new Vector2(100, 100),
            Type = AddressType.SuburbanHome, Number = 10, StreetId = street.Id };
        var homeB = new Address { Id = state.GenerateEntityId(), Position = new Vector2(500, 100),
            Type = AddressType.SuburbanHome, Number = 20, StreetId = street.Id };
        var work = new Address { Id = state.GenerateEntityId(), Position = new Vector2(300, 300),
            Type = AddressType.Office, Number = 1, StreetId = street.Id };
        state.Addresses[homeA.Id] = homeA;
        state.Addresses[homeB.Id] = homeB;
        state.Addresses[work.Id] = work;

        var personA = new Person
        {
            Id = state.GenerateEntityId(), FirstName = "Alice", LastName = "A",
            IsAlive = true, HomeAddressId = homeA.Id, JobId = 0,
            CurrentAddressId = homeA.Id, CurrentPosition = homeA.Position,
            CurrentAction = ActionType.Sleep
        };
        var personB = new Person
        {
            Id = state.GenerateEntityId(), FirstName = "Bob", LastName = "B",
            IsAlive = true, HomeAddressId = homeB.Id, JobId = 0,
            CurrentAddressId = homeB.Id, CurrentPosition = homeB.Position,
            CurrentAction = ActionType.Sleep
        };
        state.People[personA.Id] = personA;
        state.People[personB.Id] = personB;

        // Step 1: Generate crime
        var generator = new CrimeGenerator();
        var crime = generator.Generate(CrimeTemplateType.SerialKiller, state);
        Assert.NotNull(crime);
        Assert.Equal(CrimeStatus.InProgress, crime.Status);

        // Step 2: Identify killer and resolve objectives (which picks victim)
        var killerId = crime.Roles["Killer"].Value;
        var killer = state.People[killerId];
        var tasks = ObjectiveResolver.ResolveTasks(killer.Objectives, state);

        // Victim should now be assigned
        Assert.NotNull(crime.Roles["Victim"]);
        var victimId = crime.Roles["Victim"].Value;
        var victim = state.People[victimId];
        Assert.NotEqual(killerId, victimId);

        // Step 3: Should have a KillPerson task
        var killTask = tasks.FirstOrDefault(t => t.ActionType == ActionType.KillPerson);
        Assert.NotNull(killTask);
        Assert.Equal(victim.HomeAddressId, killTask.TargetAddressId);

        // Step 4: Simulate the kill action directly
        var murderObjective = killer.Objectives.First(o => o.Type == ObjectiveType.CommitMurder);
        ActionExecutor.Execute(killTask, killer, murderObjective, state);

        // Step 5: Verify results
        Assert.False(victim.IsAlive);
        Assert.True(state.Traces.Count >= 2); // condition + mark
        Assert.Contains(state.Traces.Values, t => t.TraceType == TraceType.Condition);
        Assert.Contains(state.Traces.Values, t => t.TraceType == TraceType.Mark);
    }
}
```

- [ ] **Step 2: Run test**

Run: `dotnet test stakeout.tests/ --filter "FullyQualifiedName~CrimeIntegrationTests" -v minimal`
Expected: PASS

- [ ] **Step 3: Run full test suite**

Run: `dotnet test stakeout.tests/ -v minimal`
Expected: all pass

- [ ] **Step 4: Commit**

```
git add stakeout.tests/Simulation/Crimes/CrimeIntegrationTests.cs
git commit -m "test: add end-to-end crime pipeline integration test

Exercises: generate crime → resolve objectives → pick victim →
execute kill action → verify death and traces."
```

---

## Task 15: Final cleanup and full test run

- [ ] **Step 1: Remove any remaining references to old types**

Search for any lingering references to `ActivityType`, `GoalType`, `GoalSet`, `GoalSetBuilder` and remove them.

Run: `grep -r "ActivityType\|GoalType\|GoalSet\|GoalSetBuilder" src/ scenes/ stakeout.tests/ --include="*.cs"`
Expected: no matches

- [ ] **Step 2: Run full test suite**

Run: `dotnet test stakeout.tests/ -v minimal`
Expected: all pass

- [ ] **Step 3: Run build for main project**

Run: `dotnet build -v minimal`
Expected: Build succeeded

- [ ] **Step 4: Commit any cleanup**

```
git add -A
git commit -m "chore: remove all references to old Goal/ActivityType system"
```
