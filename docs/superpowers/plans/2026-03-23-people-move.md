# People Move Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give each Person an AI brain that follows a daily schedule, with jobs, sleep, travel, and an event journal recording all transitions.

**Architecture:** State-primary with event journal. Entities hold mutable current state; a parallel append-only event log records all transitions for future replay. A ScheduleBuilder pre-computes daily schedules from prioritized goals. PersonBehavior executes the schedule each tick.

**Tech Stack:** Godot 4.6, C# (.NET 8.0), xUnit for tests

**Spec:** `docs/superpowers/specs/2026-03-22-people-move-design.md`

---

## File Structure

### New Files

| File | Responsibility |
|------|---------------|
| `src/simulation/entities/ActivityType.cs` | ActivityType enum (AtHome, Working, TravellingByCar, Sleeping) |
| `src/simulation/entities/Job.cs` | Job class, JobType enum |
| `src/simulation/entities/TravelInfo.cs` | TravelInfo class for in-transit state |
| `src/simulation/events/SimulationEvent.cs` | SimulationEvent record, SimulationEventType enum |
| `src/simulation/events/EventJournal.cs` | Event storage with global list + per-person index |
| `src/simulation/scheduling/Goal.cs` | Goal class, GoalType enum, GoalSet class |
| `src/simulation/scheduling/DailySchedule.cs` | DailySchedule and ScheduleEntry classes |
| `src/simulation/scheduling/SleepScheduleCalculator.cs` | Pure function to compute sleep times from job + commute |
| `src/simulation/scheduling/ScheduleBuilder.cs` | Builds DailySchedule from GoalSet + addresses |
| `src/simulation/scheduling/PersonBehavior.cs` | Executes schedule each tick, transitions state + journal |
| `src/simulation/MapConfig.cs` | Map dimensions, travel time constants |
| `stakeout.tests/Simulation/Events/EventJournalTests.cs` | Tests for event journal |
| `stakeout.tests/Simulation/Scheduling/SleepScheduleCalculatorTests.cs` | Tests for sleep schedule computation |
| `stakeout.tests/Simulation/Scheduling/ScheduleBuilderTests.cs` | Tests for schedule building |
| `stakeout.tests/Simulation/Scheduling/PersonBehaviorTests.cs` | Tests for tick-based schedule execution |
| `stakeout.tests/Simulation/MapConfigTests.cs` | Tests for travel time computation |
| `stakeout.tests/Simulation/PersonGeneratorTests.cs` | Updated tests for new generation flow |

### Modified Files

| File | Changes |
|------|---------|
| `src/simulation/entities/Person.cs` | Add JobId, CurrentActivity, CurrentPosition, TravelInfo, sleep prefs; remove WorkAddressId; make CurrentAddressId nullable |
| `src/simulation/SimulationState.cs` | Add Jobs dict, EventJournal, state transition methods |
| `src/simulation/GameClock.cs` | Change default start to 1980; add TimeScale property |
| `src/simulation/PersonGenerator.cs` | Complete overhaul: generate addresses, job, schedule on demand |
| `src/simulation/SimulationManager.cs` | New sim loop with time scaling and PersonBehavior updates |
| `src/simulation/LocationGenerator.cs` | Remove (or gut to only create city/country scaffolding) |
| `scenes/simulation_debug/SimulationDebug.cs` | Time controls, position-based rendering, activity in tooltip |
| `scenes/simulation_debug/SimulationDebug.tscn` | Add time control buttons |
| `scenes/evidence_board/DossierWindow.cs` | Update WorkAddressId → Job lookup |
| `stakeout.tests/Simulation/GameClockTests.cs` | Update default start time assertion to 1980 |
| `stakeout.tests/Simulation/SimulationStateTests.cs` | Update Person construction (nullable CurrentAddressId, remove WorkAddressId) |
| `stakeout.tests/Simulation/LocationGeneratorTests.cs` | Remove or rewrite for new scaffolding-only generator |

---

## Task 1: ActivityType and TravelInfo Entities

**Files:**
- Create: `src/simulation/entities/ActivityType.cs`
- Create: `src/simulation/entities/TravelInfo.cs`

These are simple data types with no dependencies — the foundation other tasks build on.

- [ ] **Step 1: Create ActivityType enum**

```csharp
// src/simulation/entities/ActivityType.cs
namespace Stakeout.Simulation.Entities;

public enum ActivityType
{
    AtHome,
    Working,
    TravellingByCar,
    Sleeping
}
```

- [ ] **Step 2: Create TravelInfo class**

```csharp
// src/simulation/entities/TravelInfo.cs
using System;
using Godot;

namespace Stakeout.Simulation.Entities;

public class TravelInfo
{
    public Vector2 FromPosition { get; set; }
    public Vector2 ToPosition { get; set; }
    public DateTime DepartureTime { get; set; }
    public DateTime ArrivalTime { get; set; }
    public int FromAddressId { get; set; }
    public int ToAddressId { get; set; }
}
```

- [ ] **Step 3: Commit**

```bash
git add src/simulation/entities/ActivityType.cs src/simulation/entities/TravelInfo.cs
git commit -m "Add ActivityType enum and TravelInfo class"
```

---

## Task 2: Job Entity

**Files:**
- Create: `src/simulation/entities/Job.cs`

- [ ] **Step 1: Create Job class and JobType enum**

```csharp
// src/simulation/entities/Job.cs
using System;

namespace Stakeout.Simulation.Entities;

public enum JobType
{
    DinerWaiter,
    OfficeWorker,
    Bartender
}

public class Job
{
    public int Id { get; set; }
    public JobType Type { get; set; }
    public string Title { get; set; }
    public int WorkAddressId { get; set; }
    public TimeSpan ShiftStart { get; set; }
    public TimeSpan ShiftEnd { get; set; }
    public DayOfWeek[] WorkDays { get; set; }
}
```

- [ ] **Step 2: Commit**

```bash
git add src/simulation/entities/Job.cs
git commit -m "Add Job entity and JobType enum"
```

---

## Task 3: Update Person Entity

**Files:**
- Modify: `src/simulation/entities/Person.cs`

Remove `WorkAddressId`. Add `JobId`, `CurrentActivity`, `CurrentPosition`, `TravelInfo`, `PreferredSleepTime`, `PreferredWakeTime`. Make `CurrentAddressId` nullable.

- [ ] **Step 1: Update Person class**

```csharp
// src/simulation/entities/Person.cs
using System;
using Godot;

namespace Stakeout.Simulation.Entities;

public class Person
{
    public int Id { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public DateTime CreatedAt { get; set; }
    public int HomeAddressId { get; set; }
    public int JobId { get; set; }
    public int? CurrentAddressId { get; set; }
    public Vector2 CurrentPosition { get; set; }
    public ActivityType CurrentActivity { get; set; }
    public TravelInfo TravelInfo { get; set; }
    public TimeSpan PreferredSleepTime { get; set; }
    public TimeSpan PreferredWakeTime { get; set; }

    public string FullName => $"{FirstName} {LastName}";
}
```

- [ ] **Step 2: Commit**

```bash
git add src/simulation/entities/Person.cs
git commit -m "Update Person: add JobId, activity, position, sleep prefs; remove WorkAddressId"
```

---

## Task 4: Fix Compilation Errors from Person Change

Removing `WorkAddressId` and making `CurrentAddressId` nullable will break several files. Fix them all.

**Files:**
- Modify: `src/simulation/SimulationState.cs:29` — `GetEntityNamesAtAddress` uses `p.CurrentAddressId == address.Id`, now needs null check
- Modify: `src/simulation/PersonGenerator.cs:27` — references `WorkAddressId`
- Modify: `scenes/simulation_debug/SimulationDebug.cs:159` — `person.CurrentAddressId` may be null
- Modify: `scenes/evidence_board/DossierWindow.cs:46-49` — references `person.WorkAddressId`
- Modify: `stakeout.tests/Simulation/SimulationStateTests.cs` — Person construction uses `WorkAddressId`
- Modify: `stakeout.tests/Simulation/PersonGeneratorTests.cs` — tests reference `WorkAddressId`

- [ ] **Step 1: Fix SimulationState.GetEntityNamesAtAddress**

Update the LINQ query to handle nullable `CurrentAddressId`:

```csharp
// SimulationState.cs line 28-31
public List<string> GetEntityNamesAtAddress(Address address)
{
    return People.Values
        .Where(p => p.CurrentAddressId.HasValue && p.CurrentAddressId.Value == address.Id)
        .Select(p => p.FullName)
        .ToList();
}
```

- [ ] **Step 2: Stub PersonGenerator temporarily**

The generator will be fully rewritten in Task 10. For now, make it compile by removing the `WorkAddressId` assignment and adding a placeholder `JobId = 0`:

```csharp
// PersonGenerator.cs — remove lines referencing WorkAddressId and commercialAddresses
// Set person.CurrentAddressId = person.HomeAddressId (keep this)
// Set person.CurrentPosition = residential address position
// Set person.CurrentActivity = ActivityType.AtHome
```

- [ ] **Step 3: Fix SimulationDebug.OnPersonAdded**

Use `CurrentPosition` instead of looking up `CurrentAddressId`:

```csharp
// SimulationDebug.cs line 157-165
private void OnPersonAdded(Person person)
{
    var size = new Vector2(EntityDotSize, EntityDotSize);
    var dot = CreateIconPanel(size, PersonColor, BorderColor, DotBorderWidth);
    dot.Position = person.CurrentPosition - size / 2;
    _entityDots.AddChild(dot);
    _personNodes[person.Id] = dot;
}
```

- [ ] **Step 4: Fix DossierWindow.Populate**

Look up work address via Job instead of `person.WorkAddressId`:

```csharp
// DossierWindow.cs lines 46-49 — replace WorkAddressId lookup:
if (state.Jobs.TryGetValue(person.JobId, out var job) &&
    state.Addresses.TryGetValue(job.WorkAddressId, out var work))
{
    var street = state.Streets[work.StreetId];
    lines.Add($"Work: {work.Number} {street.Name} ({job.Title})");
}
```

