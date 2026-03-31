# Day Planner Overhaul Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix the NPC day planner so all schedules (including night-shift workers) produce valid plans, by switching from TimeSpan-based scheduling to a 24-hour DateTime window.

**Architecture:** The planner becomes `PlanDay(person, state, DateTime now)` → plans a 24-hour window from `now`. All time slots use `DateTime` instead of `TimeSpan`. Sleep competes with other objectives by priority instead of being structurally special-cased. Objectives receive `(planStart, planEnd)` as `DateTime` and return actions with `DateTime` time windows.

**Tech Stack:** C# / .NET / Godot 4.6 / xUnit

**Spec:** `docs/superpowers/specs/2026-03-29-day-planner-overhaul-design.md`

**CRITICAL: Never prefix shell commands with `cd`. The working directory is already the project root. Run commands directly (e.g., `dotnet test stakeout.tests/`, not `cd path && dotnet test`).**

---

### Task 1: Update PlannedAction — TimeSpan to DateTime

**Files:**
- Modify: `src/simulation/objectives/PlannedAction.cs`
- Modify: `stakeout.tests/Simulation/Brain/NpcBrainTests.cs` (TestObjective helper)

- [ ] **Step 1: Update PlannedAction fields**

In `src/simulation/objectives/PlannedAction.cs`, change `TimeWindowStart` and `TimeWindowEnd` from `TimeSpan` to `DateTime`:

```csharp
using System;
using Stakeout.Simulation.Actions;

namespace Stakeout.Simulation.Objectives;

public class PlannedAction
{
    public IAction Action { get; init; }
    public int TargetAddressId { get; init; }
    public DateTime TimeWindowStart { get; init; }
    public DateTime TimeWindowEnd { get; init; }
    public TimeSpan Duration { get; init; }
    public string DisplayText { get; init; }
    public Objective SourceObjective { get; init; }
}
```

- [ ] **Step 2: Verify the project compiles — expect errors**

Run: `dotnet build stakeout.tests/ -v minimal`
Expected: Compile errors in all files that create `PlannedAction` with `TimeSpan` values. This confirms the scope of changes needed. Note the list of files for subsequent tasks.

- [ ] **Step 3: Commit**

```bash
git add src/simulation/objectives/PlannedAction.cs
git commit -m "refactor: change PlannedAction time windows from TimeSpan to DateTime"
```

---

### Task 2: Update DayPlanEntry — TimeSpan to DateTime

**Files:**
- Modify: `src/simulation/brain/DayPlanEntry.cs`

- [ ] **Step 1: Update DayPlanEntry fields**

In `src/simulation/brain/DayPlanEntry.cs`, change `StartTime` and `EndTime` from `TimeSpan` to `DateTime`:

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
    public DateTime StartTime { get; init; }
    public DateTime EndTime { get; init; }
    public PlannedAction PlannedAction { get; init; }
    public DayPlanEntryStatus Status { get; set; } = DayPlanEntryStatus.Pending;
}
```

- [ ] **Step 2: Commit**

```bash
git add src/simulation/brain/DayPlanEntry.cs
git commit -m "refactor: change DayPlanEntry times from TimeSpan to DateTime"
```

---

### Task 3: Update Objective base class signature

**Files:**
- Modify: `src/simulation/objectives/Objective.cs`

- [ ] **Step 1: Change GetActionsForToday to GetActions**

Rename the method and change the signature to accept a `DateTime` planning window:

```csharp
using System;
using System.Collections.Generic;
using Stakeout.Simulation.Entities;

namespace Stakeout.Simulation.Objectives;

public abstract class Objective
{
    public int Id { get; set; }
    public abstract int Priority { get; }
    public abstract ObjectiveSource Source { get; }
    public ObjectiveStatus Status { get; set; } = ObjectiveStatus.Active;
    public List<Objective> Children { get; } = new();

    public abstract List<PlannedAction> GetActions(
        Person person,
        SimulationState state,
        DateTime planStart,
        DateTime planEnd);

    public virtual void OnActionCompleted(PlannedAction action, bool success) { }

