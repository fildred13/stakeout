# Business Entities & Staffing Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make businesses first-class entities with operating hours, positions, and staffing — every NPC gets a real job at a real business.

**Architecture:** Business shells (name, hours, empty positions) are created during city generation alongside commercial addresses. PersonGenerator assigns every NPC to an open position, creating a WorkShiftObjective that drives their work schedule. BusinessResolver fills remaining positions on demand. Job entity is removed; Position is the single source of truth.

**Tech Stack:** C# / .NET 8 / Godot 4.6 / xUnit

**Spec:** `docs/superpowers/specs/2026-03-29-business-entities-staffing-design.md`

**CRITICAL: Never prefix shell commands with `cd`. The working directory is already the project root. Run commands directly (e.g., `dotnet test stakeout.tests/`, not `cd path && dotnet test`). This breaks permission matching and is strictly prohibited.**

---

## File Map

### New Files

| File | Purpose |
|------|---------|
| `src/simulation/entities/Business.cs` | Business, BusinessType, BusinessHours, Position entities |
| `src/simulation/businesses/IBusinessTemplate.cs` | Interface for business type templates |
| `src/simulation/businesses/BusinessTemplateRegistry.cs` | Registry mapping BusinessType → template |
| `src/simulation/businesses/DinerBusinessTemplate.cs` | Diner: 24/7, cooks/waiters/managers |
| `src/simulation/businesses/DiveBarBusinessTemplate.cs` | Dive bar: noon–1am/4am, bartenders/managers |
| `src/simulation/businesses/OfficeBusinessTemplate.cs` | Office: 7am–7pm weekdays, workers/managers/CEO |
| `src/simulation/businesses/BusinessGenerator.cs` | Creates business shells during city gen |
| `src/simulation/data/BusinessNameData.cs` | Name pools and patterns for business name generation |
| `src/simulation/objectives/WorkShiftObjective.cs` | Priority 60 objective producing work shift actions |
| `src/simulation/SpawnRequirements.cs` | Constraint bag for PersonGenerator |
| `src/simulation/BusinessResolver.cs` | Fills all empty positions at a business on demand |
| `stakeout.tests/Simulation/Businesses/BusinessTemplateTests.cs` | Template generation tests |
| `stakeout.tests/Simulation/Businesses/BusinessGeneratorTests.cs` | City gen integration tests |
| `stakeout.tests/Simulation/Businesses/BusinessResolverTests.cs` | Resolution tests |
| `stakeout.tests/Simulation/Objectives/WorkShiftObjectiveTests.cs` | Work shift scheduling tests |
| `stakeout.tests/Simulation/Scheduling/DoorLockingServiceTests.cs` | Door lock/unlock tests |

### Modified Files

| File | Changes |
|------|---------|
| `src/simulation/entities/Person.cs` | Remove `JobId`, add `BusinessId?`, `PositionId?` |
| `src/simulation/entities/Job.cs` | **DELETE** — replaced by Position |
| `src/simulation/SimulationState.cs` | Remove `Jobs` dict, add `Businesses` dict |
| `src/simulation/PersonGenerator.cs` | Add `SpawnRequirements` overload, remove Job creation, assign positions |
| `src/simulation/scheduling/SleepScheduleCalculator.cs` | Change `Compute(Job,...)` → `Compute(Position,...)` |
| `src/simulation/scheduling/DoorLockingService.cs` | Full implementation — schedule-driven door locking |
| `src/simulation/city/CityGenerator.cs` | Call BusinessGenerator after address creation |
| `src/simulation/SimulationManager.cs` | Call DoorLockingService in _Process loop |
| `scenes/game_shell/GameShell.cs` | Restore job section in debug inspector |
| `stakeout.tests/Simulation/PersonGeneratorTests.cs` | Update all Job-related assertions to use Business/Position |
| `stakeout.tests/Simulation/Scheduling/SleepScheduleCalculatorTests.cs` | Update to use Position |

---

## Task 1: Business Entity & Data Model

**Files:**
- Create: `src/simulation/entities/Business.cs`
- Modify: `src/simulation/SimulationState.cs:18`

- [ ] **Step 1: Write tests for Business entity**

Create `stakeout.tests/Simulation/Entities/BusinessTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using Stakeout.Simulation.Entities;
using Xunit;

namespace Stakeout.Tests.Simulation.Entities;

public class BusinessTests
{
    [Fact]
    public void Business_DefaultState_IsNotResolved()
    {
        var biz = new Business { Id = 1, Name = "Test", Type = BusinessType.Diner };
        Assert.False(biz.IsResolved);
    }

    [Fact]
    public void Business_Positions_StartsEmpty()
    {
        var biz = new Business { Id = 1, Name = "Test", Type = BusinessType.Diner };
        Assert.Empty(biz.Positions);
    }

    [Fact]
    public void Business_Hours_StartsEmpty()
    {
        var biz = new Business { Id = 1, Name = "Test", Type = BusinessType.Diner };
        Assert.Empty(biz.Hours);
    }

    [Fact]
    public void Position_DefaultAssignment_IsNull()
    {
        var pos = new Position { Id = 1, BusinessId = 1, Role = "cook" };
        Assert.Null(pos.AssignedPersonId);
    }

    [Fact]
    public void BusinessHours_NullOpenTime_MeansClosed()
    {
        var hours = new BusinessHours { Day = DayOfWeek.Sunday, OpenTime = null, CloseTime = null };
        Assert.Null(hours.OpenTime);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test stakeout.tests/ --filter "FullyQualifiedName~BusinessTests" -v minimal`
Expected: FAIL — types don't exist yet

- [ ] **Step 3: Create Business entity file**

Create `src/simulation/entities/Business.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace Stakeout.Simulation.Entities;

public enum BusinessType { Diner, DiveBar, Office }

public class Business
{
    public int Id { get; set; }
    public int AddressId { get; set; }
    public string Name { get; set; }
    public BusinessType Type { get; set; }
    public List<BusinessHours> Hours { get; set; } = new();
    public List<Position> Positions { get; set; } = new();
    public bool IsResolved { get; set; }
}

public class BusinessHours
{
    public DayOfWeek Day { get; set; }
    public TimeSpan? OpenTime { get; set; }
    public TimeSpan? CloseTime { get; set; }
}

public class Position
{
    public int Id { get; set; }
    public int BusinessId { get; set; }
    public string Role { get; set; }
    public TimeSpan ShiftStart { get; set; }
    public TimeSpan ShiftEnd { get; set; }
    public DayOfWeek[] WorkDays { get; set; }
    public int? AssignedPersonId { get; set; }
}
```

- [ ] **Step 4: Add Businesses dictionary to SimulationState**

In `src/simulation/SimulationState.cs`, add after the Jobs line (line 18):

```csharp
public Dictionary<int, Business> Businesses { get; } = new();
```

Do NOT remove Jobs yet — that happens in Task 5 when all references are updated.

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test stakeout.tests/ --filter "FullyQualifiedName~BusinessTests" -v minimal`
Expected: PASS

- [ ] **Step 6: Run full test suite**

Run: `dotnet test stakeout.tests/ -v minimal`
Expected: All existing tests still pass

- [ ] **Step 7: Commit**

```
git add src/simulation/entities/Business.cs src/simulation/SimulationState.cs stakeout.tests/Simulation/Entities/BusinessTests.cs
git commit -m "feat: add Business, Position, BusinessHours entities and Businesses dictionary"
```

---

## Task 2: Business Name Data

**Files:**
- Create: `src/simulation/data/BusinessNameData.cs`

- [ ] **Step 1: Write test for name generation**

Create `stakeout.tests/Simulation/Data/BusinessNameDataTests.cs`:

```csharp
using System;
using Stakeout.Simulation.Data;
using Stakeout.Simulation.Entities;
using Xunit;

namespace Stakeout.Tests.Simulation.Data;

public class BusinessNameDataTests
{
    [Theory]
    [InlineData(BusinessType.Diner)]
    [InlineData(BusinessType.DiveBar)]
    [InlineData(BusinessType.Office)]
    public void GenerateName_ReturnsNonEmpty(BusinessType type)
    {
        var name = BusinessNameData.GenerateName(type, new Random(42));
        Assert.False(string.IsNullOrWhiteSpace(name));
    }