Also update the address people query (lines 59-60) to check via Job:

```csharp
var people = state.People.Values
    .Where(p => p.HomeAddressId == address.Id ||
                (state.Jobs.TryGetValue(p.JobId, out var j) && j.WorkAddressId == address.Id))
    .ToList();
```

And update the rel string (line 67):

```csharp
var rel = p.HomeAddressId == address.Id ? "lives here" : "works here";
```

- [ ] **Step 5: Fix SimulationStateTests**

Update all Person construction to remove `WorkAddressId` and use nullable `CurrentAddressId`:

In every test that creates a `Person` with `WorkAddressId = X, CurrentAddressId = Y`, change to `JobId = 0, CurrentAddressId = Y` (keeping CurrentAddressId as an int? value, which will implicitly convert).

- [ ] **Step 6: Fix PersonGeneratorTests**

- Remove `GeneratePerson_WorkAddressIsCommercial` test (will be replaced in Task 10).
- Update `GeneratePerson_CurrentAddressStartsAtHome` to compare nullable: `Assert.Equal(person.HomeAddressId, person.CurrentAddressId.Value)`.
- The `CreatePopulatedState()` helper still uses `LocationGenerator.GenerateCity()` which is fine for now.

- [ ] **Step 7: Run tests to verify compilation and passing**

Run: `dotnet test stakeout.tests/ -v minimal`
Expected: All tests pass (with the removed test gone).

- [ ] **Step 8: Commit**

```bash
git add -A
git commit -m "Fix compilation errors from Person model update"
```

---

## Task 5: MapConfig

**Files:**
- Create: `src/simulation/MapConfig.cs`
- Create: `stakeout.tests/Simulation/MapConfigTests.cs`

- [ ] **Step 1: Write failing test for travel time computation**

```csharp
// stakeout.tests/Simulation/MapConfigTests.cs
using Godot;
using Stakeout.Simulation;
using Xunit;

namespace Stakeout.Tests.Simulation;

public class MapConfigTests
{
    [Fact]
    public void ComputeTravelTimeHours_FullDiagonal_ReturnsMaxTravelTime()
    {
        var config = new MapConfig();
        var from = new Vector2(config.MinX, config.MinY);
        var to = new Vector2(config.MaxX, config.MaxY);

        var hours = config.ComputeTravelTimeHours(from, to);

        Assert.Equal(config.MaxTravelTimeHours, hours, precision: 2);
    }

    [Fact]
    public void ComputeTravelTimeHours_ZeroDistance_ReturnsZero()
    {
        var config = new MapConfig();
        var pos = new Vector2(100, 100);

        var hours = config.ComputeTravelTimeHours(pos, pos);

        Assert.Equal(0.0f, hours);
    }

    [Fact]
    public void ComputeTravelTimeHours_HalfDiagonal_ReturnsHalfMaxTime()
    {
        var config = new MapConfig();
        var center = new Vector2(
            (config.MinX + config.MaxX) / 2,
            (config.MinY + config.MaxY) / 2);
        var corner = new Vector2(config.MaxX, config.MaxY);

        var hours = config.ComputeTravelTimeHours(center, corner);

        Assert.Equal(config.MaxTravelTimeHours / 2, hours, precision: 2);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test stakeout.tests/ --filter MapConfigTests -v minimal`
Expected: FAIL — `MapConfig` does not exist.

- [ ] **Step 3: Implement MapConfig**

```csharp
// src/simulation/MapConfig.cs
using System;
using Godot;

namespace Stakeout.Simulation;

public class MapConfig
{
    public float MinX { get; } = 40f;
    public float MaxX { get; } = 1240f;
    public float MinY { get; } = 40f;
    public float MaxY { get; } = 680f;
    public float MaxTravelTimeHours { get; } = 1.0f;

    public float MapDiagonal =>
        new Vector2(MaxX - MinX, MaxY - MinY).Length();

    public float ComputeTravelTimeHours(Vector2 from, Vector2 to)
    {
        var distance = from.DistanceTo(to);
        return distance / MapDiagonal * MaxTravelTimeHours;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test stakeout.tests/ --filter MapConfigTests -v minimal`
Expected: All 3 tests pass.

- [ ] **Step 5: Commit**

```bash
git add src/simulation/MapConfig.cs stakeout.tests/Simulation/MapConfigTests.cs
git commit -m "Add MapConfig with travel time computation"
```

---

## Task 6: Event Journal

**Files:**
- Create: `src/simulation/events/SimulationEvent.cs`
- Create: `src/simulation/events/EventJournal.cs`
- Create: `stakeout.tests/Simulation/Events/EventJournalTests.cs`

- [ ] **Step 1: Create SimulationEvent and SimulationEventType**

```csharp
// src/simulation/events/SimulationEvent.cs
using System;
using Stakeout.Simulation.Entities;

namespace Stakeout.Simulation.Events;

public enum SimulationEventType
{
    DepartedAddress,
    ArrivedAtAddress,
    StartedWorking,
    StoppedWorking,
    FellAsleep,
    WokeUp,
    ActivityChanged
}

public class SimulationEvent
{
    public DateTime Timestamp { get; set; }
    public int PersonId { get; set; }
    public SimulationEventType EventType { get; set; }
    public int? FromAddressId { get; set; }
    public int? ToAddressId { get; set; }
    public int? AddressId { get; set; }
    public ActivityType? OldActivity { get; set; }
    public ActivityType? NewActivity { get; set; }
}
```

- [ ] **Step 2: Write failing tests for EventJournal**

```csharp
// stakeout.tests/Simulation/Events/EventJournalTests.cs
using System;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Events;
using Xunit;

namespace Stakeout.Tests.Simulation.Events;

public class EventJournalTests
{
    [Fact]
    public void Append_AddsToGlobalList()
    {
        var journal = new EventJournal();
        var evt = new SimulationEvent
        {
            Timestamp = new DateTime(1980, 1, 1, 8, 0, 0),
            PersonId = 1,
            EventType = SimulationEventType.WokeUp,
            AddressId = 10
        };

        journal.Append(evt);

        Assert.Single(journal.AllEvents);
        Assert.Same(evt, journal.AllEvents[0]);
    }

    [Fact]
    public void Append_IndexesByPersonId()
    {
        var journal = new EventJournal();
        var evt = new SimulationEvent
        {
            Timestamp = new DateTime(1980, 1, 1, 8, 0, 0),
            PersonId = 5,
            EventType = SimulationEventType.WokeUp
        };

        journal.Append(evt);

        var personEvents = journal.GetEventsForPerson(5);
        Assert.Single(personEvents);
        Assert.Same(evt, personEvents[0]);
    }

    [Fact]
    public void GetEventsForPerson_UnknownPerson_ReturnsEmptyList()
    {
        var journal = new EventJournal();

        var events = journal.GetEventsForPerson(999);

        Assert.Empty(events);
    }

    [Fact]
    public void Append_MultipleEventsForSamePerson_AllIndexed()
    {
        var journal = new EventJournal();
        var evt1 = new SimulationEvent { Timestamp = new DateTime(1980, 1, 1, 8, 0, 0), PersonId = 1, EventType = SimulationEventType.WokeUp };
        var evt2 = new SimulationEvent { Timestamp = new DateTime(1980, 1, 1, 9, 0, 0), PersonId = 1, EventType = SimulationEventType.DepartedAddress };

        journal.Append(evt1);
        journal.Append(evt2);

        Assert.Equal(2, journal.AllEvents.Count);
        Assert.Equal(2, journal.GetEventsForPerson(1).Count);
    }

    [Fact]
    public void Append_DifferentPeople_IndexedSeparately()
    {
        var journal = new EventJournal();
        var evt1 = new SimulationEvent { Timestamp = new DateTime(1980, 1, 1, 8, 0, 0), PersonId = 1, EventType = SimulationEventType.WokeUp };
        var evt2 = new SimulationEvent { Timestamp = new DateTime(1980, 1, 1, 8, 0, 0), PersonId = 2, EventType = SimulationEventType.WokeUp };

        journal.Append(evt1);
        journal.Append(evt2);

        Assert.Equal(2, journal.AllEvents.Count);
        Assert.Single(journal.GetEventsForPerson(1));
        Assert.Single(journal.GetEventsForPerson(2));
    }
}
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test stakeout.tests/ --filter EventJournalTests -v minimal`
Expected: FAIL — `EventJournal` does not exist.

- [ ] **Step 4: Implement EventJournal**

```csharp
// src/simulation/events/EventJournal.cs
using System.Collections.Generic;

namespace Stakeout.Simulation.Events;

public class EventJournal
{
    private readonly List<SimulationEvent> _allEvents = new();
    private readonly Dictionary<int, List<SimulationEvent>> _byPerson = new();

    public IReadOnlyList<SimulationEvent> AllEvents => _allEvents;

    public void Append(SimulationEvent evt)
    {
        _allEvents.Add(evt);

        if (!_byPerson.TryGetValue(evt.PersonId, out var personEvents))
        {
            personEvents = new List<SimulationEvent>();
            _byPerson[evt.PersonId] = personEvents;
        }
        personEvents.Add(evt);
    }

    public IReadOnlyList<SimulationEvent> GetEventsForPerson(int personId)
    {
        return _byPerson.TryGetValue(personId, out var events)
            ? events
            : [];
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test stakeout.tests/ --filter EventJournalTests -v minimal`
Expected: All 5 tests pass.

- [ ] **Step 6: Commit**

```bash
git add src/simulation/events/SimulationEvent.cs src/simulation/events/EventJournal.cs stakeout.tests/Simulation/Events/EventJournalTests.cs
git commit -m "Add EventJournal with global list and per-person index"
```

---

## Task 7: Update SimulationState with Jobs and EventJournal

**Files:**
- Modify: `src/simulation/SimulationState.cs`
- Modify: `stakeout.tests/Simulation/SimulationStateTests.cs`

- [ ] **Step 1: Add Jobs dictionary and EventJournal to SimulationState**