    /// <summary>
    /// Called by ActionRunner after an action completes successfully.
    /// Override to emit traces at the action's location.
    /// </summary>
    public virtual void EmitTraces(PlannedAction action, Person person, SimulationState state) { }
}
```

- [ ] **Step 2: Commit**

```bash
git add src/simulation/objectives/Objective.cs
git commit -m "refactor: rename GetActionsForToday to GetActions with DateTime window"
```

---

### Task 4: Update SleepObjective

**Files:**
- Modify: `src/simulation/objectives/SleepObjective.cs`
- Modify: `stakeout.tests/Simulation/Objectives/SleepObjectiveTests.cs`

- [ ] **Step 1: Write failing tests for new SleepObjective behavior**

Replace the contents of `stakeout.tests/Simulation/Objectives/SleepObjectiveTests.cs`:

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
    private static (SimulationState state, Person person) CreateSetup(
        TimeSpan sleepTime, TimeSpan wakeTime)
    {
        var state = new SimulationState();
        state.Addresses[1] = new Address { Id = 1 };
        var person = new Person
        {
            Id = 1,
            HomeAddressId = 1,
            PreferredSleepTime = sleepTime,
            PreferredWakeTime = wakeTime
        };
        return (state, person);
    }

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
    public void GetActions_ReturnsSleepAction()
    {
        var (state, person) = CreateSetup(
            TimeSpan.FromHours(22), TimeSpan.FromHours(6));
        var planStart = new DateTime(1980, 1, 1, 6, 0, 0);
        var planEnd = planStart.AddHours(24);

        var obj = new SleepObjective();
        var actions = obj.GetActions(person, state, planStart, planEnd);

        Assert.Single(actions);
        Assert.Equal("sleeping", actions[0].DisplayText);
        Assert.Equal(1, actions[0].TargetAddressId);
    }

    [Fact]
    public void GetActions_DurationIs8Hours()
    {
        var (state, person) = CreateSetup(
            TimeSpan.FromHours(22), TimeSpan.FromHours(6));
        var planStart = new DateTime(1980, 1, 1, 6, 0, 0);
        var planEnd = planStart.AddHours(24);

        var obj = new SleepObjective();
        var actions = obj.GetActions(person, state, planStart, planEnd);

        Assert.Equal(TimeSpan.FromHours(8), actions[0].Duration);
    }

    [Fact]
    public void GetActions_SleepStartsAtPreferredTime()
    {
        var (state, person) = CreateSetup(
            TimeSpan.FromHours(22), TimeSpan.FromHours(6));
        // Plan starts at 06:00 — sleep should be at 22:00 same day
        var planStart = new DateTime(1980, 1, 1, 6, 0, 0);
        var planEnd = planStart.AddHours(24);

        var obj = new SleepObjective();
        var actions = obj.GetActions(person, state, planStart, planEnd);

        Assert.Equal(new DateTime(1980, 1, 1, 22, 0, 0), actions[0].TimeWindowStart);
    }

    [Fact]
    public void GetActions_MidSleep_ReturnsRemainingSleep()
    {
        // NPC sleeps 22:00-06:00. Plan starts at 02:00 (mid-sleep).
        var (state, person) = CreateSetup(
            TimeSpan.FromHours(22), TimeSpan.FromHours(6));
        var planStart = new DateTime(1980, 1, 2, 2, 0, 0); // 02:00 day 2
        var planEnd = planStart.AddHours(24);

        var obj = new SleepObjective();
        var actions = obj.GetActions(person, state, planStart, planEnd);

        // Should get TWO sleep actions:
        // 1. Remaining sleep: 02:00 to 06:00 (4h)
        // 2. Next night's sleep: 22:00 to 06:00 (8h)
        Assert.Equal(2, actions.Count);

        // First: remaining sleep starting now
        Assert.Equal(planStart, actions[0].TimeWindowStart);
        Assert.Equal(TimeSpan.FromHours(4), actions[0].Duration);

        // Second: next full sleep
        Assert.Equal(new DateTime(1980, 1, 2, 22, 0, 0), actions[1].TimeWindowStart);
        Assert.Equal(TimeSpan.FromHours(8), actions[1].Duration);
    }

    [Fact]
    public void GetActions_NightShift_SleepAtCorrectTime()
    {
        // Night worker: sleeps 07:30-15:30. Plan starts at 15:30.
        var (state, person) = CreateSetup(
            TimeSpan.FromHours(7.5), TimeSpan.FromHours(15.5));
        var planStart = new DateTime(1980, 1, 1, 15, 30, 0);
        var planEnd = planStart.AddHours(24);

        var obj = new SleepObjective();
        var actions = obj.GetActions(person, state, planStart, planEnd);

        // Sleep should be at 07:30 next day
        Assert.Single(actions);
        Assert.Equal(new DateTime(1980, 1, 2, 7, 30, 0), actions[0].TimeWindowStart);
        Assert.Equal(TimeSpan.FromHours(8), actions[0].Duration);
    }

    [Fact]
    public void GetActions_NightShift_MidSleep_ReturnsRemaining()
    {
        // Night worker: sleeps 07:30-15:30. Plan starts at 10:00 (mid-sleep).
        var (state, person) = CreateSetup(
            TimeSpan.FromHours(7.5), TimeSpan.FromHours(15.5));
        var planStart = new DateTime(1980, 1, 1, 10, 0, 0);
        var planEnd = planStart.AddHours(24);

        var obj = new SleepObjective();
        var actions = obj.GetActions(person, state, planStart, planEnd);

        // Should get TWO sleep actions:
        // 1. Remaining: 10:00 to 15:30 (5.5h)
        // 2. Next sleep: 07:30 next day (8h)
        Assert.Equal(2, actions.Count);
        Assert.Equal(planStart, actions[0].TimeWindowStart);
        Assert.Equal(TimeSpan.FromHours(5.5), actions[0].Duration);
        Assert.Equal(new DateTime(1980, 1, 2, 7, 30, 0), actions[1].TimeWindowStart);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test stakeout.tests/ --filter "FullyQualifiedName~SleepObjectiveTests" -v minimal`