    [Fact]
    public void GenerateName_DifferentSeeds_ProduceDifferentNames()
    {
        var name1 = BusinessNameData.GenerateName(BusinessType.Diner, new Random(1));
        var name2 = BusinessNameData.GenerateName(BusinessType.Diner, new Random(99));
        // With enough variety in the pool, different seeds should produce different names
        // (not guaranteed but extremely likely with good pools)
        Assert.NotEqual(name1, name2);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test stakeout.tests/ --filter "FullyQualifiedName~BusinessNameDataTests" -v minimal`
Expected: FAIL

- [ ] **Step 3: Create BusinessNameData**

Create `src/simulation/data/BusinessNameData.cs`:

```csharp
using System;
using Stakeout.Simulation.Entities;

namespace Stakeout.Simulation.Data;

public static class BusinessNameData
{
    // Diner patterns: "{First}'s Diner", "{Last}'s", "The {Adj} {Noun}"
    private static readonly string[] DinerAdjectives =
        { "Golden", "Silver", "Lucky", "Blue", "Red", "Sunny", "Happy", "Cozy" };
    private static readonly string[] DinerNouns =
        { "Spoon", "Plate", "Griddle", "Skillet", "Fork", "Cup", "Kettle", "Pan" };

    // Dive bar patterns: "The {Adj} {Noun}"
    private static readonly string[] BarAdjectives =
        { "Rusty", "Blind", "Brass", "Iron", "Broken", "Crooked", "Lucky", "Dusty", "Salty", "Smoky" };
    private static readonly string[] BarNouns =
        { "Nail", "Pig", "Monkey", "Anchor", "Barrel", "Lantern", "Horseshoe", "Parrot", "Compass", "Rudder" };

    // Office patterns: "{Last} & {Last}", "{Last} {Suffix}"
    private static readonly string[] OfficeSuffixes =
        { "Associates", "Partners", "Group", "Consulting", "Solutions", "Industries", "Corp", "Holdings" };

    public static string GenerateName(BusinessType type, Random random)
    {
        return type switch
        {
            BusinessType.Diner => GenerateDinerName(random),
            BusinessType.DiveBar => GenerateBarName(random),
            BusinessType.Office => GenerateOfficeName(random),
            _ => $"Business #{random.Next(1000)}"
        };
    }

    private static string GenerateDinerName(Random random)
    {
        var pattern = random.Next(3);
        return pattern switch
        {
            0 => $"{NameData.FirstNames[random.Next(NameData.FirstNames.Length)]}'s Diner",
            1 => $"{NameData.LastNames[random.Next(NameData.LastNames.Length)]}'s",
            _ => $"The {Pick(DinerAdjectives, random)} {Pick(DinerNouns, random)}"
        };
    }

    private static string GenerateBarName(Random random)
    {
        return $"The {Pick(BarAdjectives, random)} {Pick(BarNouns, random)}";
    }

    private static string GenerateOfficeName(Random random)
    {
        var pattern = random.Next(2);
        return pattern switch
        {
            0 => $"{Pick(NameData.LastNames, random)} & {Pick(NameData.LastNames, random)}",
            _ => $"{Pick(NameData.LastNames, random)} {Pick(OfficeSuffixes, random)}"
        };
    }

    private static string Pick(string[] array, Random random) => array[random.Next(array.Length)];
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test stakeout.tests/ --filter "FullyQualifiedName~BusinessNameDataTests" -v minimal`
Expected: PASS

- [ ] **Step 5: Commit**

```
git add src/simulation/data/BusinessNameData.cs stakeout.tests/Simulation/Data/BusinessNameDataTests.cs
git commit -m "feat: add business name generation with per-type word pools"
```

---

## Task 3: Business Templates

**Files:**
- Create: `src/simulation/businesses/IBusinessTemplate.cs`
- Create: `src/simulation/businesses/BusinessTemplateRegistry.cs`
- Create: `src/simulation/businesses/DinerBusinessTemplate.cs`
- Create: `src/simulation/businesses/DiveBarBusinessTemplate.cs`
- Create: `src/simulation/businesses/OfficeBusinessTemplate.cs`
- Test: `stakeout.tests/Simulation/Businesses/BusinessTemplateTests.cs`

- [ ] **Step 1: Write tests for all three templates**

Create `stakeout.tests/Simulation/Businesses/BusinessTemplateTests.cs`:

```csharp
using System;
using System.Linq;
using Stakeout.Simulation;
using Stakeout.Simulation.Businesses;
using Stakeout.Simulation.Entities;
using Xunit;

namespace Stakeout.Tests.Simulation.Businesses;

public class BusinessTemplateTests
{
    private static SimulationState CreateState() => new SimulationState();

    // --- Diner ---

    [Fact]
    public void DinerTemplate_Hours_Open24_7()
    {
        var template = new DinerBusinessTemplate();
        var hours = template.GenerateHours();
        Assert.Equal(7, hours.Count);
        Assert.All(hours, h => Assert.NotNull(h.OpenTime));
    }

    [Fact]
    public void DinerTemplate_Positions_HasCooksAndWaiters()
    {
        var template = new DinerBusinessTemplate();
        var state = CreateState();
        var positions = template.GeneratePositions(state, new Random(42));
        Assert.Contains(positions, p => p.Role == "cook");
        Assert.Contains(positions, p => p.Role == "waiter");
        Assert.True(positions.Count(p => p.Role == "cook") >= 2);
    }

    [Fact]
    public void DinerTemplate_GenerateName_ReturnsNonEmpty()
    {
        var template = new DinerBusinessTemplate();
        Assert.False(string.IsNullOrWhiteSpace(template.GenerateName(new Random(42))));
    }

    // --- DiveBar ---

    [Fact]
    public void DiveBarTemplate_Hours_ClosedSunday()
    {
        var template = new DiveBarBusinessTemplate();
        var hours = template.GenerateHours();
        var sunday = hours.First(h => h.Day == DayOfWeek.Sunday);
        Assert.Null(sunday.OpenTime);
    }

    [Fact]
    public void DiveBarTemplate_Hours_FridayClosesAt4am()
    {
        var template = new DiveBarBusinessTemplate();
        var hours = template.GenerateHours();
        var friday = hours.First(h => h.Day == DayOfWeek.Friday);
        Assert.Equal(new TimeSpan(4, 0, 0), friday.CloseTime);
    }

    [Fact]
    public void DiveBarTemplate_Positions_HasBartenders()
    {
        var template = new DiveBarBusinessTemplate();
        var state = CreateState();
        var positions = template.GeneratePositions(state, new Random(42));
        Assert.Contains(positions, p => p.Role == "bartender");
    }

    // --- Office ---

    [Fact]
    public void OfficeTemplate_Hours_ClosedWeekends()
    {
        var template = new OfficeBusinessTemplate();
        var hours = template.GenerateHours();
        var saturday = hours.First(h => h.Day == DayOfWeek.Saturday);
        var sunday = hours.First(h => h.Day == DayOfWeek.Sunday);
        Assert.Null(saturday.OpenTime);
        Assert.Null(sunday.OpenTime);
    }

    [Fact]
    public void OfficeTemplate_Hours_OpenWeekdays7to7()
    {
        var template = new OfficeBusinessTemplate();
        var hours = template.GenerateHours();
        var monday = hours.First(h => h.Day == DayOfWeek.Monday);
        Assert.Equal(new TimeSpan(7, 0, 0), monday.OpenTime);
        Assert.Equal(new TimeSpan(19, 0, 0), monday.CloseTime);
    }

    [Fact]
    public void OfficeTemplate_Positions_HasCEO()
    {
        var template = new OfficeBusinessTemplate();
        var state = CreateState();
        var positions = template.GeneratePositions(state, new Random(42));
        Assert.Single(positions.Where(p => p.Role == "ceo"));
    }

    [Fact]
    public void OfficeTemplate_Positions_AllWeekdaysOnly()
    {
        var template = new OfficeBusinessTemplate();
        var state = CreateState();
        var positions = template.GeneratePositions(state, new Random(42));
        foreach (var p in positions)
        {
            Assert.DoesNotContain(DayOfWeek.Saturday, p.WorkDays);
            Assert.DoesNotContain(DayOfWeek.Sunday, p.WorkDays);
        }
    }

    // --- Registry ---

    [Theory]
    [InlineData(BusinessType.Diner)]
    [InlineData(BusinessType.DiveBar)]
    [InlineData(BusinessType.Office)]
    public void Registry_Get_ReturnsTemplateForAllTypes(BusinessType type)
    {
        BusinessTemplateRegistry.RegisterAll();
        Assert.NotNull(BusinessTemplateRegistry.Get(type));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test stakeout.tests/ --filter "FullyQualifiedName~BusinessTemplateTests" -v minimal`
Expected: FAIL

- [ ] **Step 3: Create IBusinessTemplate interface**

Create `src/simulation/businesses/IBusinessTemplate.cs`:

```csharp
using System;
using System.Collections.Generic;
using Stakeout.Simulation.Entities;

namespace Stakeout.Simulation.Businesses;

public interface IBusinessTemplate
{
    BusinessType Type { get; }
    List<BusinessHours> GenerateHours();
    List<Position> GeneratePositions(SimulationState state, Random random);
    string GenerateName(Random random);
}
```

- [ ] **Step 4: Create DinerBusinessTemplate**

Create `src/simulation/businesses/DinerBusinessTemplate.cs`:

```csharp
using System;
using System.Collections.Generic;
using Stakeout.Simulation.Data;
using Stakeout.Simulation.Entities;

namespace Stakeout.Simulation.Businesses;

public class DinerBusinessTemplate : IBusinessTemplate
{
    public BusinessType Type => BusinessType.Diner;

    public List<BusinessHours> GenerateHours()
    {
        // 24/7
        var hours = new List<BusinessHours>();
        foreach (DayOfWeek day in Enum.GetValues<DayOfWeek>())
        {
            hours.Add(new BusinessHours
            {
                Day = day,
                OpenTime = TimeSpan.Zero,
                CloseTime = TimeSpan.Zero // 00:00 to 00:00 = 24 hours
            });
        }
        return hours;
    }

    public List<Position> GeneratePositions(SimulationState state, Random random)
    {
        var positions = new List<Position>();
        // 3 shifts: morning (5-13), afternoon (13-21), night (21-5)
        var shifts = new[]
        {
            (new TimeSpan(5, 0, 0), new TimeSpan(13, 0, 0)),
            (new TimeSpan(13, 0, 0), new TimeSpan(21, 0, 0)),
            (new TimeSpan(21, 0, 0), new TimeSpan(5, 0, 0)),
        };
        var allDays = Enum.GetValues<DayOfWeek>();

        foreach (var (start, end) in shifts)
        {
            // 1 cook per shift
            positions.Add(MakePosition(state, "cook", start, end, allDays));

            // 1-2 waiters per shift
            var waiterCount = 1 + random.Next(2);
            for (int i = 0; i < waiterCount; i++)
                positions.Add(MakePosition(state, "waiter", start, end, allDays));

            // 0-1 manager per shift
            if (random.Next(2) == 0)
                positions.Add(MakePosition(state, "manager", start, end, allDays));
        }

        return positions;
    }

    public string GenerateName(Random random) => BusinessNameData.GenerateName(BusinessType.Diner, random);

    private static Position MakePosition(SimulationState state, string role, TimeSpan start, TimeSpan end, DayOfWeek[] days)
    {
        return new Position
        {
            Id = state.GenerateEntityId(),
            Role = role,
            ShiftStart = start,
            ShiftEnd = end,
            WorkDays = days
        };
    }
}
```

- [ ] **Step 5: Create DiveBarBusinessTemplate**

Create `src/simulation/businesses/DiveBarBusinessTemplate.cs`:

```csharp
using System;
using System.Collections.Generic;
using Stakeout.Simulation.Data;
using Stakeout.Simulation.Entities;

namespace Stakeout.Simulation.Businesses;

public class DiveBarBusinessTemplate : IBusinessTemplate
{
    public BusinessType Type => BusinessType.DiveBar;

    public List<BusinessHours> GenerateHours()
    {
        var hours = new List<BusinessHours>();
        foreach (DayOfWeek day in Enum.GetValues<DayOfWeek>())
        {
            if (day == DayOfWeek.Sunday)
            {
                hours.Add(new BusinessHours { Day = day, OpenTime = null, CloseTime = null });
                continue;
            }

            var closeTime = (day == DayOfWeek.Friday || day == DayOfWeek.Saturday)
                ? new TimeSpan(4, 0, 0)   // 4am next day
                : new TimeSpan(1, 0, 0);  // 1am next day

            hours.Add(new BusinessHours
            {
                Day = day,
                OpenTime = new TimeSpan(12, 0, 0),
                CloseTime = closeTime
            });
        }
        return hours;
    }

    public List<Position> GeneratePositions(SimulationState state, Random random)
    {
        var positions = new List<Position>();
        // Work days: Mon-Sat (bar is closed Sunday)
        var workDays = new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
                               DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday };

        // Single shift aligned to operating hours (noon to close)
        // Use 12:00-01:00 as the standard shift (close time varies by day, but shift is fixed)
        var shiftStart = new TimeSpan(16, 0, 0);
        var shiftEnd = new TimeSpan(2, 0, 0); // 2am

        // 1-2 bartenders
        var bartenderCount = 1 + random.Next(2);
        for (int i = 0; i < bartenderCount; i++)
            positions.Add(MakePosition(state, "bartender", shiftStart, shiftEnd, workDays));

        // 0-1 manager
        if (random.Next(2) == 0)
            positions.Add(MakePosition(state, "manager", shiftStart, shiftEnd, workDays));

        return positions;
    }

    public string GenerateName(Random random) => BusinessNameData.GenerateName(BusinessType.DiveBar, random);

    private static Position MakePosition(SimulationState state, string role, TimeSpan start, TimeSpan end, DayOfWeek[] days)
    {
        return new Position
        {
            Id = state.GenerateEntityId(),
            Role = role,
            ShiftStart = start,
            ShiftEnd = end,
            WorkDays = days
        };
    }
}
```

- [ ] **Step 6: Create OfficeBusinessTemplate**

Create `src/simulation/businesses/OfficeBusinessTemplate.cs`:

```csharp
using System;
using System.Collections.Generic;
using Stakeout.Simulation.Data;
using Stakeout.Simulation.Entities;

namespace Stakeout.Simulation.Businesses;

public class OfficeBusinessTemplate : IBusinessTemplate
{
    public BusinessType Type => BusinessType.Office;

    public List<BusinessHours> GenerateHours()
    {
        var hours = new List<BusinessHours>();
        foreach (DayOfWeek day in Enum.GetValues<DayOfWeek>())
        {
            if (day == DayOfWeek.Saturday || day == DayOfWeek.Sunday)
            {
                hours.Add(new BusinessHours { Day = day, OpenTime = null, CloseTime = null });
                continue;
            }

            hours.Add(new BusinessHours
            {
                Day = day,
                OpenTime = new TimeSpan(7, 0, 0),
                CloseTime = new TimeSpan(19, 0, 0)
            });
        }
        return hours;
    }

    public List<Position> GeneratePositions(SimulationState state, Random random)
    {
        var positions = new List<Position>();
        var weekdays = new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
                               DayOfWeek.Thursday, DayOfWeek.Friday };
        var shiftStart = new TimeSpan(9, 0, 0);
        var shiftEnd = new TimeSpan(17, 0, 0);

        // 5-10 office workers
        var workerCount = 5 + random.Next(6);
        for (int i = 0; i < workerCount; i++)
            positions.Add(MakePosition(state, "office_worker", shiftStart, shiftEnd, weekdays));

        // 1-2 managers
        var managerCount = 1 + random.Next(2);
        for (int i = 0; i < managerCount; i++)
            positions.Add(MakePosition(state, "manager", shiftStart, shiftEnd, weekdays));

        // 1 CEO
        positions.Add(MakePosition(state, "ceo", shiftStart, shiftEnd, weekdays));

        return positions;
    }

    public string GenerateName(Random random) => BusinessNameData.GenerateName(BusinessType.Office, random);

    private static Position MakePosition(SimulationState state, string role, TimeSpan start, TimeSpan end, DayOfWeek[] days)
    {
        return new Position
        {
            Id = state.GenerateEntityId(),
            Role = role,
            ShiftStart = start,
            ShiftEnd = end,
            WorkDays = days
        };
    }
}
```

- [ ] **Step 7: Create BusinessTemplateRegistry**

Create `src/simulation/businesses/BusinessTemplateRegistry.cs`:

```csharp
using System.Collections.Generic;
using Stakeout.Simulation.Entities;

namespace Stakeout.Simulation.Businesses;

public static class BusinessTemplateRegistry
{
    private static readonly Dictionary<BusinessType, IBusinessTemplate> _templates = new();

    public static void Register(BusinessType type, IBusinessTemplate template)
    {
        _templates[type] = template;
    }

    public static IBusinessTemplate Get(BusinessType type)
    {
        return _templates.TryGetValue(type, out var template) ? template : null;
    }

    public static void RegisterAll()
    {
        Register(BusinessType.Diner, new DinerBusinessTemplate());
        Register(BusinessType.DiveBar, new DiveBarBusinessTemplate());
        Register(BusinessType.Office, new OfficeBusinessTemplate());
    }
}
```

- [ ] **Step 8: Run tests to verify they pass**

Run: `dotnet test stakeout.tests/ --filter "FullyQualifiedName~BusinessTemplateTests" -v minimal`
Expected: PASS

- [ ] **Step 9: Run full test suite**

Run: `dotnet test stakeout.tests/ -v minimal`
Expected: All pass

- [ ] **Step 10: Commit**

```
git add src/simulation/businesses/ stakeout.tests/Simulation/Businesses/BusinessTemplateTests.cs
git commit -m "feat: add business templates for diner, dive bar, and office"
```

---

## Task 4: BusinessGenerator & City Gen Integration

**Files:**
- Create: `src/simulation/businesses/BusinessGenerator.cs`
- Modify: `src/simulation/city/CityGenerator.cs:33-42`
- Test: `stakeout.tests/Simulation/Businesses/BusinessGeneratorTests.cs`

- [ ] **Step 1: Write tests for BusinessGenerator**

Create `stakeout.tests/Simulation/Businesses/BusinessGeneratorTests.cs`:

```csharp
using System;
using System.Linq;
using Stakeout.Simulation;
using Stakeout.Simulation.Addresses;
using Stakeout.Simulation.Businesses;
using Stakeout.Simulation.City;
using Stakeout.Simulation.Entities;
using Xunit;
using CityEntity = Stakeout.Simulation.Entities.City;

namespace Stakeout.Tests.Simulation.Businesses;

public class BusinessGeneratorTests
{
    private static (SimulationState state, CityEntity city) CreateStateWithCity()
    {
        AddressTemplateRegistry.RegisterAll();
        BusinessTemplateRegistry.RegisterAll();
        var state = new SimulationState();
        var city = new CityEntity
        {
            Id = state.GenerateEntityId(),
            Name = "Boston",
            CountryName = "United States"
        };
        state.Cities[city.Id] = city;
        return (state, city);
    }

    [Fact]
    public void CreateBusiness_ReturnsBusiness_WithCorrectAddressId()
    {
        var (state, city) = CreateStateWithCity();
        var address = new Address
        {
            Id = state.GenerateEntityId(),
            CityId = city.Id,
            Type = AddressType.Diner,
            GridX = 5, GridY = 5
        };
        state.Addresses[address.Id] = address;

        var biz = BusinessGenerator.CreateBusiness(state, address, new Random(42));

        Assert.Equal(address.Id, biz.AddressId);
        Assert.Equal(BusinessType.Diner, biz.Type);
        Assert.False(biz.IsResolved);
    }

    [Fact]
    public void CreateBusiness_AddedToState()
    {
        var (state, city) = CreateStateWithCity();
        var address = new Address
        {
            Id = state.GenerateEntityId(),
            CityId = city.Id,
            Type = AddressType.Diner,
            GridX = 5, GridY = 5
        };
        state.Addresses[address.Id] = address;

        var biz = BusinessGenerator.CreateBusiness(state, address, new Random(42));

        Assert.True(state.Businesses.ContainsKey(biz.Id));
    }

    [Fact]
    public void CreateBusiness_HasName()
    {
        var (state, city) = CreateStateWithCity();
        var address = new Address
        {
            Id = state.GenerateEntityId(),
            CityId = city.Id,
            Type = AddressType.Diner,
            GridX = 5, GridY = 5
        };
        state.Addresses[address.Id] = address;

        var biz = BusinessGenerator.CreateBusiness(state, address, new Random(42));

        Assert.False(string.IsNullOrWhiteSpace(biz.Name));
    }

    [Fact]
    public void CreateBusiness_HasPositionsWithIds()
    {
        var (state, city) = CreateStateWithCity();
        var address = new Address
        {
            Id = state.GenerateEntityId(),
            CityId = city.Id,
            Type = AddressType.Office,
            GridX = 5, GridY = 5
        };
        state.Addresses[address.Id] = address;

        var biz = BusinessGenerator.CreateBusiness(state, address, new Random(42));

        Assert.NotEmpty(biz.Positions);
        Assert.All(biz.Positions, p => Assert.True(p.Id > 0));
        Assert.All(biz.Positions, p => Assert.Equal(biz.Id, p.BusinessId));
    }

    [Fact]
    public void CreateBusiness_AllPositionsUnassigned()
    {
        var (state, city) = CreateStateWithCity();
        var address = new Address
        {
            Id = state.GenerateEntityId(),
            CityId = city.Id,
            Type = AddressType.DiveBar,
            GridX = 5, GridY = 5
        };
        state.Addresses[address.Id] = address;

        var biz = BusinessGenerator.CreateBusiness(state, address, new Random(42));

        Assert.All(biz.Positions, p => Assert.Null(p.AssignedPersonId));
    }

    [Fact]
    public void CreateBusiness_ReturnsNull_ForNonCommercialAddress()
    {
        var (state, city) = CreateStateWithCity();
        var address = new Address
        {
            Id = state.GenerateEntityId(),
            CityId = city.Id,
            Type = AddressType.SuburbanHome,
            GridX = 5, GridY = 5
        };
        state.Addresses[address.Id] = address;

        var biz = BusinessGenerator.CreateBusiness(state, address, new Random(42));

        Assert.Null(biz);
    }

    [Fact]
    public void CityGeneration_CreatesBusinesses_ForCommercialAddresses()
    {
        var (state, city) = CreateStateWithCity();
        var cityGen = new CityGenerator(seed: 42);
        state.CityGrids[city.Id] = cityGen.Generate(state, city);

        // City should have businesses
        Assert.NotEmpty(state.Businesses);

        // Every business should reference a valid address
        foreach (var biz in state.Businesses.Values)
        {
            Assert.True(state.Addresses.ContainsKey(biz.AddressId));
            var addr = state.Addresses[biz.AddressId];
            Assert.Equal(AddressCategory.Commercial, addr.Category);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test stakeout.tests/ --filter "FullyQualifiedName~BusinessGeneratorTests" -v minimal`
Expected: FAIL

- [ ] **Step 3: Create BusinessGenerator**

Create `src/simulation/businesses/BusinessGenerator.cs`:

```csharp
using System;
using Stakeout.Simulation.Entities;

namespace Stakeout.Simulation.Businesses;

public static class BusinessGenerator
{
    private static readonly AddressType[] CommercialTypes =
        { AddressType.Diner, AddressType.DiveBar, AddressType.Office };

    public static Business CreateBusiness(SimulationState state, Address address, Random random)
    {
        var businessType = AddressTypeToBusinessType(address.Type);
        if (businessType == null) return null;

        var template = BusinessTemplateRegistry.Get(businessType.Value);
        if (template == null) return null;

        var business = new Business
        {
            Id = state.GenerateEntityId(),
            AddressId = address.Id,
            Name = template.GenerateName(random),
            Type = businessType.Value,
            Hours = template.GenerateHours(),
            Positions = template.GeneratePositions(state, random),
            IsResolved = false
        };

        // Set BusinessId on all positions
        foreach (var pos in business.Positions)
            pos.BusinessId = business.Id;

        state.Businesses[business.Id] = business;
        return business;
    }

    /// <summary>
    /// Creates business shells for all commercial addresses in the state.
    /// Called during city generation after addresses are created.
    /// </summary>
    public static void CreateBusinessesForCity(SimulationState state, Random random)
    {
        foreach (var address in state.Addresses.Values)
        {
            if (address.Category != AddressCategory.Commercial) continue;
            if (BusinessExistsForAddress(state, address.Id)) continue;
            CreateBusiness(state, address, random);
        }
    }

    private static bool BusinessExistsForAddress(SimulationState state, int addressId)
    {
        foreach (var biz in state.Businesses.Values)
        {
            if (biz.AddressId == addressId) return true;
        }
        return false;
    }

    private static BusinessType? AddressTypeToBusinessType(AddressType type)
    {
        return type switch
        {
            AddressType.Diner => BusinessType.Diner,
            AddressType.DiveBar => BusinessType.DiveBar,
            AddressType.Office => BusinessType.Office,
            _ => null
        };
    }
}
```

- [ ] **Step 4: Hook into CityGenerator**

In `src/simulation/city/CityGenerator.cs`, add a using at the top:

```csharp
using Stakeout.Simulation.Businesses;
```

In the `Generate` method (line 33-42), add a call after `PlaceAirport`:

```csharp
public CityGrid Generate(SimulationState state, CityEntity city = null)
{
    var grid = new CityGrid(GridSize, GridSize);
    PlaceArterialRoads(grid);
    SubdivideSuperBlocks(grid);
    AssignStreetNames(grid, state, city);
    AssignPlotTypes(grid);
    ResolveFacingAndCreateAddresses(grid, state, city);
    PlaceAirport(grid, state, city);
    BusinessTemplateRegistry.RegisterAll();
    BusinessGenerator.CreateBusinessesForCity(state, _rng);
    return grid;
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test stakeout.tests/ --filter "FullyQualifiedName~BusinessGeneratorTests" -v minimal`
Expected: PASS

- [ ] **Step 6: Run full test suite**

Run: `dotnet test stakeout.tests/ -v minimal`
Expected: All pass

- [ ] **Step 7: Commit**

```
git add src/simulation/businesses/BusinessGenerator.cs src/simulation/city/CityGenerator.cs stakeout.tests/Simulation/Businesses/BusinessGeneratorTests.cs
git commit -m "feat: create business shells during city generation for all commercial addresses"
```

---

## Task 5: Remove Job Entity, Update Person & SleepScheduleCalculator

This task removes `Job`/`JobType` and updates all references. It will temporarily break PersonGenerator (fixed in Task 6).

**Files:**
- Delete: `src/simulation/entities/Job.cs`
- Modify: `src/simulation/entities/Person.cs:19`
- Modify: `src/simulation/SimulationState.cs:18`
- Modify: `src/simulation/scheduling/SleepScheduleCalculator.cs:12`
- Modify: `stakeout.tests/Simulation/Scheduling/SleepScheduleCalculatorTests.cs`

- [ ] **Step 1: Update SleepScheduleCalculator to use Position**

In `src/simulation/scheduling/SleepScheduleCalculator.cs`, change the `using` and method signature:

Replace:
```csharp
using Stakeout.Simulation.Entities;
```
With:
```csharp
using Stakeout.Simulation.Entities;
```

Replace the `Compute` method signature (line 12):
```csharp
public static (TimeSpan sleepTime, TimeSpan wakeTime) Compute(Job job, float commuteHours)
```
With:
```csharp
public static (TimeSpan sleepTime, TimeSpan wakeTime) Compute(Position position, float commuteHours)
```

And update the two references inside (lines 15-16):
```csharp
var workBlockStart = Mod24(position.ShiftStart - commute);
var workBlockEnd = Mod24(position.ShiftEnd + commute);
```

- [ ] **Step 2: Update SleepScheduleCalculator tests**

In the test file `stakeout.tests/Simulation/Scheduling/SleepScheduleCalculatorTests.cs`, replace all `Job` references with `Position`. Each test creates a `Job` — replace with `Position` using the same field values. For example, change patterns like:

```csharp
var job = new Job { ShiftStart = new TimeSpan(9, 0, 0), ShiftEnd = new TimeSpan(17, 0, 0) };
var (sleep, wake) = SleepScheduleCalculator.Compute(job, 0.5f);
```

To:

```csharp
var position = new Position { ShiftStart = new TimeSpan(9, 0, 0), ShiftEnd = new TimeSpan(17, 0, 0) };
var (sleep, wake) = SleepScheduleCalculator.Compute(position, 0.5f);
```

- [ ] **Step 3: Update Person entity**

In `src/simulation/entities/Person.cs`, replace line 19:

```csharp
public int JobId { get; set; }
```

With:

```csharp
public int? BusinessId { get; set; }
public int? PositionId { get; set; }
```

- [ ] **Step 4: Update SimulationState**

In `src/simulation/SimulationState.cs`, remove line 18:

```csharp
public Dictionary<int, Job> Jobs { get; } = new();
```

Also remove the `using Stakeout.Simulation.Entities;` if `Job` was the only entity it was using from that namespace (check — it likely uses `Person`, `Address`, etc. so the using stays).

- [ ] **Step 5: Delete Job.cs**

Delete `src/simulation/entities/Job.cs`.

- [ ] **Step 6: Fix remaining compile errors**

At this point, `PersonGenerator.cs` and `GameShell.cs` will have compile errors referencing `Job`/`JobType`. These will be fixed in Tasks 6 and 9. For now, comment out the broken code in PersonGenerator with `// TODO: Task 6 will rewrite this` to get a clean compile. Specifically, comment out or stub:
- `PickJobType()` method body
- `JobTypeToAddressType()` method body
- `CreateJob()` method body
- `GenerateDinerShiftStart()` method body
- Lines 30-31 in `GeneratePerson` that call these methods
- Line 61 that creates the job and line 62 that adds to `state.Jobs`
- Line 65 that passes `job` to `SleepScheduleCalculator`

In `GameShell.cs`, the Job section is already a TODO comment (line 480), but check if there are any other `Job`/`JobId` references in the file and update them.

- [ ] **Step 7: Run tests**

Run: `dotnet test stakeout.tests/ -v minimal`
Expected: SleepScheduleCalculator tests pass. Some PersonGenerator tests may fail (expected — Task 6 fixes them). The build should compile.

- [ ] **Step 8: Commit**

```
git add src/simulation/entities/Person.cs src/simulation/SimulationState.cs src/simulation/scheduling/SleepScheduleCalculator.cs src/simulation/PersonGenerator.cs scenes/game_shell/GameShell.cs stakeout.tests/Simulation/Scheduling/SleepScheduleCalculatorTests.cs
git rm src/simulation/entities/Job.cs
git commit -m "refactor: remove Job entity, update Person to use BusinessId/PositionId, SleepScheduleCalculator to use Position"
```

---

## Task 6: SpawnRequirements & PersonGenerator Rewrite

**Files:**
- Create: `src/simulation/SpawnRequirements.cs`
- Modify: `src/simulation/PersonGenerator.cs` (major rewrite)
- Modify: `stakeout.tests/Simulation/PersonGeneratorTests.cs`

- [ ] **Step 1: Create SpawnRequirements**

Create `src/simulation/SpawnRequirements.cs`:

```csharp
namespace Stakeout.Simulation;

public class SpawnRequirements
{
    public int? BusinessId { get; set; }
    public int? PositionId { get; set; }
    public int? HomeAddressId { get; set; }
    public int? HomeLocationId { get; set; }
}
```

- [ ] **Step 2: Rewrite PersonGenerator**

Rewrite `src/simulation/PersonGenerator.cs`. The key changes:

1. Remove: `PickJobType`, `JobTypeToAddressType`, `CreateJob`, `GenerateDinerShiftStart`
2. Add: `GeneratePerson(SimulationState state, SpawnRequirements requirements)` overload
3. Original `GeneratePerson(state)` calls the new overload with `new SpawnRequirements()`
4. New flow:
   - If requirements specify business/position: use those
   - Else: find a business in the same city with an open position, claim it
   - Set `person.BusinessId` and `person.PositionId`
   - Compute sleep from Position shift times
   - Create WorkShiftObjective (Task 7 — for now, leave the TODO marker but don't block on it)

Here is the new `GeneratePerson` method body (the helper methods for home selection, keys, etc. stay the same):

```csharp
public Person GeneratePerson(SimulationState state)
{
    return GeneratePerson(state, new SpawnRequirements());
}

public Person GeneratePerson(SimulationState state, SpawnRequirements requirements)
{
    int cityId = 0;
    foreach (var key in state.Cities.Keys) { cityId = key; break; }

    // 1. Determine business and position
    Business business;
    Position position;

    if (requirements.BusinessId.HasValue && requirements.PositionId.HasValue)
    {
        business = state.Businesses[requirements.BusinessId.Value];
        position = business.Positions.First(p => p.Id == requirements.PositionId.Value);
    }
    else
    {
        (business, position) = FindOpenPosition(state, cityId);
    }

    // Ensure work address interior is resolved
    var workAddress = state.Addresses[business.AddressId];
    if (workAddress.LocationIds.Count == 0)
        LocationGenerator.ResolveAddressInterior(workAddress, state, _random);

    // 2. Claim home address
    Address homeAddress;
    int? homeLocationId;

    if (requirements.HomeAddressId.HasValue)
    {
        homeAddress = state.Addresses[requirements.HomeAddressId.Value];
        homeLocationId = requirements.HomeLocationId;
    }
    else
    {
        var homeType = _random.NextDouble() < 0.5 ? AddressType.SuburbanHome : AddressType.ApartmentBuilding;
        homeAddress = homeType == AddressType.ApartmentBuilding
            ? FindApartmentBuilding(state)
            : PickAndResolveAddress(state, homeType);

        homeLocationId = null;
        if (homeType == AddressType.ApartmentBuilding)
        {
            homeLocationId = AssignVacantUnit(state, homeAddress);
        }
        else
        {
            foreach (var locId in homeAddress.LocationIds)
            {
                var loc = state.Locations[locId];
                if (loc.HasTag("residential"))
                {
                    homeLocationId = loc.Id;
                    break;
                }
            }
        }
    }

    // 3. Compute sleep schedule from position
    var commuteHours = _mapConfig.ComputeTravelTimeHours(homeAddress.Position, workAddress.Position);
    var (sleepTime, wakeTime) = SleepScheduleCalculator.Compute(position, commuteHours);

    // 4. Create person
    var person = new Person
    {
        Id = state.GenerateEntityId(),
        FirstName = NameData.FirstNames[_random.Next(NameData.FirstNames.Length)],
        LastName = NameData.LastNames[_random.Next(NameData.LastNames.Length)],
        CreatedAt = state.Clock.CurrentTime,
        CurrentCityId = cityId,
        HomeAddressId = homeAddress.Id,
        HomeLocationId = homeLocationId,
        BusinessId = business.Id,
        PositionId = position.Id,
        CurrentAddressId = homeAddress.Id,
        CurrentPosition = homeAddress.Position,
        PreferredSleepTime = sleepTime,
        PreferredWakeTime = wakeTime,
    };

    // Claim position
    position.AssignedPersonId = person.Id;

    // Assign traits
    var allTraits = TraitDefinitions.GetAllTraitNames();
    var traitCount = _random.Next(0, 3);
    var shuffled = allTraits.OrderBy(_ => _random.Next()).Take(traitCount);
    foreach (var trait in shuffled)
        person.Traits.Add(trait);

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

    // TODO: Task 7 will add WorkShiftObjective here

    state.People[person.Id] = person;
    CreateHomeKey(state, person, homeAddress);

    state.Journal.Append(new SimulationEvent
    {
        Timestamp = state.Clock.CurrentTime,
        PersonId = person.Id,
        EventType = SimulationEventType.ActivityStarted,
        Description = "Spawned"
    });

    return person;
}

private (Business business, Position position) FindOpenPosition(SimulationState state, int cityId)
{
    // Find a business in this city with an open position
    foreach (var biz in state.Businesses.Values)
    {
        var addr = state.Addresses[biz.AddressId];
        if (addr.CityId != cityId) continue;

        foreach (var pos in biz.Positions)
        {
            if (pos.AssignedPersonId == null)
                return (biz, pos);
        }
    }

    throw new InvalidOperationException("No open positions available in any business in this city");
}
```

Remove these methods entirely: `PickJobType`, `JobTypeToAddressType`, `CreateJob`, `GenerateDinerShiftStart`.

- [ ] **Step 3: Update PersonGenerator tests**

In `stakeout.tests/Simulation/PersonGeneratorTests.cs`:

- The `CreateState()` helper needs to also call `BusinessTemplateRegistry.RegisterAll()` (it already calls `AddressTemplateRegistry.RegisterAll()`). Businesses are now created during `CityGenerator.Generate`.
- Update the test `GeneratePerson_CreatesJobWithMatchingAddress` — rename to `GeneratePerson_HasBusinessAndPosition`:

```csharp
[Fact]
public void GeneratePerson_HasBusinessAndPosition()
{
    var state = CreateState();
    var person = CreateGenerator().GeneratePerson(state);
    Assert.NotNull(person.BusinessId);
    Assert.NotNull(person.PositionId);
    var biz = state.Businesses[person.BusinessId.Value];
    Assert.Equal(AddressCategory.Commercial, state.Addresses[biz.AddressId].Category);
    var pos = biz.Positions.First(p => p.Id == person.PositionId.Value);
    Assert.Equal(person.Id, pos.AssignedPersonId);
}
```

- Add test for constrained generation:

```csharp
[Fact]
public void GeneratePerson_WithRequirements_UsesSpecifiedPosition()
{
    var state = CreateState();
    // Pick a specific business and position
    var biz = state.Businesses.Values.First();
    var pos = biz.Positions.First();

    var person = CreateGenerator().GeneratePerson(state, new SpawnRequirements
    {
        BusinessId = biz.Id,
        PositionId = pos.Id
    });

    Assert.Equal(biz.Id, person.BusinessId);
    Assert.Equal(pos.Id, person.PositionId);
    Assert.Equal(person.Id, pos.AssignedPersonId);
}
```

- Add test for same-city constraint:

```csharp
[Fact]
public void GeneratePerson_WorksInSameCity()
{
    var state = CreateState();
    var person = CreateGenerator().GeneratePerson(state);
    var biz = state.Businesses[person.BusinessId.Value];
    var workAddr = state.Addresses[biz.AddressId];
    Assert.Equal(person.CurrentCityId, workAddr.CityId);
}
```

- Remove or update any tests that reference `JobId`, `Job`, `state.Jobs`, or `JobType`.

- [ ] **Step 4: Run tests**

Run: `dotnet test stakeout.tests/ -v minimal`
Expected: All pass. PersonGenerator tests updated, SleepScheduleCalculator tests already pass from Task 5.

- [ ] **Step 5: Commit**

```
git add src/simulation/SpawnRequirements.cs src/simulation/PersonGenerator.cs stakeout.tests/Simulation/PersonGeneratorTests.cs
git commit -m "feat: rewrite PersonGenerator to assign NPCs to business positions with SpawnRequirements"
```

---

## Task 7: WorkShiftObjective

**Files:**
- Create: `src/simulation/objectives/WorkShiftObjective.cs`
- Create: `stakeout.tests/Simulation/Objectives/WorkShiftObjectiveTests.cs`
- Modify: `src/simulation/PersonGenerator.cs` (wire up objective)

- [ ] **Step 1: Write tests**

Create `stakeout.tests/Simulation/Objectives/WorkShiftObjectiveTests.cs`:

```csharp
using System;
using System.Linq;
using Stakeout.Simulation;
using Stakeout.Simulation.Addresses;
using Stakeout.Simulation.Businesses;
using Stakeout.Simulation.City;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Objectives;
using Xunit;
using CityEntity = Stakeout.Simulation.Entities.City;

namespace Stakeout.Tests.Simulation.Objectives;

public class WorkShiftObjectiveTests
{
    private static (SimulationState state, Business biz, Person person) Setup()
    {
        AddressTemplateRegistry.RegisterAll();
        BusinessTemplateRegistry.RegisterAll();
        var state = new SimulationState();
        var city = new CityEntity { Id = state.GenerateEntityId(), Name = "Boston", CountryName = "US" };
        state.Cities[city.Id] = city;
        var cityGen = new CityGenerator(seed: 42);
        state.CityGrids[city.Id] = cityGen.Generate(state, city);

        var gen = new PersonGenerator(new MapConfig());
        var person = gen.GeneratePerson(state);
        var biz = state.Businesses[person.BusinessId.Value];

        return (state, biz, person);
    }

    [Fact]
    public void Priority_Is60()
    {
        var obj = new WorkShiftObjective(1, 1);
        Assert.Equal(60, obj.Priority);
    }

    [Fact]
    public void Source_IsJob()
    {
        var obj = new WorkShiftObjective(1, 1);
        Assert.Equal(ObjectiveSource.Job, obj.Source);
    }

    [Fact]
    public void GetActions_ReturnsActions_OnWorkDays()
    {
        var (state, biz, person) = Setup();
        var pos = biz.Positions.First(p => p.Id == person.PositionId);
        var obj = new WorkShiftObjective(biz.Id, pos.Id) { Id = state.GenerateEntityId() };

        // Plan for a single day that is a work day
        var workDay = pos.WorkDays[0];
        // Find a date that falls on that day of week
        var planStart = new DateTime(2026, 3, 30); // Monday
        while (planStart.DayOfWeek != workDay)
            planStart = planStart.AddDays(1);

        var actions = obj.GetActions(person, state, planStart, planStart.AddHours(24));

        Assert.NotEmpty(actions);
        Assert.All(actions, a => Assert.Equal(biz.AddressId, a.TargetAddressId));
    }

    [Fact]
    public void GetActions_ReturnsEmpty_OnDayOff()
    {
        var (state, biz, person) = Setup();
        var pos = biz.Positions.First(p => p.Id == person.PositionId);
        var obj = new WorkShiftObjective(biz.Id, pos.Id) { Id = state.GenerateEntityId() };

        // Find a day NOT in work days
        var allDays = Enum.GetValues<DayOfWeek>();
        DayOfWeek dayOff = DayOfWeek.Sunday;
        foreach (var d in allDays)
        {
            if (!pos.WorkDays.Contains(d)) { dayOff = d; break; }
        }

        var planStart = new DateTime(2026, 3, 30);
        while (planStart.DayOfWeek != dayOff)
            planStart = planStart.AddDays(1);

        var actions = obj.GetActions(person, state, planStart, planStart.AddHours(24));

        Assert.Empty(actions);
    }

    [Fact]
    public void GetActions_ShiftTimes_MatchPosition()
    {
        var (state, biz, person) = Setup();
        var pos = biz.Positions.First(p => p.Id == person.PositionId);
        var obj = new WorkShiftObjective(biz.Id, pos.Id) { Id = state.GenerateEntityId() };

        var workDay = pos.WorkDays[0];
        var planStart = new DateTime(2026, 3, 30);
        while (planStart.DayOfWeek != workDay)
            planStart = planStart.AddDays(1);

        var actions = obj.GetActions(person, state, planStart, planStart.AddHours(24));
        if (actions.Count == 0) return; // skip if no actions generated

        var action = actions[0];
        Assert.Equal(planStart.Date + pos.ShiftStart, action.TimeWindowStart);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test stakeout.tests/ --filter "FullyQualifiedName~WorkShiftObjectiveTests" -v minimal`
Expected: FAIL

- [ ] **Step 3: Create WorkShiftObjective**

Create `src/simulation/objectives/WorkShiftObjective.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Stakeout.Simulation.Actions.Primitives;
using Stakeout.Simulation.Entities;

namespace Stakeout.Simulation.Objectives;

public class WorkShiftObjective : Objective
{
    private readonly int _businessId;
    private readonly int _positionId;

    public override int Priority => 60;
    public override ObjectiveSource Source => ObjectiveSource.Job;

    public WorkShiftObjective(int businessId, int positionId)
    {
        _businessId = businessId;
        _positionId = positionId;
    }

    public override List<PlannedAction> GetActions(
        Person person, SimulationState state,
        DateTime planStart, DateTime planEnd)
    {
        var business = state.Businesses[_businessId];
        var position = business.Positions.First(p => p.Id == _positionId);

        var actions = new List<PlannedAction>();

        // Walk each day in the plan window
        var day = planStart.Date;
        while (day < planEnd)
        {
            if (position.WorkDays.Contains(day.DayOfWeek))
            {
                var shiftStart = day + position.ShiftStart;
                var shiftEnd = day + position.ShiftEnd;

                // Handle overnight shifts (end < start means crosses midnight)
                if (position.ShiftEnd <= position.ShiftStart)
                    shiftEnd = shiftEnd.AddDays(1);

                // Only include if shift overlaps the plan window
                if (shiftEnd > planStart && shiftStart < planEnd)
                {
                    var duration = shiftEnd - shiftStart;
                    var displayText = $"working as {position.Role}";

                    actions.Add(new PlannedAction
                    {
                        Action = new WaitAction(duration, displayText),
                        TargetAddressId = business.AddressId,
                        TimeWindowStart = shiftStart,
                        TimeWindowEnd = shiftEnd,
                        Duration = duration,
                        DisplayText = displayText,
                        SourceObjective = this
                    });
                }
            }

            day = day.AddDays(1);
        }

        return actions;
    }
}
```

- [ ] **Step 4: Wire WorkShiftObjective into PersonGenerator**

In `src/simulation/PersonGenerator.cs`, replace the `// TODO: Task 7 will add WorkShiftObjective here` line with:

```csharp
person.Objectives.Add(new WorkShiftObjective(business.Id, position.Id)
{
    Id = state.GenerateEntityId()
});
```

Add using at top: `using Stakeout.Simulation.Objectives;` (if not already there).

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test stakeout.tests/ --filter "FullyQualifiedName~WorkShiftObjectiveTests" -v minimal`
Expected: PASS

- [ ] **Step 6: Run full test suite**

Run: `dotnet test stakeout.tests/ -v minimal`
Expected: All pass

- [ ] **Step 7: Commit**

```
git add src/simulation/objectives/WorkShiftObjective.cs src/simulation/PersonGenerator.cs stakeout.tests/Simulation/Objectives/WorkShiftObjectiveTests.cs
git commit -m "feat: add WorkShiftObjective (priority 60) and wire into PersonGenerator"
```

---

## Task 8: BusinessResolver

**Files:**
- Create: `src/simulation/BusinessResolver.cs`
- Create: `stakeout.tests/Simulation/Businesses/BusinessResolverTests.cs`

- [ ] **Step 1: Write tests**

Create `stakeout.tests/Simulation/Businesses/BusinessResolverTests.cs`:

```csharp
using System.Linq;
using Stakeout.Simulation;
using Stakeout.Simulation.Addresses;
using Stakeout.Simulation.Businesses;
using Stakeout.Simulation.City;
using Stakeout.Simulation.Entities;
using Xunit;
using CityEntity = Stakeout.Simulation.Entities.City;

namespace Stakeout.Tests.Simulation.Businesses;

public class BusinessResolverTests
{
    private static (SimulationState state, PersonGenerator gen) Setup()
    {
        AddressTemplateRegistry.RegisterAll();
        BusinessTemplateRegistry.RegisterAll();
        var state = new SimulationState();
        var city = new CityEntity { Id = state.GenerateEntityId(), Name = "Boston", CountryName = "US" };
        state.Cities[city.Id] = city;
        var cityGen = new CityGenerator(seed: 42);
        state.CityGrids[city.Id] = cityGen.Generate(state, city);
        var gen = new PersonGenerator(new MapConfig());
        return (state, gen);
    }

    [Fact]
    public void Resolve_FillsAllPositions()
    {
        var (state, gen) = Setup();
        var biz = state.Businesses.Values.First();
        var emptyCount = biz.Positions.Count(p => p.AssignedPersonId == null);

        var spawned = BusinessResolver.Resolve(state, biz, gen);

        Assert.Equal(emptyCount, spawned.Count);
        Assert.All(biz.Positions, p => Assert.NotNull(p.AssignedPersonId));
    }

    [Fact]
    public void Resolve_SetsIsResolved()
    {
        var (state, gen) = Setup();
        var biz = state.Businesses.Values.First();

        BusinessResolver.Resolve(state, biz, gen);

        Assert.True(biz.IsResolved);
    }

    [Fact]
    public void Resolve_SkipsAlreadyAssignedPositions()
    {
        var (state, gen) = Setup();
        var biz = state.Businesses.Values.First();

        // Pre-assign one position
        var person = gen.GeneratePerson(state, new SpawnRequirements
        {
            BusinessId = biz.Id,
            PositionId = biz.Positions[0].Id
        });

        var remainingEmpty = biz.Positions.Count(p => p.AssignedPersonId == null);
        var spawned = BusinessResolver.Resolve(state, biz, gen);

        Assert.Equal(remainingEmpty, spawned.Count);
    }

    [Fact]
    public void Resolve_AlreadyResolved_ReturnsEmpty()
    {
        var (state, gen) = Setup();
        var biz = state.Businesses.Values.First();

        BusinessResolver.Resolve(state, biz, gen);
        var second = BusinessResolver.Resolve(state, biz, gen);

        Assert.Empty(second);
    }

    [Fact]
    public void Resolve_SpawnedPeople_HaveWorkShiftObjective()
    {
        var (state, gen) = Setup();
        var biz = state.Businesses.Values.First();

        var spawned = BusinessResolver.Resolve(state, biz, gen);

        Assert.All(spawned, p =>
            Assert.Contains(p.Objectives, o => o is Stakeout.Simulation.Objectives.WorkShiftObjective));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test stakeout.tests/ --filter "FullyQualifiedName~BusinessResolverTests" -v minimal`
Expected: FAIL

- [ ] **Step 3: Create BusinessResolver**

Create `src/simulation/BusinessResolver.cs`:

```csharp
using System.Collections.Generic;
using Stakeout.Simulation.Entities;

namespace Stakeout.Simulation;

public static class BusinessResolver
{
    public static List<Person> Resolve(SimulationState state, Business business, PersonGenerator generator)
    {
        if (business.IsResolved) return new();

        // Ensure address interior is resolved
        var address = state.Addresses[business.AddressId];
        LocationGenerator.ResolveAddressInterior(address, state);

        var spawned = new List<Person>();
        foreach (var position in business.Positions)
        {
            if (position.AssignedPersonId != null) continue;

            var person = generator.GeneratePerson(state, new SpawnRequirements
            {
                BusinessId = business.Id,
                PositionId = position.Id
            });

            spawned.Add(person);
        }

        business.IsResolved = true;
        return spawned;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test stakeout.tests/ --filter "FullyQualifiedName~BusinessResolverTests" -v minimal`
Expected: PASS

- [ ] **Step 5: Run full test suite**

Run: `dotnet test stakeout.tests/ -v minimal`
Expected: All pass

- [ ] **Step 6: Commit**

```
git add src/simulation/BusinessResolver.cs stakeout.tests/Simulation/Businesses/BusinessResolverTests.cs
git commit -m "feat: add BusinessResolver to fill all empty positions at a business on demand"
```

---

## Task 9: DoorLockingService

**Files:**
- Modify: `src/simulation/scheduling/DoorLockingService.cs`
- Modify: `src/simulation/SimulationManager.cs:99-127`
- Create: `stakeout.tests/Simulation/Scheduling/DoorLockingServiceTests.cs`

- [ ] **Step 1: Write tests**

Create `stakeout.tests/Simulation/Scheduling/DoorLockingServiceTests.cs`:

```csharp
using System;
using System.Linq;
using Stakeout.Simulation;
using Stakeout.Simulation.Addresses;
using Stakeout.Simulation.Businesses;
using Stakeout.Simulation.City;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Scheduling;
using Xunit;
using CityEntity = Stakeout.Simulation.Entities.City;

namespace Stakeout.Tests.Simulation.Scheduling;

public class DoorLockingServiceTests
{
    private static (SimulationState state, Business biz) SetupResolved()
    {
        AddressTemplateRegistry.RegisterAll();
        BusinessTemplateRegistry.RegisterAll();
        var state = new SimulationState();
        var city = new CityEntity { Id = state.GenerateEntityId(), Name = "Boston", CountryName = "US" };
        state.Cities[city.Id] = city;
        var cityGen = new CityGenerator(seed: 42);
        state.CityGrids[city.Id] = cityGen.Generate(state, city);

        // Resolve a business so it participates in door locking
        var gen = new PersonGenerator(new MapConfig());
        var biz = state.Businesses.Values.First();
        BusinessResolver.Resolve(state, biz, gen);

        return (state, biz);
    }

    [Fact]
    public void UpdateDoorStates_DuringBusinessHours_UnlocksEntrance()
    {
        var (state, biz) = SetupResolved();
        // Find a time during business hours
        var hours = biz.Hours.First(h => h.OpenTime.HasValue);
        var duringHours = new DateTime(2026, 3, 30).Date + hours.OpenTime.Value + TimeSpan.FromHours(1);
        // Adjust to correct day of week
        while (duringHours.DayOfWeek != hours.Day)
            duringHours = duringHours.AddDays(1);

        DoorLockingService.UpdateDoorStates(state, duringHours);

        // Check that main entrance is unlocked
        var locations = state.GetLocationsForAddress(biz.AddressId);
        var entrance = locations.SelectMany(l => l.AccessPoints)
            .FirstOrDefault(ap => ap.HasTag("main_entrance"));
        if (entrance != null)
            Assert.False(entrance.IsLocked);
    }

    [Fact]
    public void UpdateDoorStates_OutsideBusinessHours_LocksEntrance()
    {
        var (state, biz) = SetupResolved();
        // Use office (closed weekends)
        var officeBiz = state.Businesses.Values.FirstOrDefault(b => b.Type == BusinessType.Office);
        if (officeBiz == null) return;
        if (!officeBiz.IsResolved)
        {
            var gen = new PersonGenerator(new MapConfig());
            BusinessResolver.Resolve(state, officeBiz, gen);
        }

        // Saturday = closed for offices
        var saturday = new DateTime(2026, 3, 28, 12, 0, 0); // Saturday noon
        while (saturday.DayOfWeek != DayOfWeek.Saturday)
            saturday = saturday.AddDays(1);

        DoorLockingService.UpdateDoorStates(state, saturday);

        var locations = state.GetLocationsForAddress(officeBiz.AddressId);
        var entrance = locations.SelectMany(l => l.AccessPoints)
            .FirstOrDefault(ap => ap.HasTag("main_entrance"));
        if (entrance != null)
            Assert.True(entrance.IsLocked);
    }

    [Fact]
    public void UpdateDoorStates_SkipsUnresolvedBusinesses()
    {
        AddressTemplateRegistry.RegisterAll();
        BusinessTemplateRegistry.RegisterAll();
        var state = new SimulationState();
        var city = new CityEntity { Id = state.GenerateEntityId(), Name = "Boston", CountryName = "US" };
        state.Cities[city.Id] = city;
        var cityGen = new CityGenerator(seed: 42);
        state.CityGrids[city.Id] = cityGen.Generate(state, city);

        // Don't resolve any businesses — should not throw
        var now = new DateTime(2026, 3, 30, 12, 0, 0);
        DoorLockingService.UpdateDoorStates(state, now);
        // If we got here without exception, test passes
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test stakeout.tests/ --filter "FullyQualifiedName~DoorLockingServiceTests" -v minimal`
Expected: FAIL (NotImplementedException from stub)

- [ ] **Step 3: Implement DoorLockingService**

Replace `src/simulation/scheduling/DoorLockingService.cs`:

```csharp
using System;
using System.Linq;
using Stakeout.Simulation.Entities;

namespace Stakeout.Simulation.Scheduling;

public static class DoorLockingService
{
    public static void UpdateDoorStates(SimulationState state, DateTime currentTime)
    {
        foreach (var business in state.Businesses.Values)
        {
            if (!business.IsResolved) continue;

            var hours = business.Hours.FirstOrDefault(h => h.Day == currentTime.DayOfWeek);
            var isOpen = IsWithinOperatingHours(hours, currentTime.TimeOfDay);

            SetEntranceLockState(state, business.AddressId, locked: !isOpen);
        }
    }

    private static bool IsWithinOperatingHours(BusinessHours hours, TimeSpan timeOfDay)
    {
        if (hours == null || !hours.OpenTime.HasValue || !hours.CloseTime.HasValue)
            return false;

        var open = hours.OpenTime.Value;
        var close = hours.CloseTime.Value;

        if (close > open)
        {
            // Same day: e.g., 7:00 - 19:00
            return timeOfDay >= open && timeOfDay < close;
        }
        else if (close < open || (close == open && open == TimeSpan.Zero))
        {
            // Crosses midnight: e.g., 12:00 - 01:00 or 24/7 (00:00 - 00:00)
            if (close == open) return true; // 24/7
            return timeOfDay >= open || timeOfDay < close;
        }

        return false;
    }

    private static void SetEntranceLockState(SimulationState state, int addressId, bool locked)
    {
        if (!state.Addresses.TryGetValue(addressId, out var address)) return;

        foreach (var locId in address.LocationIds)
        {
            if (!state.Locations.TryGetValue(locId, out var location)) continue;
            foreach (var ap in location.AccessPoints)
            {
                if (ap.HasTag("main_entrance"))
                    ap.IsLocked = locked;
            }
        }
    }
}
```

- [ ] **Step 4: Hook DoorLockingService into SimulationManager**

In `src/simulation/SimulationManager.cs`, add using:

```csharp
using Stakeout.Simulation.Scheduling;
```

In the `_Process` method, add after the people loop (after line 124, before `UpdatePlayerTravel`):

```csharp
DoorLockingService.UpdateDoorStates(State, State.Clock.CurrentTime);
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test stakeout.tests/ --filter "FullyQualifiedName~DoorLockingServiceTests" -v minimal`
Expected: PASS

- [ ] **Step 6: Run full test suite**

Run: `dotnet test stakeout.tests/ -v minimal`
Expected: All pass

- [ ] **Step 7: Commit**

```
git add src/simulation/scheduling/DoorLockingService.cs src/simulation/SimulationManager.cs stakeout.tests/Simulation/Scheduling/DoorLockingServiceTests.cs
git commit -m "feat: implement schedule-driven DoorLockingService, hook into SimulationManager"
```

---

## Task 10: Debug Inspector & GameShell Updates

**Files:**
- Modify: `scenes/game_shell/GameShell.cs:480`

- [ ] **Step 1: Restore job section in debug inspector**

In `scenes/game_shell/GameShell.cs`, replace line 480:

```csharp
// TODO: Project 4 — Job section will be restored with Business entities
```

With:

```csharp
// Job / Business info
var jobLines = new List<string>();
if (person.BusinessId.HasValue && state.Businesses.TryGetValue(person.BusinessId.Value, out var personBiz))
{
    jobLines.Add($"Business: {personBiz.Name} ({personBiz.Type})");
    if (person.PositionId.HasValue)
    {
        var personPos = personBiz.Positions.FirstOrDefault(p => p.Id == person.PositionId.Value);
        if (personPos != null)
        {
            jobLines.Add($"Role: {personPos.Role}");
            var shiftEnd = personPos.ShiftEnd <= personPos.ShiftStart
                ? $"{personPos.ShiftEnd:hh\\:mm} (+1d)"
                : $"{personPos.ShiftEnd:hh\\:mm}";
            jobLines.Add($"Shift: {personPos.ShiftStart:hh\\:mm} - {shiftEnd}");
            var days = string.Join(", ", personPos.WorkDays.Select(d => d.ToString()[..3]));
            jobLines.Add($"Days: {days}");

            // Current work status
            var now = state.Clock.CurrentTime;
            var isWorkDay = personPos.WorkDays.Contains(now.DayOfWeek);
            if (!isWorkDay)
                jobLines.Add("Status: day off");
            else
            {
                var onShift = IsOnShift(personPos, now.TimeOfDay);
                jobLines.Add(onShift ? "Status: on shift" : "Status: off duty");
            }
        }
    }
}
else
{
    jobLines.Add("(unassigned)");
}
AddInspectorSection(vbox, font, "— Job —", jobLines.ToArray());
```

Also add a helper method somewhere accessible in the class:

```csharp
private static bool IsOnShift(Position pos, TimeSpan timeOfDay)
{
    if (pos.ShiftEnd > pos.ShiftStart)
        return timeOfDay >= pos.ShiftStart && timeOfDay < pos.ShiftEnd;
    // Crosses midnight
    return timeOfDay >= pos.ShiftStart || timeOfDay < pos.ShiftEnd;
}
```

Add using at top of GameShell.cs: `using Stakeout.Simulation.Entities;` (if not already present — check for `Position` type).

Also check for any remaining `JobId` references in GameShell.cs and update them.

- [ ] **Step 2: Build to verify no compile errors**

Run: `dotnet build stakeout.sln`
Expected: Build succeeds

- [ ] **Step 3: Commit**

```
git add scenes/game_shell/GameShell.cs
git commit -m "feat: restore job/business section in debug inspector"
```

---

## Task 11: Integration Test

**Files:**
- Create: `stakeout.tests/Simulation/Businesses/BusinessIntegrationTests.cs`

- [ ] **Step 1: Write integration test**

Create `stakeout.tests/Simulation/Businesses/BusinessIntegrationTests.cs`:

```csharp
using System;
using System.Linq;
using Stakeout.Simulation;
using Stakeout.Simulation.Actions;
using Stakeout.Simulation.Addresses;
using Stakeout.Simulation.Brain;
using Stakeout.Simulation.Businesses;
using Stakeout.Simulation.City;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Objectives;
using Xunit;
using CityEntity = Stakeout.Simulation.Entities.City;

namespace Stakeout.Tests.Simulation.Businesses;

public class BusinessIntegrationTests
{
    private static SimulationState CreateState()
    {
        AddressTemplateRegistry.RegisterAll();
        BusinessTemplateRegistry.RegisterAll();
        var state = new SimulationState();
        var city = new CityEntity { Id = state.GenerateEntityId(), Name = "Boston", CountryName = "US" };
        state.Cities[city.Id] = city;
        var cityGen = new CityGenerator(seed: 42);
        state.CityGrids[city.Id] = cityGen.Generate(state, city);
        return state;
    }

    [Fact]
    public void FullDaySimulation_NpcGoesToWork()
    {
        var state = CreateState();
        var gen = new PersonGenerator(new MapConfig());
        var person = gen.GeneratePerson(state);

        // Verify person has work shift objective
        Assert.Contains(person.Objectives, o => o is WorkShiftObjective);

        // Plan a work day
        var biz = state.Businesses[person.BusinessId.Value];
        var pos = biz.Positions.First(p => p.Id == person.PositionId);
        var workDay = pos.WorkDays[0];

        var planDate = new DateTime(2026, 3, 30);
        while (planDate.DayOfWeek != workDay)
            planDate = planDate.AddDays(1);

        // Start at wake time
        var startTime = planDate + person.PreferredWakeTime;
        var plan = NpcBrain.PlanDay(person, state, startTime);
        person.DayPlan = plan;

        // Plan should include a work entry
        var workEntry = plan.Entries.FirstOrDefault(e =>
            e.PlannedAction.DisplayText.Contains("working as"));
        Assert.NotNull(workEntry);

        // Work entry should target the business address
        Assert.Equal(biz.AddressId, workEntry.PlannedAction.TargetAddressId);
    }

    [Fact]
    public void FullDaySimulation_WithActionRunner_CompletesActivities()
    {
        var state = CreateState();
        var gen = new PersonGenerator(new MapConfig());
        var person = gen.GeneratePerson(state);

        var biz = state.Businesses[person.BusinessId.Value];
        var pos = biz.Positions.First(p => p.Id == person.PositionId);
        var workDay = pos.WorkDays[0];

        var planDate = new DateTime(2026, 3, 30);
        while (planDate.DayOfWeek != workDay)
            planDate = planDate.AddDays(1);

        var startTime = planDate + person.PreferredWakeTime;
        state.Clock.SetTime(startTime);
        person.DayPlan = NpcBrain.PlanDay(person, state, startTime);

        var runner = new ActionRunner();
        // Simulate 18 hours in 1-minute ticks
        for (int i = 0; i < 18 * 60; i++)
        {
            state.Clock.Tick(60.0); // 60 seconds
            runner.Tick(person, state, TimeSpan.FromMinutes(1));
        }

        // Should have completed at least one activity
        var events = state.Journal.GetEventsForPerson(person.Id);
        Assert.Contains(events, e =>
            e.EventType == Stakeout.Simulation.Events.SimulationEventType.ActivityCompleted);
    }

    [Fact]
    public void BusinessResolver_SpawnedWorkers_AllHaveSchedules()
    {
        var state = CreateState();
        var gen = new PersonGenerator(new MapConfig());
        var biz = state.Businesses.Values.First();

        var spawned = BusinessResolver.Resolve(state, biz, gen);

        foreach (var person in spawned)
        {
            Assert.Contains(person.Objectives, o => o is WorkShiftObjective);
            Assert.Contains(person.Objectives, o => o is SleepObjective);
            Assert.NotNull(person.BusinessId);
            Assert.NotNull(person.PositionId);
        }
    }
}
```

- [ ] **Step 2: Run integration tests**

Run: `dotnet test stakeout.tests/ --filter "FullyQualifiedName~BusinessIntegrationTests" -v minimal`
Expected: PASS

- [ ] **Step 3: Run full test suite**

Run: `dotnet test stakeout.tests/ -v minimal`
Expected: All pass

- [ ] **Step 4: Commit**

```
git add stakeout.tests/Simulation/Businesses/BusinessIntegrationTests.cs
git commit -m "test: add business integration tests — full day simulation with work shifts"
```

---

## Task 12: Cleanup & Final Verification

- [ ] **Step 1: Search for any remaining Job/JobType/JobId references**

Search the codebase for `JobId`, `JobType`, `Job `, `state.Jobs` to find any remaining references that need cleanup. Grep for these patterns.

- [ ] **Step 2: Fix any remaining references**

Update or remove any stale references found.

- [ ] **Step 3: Run full test suite**

Run: `dotnet test stakeout.tests/ -v minimal`
Expected: All pass

- [ ] **Step 4: Build the full solution**

Run: `dotnet build stakeout.sln`
Expected: Build succeeds with no errors

- [ ] **Step 5: Commit any cleanup**

```
git add -A
git commit -m "chore: clean up remaining Job references from P4 migration"
```

- [ ] **Step 6: Verify all P4 TODO markers are resolved**

Grep for `TODO: Project 4` and `TODO: P4` — all should be resolved.

- [ ] **Step 7: Final commit if needed**

If any TODO markers remain, resolve them and commit.