```csharp
// SimulationState.cs — add imports and new properties
using Stakeout.Simulation.Events;

// Add to class body:
public Dictionary<int, Job> Jobs { get; } = new();
public EventJournal Journal { get; } = new();
```

- [ ] **Step 2: Update SimulationStateTests constructor test**

Add assertions for the new collections:

```csharp
Assert.Empty(state.Jobs);
Assert.Empty(state.Journal.AllEvents);
```

- [ ] **Step 3: Run tests**

Run: `dotnet test stakeout.tests/ --filter SimulationStateTests -v minimal`
Expected: All tests pass.

- [ ] **Step 4: Commit**

```bash
git add src/simulation/SimulationState.cs stakeout.tests/Simulation/SimulationStateTests.cs
git commit -m "Add Jobs dictionary and EventJournal to SimulationState"
```

---

## Task 8: Update GameClock

**Files:**
- Modify: `src/simulation/GameClock.cs`
- Modify: `stakeout.tests/Simulation/GameClockTests.cs`

- [ ] **Step 1: Update default start time to 1980 and add TimeScale**

```csharp
// GameClock.cs
using System;

namespace Stakeout.Simulation;

public class GameClock
{
    public DateTime CurrentTime { get; private set; }
    public double ElapsedSeconds { get; private set; }
    public float TimeScale { get; set; } = 1.0f;

    public GameClock(DateTime? startTime = null)
    {
        CurrentTime = startTime ?? new DateTime(1980, 1, 1, 0, 0, 0);
        ElapsedSeconds = 0.0;
    }

    public void Tick(double deltaSec)
    {
        ElapsedSeconds += deltaSec;
        CurrentTime = CurrentTime.AddSeconds(deltaSec);
    }
}
```

Note: `TimeScale` is just a property on the clock. The scaling math happens in `SimulationManager._Process()` which multiplies `delta * TimeScale` before calling `Tick()`.

- [ ] **Step 2: Update GameClockTests**

Change the default start time assertion from 1984 to 1980:

```csharp
// GameClockTests.cs — Constructor_Default test
Assert.Equal(new DateTime(1980, 1, 1, 0, 0, 0), clock.CurrentTime);
```

Update `Tick_OneSecond` and `Tick_MultipleCalls` and `Tick_LargeDelta` tests to use 1980 instead of 1984.

Add a test for TimeScale default:

```csharp
[Fact]
public void TimeScale_DefaultsToOne()
{
    var clock = new GameClock();
    Assert.Equal(1.0f, clock.TimeScale);
}
```

- [ ] **Step 3: Run tests**

Run: `dotnet test stakeout.tests/ --filter GameClockTests -v minimal`
Expected: All tests pass.

- [ ] **Step 4: Commit**

```bash
git add src/simulation/GameClock.cs stakeout.tests/Simulation/GameClockTests.cs
git commit -m "Update GameClock: default start 1980, add TimeScale property"
```

---

## Task 9: Goal System and SleepScheduleCalculator

**Files:**
- Create: `src/simulation/scheduling/Goal.cs`
- Create: `src/simulation/scheduling/SleepScheduleCalculator.cs`
- Create: `stakeout.tests/Simulation/Scheduling/SleepScheduleCalculatorTests.cs`

- [ ] **Step 1: Create Goal, GoalType, and GoalSet**

```csharp
// src/simulation/scheduling/Goal.cs
using System;
using System.Collections.Generic;

namespace Stakeout.Simulation.Scheduling;

public enum GoalType
{
    BeAtWork,
    BeAtHome,
    Sleep
}

public class Goal
{
    public GoalType Type { get; set; }
    public int Priority { get; set; }
    public TimeSpan WindowStart { get; set; }
    public TimeSpan WindowEnd { get; set; }
}

public class GoalSet
{
    public List<Goal> Goals { get; } = new();
}
```

- [ ] **Step 2: Write failing tests for SleepScheduleCalculator**

```csharp
// stakeout.tests/Simulation/Scheduling/SleepScheduleCalculatorTests.cs
using System;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Scheduling;
using Xunit;

namespace Stakeout.Tests.Simulation.Scheduling;

public class SleepScheduleCalculatorTests
{
    [Fact]
    public void Compute_OfficeWorker_ReturnsDefaultSleepSchedule()
    {
        var job = new Job
        {
            ShiftStart = new TimeSpan(9, 0, 0),
            ShiftEnd = new TimeSpan(17, 0, 0)
        };

        var (sleepTime, wakeTime) = SleepScheduleCalculator.Compute(job, commuteHours: 0.5f);

        // Default 22:00-06:00 doesn't overlap 08:30-17:30 work block
        Assert.Equal(new TimeSpan(22, 0, 0), sleepTime);
        Assert.Equal(new TimeSpan(6, 0, 0), wakeTime);
    }

    [Fact]
    public void Compute_Bartender_ShiftsSleepToAfterShift()
    {
        var job = new Job
        {
            ShiftStart = new TimeSpan(16, 0, 0),
            ShiftEnd = new TimeSpan(2, 0, 0)  // overnight
        };

        var (sleepTime, wakeTime) = SleepScheduleCalculator.Compute(job, commuteHours: 0.5f);

        // Work block ends at 02:00 + 30min commute = 02:30
        // Sleep should start at or after 02:30, wake 8hrs later
        Assert.True(sleepTime >= new TimeSpan(2, 30, 0),
            $"Sleep start {sleepTime} should be >= 02:30 (after shift end + commute)");
        Assert.Equal(sleepTime.Add(new TimeSpan(8, 0, 0)).TotalHours % 24,
            wakeTime.TotalHours % 24, precision: 2);
    }

    [Fact]
    public void Compute_EarlyDinerShift_PushesSleepEarlier()
    {
        var job = new Job
        {
            ShiftStart = new TimeSpan(5, 0, 0),
            ShiftEnd = new TimeSpan(17, 0, 0)
        };

        var (sleepTime, wakeTime) = SleepScheduleCalculator.Compute(job, commuteHours: 0.33f);

        // Work block starts at 05:00 - 20min commute = ~04:40
        // Default wake 06:00 overlaps, so push earlier
        // Wake must be before 04:40
        Assert.True(wakeTime.TotalHours < 5.0,
            $"Wake time {wakeTime} should be before 05:00 to allow commute");
        // Sleep duration should be 8 hours
        var duration = (wakeTime - sleepTime).TotalHours;
        if (duration < 0) duration += 24;
        Assert.Equal(8.0, duration, precision: 1);
    }

    [Fact]
    public void Compute_AlwaysReturns8HourSleepDuration()
    {
        var jobs = new[]
        {
            new Job { ShiftStart = new TimeSpan(9, 0, 0), ShiftEnd = new TimeSpan(17, 0, 0) },
            new Job { ShiftStart = new TimeSpan(16, 0, 0), ShiftEnd = new TimeSpan(2, 0, 0) },
            new Job { ShiftStart = new TimeSpan(5, 0, 0), ShiftEnd = new TimeSpan(17, 0, 0) },
            new Job { ShiftStart = new TimeSpan(21, 0, 0), ShiftEnd = new TimeSpan(9, 0, 0) },
        };

        foreach (var job in jobs)
        {
            var (sleepTime, wakeTime) = SleepScheduleCalculator.Compute(job, commuteHours: 0.5f);
            var duration = (wakeTime - sleepTime).TotalHours;
            if (duration < 0) duration += 24;
            Assert.Equal(8.0, duration, precision: 1);
        }
    }
}
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test stakeout.tests/ --filter SleepScheduleCalculatorTests -v minimal`
Expected: FAIL — `SleepScheduleCalculator` does not exist.

- [ ] **Step 4: Implement SleepScheduleCalculator**

```csharp
// src/simulation/scheduling/SleepScheduleCalculator.cs
using System;
using Stakeout.Simulation.Entities;

namespace Stakeout.Simulation.Scheduling;

public static class SleepScheduleCalculator
{
    private static readonly TimeSpan DefaultSleepTime = new(22, 0, 0);
    private static readonly TimeSpan DefaultWakeTime = new(6, 0, 0);
    private static readonly TimeSpan SleepDuration = new(8, 0, 0);

    public static (TimeSpan sleepTime, TimeSpan wakeTime) Compute(Job job, float commuteHours)
    {
        var commute = TimeSpan.FromHours(commuteHours);
        var workBlockStart = Mod24(job.ShiftStart - commute);
        var workBlockEnd = Mod24(job.ShiftEnd + commute);

        // Check if default sleep window overlaps work block
        if (!Overlaps(DefaultSleepTime, DefaultWakeTime, workBlockStart, workBlockEnd))
        {
            return (DefaultSleepTime, DefaultWakeTime);
        }

        // Place sleep immediately after work block ends
        var sleepTime = workBlockEnd;
        var wakeTime = Mod24(sleepTime + SleepDuration);

        // If this new sleep window overlaps the work block, try placing sleep before work
        if (Overlaps(sleepTime, wakeTime, workBlockStart, workBlockEnd))
        {
            wakeTime = workBlockStart;
            sleepTime = Mod24(wakeTime - SleepDuration);
        }

        return (sleepTime, wakeTime);
    }

    private static bool Overlaps(TimeSpan aStart, TimeSpan aEnd, TimeSpan bStart, TimeSpan bEnd)
    {
        // Convert to minutes-in-day ranges, handling wrapping
        var aRanges = ToMinuteRanges(aStart, aEnd);
        var bRanges = ToMinuteRanges(bStart, bEnd);

        foreach (var a in aRanges)
        foreach (var b in bRanges)
        {
            if (a.start < b.end && b.start < a.end)
                return true;
        }
        return false;
    }

    private static (int start, int end)[] ToMinuteRanges(TimeSpan start, TimeSpan end)
    {
        int s = (int)start.TotalMinutes;
        int e = (int)end.TotalMinutes;

        if (s < e)
            return [(s, e)];

        // Wraps midnight: split into two ranges
        return [(s, 1440), (0, e)];
    }

    private static TimeSpan Mod24(TimeSpan t)
    {
        var totalMinutes = ((int)t.TotalMinutes % 1440 + 1440) % 1440;
        return TimeSpan.FromMinutes(totalMinutes);
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test stakeout.tests/ --filter SleepScheduleCalculatorTests -v minimal`
Expected: All 4 tests pass.