Expected: FAIL — `GetActionsForToday` no longer exists, compile errors.

- [ ] **Step 3: Implement new SleepObjective**

Replace the contents of `src/simulation/objectives/SleepObjective.cs`:

```csharp
using System;
using System.Collections.Generic;
using Stakeout.Simulation.Actions.Primitives;
using Stakeout.Simulation.Entities;

namespace Stakeout.Simulation.Objectives;

public class SleepObjective : Objective
{
    public override int Priority => 80;
    public override ObjectiveSource Source => ObjectiveSource.Universal;

    public override List<PlannedAction> GetActions(
        Person person, SimulationState state,
        DateTime planStart, DateTime planEnd)
    {
        var sleepTimeOfDay = person.PreferredSleepTime;
        var wakeTimeOfDay = person.PreferredWakeTime;

        var sleepDuration = wakeTimeOfDay - sleepTimeOfDay;
        if (sleepDuration < TimeSpan.Zero)
            sleepDuration += TimeSpan.FromHours(24);

        var actions = new List<PlannedAction>();

        // Check if planStart is mid-sleep
        if (IsInSleepWindow(planStart.TimeOfDay, sleepTimeOfDay, wakeTimeOfDay))
        {
            var wakeUpToday = planStart.Date + wakeTimeOfDay;
            if (wakeUpToday <= planStart)
                wakeUpToday = wakeUpToday.AddDays(1);
            var remaining = wakeUpToday - planStart;

            actions.Add(MakeSleepAction(person, planStart, remaining));
        }

        // Find next preferred sleep time within window
        var nextSleep = planStart.Date + sleepTimeOfDay;
        // Advance until nextSleep is after planStart (and after any mid-sleep wake)
        while (nextSleep <= planStart)
            nextSleep = nextSleep.AddDays(1);
        // If mid-sleep, also skip past the remaining sleep window
        if (actions.Count > 0 && nextSleep < actions[0].TimeWindowStart + actions[0].Duration)
            nextSleep = nextSleep.AddDays(1);

        if (nextSleep + sleepDuration <= planEnd)
        {
            actions.Add(MakeSleepAction(person, nextSleep, sleepDuration));
        }

        return actions;
    }

    private static bool IsInSleepWindow(TimeSpan timeOfDay, TimeSpan sleepStart, TimeSpan wakeEnd)
    {
        if (sleepStart < wakeEnd)
        {
            // Simple case: sleep 22:00-06:00 doesn't wrap — wait, this IS wrapping.
            // sleepStart < wakeEnd means sleep window doesn't cross midnight (e.g., 01:00-09:00)
            return timeOfDay >= sleepStart && timeOfDay < wakeEnd;
        }
        else
        {
            // Wraps midnight: e.g., sleep 22:00, wake 06:00
            return timeOfDay >= sleepStart || timeOfDay < wakeEnd;
        }
    }

    private PlannedAction MakeSleepAction(Person person, DateTime start, TimeSpan duration)
    {
        return new PlannedAction
        {
            Action = new WaitAction(duration, "sleeping"),
            TargetAddressId = person.HomeAddressId,
            TimeWindowStart = start,
            TimeWindowEnd = start + duration,
            Duration = duration,
            DisplayText = "sleeping",
            SourceObjective = this
        };
    }
}
```

- [ ] **Step 4: Run SleepObjective tests to verify they pass**