- [ ] **Step 6: Commit**

```bash
git add src/simulation/scheduling/Goal.cs src/simulation/scheduling/SleepScheduleCalculator.cs stakeout.tests/Simulation/Scheduling/SleepScheduleCalculatorTests.cs
git commit -m "Add Goal system and SleepScheduleCalculator"
```

---

## Task 10: DailySchedule and ScheduleBuilder

**Files:**
- Create: `src/simulation/scheduling/DailySchedule.cs`
- Create: `src/simulation/scheduling/ScheduleBuilder.cs`
- Create: `stakeout.tests/Simulation/Scheduling/ScheduleBuilderTests.cs`

- [ ] **Step 1: Create DailySchedule and ScheduleEntry**

```csharp
// src/simulation/scheduling/DailySchedule.cs
using System;
using System.Collections.Generic;
using Stakeout.Simulation.Entities;

namespace Stakeout.Simulation.Scheduling;

public class ScheduleEntry
{
    public ActivityType Activity { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public int? TargetAddressId { get; set; }
    public int? FromAddressId { get; set; }
}

public class DailySchedule
{
    public List<ScheduleEntry> Entries { get; } = new();

    public ScheduleEntry GetEntryAtTime(TimeSpan timeOfDay)
    {
        foreach (var entry in Entries)
        {
            if (SpanContains(entry.StartTime, entry.EndTime, timeOfDay))
                return entry;
        }
        // Fallback to last entry (shouldn't happen with valid schedule)
        return Entries[^1];
    }

    private static bool SpanContains(TimeSpan start, TimeSpan end, TimeSpan time)
    {
        if (start <= end)
            return time >= start && time < end;
        // Wraps midnight
        return time >= start || time < end;
    }
}
```

- [ ] **Step 2: Write failing tests for ScheduleBuilder**

```csharp
// stakeout.tests/Simulation/Scheduling/ScheduleBuilderTests.cs
using System;
using System.Linq;
using Godot;
using Stakeout.Simulation;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Scheduling;
using Xunit;

namespace Stakeout.Tests.Simulation.Scheduling;

public class ScheduleBuilderTests
{
    private static MapConfig DefaultConfig => new();

    private static (Address home, Address work) CreateAddresses(float distance = 500f)
    {
        var home = new Address { Id = 1, Position = new Vector2(100, 100), Type = AddressType.SuburbanHome };
        var work = new Address { Id = 2, Position = new Vector2(100 + distance, 100), Type = AddressType.Office };
        return (home, work);
    }

    [Fact]
    public void Build_OfficeWorker_HasCorrectActivitySequence()
    {
        var (home, work) = CreateAddresses();
        var job = new Job { ShiftStart = new TimeSpan(9, 0, 0), ShiftEnd = new TimeSpan(17, 0, 0), WorkAddressId = work.Id };
        var commuteHours = DefaultConfig.ComputeTravelTimeHours(home.Position, work.Position);
        var (sleepTime, wakeTime) = SleepScheduleCalculator.Compute(job, commuteHours);

        var goalSet = GoalSetBuilder.Build(job, sleepTime, wakeTime);
        var schedule = ScheduleBuilder.Build(goalSet, home, work, DefaultConfig);

        var activities = schedule.Entries.Select(e => e.Activity).ToList();
        // Should include: Sleeping, AtHome, TravellingByCar, Working, TravellingByCar, AtHome, Sleeping
        Assert.Contains(ActivityType.Sleeping, activities);
        Assert.Contains(ActivityType.AtHome, activities);
        Assert.Contains(ActivityType.Working, activities);
        Assert.Contains(ActivityType.TravellingByCar, activities);
    }

    [Fact]
    public void Build_ScheduleCovers24Hours()
    {
        var (home, work) = CreateAddresses();
        var job = new Job { ShiftStart = new TimeSpan(9, 0, 0), ShiftEnd = new TimeSpan(17, 0, 0), WorkAddressId = work.Id };
        var commuteHours = DefaultConfig.ComputeTravelTimeHours(home.Position, work.Position);
        var (sleepTime, wakeTime) = SleepScheduleCalculator.Compute(job, commuteHours);

        var goalSet = GoalSetBuilder.Build(job, sleepTime, wakeTime);
        var schedule = ScheduleBuilder.Build(goalSet, home, work, DefaultConfig);

        // Sum of all entry durations should equal 24 hours
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
    public void Build_TravelEntriesHaveAddressIds()
    {
        var (home, work) = CreateAddresses();
        var job = new Job { ShiftStart = new TimeSpan(9, 0, 0), ShiftEnd = new TimeSpan(17, 0, 0), WorkAddressId = work.Id };
        var commuteHours = DefaultConfig.ComputeTravelTimeHours(home.Position, work.Position);
        var (sleepTime, wakeTime) = SleepScheduleCalculator.Compute(job, commuteHours);

        var goalSet = GoalSetBuilder.Build(job, sleepTime, wakeTime);
        var schedule = ScheduleBuilder.Build(goalSet, home, work, DefaultConfig);

        var travelEntries = schedule.Entries.Where(e => e.Activity == ActivityType.TravellingByCar).ToList();
        Assert.True(travelEntries.Count >= 2, "Should have at least 2 travel entries (to work and back)");
        foreach (var travel in travelEntries)
        {
            Assert.NotNull(travel.FromAddressId);
            Assert.NotNull(travel.TargetAddressId);
        }
    }

    [Fact]
    public void GetEntryAtTime_ReturnsCorrectEntry()
    {
        var (home, work) = CreateAddresses();
        var job = new Job { ShiftStart = new TimeSpan(9, 0, 0), ShiftEnd = new TimeSpan(17, 0, 0), WorkAddressId = work.Id };
        var commuteHours = DefaultConfig.ComputeTravelTimeHours(home.Position, work.Position);
        var (sleepTime, wakeTime) = SleepScheduleCalculator.Compute(job, commuteHours);

        var goalSet = GoalSetBuilder.Build(job, sleepTime, wakeTime);
        var schedule = ScheduleBuilder.Build(goalSet, home, work, DefaultConfig);

        // At 12:00 should be working
        var midday = schedule.GetEntryAtTime(new TimeSpan(12, 0, 0));
        Assert.Equal(ActivityType.Working, midday.Activity);

        // At 03:00 should be sleeping
        var night = schedule.GetEntryAtTime(new TimeSpan(3, 0, 0));
        Assert.Equal(ActivityType.Sleeping, night.Activity);
    }
}
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test stakeout.tests/ --filter ScheduleBuilderTests -v minimal`
Expected: FAIL — `ScheduleBuilder` and `GoalSetBuilder` do not exist.

- [ ] **Step 4: Add GoalSetBuilder to Goal.cs**

```csharp
// Add to src/simulation/scheduling/Goal.cs

public static class GoalSetBuilder
{
    public static GoalSet Build(Job job, TimeSpan sleepTime, TimeSpan wakeTime)
    {
        var goalSet = new GoalSet();

        goalSet.Goals.Add(new Goal
        {
            Type = GoalType.Sleep,
            Priority = 30,
            WindowStart = sleepTime,
            WindowEnd = wakeTime
        });

        goalSet.Goals.Add(new Goal
        {
            Type = GoalType.BeAtWork,
            Priority = 20,
            WindowStart = job.ShiftStart,
            WindowEnd = job.ShiftEnd
        });

        goalSet.Goals.Add(new Goal
        {
            Type = GoalType.BeAtHome,
            Priority = 10,
            WindowStart = TimeSpan.Zero,
            WindowEnd = TimeSpan.Zero  // Always active (24h)
        });

        return goalSet;
    }
}
```

- [ ] **Step 5: Implement ScheduleBuilder**

The builder walks the 24-hour day, resolves the winning goal at each point, and inserts travel entries.

```csharp
// src/simulation/scheduling/ScheduleBuilder.cs
using System;
using System.Collections.Generic;
using System.Linq;
using Stakeout.Simulation.Entities;

namespace Stakeout.Simulation.Scheduling;

public static class ScheduleBuilder
{
    public static DailySchedule Build(GoalSet goalSet, Address homeAddress, Address workAddress, MapConfig mapConfig)
    {
        var commuteHours = mapConfig.ComputeTravelTimeHours(homeAddress.Position, workAddress.Position);
        var commuteSpan = TimeSpan.FromHours(commuteHours);
        var schedule = new DailySchedule();

        // Generate minute-by-minute winning goal, then merge into contiguous blocks
        var minuteGoals = new GoalType[1440];
        for (int m = 0; m < 1440; m++)
        {
            var time = TimeSpan.FromMinutes(m);
            minuteGoals[m] = GetWinningGoal(goalSet, time);
        }

        // Convert goal blocks to activities with travel inserted
        var blocks = MergeToBlocks(minuteGoals);
        var entries = new List<ScheduleEntry>();

        for (int i = 0; i < blocks.Count; i++)
        {
            var block = blocks[i];
            var activity = GoalToActivity(block.goal);
            var locationId = GoalToLocation(block.goal, homeAddress.Id, workAddress.Id);

            // Check if we need travel before this block
            int prevLocationId;
            if (i == 0)
            {
                // Determine where person is at midnight based on last block (wrapping)
                var lastBlock = blocks[^1];
                prevLocationId = GoalToLocation(lastBlock.goal, homeAddress.Id, workAddress.Id);
            }
            else
            {
                // Get location from previous entry (could be a travel entry we just added)
                var prevEntry = entries[^1];
                prevLocationId = prevEntry.TargetAddressId ?? GoalToLocation(blocks[i - 1].goal, homeAddress.Id, workAddress.Id);
            }

            if (locationId != prevLocationId)
            {
                // Insert travel entry, stealing time from the start of this block
                var travelStart = block.startTime;
                var travelEnd = Mod24(travelStart + commuteSpan);
                entries.Add(new ScheduleEntry
                {
                    Activity = ActivityType.TravellingByCar,
                    StartTime = travelStart,
                    EndTime = travelEnd,
                    FromAddressId = prevLocationId,
                    TargetAddressId = locationId
                });

                entries.Add(new ScheduleEntry
                {
                    Activity = activity,
                    StartTime = travelEnd,
                    EndTime = block.endTime
                });
            }
            else
            {
                entries.Add(new ScheduleEntry
                {
                    Activity = activity,
                    StartTime = block.startTime,
                    EndTime = block.endTime
                });
            }
        }

        schedule.Entries.AddRange(entries);
        return schedule;
    }

    private static GoalType GetWinningGoal(GoalSet goalSet, TimeSpan time)
    {
        Goal winner = null;
        foreach (var goal in goalSet.Goals)
        {
            if (!IsGoalActive(goal, time)) continue;
            if (winner == null || goal.Priority > winner.Priority)
                winner = goal;
        }
        return winner?.Type ?? GoalType.BeAtHome;
    }

    private static bool IsGoalActive(Goal goal, TimeSpan time)
    {
        // BeAtHome is always active
        if (goal.Type == GoalType.BeAtHome && goal.WindowStart == goal.WindowEnd)
            return true;

        var start = goal.WindowStart;
        var end = goal.WindowEnd;

        if (start <= end)
            return time >= start && time < end;
        // Wraps midnight
        return time >= start || time < end;
    }

    private static List<(GoalType goal, TimeSpan startTime, TimeSpan endTime)> MergeToBlocks(GoalType[] minuteGoals)
    {
        var blocks = new List<(GoalType, TimeSpan, TimeSpan)>();
        var currentGoal = minuteGoals[0];
        int startMinute = 0;

        for (int m = 1; m < 1440; m++)
        {
            if (minuteGoals[m] != currentGoal)
            {
                blocks.Add((currentGoal, TimeSpan.FromMinutes(startMinute), TimeSpan.FromMinutes(m)));
                currentGoal = minuteGoals[m];
                startMinute = m;
            }
        }
        // Close last block
        blocks.Add((currentGoal, TimeSpan.FromMinutes(startMinute), TimeSpan.FromMinutes(1440)));

        // If first and last block are same goal and last ends at midnight, merge them
        if (blocks.Count > 1 && blocks[0].Item1 == blocks[^1].Item1 && blocks[^1].Item3 == TimeSpan.FromMinutes(1440))
        {
            var first = blocks[0];
            var last = blocks[^1];
            blocks[^1] = (last.Item1, last.Item2, first.Item3);
            blocks.RemoveAt(0);
        }

        return blocks;
    }

    private static ActivityType GoalToActivity(GoalType goal)
    {
        return goal switch
        {
            GoalType.BeAtWork => ActivityType.Working,
            GoalType.Sleep => ActivityType.Sleeping,
            _ => ActivityType.AtHome
        };
    }

    private static int GoalToLocation(GoalType goal, int homeId, int workId)
    {
        return goal switch
        {
            GoalType.BeAtWork => workId,
            _ => homeId
        };
    }

    private static TimeSpan Mod24(TimeSpan t)
    {
        var totalMinutes = ((int)t.TotalMinutes % 1440 + 1440) % 1440;
        return TimeSpan.FromMinutes(totalMinutes);
    }
}
```

- [ ] **Step 6: Run tests to verify they pass**

Run: `dotnet test stakeout.tests/ --filter ScheduleBuilderTests -v minimal`
Expected: All 4 tests pass.

- [ ] **Step 7: Commit**

```bash
git add src/simulation/scheduling/DailySchedule.cs src/simulation/scheduling/ScheduleBuilder.cs src/simulation/scheduling/Goal.cs stakeout.tests/Simulation/Scheduling/ScheduleBuilderTests.cs
git commit -m "Add ScheduleBuilder and DailySchedule"
```

---

## Task 11: PersonBehavior

**Files:**
- Create: `src/simulation/scheduling/PersonBehavior.cs`
- Create: `stakeout.tests/Simulation/Scheduling/PersonBehaviorTests.cs`

- [ ] **Step 1: Write failing tests for PersonBehavior**

```csharp
// stakeout.tests/Simulation/Scheduling/PersonBehaviorTests.cs
using System;
using Godot;
using Stakeout.Simulation;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Scheduling;
using Xunit;

namespace Stakeout.Tests.Simulation.Scheduling;

public class PersonBehaviorTests
{
    private static (SimulationState state, Person person, DailySchedule schedule) CreateTestScenario()
    {
        var state = new SimulationState();
        var home = new Address { Id = state.GenerateEntityId(), Position = new Vector2(100, 100), Type = AddressType.SuburbanHome, Number = 1, StreetId = 1 };
        var work = new Address { Id = state.GenerateEntityId(), Position = new Vector2(600, 100), Type = AddressType.Office, Number = 2, StreetId = 1 };
        state.Addresses[home.Id] = home;
        state.Addresses[work.Id] = work;

        var job = new Job
        {
            Id = state.GenerateEntityId(),
            Type = JobType.OfficeWorker,
            Title = "Office Worker",
            WorkAddressId = work.Id,
            ShiftStart = new TimeSpan(9, 0, 0),
            ShiftEnd = new TimeSpan(17, 0, 0)
        };
        state.Jobs[job.Id] = job;

        var mapConfig = new MapConfig();
        var commuteHours = mapConfig.ComputeTravelTimeHours(home.Position, work.Position);
        var (sleepTime, wakeTime) = SleepScheduleCalculator.Compute(job, commuteHours);

        var person = new Person
        {
            Id = state.GenerateEntityId(),
            FirstName = "Test",
            LastName = "Person",
            HomeAddressId = home.Id,
            JobId = job.Id,
            CurrentAddressId = home.Id,
            CurrentPosition = home.Position,
            CurrentActivity = ActivityType.Sleeping,
            PreferredSleepTime = sleepTime,
            PreferredWakeTime = wakeTime
        };
        state.People[person.Id] = person;

        var goalSet = GoalSetBuilder.Build(job, sleepTime, wakeTime);
        var schedule = ScheduleBuilder.Build(goalSet, home, work, mapConfig);

        return (state, person, schedule);
    }

    [Fact]
    public void Update_AtMiddayOnWorkday_PersonIsWorking()
    {
        var (state, person, schedule) = CreateTestScenario();
        // Set clock to noon
        state.Clock.Tick(12 * 3600);
        var behavior = new PersonBehavior(new MapConfig());

        behavior.Update(person, schedule, state);

        Assert.Equal(ActivityType.Working, person.CurrentActivity);
    }

    [Fact]
    public void Update_At3AM_PersonIsSleeping()
    {
        var (state, person, schedule) = CreateTestScenario();
        // Set clock to 03:00
        state.Clock.Tick(3 * 3600);
        var behavior = new PersonBehavior(new MapConfig());

        behavior.Update(person, schedule, state);

        Assert.Equal(ActivityType.Sleeping, person.CurrentActivity);
    }

    [Fact]
    public void Update_TransitionAppendsJournalEvent()
    {
        var (state, person, schedule) = CreateTestScenario();
        // Start at midnight (sleeping), jump to 07:00 (should be AtHome after waking)
        state.Clock.Tick(7 * 3600);
        var behavior = new PersonBehavior(new MapConfig());

        behavior.Update(person, schedule, state);

        Assert.True(state.Journal.AllEvents.Count > 0, "Should have recorded activity transition events");
    }

    [Fact]
    public void Update_DuringTravel_InterpolatesPosition()
    {
        var (state, person, schedule) = CreateTestScenario();
        var behavior = new PersonBehavior(new MapConfig());

        // Find a travel entry in the schedule
        ScheduleEntry travelEntry = null;
        foreach (var entry in schedule.Entries)
        {
            if (entry.Activity == ActivityType.TravellingByCar)
            {
                travelEntry = entry;
                break;
            }
        }
        Assert.NotNull(travelEntry);

        // Set clock to midpoint of travel
        var midTime = travelEntry.StartTime + (travelEntry.EndTime - travelEntry.StartTime) / 2;
        if (midTime < TimeSpan.Zero) midTime += TimeSpan.FromHours(24);
        state.Clock.Tick(midTime.TotalSeconds);

        behavior.Update(person, schedule, state);

        // Person should be travelling and position should not equal home or work exactly
        Assert.Equal(ActivityType.TravellingByCar, person.CurrentActivity);
        Assert.Null(person.CurrentAddressId);
        Assert.NotNull(person.TravelInfo);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test stakeout.tests/ --filter PersonBehaviorTests -v minimal`
Expected: FAIL — `PersonBehavior` does not exist.

- [ ] **Step 3: Implement PersonBehavior**