Run: `dotnet test stakeout.tests/ --filter "FullyQualifiedName~SleepObjectiveTests" -v minimal`
Expected: All PASS.

- [ ] **Step 5: Commit**

```bash
git add src/simulation/objectives/SleepObjective.cs stakeout.tests/Simulation/Objectives/SleepObjectiveTests.cs
git commit -m "feat: rewrite SleepObjective for DateTime-based planning window"
```

---

### Task 5: Update EatOutObjective

**Files:**
- Modify: `src/simulation/objectives/EatOutObjective.cs`
- Modify: `stakeout.tests/Simulation/Objectives/EatOutObjectiveTests.cs`

- [ ] **Step 1: Write failing tests for new EatOutObjective signature**

Replace the contents of `stakeout.tests/Simulation/Objectives/EatOutObjectiveTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using Stakeout.Simulation;
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
        state.Cities[1] = new Stakeout.Simulation.Entities.City { Id = 1, AddressIds = { 1, 2 } };
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
    public void GetActions_ReturnsEatAction()
    {
        var state = CreateStateWithDiner();
        var person = new Person { Id = 1, HomeAddressId = 1, CurrentCityId = 1 };
        var planStart = new DateTime(1980, 1, 1, 6, 0, 0);
        var planEnd = planStart.AddHours(24);

        var obj = new EatOutObjective();
        var actions = obj.GetActions(person, state, planStart, planEnd);

        Assert.Single(actions);
        Assert.Contains("eating", actions[0].DisplayText);
        Assert.Equal(2, actions[0].TargetAddressId);
    }

    [Fact]
    public void GetActions_NoDiner_ReturnsEmpty()
    {
        var state = new SimulationState();
        state.Addresses[1] = new Address { Id = 1, Type = AddressType.SuburbanHome };
        state.Cities[1] = new Stakeout.Simulation.Entities.City { Id = 1, AddressIds = { 1 } };
        var person = new Person { Id = 1, HomeAddressId = 1, CurrentCityId = 1 };
        var planStart = new DateTime(1980, 1, 1, 6, 0, 0);
        var planEnd = planStart.AddHours(24);

        var obj = new EatOutObjective();
        var actions = obj.GetActions(person, state, planStart, planEnd);

        Assert.Empty(actions);
    }

    [Fact]
    public void GetActions_Duration_Is30Minutes()
    {
        var state = CreateStateWithDiner();
        var person = new Person { Id = 1, HomeAddressId = 1, CurrentCityId = 1 };
        var planStart = new DateTime(1980, 1, 1, 6, 0, 0);
        var planEnd = planStart.AddHours(24);

        var obj = new EatOutObjective();
        var actions = obj.GetActions(person, state, planStart, planEnd);

        Assert.Equal(TimeSpan.FromMinutes(30), actions[0].Duration);
    }

    [Fact]
    public void GetActions_TimeWindowFallsWithinPlanWindow()
    {
        var state = CreateStateWithDiner();
        var person = new Person { Id = 1, HomeAddressId = 1, CurrentCityId = 1 };
        var planStart = new DateTime(1980, 1, 1, 6, 0, 0);
        var planEnd = planStart.AddHours(24);

        var obj = new EatOutObjective();
        var actions = obj.GetActions(person, state, planStart, planEnd);

        Assert.True(actions[0].TimeWindowStart >= planStart);
        Assert.True(actions[0].TimeWindowEnd <= planEnd);
    }

    [Fact]
    public void GetActions_MealScheduledInFirstHalfOfWakingWindow()
    {
        var state = CreateStateWithDiner();
        var person = new Person { Id = 1, HomeAddressId = 1, CurrentCityId = 1 };
        var planStart = new DateTime(1980, 1, 1, 6, 0, 0);
        var planEnd = planStart.AddHours(24);

        var obj = new EatOutObjective();
        var actions = obj.GetActions(person, state, planStart, planEnd);

        // Meal should be in first half of planning window
        var midpoint = planStart + TimeSpan.FromHours(12);
        Assert.True(actions[0].TimeWindowStart < midpoint);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test stakeout.tests/ --filter "FullyQualifiedName~EatOutObjectiveTests" -v minimal`
Expected: FAIL — compile errors from old method name.

- [ ] **Step 3: Implement new EatOutObjective**