```csharp
// src/simulation/scheduling/PersonBehavior.cs
using System;
using Godot;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Events;

namespace Stakeout.Simulation.Scheduling;

public class PersonBehavior
{
    private readonly MapConfig _mapConfig;

    public PersonBehavior(MapConfig mapConfig)
    {
        _mapConfig = mapConfig;
    }

    public void Update(Person person, DailySchedule schedule, SimulationState state)
    {
        var timeOfDay = state.Clock.CurrentTime.TimeOfDay;
        var entry = schedule.GetEntryAtTime(timeOfDay);

        // Handle travel position interpolation
        if (person.CurrentActivity == ActivityType.TravellingByCar && person.TravelInfo != null)
        {
            var elapsed = (state.Clock.CurrentTime - person.TravelInfo.DepartureTime).TotalSeconds;
            var totalTravel = (person.TravelInfo.ArrivalTime - person.TravelInfo.DepartureTime).TotalSeconds;

            if (totalTravel > 0)
            {
                var progress = Math.Clamp(elapsed / totalTravel, 0.0, 1.0);
                person.CurrentPosition = person.TravelInfo.FromPosition.Lerp(person.TravelInfo.ToPosition, (float)progress);
            }

            // Check if arrived
            if (state.Clock.CurrentTime >= person.TravelInfo.ArrivalTime)
            {
                var arrivedAddressId = person.TravelInfo.ToAddressId;
                person.CurrentPosition = person.TravelInfo.ToPosition;
                person.CurrentAddressId = arrivedAddressId;
                person.TravelInfo = null;

                state.Journal.Append(new SimulationEvent
                {
                    Timestamp = state.Clock.CurrentTime,
                    PersonId = person.Id,
                    EventType = SimulationEventType.ArrivedAtAddress,
                    AddressId = arrivedAddressId
                });
            }
        }

        // Transition to new activity if schedule says so
        if (entry.Activity != person.CurrentActivity && person.CurrentActivity != ActivityType.TravellingByCar)
        {
            TransitionTo(person, entry, state);
        }
        // If we just arrived and the entry is different from travelling, also transition
        else if (entry.Activity != person.CurrentActivity && person.TravelInfo == null)
        {
            TransitionTo(person, entry, state);
        }
    }

    private void TransitionTo(Person person, ScheduleEntry entry, SimulationState state)
    {
        var oldActivity = person.CurrentActivity;
        var newActivity = entry.Activity;

        // Log departure from old activity
        LogActivityEnd(person, oldActivity, state);

        if (newActivity == ActivityType.TravellingByCar && entry.FromAddressId.HasValue && entry.TargetAddressId.HasValue)
        {
            var fromAddr = state.Addresses[entry.FromAddressId.Value];
            var toAddr = state.Addresses[entry.TargetAddressId.Value];
            var travelHours = _mapConfig.ComputeTravelTimeHours(fromAddr.Position, toAddr.Position);

            person.CurrentActivity = ActivityType.TravellingByCar;
            person.CurrentAddressId = null;
            person.TravelInfo = new TravelInfo
            {
                FromPosition = fromAddr.Position,
                ToPosition = toAddr.Position,
                DepartureTime = state.Clock.CurrentTime,
                ArrivalTime = state.Clock.CurrentTime.AddHours(travelHours),
                FromAddressId = entry.FromAddressId.Value,
                ToAddressId = entry.TargetAddressId.Value
            };

            state.Journal.Append(new SimulationEvent
            {
                Timestamp = state.Clock.CurrentTime,
                PersonId = person.Id,
                EventType = SimulationEventType.DepartedAddress,
                FromAddressId = entry.FromAddressId.Value,
                ToAddressId = entry.TargetAddressId.Value
            });
        }
        else
        {
            person.CurrentActivity = newActivity;
            LogActivityStart(person, newActivity, state);
        }

        state.Journal.Append(new SimulationEvent
        {
            Timestamp = state.Clock.CurrentTime,
            PersonId = person.Id,
            EventType = SimulationEventType.ActivityChanged,
            OldActivity = oldActivity,
            NewActivity = newActivity
        });
    }

    private static void LogActivityEnd(Person person, ActivityType activity, SimulationState state)
    {
        var eventType = activity switch
        {
            ActivityType.Working => SimulationEventType.StoppedWorking,
            ActivityType.Sleeping => SimulationEventType.WokeUp,
            _ => (SimulationEventType?)null
        };
        if (eventType.HasValue)
        {
            state.Journal.Append(new SimulationEvent
            {
                Timestamp = state.Clock.CurrentTime,
                PersonId = person.Id,
                EventType = eventType.Value,
                AddressId = person.CurrentAddressId
            });
        }
    }

    private static void LogActivityStart(Person person, ActivityType activity, SimulationState state)
    {
        var eventType = activity switch
        {
            ActivityType.Working => SimulationEventType.StartedWorking,
            ActivityType.Sleeping => SimulationEventType.FellAsleep,
            _ => (SimulationEventType?)null
        };
        if (eventType.HasValue)
        {
            state.Journal.Append(new SimulationEvent
            {
                Timestamp = state.Clock.CurrentTime,
                PersonId = person.Id,
                EventType = eventType.Value,
                AddressId = person.CurrentAddressId
            });
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test stakeout.tests/ --filter PersonBehaviorTests -v minimal`
Expected: All 4 tests pass.

- [ ] **Step 5: Commit**

```bash
git add src/simulation/scheduling/PersonBehavior.cs stakeout.tests/Simulation/Scheduling/PersonBehaviorTests.cs
git commit -m "Add PersonBehavior: schedule-driven activity transitions with travel interpolation"
```

---

## Task 12: Overhaul PersonGenerator

**Files:**
- Modify: `src/simulation/PersonGenerator.cs`
- Modify: `src/simulation/LocationGenerator.cs`
- Modify: `stakeout.tests/Simulation/PersonGeneratorTests.cs`
- Remove or update: `stakeout.tests/Simulation/LocationGeneratorTests.cs`

- [ ] **Step 1: Gut LocationGenerator to scaffolding-only**

```csharp
// src/simulation/LocationGenerator.cs
using System;
using Godot;
using Stakeout.Simulation.Data;
using Stakeout.Simulation.Entities;

namespace Stakeout.Simulation;

public class LocationGenerator
{
    private readonly Random _random = new();
    private readonly MapConfig _mapConfig;

    public LocationGenerator(MapConfig mapConfig)
    {
        _mapConfig = mapConfig;
    }

    public void GenerateCityScaffolding(SimulationState state)
    {
        var country = new Country { Name = "United States" };
        state.Countries.Add(country);

        var city = new City
        {
            Id = state.GenerateEntityId(),
            Name = "Boston",
            CountryName = country.Name
        };
        state.Cities[city.Id] = city;
    }

    public Address GenerateAddress(SimulationState state, AddressType type)
    {
        // Find or create a street in the first city
        var cityId = state.Cities.Keys.GetEnumerator();
        cityId.MoveNext();

        var street = FindOrCreateStreet(state, cityId.Current);

        var address = new Address
        {
            Id = state.GenerateEntityId(),
            Number = GenerateAddressNumber(),
            StreetId = street.Id,
            Type = type,
            Position = new Vector2(
                (float)(_random.NextDouble() * (_mapConfig.MaxX - _mapConfig.MinX) + _mapConfig.MinX),
                (float)(_random.NextDouble() * (_mapConfig.MaxY - _mapConfig.MinY) + _mapConfig.MinY)
            )
        };
        state.Addresses[address.Id] = address;
        return address;
    }

    private Street FindOrCreateStreet(SimulationState state, int cityId)
    {
        // Reuse a random existing street or create a new one
        if (state.Streets.Count > 0 && _random.NextDouble() < 0.5)
        {
            var streets = new System.Collections.Generic.List<Street>(state.Streets.Values);
            return streets[_random.Next(streets.Count)];
        }

        var usedNames = new System.Collections.Generic.HashSet<string>();
        foreach (var s in state.Streets.Values) usedNames.Add(s.Name);

        string streetName;
        do
        {
            var baseName = StreetData.StreetNames[_random.Next(StreetData.StreetNames.Length)];
            var suffix = StreetData.StreetSuffixes[_random.Next(StreetData.StreetSuffixes.Length)];
            streetName = $"{baseName} {suffix}";
        } while (!usedNames.Add(streetName));

        var street = new Street
        {
            Id = state.GenerateEntityId(),
            Name = streetName,
            CityId = cityId
        };
        state.Streets[street.Id] = street;
        return street;
    }

    private int GenerateAddressNumber()
    {
        double u1 = 1.0 - _random.NextDouble();
        double u2 = _random.NextDouble();
        double normal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
        double logMean = Math.Log(200);
        double logStd = 1.0;
        return (int)Math.Clamp(Math.Exp(logMean + logStd * normal), 1, 10000);
    }
}
```

- [ ] **Step 2: Rewrite PersonGenerator for on-demand generation**

```csharp
// src/simulation/PersonGenerator.cs
using System;
using Stakeout.Simulation.Data;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Events;
using Stakeout.Simulation.Scheduling;

namespace Stakeout.Simulation;

public class PersonGenerator
{
    private readonly Random _random = new();
    private readonly LocationGenerator _locationGenerator;
    private readonly MapConfig _mapConfig;

    public PersonGenerator(LocationGenerator locationGenerator, MapConfig mapConfig)
    {
        _locationGenerator = locationGenerator;
        _mapConfig = mapConfig;
    }

    public (Person person, DailySchedule schedule) GeneratePerson(SimulationState state)
    {
        // 1. Pick job type and generate matching address
        var jobType = PickJobType();
        var addressType = JobTypeToAddressType(jobType);
        var workAddress = _locationGenerator.GenerateAddress(state, addressType);

        // 2. Generate home address
        var homeAddress = _locationGenerator.GenerateAddress(state, AddressType.SuburbanHome);

        // 3. Create Job
        var job = CreateJob(state, jobType, workAddress.Id);
        state.Jobs[job.Id] = job;

        // 4. Compute commute and sleep schedule
        var commuteHours = _mapConfig.ComputeTravelTimeHours(homeAddress.Position, workAddress.Position);
        var (sleepTime, wakeTime) = SleepScheduleCalculator.Compute(job, commuteHours);

        // 5. Build schedule
        var goalSet = GoalSetBuilder.Build(job, sleepTime, wakeTime);
        var schedule = ScheduleBuilder.Build(goalSet, homeAddress, workAddress, _mapConfig);

        // 6. Determine initial state from schedule and current time
        var timeOfDay = state.Clock.CurrentTime.TimeOfDay;
        var currentEntry = schedule.GetEntryAtTime(timeOfDay);
        var initialActivity = currentEntry.Activity;

        // For simplicity, if the person is generated mid-travel, place them at home
        int? currentAddressId;
        var currentPosition = homeAddress.Position;
        if (initialActivity == ActivityType.TravellingByCar)
        {
            initialActivity = ActivityType.AtHome;
            currentAddressId = homeAddress.Id;
        }
        else if (initialActivity == ActivityType.Working)
        {
            currentAddressId = workAddress.Id;
            currentPosition = workAddress.Position;
        }
        else
        {
            currentAddressId = homeAddress.Id;
        }

        // 7. Create person
        var person = new Person
        {
            Id = state.GenerateEntityId(),
            FirstName = NameData.FirstNames[_random.Next(NameData.FirstNames.Length)],
            LastName = NameData.LastNames[_random.Next(NameData.LastNames.Length)],
            CreatedAt = state.Clock.CurrentTime,
            HomeAddressId = homeAddress.Id,
            JobId = job.Id,
            CurrentAddressId = currentAddressId,
            CurrentPosition = currentPosition,
            CurrentActivity = initialActivity,
            PreferredSleepTime = sleepTime,
            PreferredWakeTime = wakeTime
        };
        state.People[person.Id] = person;

        // 8. Log initial event
        state.Journal.Append(new SimulationEvent
        {
            Timestamp = state.Clock.CurrentTime,
            PersonId = person.Id,
            EventType = SimulationEventType.ActivityChanged,
            NewActivity = initialActivity
        });

        return (person, schedule);
    }

    private JobType PickJobType()
    {
        var types = Enum.GetValues<JobType>();
        return types[_random.Next(types.Length)];
    }

    private static AddressType JobTypeToAddressType(JobType jobType)
    {
        return jobType switch
        {
            JobType.DinerWaiter => AddressType.Diner,
            JobType.OfficeWorker => AddressType.Office,
            JobType.Bartender => AddressType.DiveBar,
            _ => AddressType.Office
        };
    }

    private Job CreateJob(SimulationState state, JobType jobType, int workAddressId)
    {
        var (title, shiftStart, shiftEnd) = jobType switch
        {
            JobType.DinerWaiter => (
                "Waiter",
                GenerateDinerShiftStart(),
                TimeSpan.Zero // placeholder, computed below
            ),
            JobType.OfficeWorker => (
                "Office Worker",
                new TimeSpan(9, 0, 0),
                new TimeSpan(17, 0, 0)
            ),
            JobType.Bartender => (
                "Bartender",
                new TimeSpan(16, 0, 0),
                new TimeSpan(2, 0, 0)
            ),
            _ => ("Worker", new TimeSpan(9, 0, 0), new TimeSpan(17, 0, 0))
        };

        // Compute diner shift end (12 hours after start)
        if (jobType == JobType.DinerWaiter)
        {
            var totalMinutes = ((int)shiftStart.TotalMinutes + 720) % 1440;
            shiftEnd = TimeSpan.FromMinutes(totalMinutes);
        }

        return new Job
        {
            Id = state.GenerateEntityId(),
            Type = jobType,
            Title = title,
            WorkAddressId = workAddressId,
            ShiftStart = shiftStart,
            ShiftEnd = shiftEnd,
            WorkDays = new[] { DayOfWeek.Sunday, DayOfWeek.Monday, DayOfWeek.Tuesday,
                              DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday }
        };
    }

    private TimeSpan GenerateDinerShiftStart()
    {
        // Random start between 05:00 and 21:00
        var startHour = 5 + _random.Next(17); // 5 to 21 inclusive
        return new TimeSpan(startHour, 0, 0);
    }
}
```

- [ ] **Step 3: Rewrite PersonGeneratorTests**

```csharp
// stakeout.tests/Simulation/PersonGeneratorTests.cs
using System;
using System.Linq;
using Stakeout.Simulation;
using Stakeout.Simulation.Data;
using Stakeout.Simulation.Entities;
using Xunit;

namespace Stakeout.Tests.Simulation;

public class PersonGeneratorTests
{
    private static SimulationState CreateState()
    {
        var state = new SimulationState();
        var mapConfig = new MapConfig();
        var locationGen = new LocationGenerator(mapConfig);
        locationGen.GenerateCityScaffolding(state);
        return state;
    }

    private static PersonGenerator CreateGenerator()
    {
        var mapConfig = new MapConfig();
        return new PersonGenerator(new LocationGenerator(mapConfig), mapConfig);
    }

    [Fact]
    public void GeneratePerson_ReturnsPersonWithValidId()
    {
        var state = CreateState();
        var generator = CreateGenerator();

        var (person, _) = generator.GeneratePerson(state);

        Assert.True(person.Id > 0);
    }

    [Fact]
    public void GeneratePerson_AddsPersonToState()
    {
        var state = CreateState();
        var generator = CreateGenerator();

        var (person, _) = generator.GeneratePerson(state);

        Assert.Contains(person.Id, state.People.Keys);
    }

    [Fact]
    public void GeneratePerson_NameComesFromNameDataPools()
    {
        var state = CreateState();
        var generator = CreateGenerator();

        var (person, _) = generator.GeneratePerson(state);

        Assert.Contains(person.FirstName, NameData.FirstNames);
        Assert.Contains(person.LastName, NameData.LastNames);
    }

    [Fact]
    public void GeneratePerson_CreatesHomeAddress()
    {
        var state = CreateState();
        var generator = CreateGenerator();

        var (person, _) = generator.GeneratePerson(state);

        Assert.True(state.Addresses.ContainsKey(person.HomeAddressId));
        Assert.Equal(AddressCategory.Residential, state.Addresses[person.HomeAddressId].Category);
    }

    [Fact]
    public void GeneratePerson_CreatesJobWithMatchingAddress()
    {
        var state = CreateState();
        var generator = CreateGenerator();

        var (person, _) = generator.GeneratePerson(state);

        Assert.True(state.Jobs.ContainsKey(person.JobId));
        var job = state.Jobs[person.JobId];
        Assert.True(state.Addresses.ContainsKey(job.WorkAddressId));
        Assert.Equal(AddressCategory.Commercial, state.Addresses[job.WorkAddressId].Category);
    }

    [Fact]
    public void GeneratePerson_ReturnsDailySchedule()
    {
        var state = CreateState();
        var generator = CreateGenerator();

        var (_, schedule) = generator.GeneratePerson(state);

        Assert.NotNull(schedule);
        Assert.True(schedule.Entries.Count > 0);
    }

    [Fact]
    public void GeneratePerson_SetsInitialActivity()
    {
        var state = CreateState();
        var generator = CreateGenerator();

        var (person, _) = generator.GeneratePerson(state);

        // Should have a valid activity (not default)
        Assert.True(Enum.IsDefined(person.CurrentActivity));
    }

    [Fact]
    public void GeneratePerson_AppendsJournalEvent()
    {
        var state = CreateState();
        var generator = CreateGenerator();

        var (person, _) = generator.GeneratePerson(state);

        Assert.True(state.Journal.GetEventsForPerson(person.Id).Count > 0);
    }

    [Fact]
    public void GeneratePerson_HasSleepSchedule()
    {
        var state = CreateState();
        var generator = CreateGenerator();

        var (person, _) = generator.GeneratePerson(state);

        // Sleep preferences should be set (non-zero duration between sleep and wake)
        var duration = (person.PreferredWakeTime - person.PreferredSleepTime).TotalHours;
        if (duration < 0) duration += 24;
        Assert.Equal(8.0, duration, precision: 1);
    }
}
```

- [ ] **Step 4: Update LocationGeneratorTests**

Replace with minimal tests for the new scaffolding-only behavior:

```csharp
// stakeout.tests/Simulation/LocationGeneratorTests.cs
using Stakeout.Simulation;
using Stakeout.Simulation.Entities;
using Xunit;

namespace Stakeout.Tests.Simulation;

public class LocationGeneratorTests
{
    [Fact]
    public void GenerateCityScaffolding_CreatesCountryAndCity()
    {
        var state = new SimulationState();
        var generator = new LocationGenerator(new MapConfig());
        generator.GenerateCityScaffolding(state);

        Assert.Single(state.Countries);
        Assert.Single(state.Cities);
    }

    [Fact]
    public void GenerateAddress_CreatesAddressInState()
    {
        var state = new SimulationState();
        var generator = new LocationGenerator(new MapConfig());
        generator.GenerateCityScaffolding(state);

        var address = generator.GenerateAddress(state, AddressType.Office);

        Assert.Contains(address.Id, state.Addresses.Keys);
        Assert.Equal(AddressType.Office, address.Type);
    }

    [Fact]
    public void GenerateAddress_PositionWithinMapBounds()
    {
        var state = new SimulationState();
        var config = new MapConfig();
        var generator = new LocationGenerator(config);
        generator.GenerateCityScaffolding(state);

        var address = generator.GenerateAddress(state, AddressType.SuburbanHome);

        Assert.InRange(address.Position.X, config.MinX, config.MaxX);
        Assert.InRange(address.Position.Y, config.MinY, config.MaxY);
    }
}
```

- [ ] **Step 5: Run all tests**

Run: `dotnet test stakeout.tests/ -v minimal`
Expected: All tests pass.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "Overhaul PersonGenerator and LocationGenerator for on-demand generation"
```

---

## Task 13: Update SimulationManager

**Files:**
- Modify: `src/simulation/SimulationManager.cs`

- [ ] **Step 1: Rewrite SimulationManager**

```csharp
// src/simulation/SimulationManager.cs
using System;
using System.Collections.Generic;
using Godot;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Scheduling;

namespace Stakeout.Simulation;

public partial class SimulationManager : Node
{
    public SimulationState State { get; private set; }

    public event Action<Person> PersonAdded;
    public event Action<Address> AddressAdded;
    public event Action PlayerCreated;

    private readonly MapConfig _mapConfig = new();
    private readonly PersonGenerator _personGenerator;
    private readonly LocationGenerator _locationGenerator;
    private readonly PersonBehavior _personBehavior;

    // Store schedules per person (keyed by person ID)
    private readonly Dictionary<int, DailySchedule> _schedules = new();

    public SimulationManager(SimulationState state)
    {
        State = state;
        _locationGenerator = new LocationGenerator(_mapConfig);
        _personGenerator = new PersonGenerator(_locationGenerator, _mapConfig);
        _personBehavior = new PersonBehavior(_mapConfig);
    }

    public override void _Ready()
    {
        _locationGenerator.GenerateCityScaffolding(State);

        // Generate 1 person
        var (person, schedule) = _personGenerator.GeneratePerson(State);
        _schedules[person.Id] = schedule;

        // Emit events for all generated addresses
        foreach (var address in State.Addresses.Values)
        {
            AddressAdded?.Invoke(address);
        }
        PersonAdded?.Invoke(person);

        // Create player at a random residential address
        var random = new Random();
        var homeAddress = State.Addresses[person.HomeAddressId]; // Use same neighborhood
        var playerHome = _locationGenerator.GenerateAddress(State, AddressType.SuburbanHome);
        AddressAdded?.Invoke(playerHome);

        State.Player = new Player
        {
            HomeAddressId = playerHome.Id,
            CurrentAddressId = playerHome.Id
        };
        PlayerCreated?.Invoke();
    }