Update `src/simulation/objectives/EatOutObjective.cs` — change method signature and use DateTime-relative time windows. The meal window is "3-8 hours after plan start" — this places a meal in the first half of the waking period regardless of what wall-clock hours those are:

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

    public override List<PlannedAction> GetActions(
        Person person, SimulationState state,
        DateTime planStart, DateTime planEnd)
    {
        var dinerId = FindRestaurant(person, state);
        if (dinerId == null) return new List<PlannedAction>();

        // Schedule meal 3-8 hours into the planning window
        var windowStart = planStart + TimeSpan.FromHours(3);
        var windowEnd = planStart + TimeSpan.FromHours(8);
        // Clamp to plan bounds
        if (windowEnd > planEnd) windowEnd = planEnd;
        if (windowStart >= windowEnd) return new List<PlannedAction>();

        return new List<PlannedAction>
        {
            new()
            {
                Action = new WaitAction(MealDuration, "eating at the counter"),
                TargetAddressId = dinerId.Value,
                TimeWindowStart = windowStart,
                TimeWindowEnd = windowEnd,
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
            Traces.TraceEmitter.EmitSighting(state, person.Id,
                locations[0].Id, $"{person.FullName} was seen eating", decayDays: 3);

            var fixtures = state.GetFixturesForLocation(locations[0].Id);
            var trash = fixtures.FirstOrDefault(f => f.Type == Fixtures.FixtureType.TrashCan);
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
            .FirstOrDefault(a => a.Type == AddressType.Diner)?.Id;
    }
}
```

- [ ] **Step 4: Run EatOutObjective tests to verify they pass**

Run: `dotnet test stakeout.tests/ --filter "FullyQualifiedName~EatOutObjectiveTests" -v minimal`
Expected: All PASS.

- [ ] **Step 5: Commit**

```bash
git add src/simulation/objectives/EatOutObjective.cs stakeout.tests/Simulation/Objectives/EatOutObjectiveTests.cs
git commit -m "feat: update EatOutObjective for DateTime-based planning window"
```

---

### Task 6: Update GoForARunObjective

**Files:**
- Modify: `src/simulation/objectives/GoForARunObjective.cs`
- Modify: `stakeout.tests/Simulation/Objectives/GoForARunObjectiveTests.cs`

- [ ] **Step 1: Write failing tests for new GoForARunObjective signature**

Read the existing `stakeout.tests/Simulation/Objectives/GoForARunObjectiveTests.cs` and update it to use the new `GetActions(person, state, planStart, planEnd)` signature. Follow the same pattern as EatOutObjectiveTests — pass `DateTime` planStart/planEnd, verify the action's time window falls within the plan window.

The run window should be "1-6 hours after plan start" — early in the waking period.

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test stakeout.tests/ --filter "FullyQualifiedName~GoForARunObjectiveTests" -v minimal`
Expected: FAIL — compile errors.

- [ ] **Step 3: Implement new GoForARunObjective**

Update `src/simulation/objectives/GoForARunObjective.cs` — change method signature, use DateTime-relative time windows. Same pattern as EatOutObjective: window is "1-6 hours after plan start."

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

    public override List<PlannedAction> GetActions(
        Person person, SimulationState state,
        DateTime planStart, DateTime planEnd)
    {
        var parkId = FindPark(person, state);
        if (parkId == null) return new List<PlannedAction>();

        // Schedule run 1-6 hours into the planning window
        var windowStart = planStart + TimeSpan.FromHours(1);
        var windowEnd = planStart + TimeSpan.FromHours(6);
        if (windowEnd > planEnd) windowEnd = planEnd;
        if (windowStart >= windowEnd) return new List<PlannedAction>();

        return new List<PlannedAction>
        {
            new()
            {
                Action = new WaitAction(RunDuration, "running on the trails"),
                TargetAddressId = parkId.Value,
                TimeWindowStart = windowStart,
                TimeWindowEnd = windowEnd,
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
            .FirstOrDefault(a => a.Type == AddressType.Park)?.Id;
    }
}
```

- [ ] **Step 4: Run GoForARunObjective tests to verify they pass**

Run: `dotnet test stakeout.tests/ --filter "FullyQualifiedName~GoForARunObjectiveTests" -v minimal`
Expected: All PASS.

- [ ] **Step 5: Commit**

```bash
git add src/simulation/objectives/GoForARunObjective.cs stakeout.tests/Simulation/Objectives/GoForARunObjectiveTests.cs
git commit -m "feat: update GoForARunObjective for DateTime-based planning window"
```

---

### Task 7: Rewrite NpcBrain.PlanDay

**Files:**
- Modify: `src/simulation/brain/NpcBrain.cs`
- Modify: `stakeout.tests/Simulation/Brain/NpcBrainTests.cs`

- [ ] **Step 1: Write failing tests for new PlanDay**

Replace the contents of `stakeout.tests/Simulation/Brain/NpcBrainTests.cs`. The `TestObjective` helper must use the new `GetActions` signature with `DateTime` time windows. Key new tests: night-shift NPC gets valid plan, mid-sleep plan starts with sleep.

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
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
    private static SimulationState CreateState(DateTime? startTime = null)
    {
        var state = new SimulationState(
            new GameClock(startTime ?? new DateTime(1980, 1, 1, 6, 0, 0)));
        var home = new Address { Id = 1, GridX = 0, GridY = 0 };
        state.Addresses[home.Id] = home;
        return state;
    }

    private static Person CreatePerson(SimulationState state,
        TimeSpan wakeTime, TimeSpan sleepTime)
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
        person.Objectives.Add(new SleepObjective { Id = state.GenerateEntityId() });
        state.People[person.Id] = person;
        return person;
    }

    [Fact]
    public void PlanDay_DayShift_HasSleepAndIdle()
    {
        var state = CreateState();
        var person = CreatePerson(state,
            TimeSpan.FromHours(6), TimeSpan.FromHours(22));

        var plan = NpcBrain.PlanDay(person, state, state.Clock.CurrentTime);

        Assert.NotEmpty(plan.Entries);
        Assert.Contains(plan.Entries, e => e.PlannedAction.DisplayText == "sleeping");
        Assert.Contains(plan.Entries, e => e.PlannedAction.DisplayText == "relaxing at home");
    }

    [Fact]
    public void PlanDay_NightShift_HasSleepAndIdle()
    {
        // Night worker: wakes 15:30, sleeps 07:30
        var state = CreateState(new DateTime(1980, 1, 1, 15, 30, 0));
        var person = CreatePerson(state,
            TimeSpan.FromHours(15.5), TimeSpan.FromHours(7.5));

        var plan = NpcBrain.PlanDay(person, state, state.Clock.CurrentTime);

        Assert.NotEmpty(plan.Entries);
        Assert.Contains(plan.Entries, e => e.PlannedAction.DisplayText == "sleeping");
        Assert.Contains(plan.Entries, e => e.PlannedAction.DisplayText == "relaxing at home");
    }

    [Fact]
    public void PlanDay_MidSleep_StartsWithSleep()
    {
        // Day worker (sleep 22:00-06:00), but plan starts at 02:00 (mid-sleep)
        var state = CreateState(new DateTime(1980, 1, 1, 2, 0, 0));
        var person = CreatePerson(state,
            TimeSpan.FromHours(6), TimeSpan.FromHours(22));

        var plan = NpcBrain.PlanDay(person, state, state.Clock.CurrentTime);

        Assert.NotEmpty(plan.Entries);
        // First entry should be sleep
        Assert.Equal("sleeping", plan.Entries[0].PlannedAction.DisplayText);
        // Should start at plan start (02:00), not at 22:00
        Assert.Equal(state.Clock.CurrentTime, plan.Entries[0].StartTime);
    }

    [Fact]
    public void PlanDay_AllEntriesUseDateTime()
    {
        var state = CreateState();
        var person = CreatePerson(state,
            TimeSpan.FromHours(6), TimeSpan.FromHours(22));

        var plan = NpcBrain.PlanDay(person, state, state.Clock.CurrentTime);

        // All entries should have absolute DateTimes, not default(DateTime)
        foreach (var entry in plan.Entries)
        {
            Assert.True(entry.StartTime > DateTime.MinValue);
            Assert.True(entry.EndTime > entry.StartTime);
        }
    }

    [Fact]
    public void PlanDay_EntriesAreChronological()
    {
        var state = CreateState();
        var person = CreatePerson(state,
            TimeSpan.FromHours(6), TimeSpan.FromHours(22));

        var plan = NpcBrain.PlanDay(person, state, state.Clock.CurrentTime);

        for (int i = 1; i < plan.Entries.Count; i++)
        {
            Assert.True(plan.Entries[i].StartTime >= plan.Entries[i - 1].EndTime,
                $"Entry {i} starts before entry {i - 1} ends");
        }
    }

    [Fact]
    public void PlanDay_HigherPriorityScheduledFirst()
    {
        var now = new DateTime(1980, 1, 1, 6, 0, 0);
        var state = CreateState(now);
        var person = CreatePerson(state,
            TimeSpan.FromHours(6), TimeSpan.FromHours(22));

        person.Objectives.Add(new TestObjective(50, new PlannedAction
        {
            Action = new WaitAction(TimeSpan.FromHours(1), "high priority"),
            TargetAddressId = 1,
            TimeWindowStart = now + TimeSpan.FromHours(2),
            TimeWindowEnd = now + TimeSpan.FromHours(6),
            Duration = TimeSpan.FromHours(1),
            DisplayText = "high priority"
        }));
        person.Objectives.Add(new TestObjective(20, new PlannedAction
        {
            Action = new WaitAction(TimeSpan.FromHours(1), "low priority"),
            TargetAddressId = 1,
            TimeWindowStart = now + TimeSpan.FromHours(2),
            TimeWindowEnd = now + TimeSpan.FromHours(6),
            Duration = TimeSpan.FromHours(1),
            DisplayText = "low priority"
        }));

        var plan = NpcBrain.PlanDay(person, state, now);

        var nonIdle = plan.Entries
            .Where(e => e.PlannedAction.DisplayText != "relaxing at home"
                     && e.PlannedAction.DisplayText != "sleeping")
            .ToList();
        Assert.Equal(2, nonIdle.Count);
        Assert.True(nonIdle[0].StartTime <= nonIdle[1].StartTime);
        Assert.Equal("high priority", nonIdle[0].PlannedAction.DisplayText);
    }

    [Fact]
    public void PlanDay_GapsFilledWithIdleAtHome()
    {
        var now = new DateTime(1980, 1, 1, 6, 0, 0);
        var state = CreateState(now);
        var person = CreatePerson(state,
            TimeSpan.FromHours(6), TimeSpan.FromHours(22));

        person.Objectives.Add(new TestObjective(40, new PlannedAction
        {
            Action = new WaitAction(TimeSpan.FromHours(1), "eating"),
            TargetAddressId = 1,
            TimeWindowStart = now + TimeSpan.FromHours(6),
            TimeWindowEnd = now + TimeSpan.FromHours(7),
            Duration = TimeSpan.FromHours(1),
            DisplayText = "eating"
        }));

        var plan = NpcBrain.PlanDay(person, state, now);

        Assert.True(plan.Entries.Count >= 3);
        Assert.Contains(plan.Entries, e => e.PlannedAction.DisplayText == "eating");
        Assert.Contains(plan.Entries, e => e.PlannedAction.DisplayText == "relaxing at home");
    }

    [Fact]
    public void PlanDay_PlanSpans24Hours()
    {
        var now = new DateTime(1980, 1, 1, 6, 0, 0);
        var state = CreateState(now);
        var person = CreatePerson(state,
            TimeSpan.FromHours(6), TimeSpan.FromHours(22));

        var plan = NpcBrain.PlanDay(person, state, now);

        Assert.Equal(now, plan.Entries.First().StartTime);
        // Last entry should end at or near now + 24h
        var lastEnd = plan.Entries.Last().EndTime;
        Assert.True(lastEnd <= now.AddHours(24),
            $"Plan extends past 24h: ends at {lastEnd}");
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

        public override List<PlannedAction> GetActions(
            Person person, SimulationState state,
            DateTime planStart, DateTime planEnd)
        {
            return new List<PlannedAction> { _action };
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test stakeout.tests/ --filter "FullyQualifiedName~NpcBrainTests" -v minimal`
Expected: FAIL — old `PlanDay` signature/logic doesn't match new tests.

- [ ] **Step 3: Rewrite NpcBrain.PlanDay**

Replace the contents of `src/simulation/brain/NpcBrain.cs`:

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
    public static DayPlan PlanDay(Person person, SimulationState state,
        DateTime currentTime, MapConfig mapConfig = null)
    {
        var plan = new DayPlan();
        var planStart = currentTime;
        var planEnd = currentTime.AddHours(24);

        // Collect all objectives sorted by priority (descending)
        var objectives = person.Objectives
            .Where(o => o.Status == ObjectiveStatus.Active)
            .OrderByDescending(o => o.Priority)
            .ToList();

        // Schedule greedily by priority
        var scheduled = new List<(DateTime start, DateTime end, PlannedAction action)>();

        foreach (var objective in objectives)
        {
            var actions = objective.GetActions(person, state, planStart, planEnd);
            foreach (var action in actions)
            {
                var travelHours = EstimateTravelTime(
                    person, action.TargetAddressId, state, mapConfig);
                var totalDuration = action.Duration + TimeSpan.FromHours(travelHours);

                var slot = FindSlot(scheduled, action.TimeWindowStart,
                    action.TimeWindowEnd, totalDuration, planStart, planEnd);
                if (slot.HasValue)
                {
                    scheduled.Add((slot.Value, slot.Value + totalDuration, action));
                }
            }
        }

        // Sort by start time
        scheduled.Sort((a, b) => a.start.CompareTo(b.start));

        // Build plan entries, filling gaps with IdleAtHome
        var currentSlotTime = planStart;
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

        // Fill remaining time with idle
        if (currentSlotTime < planEnd)
        {
            AddIdleEntry(plan, currentSlotTime, planEnd, person.HomeAddressId);
        }

        return plan;
    }

    private static DateTime? FindSlot(
        List<(DateTime start, DateTime end, PlannedAction action)> scheduled,
        DateTime windowStart, DateTime windowEnd,
        TimeSpan totalDuration,
        DateTime planStart, DateTime planEnd)
    {
        // Clamp window to plan bounds
        var effectiveStart = windowStart < planStart ? planStart : windowStart;
        var effectiveEnd = windowEnd > planEnd ? planEnd : windowEnd;

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

    private static void AddIdleEntry(DayPlan plan, DateTime start, DateTime end,
        int homeAddressId)
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

    private static float EstimateTravelTime(Person person, int targetAddressId,
        SimulationState state, MapConfig mapConfig)
    {
        if (person.CurrentAddressId == targetAddressId) return 0f;
        if (!state.Addresses.TryGetValue(targetAddressId, out var target)) return 0f;
        if (mapConfig != null)
            return mapConfig.ComputeTravelTimeHours(person.CurrentPosition, target.Position);
        var diagonal = new Godot.Vector2(4800, 4800).Length();
        return person.CurrentPosition.DistanceTo(target.Position) / diagonal * 1.0f;
    }
}
```

- [ ] **Step 4: Run NpcBrainTests to verify they pass**

Run: `dotnet test stakeout.tests/ --filter "FullyQualifiedName~NpcBrainTests" -v minimal`
Expected: All PASS.

- [ ] **Step 5: Commit**

```bash
git add src/simulation/brain/NpcBrain.cs stakeout.tests/Simulation/Brain/NpcBrainTests.cs
git commit -m "feat: rewrite NpcBrain.PlanDay with 24-hour DateTime window"
```

---

### Task 8: Update IntegrationTests and debug inspector

**Files:**
- Modify: `stakeout.tests/Simulation/Brain/IntegrationTests.cs`
- Modify: `scenes/game_shell/GameShell.cs:475`

- [ ] **Step 1: Update IntegrationTests**

Read `stakeout.tests/Simulation/Brain/IntegrationTests.cs` and update any references. The tests call `NpcBrain.PlanDay(person, state, state.Clock.CurrentTime)` which still has the same signature — these should mostly compile. Verify and fix any issues from the `DayPlanEntry.StartTime` type change (TimeSpan → DateTime) in assertions.

- [ ] **Step 2: Update debug inspector DateTime formatting**

In `scenes/game_shell/GameShell.cs`, line 475, change the format string from TimeSpan format to DateTime format:

Old:
```csharp
planLines.Add($"{entry.StartTime:hh\\:mm} - {entry.EndTime:hh\\:mm}  {entry.PlannedAction.DisplayText} {location}{marker}");
```

New:
```csharp
planLines.Add($"{entry.StartTime:HH:mm} - {entry.EndTime:HH:mm}  {entry.PlannedAction.DisplayText} {location}{marker}");
```

Note: `HH:mm` is DateTime format (24-hour clock). The old `hh\\:mm` was TimeSpan format.

- [ ] **Step 3: Run all tests**

Run: `dotnet test stakeout.tests/ -v minimal`
Expected: All PASS. This confirms no other code was broken by the type changes.

- [ ] **Step 4: Commit**

```bash
git add stakeout.tests/Simulation/Brain/IntegrationTests.cs scenes/game_shell/GameShell.cs
git commit -m "fix: update integration tests and debug inspector for DateTime plan entries"
```

---

### Task 9: Run full test suite and verify

**Files:** None — verification only.

- [ ] **Step 1: Run full test suite**

Run: `dotnet test stakeout.tests/ -v minimal`
Expected: All tests pass.

- [ ] **Step 2: Build the Godot project**

Run: `dotnet build -v minimal`
Expected: Build succeeds with no errors.

- [ ] **Step 3: Verify no remaining TimeSpan references in plan entry times**

Search for any leftover `TimeSpan` usage in `DayPlanEntry.StartTime` or `DayPlanEntry.EndTime` across the codebase to confirm the migration is complete. Check that no code is casting or converting these back to TimeSpan.