    public override void _Process(double delta)
    {
        var scaledDelta = delta * State.Clock.TimeScale;
        if (scaledDelta <= 0) return;

        State.Clock.Tick(scaledDelta);

        foreach (var person in State.People.Values)
        {
            if (_schedules.TryGetValue(person.Id, out var schedule))
            {
                _personBehavior.Update(person, schedule, State);
            }
        }
    }
}
```

- [ ] **Step 2: Run all tests to verify nothing is broken**

Run: `dotnet test stakeout.tests/ -v minimal`
Expected: All tests pass.

- [ ] **Step 3: Commit**

```bash
git add src/simulation/SimulationManager.cs
git commit -m "Update SimulationManager: time scaling, PersonBehavior updates, single person spawn"
```

---

## Task 14: Update SimulationDebug UI

**Files:**
- Modify: `scenes/simulation_debug/SimulationDebug.cs`
- Modify: `scenes/simulation_debug/SimulationDebug.tscn`

- [ ] **Step 1: Add time control buttons to the scene file**

Add an HBoxContainer with time control buttons above or beside the clock label. The buttons go in the top-right area near the clock.

Update `SimulationDebug.tscn` to add:
- A `TimeControls` HBoxContainer with 4 Button children (Pause, Play, Fast, SuperFast)
- Position near the clock label

```
[node name="TimeControls" type="HBoxContainer" parent="."]
layout_mode = 1
anchors_preset = 1
anchor_left = 1.0
anchor_right = 1.0
offset_left = -200.0
offset_top = 50.0
offset_right = -10.0
offset_bottom = 80.0

[node name="PauseButton" type="Button" parent="TimeControls"]
layout_mode = 2
text = "⏸"
theme_override_fonts/font = ExtResource("2_font")
theme_override_font_sizes/font_size = 14

[node name="PlayButton" type="Button" parent="TimeControls"]
layout_mode = 2
text = "▶"
theme_override_fonts/font = ExtResource("2_font")
theme_override_font_sizes/font_size = 14

[node name="FastButton" type="Button" parent="TimeControls"]
layout_mode = 2
text = "▶▶"
theme_override_fonts/font = ExtResource("2_font")
theme_override_font_sizes/font_size = 14

[node name="SuperFastButton" type="Button" parent="TimeControls"]
layout_mode = 2
text = "▶▶▶"
theme_override_fonts/font = ExtResource("2_font")
theme_override_font_sizes/font_size = 14
```

- [ ] **Step 2: Update SimulationDebug.cs**

Key changes:
1. Wire up time control buttons to set `Clock.TimeScale`
2. Update clock display to show date + time
3. Update `_Process` to move person dots based on `CurrentPosition`
4. Update hover tooltip to show activity
5. Dim sleeping person dots

```csharp
// Add fields at top of class:
private Button _pauseButton;
private Button _playButton;
private Button _fastButton;
private Button _superFastButton;

private static readonly Color SleepingPersonColor = new(0.5f, 0.5f, 0.5f);
```

In `_Ready()`, add after existing setup:

```csharp
// Time controls
_pauseButton = GetNode<Button>("TimeControls/PauseButton");
_playButton = GetNode<Button>("TimeControls/PlayButton");
_fastButton = GetNode<Button>("TimeControls/FastButton");
_superFastButton = GetNode<Button>("TimeControls/SuperFastButton");

_pauseButton.Pressed += () => SetTimeScale(0f);
_playButton.Pressed += () => SetTimeScale(1f);
_fastButton.Pressed += () => SetTimeScale(4f);
_superFastButton.Pressed += () => SetTimeScale(8f);

HighlightActiveTimeButton(); // Highlight Play by default
```

Add `SetTimeScale` method:

```csharp
private void SetTimeScale(float scale)
{
    _simulationManager.State.Clock.TimeScale = scale;
    HighlightActiveTimeButton();
}

private void HighlightActiveTimeButton()
{
    var scale = _simulationManager.State.Clock.TimeScale;
    var activeColor = new Color(0.3f, 0.6f, 1.0f);
    var normalColor = new Color(1f, 1f, 1f);

    _pauseButton.Modulate = scale == 0f ? activeColor : normalColor;
    _playButton.Modulate = scale == 1f ? activeColor : normalColor;
    _fastButton.Modulate = scale == 4f ? activeColor : normalColor;
    _superFastButton.Modulate = scale == 8f ? activeColor : normalColor;
}
```

Update `_Process`:

```csharp
public override void _Process(double delta)
{
    var time = _simulationManager.State.Clock.CurrentTime;
    _clockLabel.Text = time.ToString("ddd MMM dd, yyyy HH:mm");

    // Update person dot positions and colors
    foreach (var (personId, dot) in _personNodes)
    {
        var person = _simulationManager.State.People[personId];
        var size = new Vector2(EntityDotSize, EntityDotSize);
        dot.Position = person.CurrentPosition - size / 2;

        // Dim sleeping persons
        var style = new StyleBoxFlat
        {
            BgColor = person.CurrentActivity == ActivityType.Sleeping ? SleepingPersonColor : PersonColor,
            BorderColor = BorderColor,
            BorderWidthLeft = DotBorderWidth,
            BorderWidthRight = DotBorderWidth,
            BorderWidthTop = DotBorderWidth,
            BorderWidthBottom = DotBorderWidth
        };
        dot.AddThemeStyleboxOverride("panel", style);
    }

    UpdateHoverLabel();
}
```

Update `OnPersonAdded` to use `CurrentPosition`:

```csharp
private void OnPersonAdded(Person person)
{
    var size = new Vector2(EntityDotSize, EntityDotSize);
    var dot = CreateIconPanel(size, PersonColor, BorderColor, DotBorderWidth);
    dot.Position = person.CurrentPosition - size / 2;
    _entityDots.AddChild(dot);
    _personNodes[person.Id] = dot;
}
```

Update the person hover in `UpdateHoverLabel` to show activity:

```csharp
// In the person hover section, change:
if (!lines.Contains(person.FullName))
    lines.Add(person.FullName);
// To:
if (!lines.Contains(person.FullName))
{
    var activityLabel = person.CurrentActivity switch
    {
        ActivityType.Working => "Working",
        ActivityType.Sleeping => "Sleeping",
        ActivityType.TravellingByCar => "Travelling",
        ActivityType.AtHome => "At Home",
        _ => ""
    };
    lines.Add($"{person.FullName} — {activityLabel}");
}
```

- [ ] **Step 3: Commit**

```bash
git add scenes/simulation_debug/SimulationDebug.cs scenes/simulation_debug/SimulationDebug.tscn
git commit -m "Add time controls and position-based person rendering to SimulationDebug"
```

---

## Task 15: Update DossierWindow and Final Integration

**Files:**
- Modify: `scenes/evidence_board/DossierWindow.cs`

- [ ] **Step 1: Update DossierWindow to use Job instead of WorkAddressId**

This was partially addressed in Task 4. Verify the full implementation:

```csharp
// DossierWindow.cs — in Populate method, person branch:
if (state.Jobs.TryGetValue(person.JobId, out var job) &&
    state.Addresses.TryGetValue(job.WorkAddressId, out var work))
{
    var street = state.Streets[work.StreetId];
    lines.Add($"Work: {work.Number} {street.Name} ({job.Title})");
}

// Address branch — update people query:
var people = state.People.Values
    .Where(p => p.HomeAddressId == address.Id ||
                (state.Jobs.TryGetValue(p.JobId, out var j) && j.WorkAddressId == address.Id))
    .ToList();
```

- [ ] **Step 2: Run all tests**

Run: `dotnet test stakeout.tests/ -v minimal`
Expected: All tests pass.

- [ ] **Step 3: Commit**

```bash
git add scenes/evidence_board/DossierWindow.cs
git commit -m "Update DossierWindow to use Job for work address lookup"
```

---

## Task 16: Manual Testing and Polish

**No test files** — this is visual verification in-engine.

- [ ] **Step 1: Launch the game in Godot and verify**

Check the following:
1. Game starts with 1 person and ~3 addresses (home, work, player home)
2. Clock shows date + time format: `Tue Jan 01, 1980 00:00`
3. Time control buttons work (pause, 1x, 4x, 8x)
4. At 8x speed, watch the person go through their daily cycle:
   - Wake up → idle at home → travel to work → work → travel home → idle at home → sleep
5. Person dot moves smoothly during travel
6. Person dot dims when sleeping
7. Hover tooltip shows activity (e.g. "John Smith — Working")
8. Evidence board dossier shows job title instead of just address type

- [ ] **Step 2: Fix any issues found during manual testing**

- [ ] **Step 3: Final commit if any fixes were needed**

```bash
git add -A
git commit -m "Polish: fix issues found during manual testing"
```

---

## Summary of Commits

1. Add ActivityType enum and TravelInfo class
2. Add Job entity and JobType enum
3. Update Person: add JobId, activity, position, sleep prefs; remove WorkAddressId
4. Fix compilation errors from Person model update
5. Add MapConfig with travel time computation
6. Add EventJournal with global list and per-person index
7. Add Jobs dictionary and EventJournal to SimulationState
8. Update GameClock: default start 1980, add TimeScale property
9. Add Goal system and SleepScheduleCalculator
10. Add ScheduleBuilder and DailySchedule
11. Add PersonBehavior: schedule-driven activity transitions with travel interpolation
12. Overhaul PersonGenerator and LocationGenerator for on-demand generation
13. Update SimulationManager: time scaling, PersonBehavior updates, single person spawn
14. Add time controls and position-based person rendering to SimulationDebug
15. Update DossierWindow to use Job for work address lookup
16. Polish: fix issues found during manual testing
