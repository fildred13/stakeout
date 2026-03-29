# Fixtures & Trace System Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Introduce fixtures (stable interactable objects) and redesign the trace system (dynamic evidence) with emission and query APIs.

**Architecture:** Fixtures are POCO entities stored in `SimulationState.Fixtures`, generated from address templates same as Locations. Traces are redesigned with explicit nullable fields replacing the `Data` dictionary, plus decay support. `TraceEmitter` provides static emission methods. `InvestigationQuery` merges fixtures and traces at query time. `FingerprintService` is deleted.

**Tech Stack:** C# / .NET 8, xUnit, Godot 4.6

**Spec:** `docs/superpowers/specs/2026-03-29-fixtures-and-traces-design.md`

**Pre-existing test failure:** `CrimeIntegrationTests.FullCrimePipeline_SerialKiller_VictimDiesAndTracesProduced` fails with `NotImplementedException` in `ObjectiveResolver` — this is a P3 stub, not related to this work. Ignore it.

**CRITICAL: Never prefix shell commands with `cd`. The working directory is already the project root. Run commands directly (e.g., `dotnet test stakeout.tests/`, not `cd path && dotnet test`). This breaks permission matching and is strictly prohibited.**

---

### Task 1: Fixture Entity and FixtureType Enum

**Files:**
- Create: `src/simulation/fixtures/FixtureType.cs`
- Create: `src/simulation/fixtures/Fixture.cs`
- Test: `stakeout.tests/Simulation/Fixtures/FixtureTests.cs`

- [ ] **Step 1: Create the FixtureType enum**

```csharp
// src/simulation/fixtures/FixtureType.cs
namespace Stakeout.Simulation.Fixtures;

public enum FixtureType
{
    TrashCan,
    // Future: Computer, Mailbox, Safe, FilingCabinet, AnsweringMachine,
    //         Dresser, Desk, Shelf, Vehicle
}
```

- [ ] **Step 2: Create the Fixture entity**

```csharp
// src/simulation/fixtures/Fixture.cs
using System;

namespace Stakeout.Simulation.Fixtures;

public class Fixture
{
    public int Id { get; set; }
    public int? LocationId { get; set; }
    public int? SubLocationId { get; set; }
    public string Name { get; set; }
    public FixtureType Type { get; set; }
    public string[] Tags { get; set; } = Array.Empty<string>();

    public bool HasTag(string tag) => Array.IndexOf(Tags, tag) >= 0;
}
```

- [ ] **Step 3: Write tests for Fixture**

```csharp
// stakeout.tests/Simulation/Fixtures/FixtureTests.cs
using System;
using Stakeout.Simulation.Fixtures;
using Xunit;

namespace Stakeout.Tests.Simulation.Fixtures;

public class FixtureTests
{
    [Fact]
    public void Fixture_DefaultTags_Empty()
    {
        var fixture = new Fixture { Id = 1, Name = "Trash Can", Type = FixtureType.TrashCan };
        Assert.Empty(fixture.Tags);
    }

    [Fact]
    public void Fixture_HasTag_ReturnsTrueWhenPresent()
    {
        var fixture = new Fixture
        {
            Id = 1, Name = "Trash Can", Type = FixtureType.TrashCan,
            Tags = new[] { "kitchen", "searchable" }
        };
        Assert.True(fixture.HasTag("kitchen"));
        Assert.False(fixture.HasTag("bedroom"));
    }

    [Fact]
    public void Fixture_LocationId_Set()
    {
        var fixture = new Fixture { Id = 1, LocationId = 10, Name = "Trash Can", Type = FixtureType.TrashCan };
        Assert.Equal(10, fixture.LocationId);
        Assert.Null(fixture.SubLocationId);
    }

    [Fact]
    public void Fixture_SubLocationId_Set()
    {
        var fixture = new Fixture { Id = 1, SubLocationId = 20, Name = "Trash Can", Type = FixtureType.TrashCan };
        Assert.Null(fixture.LocationId);
        Assert.Equal(20, fixture.SubLocationId);
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test stakeout.tests/ --filter "FullyQualifiedName~FixtureTests" -v minimal --nologo`
Expected: 4 passed

- [ ] **Step 5: Commit**

```
git add src/simulation/fixtures/FixtureType.cs src/simulation/fixtures/Fixture.cs stakeout.tests/Simulation/Fixtures/FixtureTests.cs
git commit -m "feat: add Fixture entity and FixtureType enum"
```

---

### Task 2: Add Fixtures Dictionary and Query Helpers to SimulationState

**Files:**
- Modify: `src/simulation/SimulationState.cs`
- Test: `stakeout.tests/Simulation/Fixtures/FixtureTests.cs` (append)

- [ ] **Step 1: Write failing tests for fixture query helpers**

Append to `stakeout.tests/Simulation/Fixtures/FixtureTests.cs`:

```csharp
// Add these imports at the top:
// using Stakeout.Simulation;
// using Stakeout.Simulation.Entities;

public class FixtureQueryTests
{
    [Fact]
    public void GetFixturesForLocation_ReturnsMatchingFixtures()
    {
        var state = new SimulationState();
        state.Fixtures[1] = new Fixture { Id = 1, LocationId = 10, Name = "Trash Can", Type = FixtureType.TrashCan };
        state.Fixtures[2] = new Fixture { Id = 2, LocationId = 10, Name = "Trash Can 2", Type = FixtureType.TrashCan };
        state.Fixtures[3] = new Fixture { Id = 3, LocationId = 99, Name = "Other", Type = FixtureType.TrashCan };

        var result = state.GetFixturesForLocation(10);
        Assert.Equal(2, result.Count);
        Assert.All(result, f => Assert.Equal(10, f.LocationId));
    }

    [Fact]
    public void GetFixturesForSubLocation_ReturnsMatchingFixtures()
    {
        var state = new SimulationState();
        state.Fixtures[1] = new Fixture { Id = 1, SubLocationId = 20, Name = "Trash Can", Type = FixtureType.TrashCan };
        state.Fixtures[2] = new Fixture { Id = 2, SubLocationId = 99, Name = "Other", Type = FixtureType.TrashCan };

        var result = state.GetFixturesForSubLocation(20);
        Assert.Single(result);
        Assert.Equal(20, result[0].SubLocationId);
    }

    [Fact]
    public void GetFixturesForLocation_EmptyWhenNone()
    {
        var state = new SimulationState();
        var result = state.GetFixturesForLocation(10);
        Assert.Empty(result);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test stakeout.tests/ --filter "FullyQualifiedName~FixtureQueryTests" -v minimal --nologo`
Expected: FAIL — `GetFixturesForLocation` does not exist

- [ ] **Step 3: Add Fixtures dictionary and query helpers to SimulationState**

In `src/simulation/SimulationState.cs`:
- Add `using Stakeout.Simulation.Fixtures;` to the imports
- Add to the properties section (after `Items` dictionary): `public Dictionary<int, Fixture> Fixtures { get; } = new();`
- Add query helpers after the existing `GetCityForAddress` method:

```csharp
public List<Fixture> GetFixturesForLocation(int locationId)
{
    return Fixtures.Values.Where(f => f.LocationId == locationId).ToList();
}

public List<Fixture> GetFixturesForSubLocation(int subLocationId)
{
    return Fixtures.Values.Where(f => f.SubLocationId == subLocationId).ToList();
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test stakeout.tests/ --filter "FullyQualifiedName~FixtureQueryTests" -v minimal --nologo`
Expected: 3 passed

- [ ] **Step 5: Commit**

```
git add src/simulation/SimulationState.cs stakeout.tests/Simulation/Fixtures/FixtureTests.cs
git commit -m "feat: add Fixtures dictionary and query helpers to SimulationState"
```

---

### Task 3: CreateFixture Helper in LocationBuilders

**Files:**
- Modify: `src/simulation/addresses/LocationBuilders.cs`
- Modify: `stakeout.tests/Simulation/Addresses/LocationBuildersTests.cs`

- [ ] **Step 1: Write failing test for CreateFixture**

Append to `stakeout.tests/Simulation/Addresses/LocationBuildersTests.cs`:

```csharp
// Add import: using Stakeout.Simulation.Fixtures;

[Fact]
public void CreateFixture_OnLocation_RegisteredInState()
{
    var (state, addr) = Setup();
    var loc = LocationBuilders.CreateLocation(state, addr, "Kitchen", new[] { "kitchen" });
    var fixture = LocationBuilders.CreateFixture(state, FixtureType.TrashCan, "Trash Can",
        locationId: loc.Id, subLocationId: null);

    Assert.True(state.Fixtures.ContainsKey(fixture.Id));
    Assert.Equal(loc.Id, fixture.LocationId);
    Assert.Null(fixture.SubLocationId);
    Assert.Equal("Trash Can", fixture.Name);
    Assert.Equal(FixtureType.TrashCan, fixture.Type);
}

[Fact]
public void CreateFixture_OnSubLocation_RegisteredInState()
{
    var (state, addr) = Setup();
    var loc = LocationBuilders.CreateLocation(state, addr, "Interior", new[] { "residential" });
    var sub = LocationBuilders.CreateSubLocation(state, loc, "Kitchen", new[] { "kitchen" });
    var fixture = LocationBuilders.CreateFixture(state, FixtureType.TrashCan, "Trash Can",
        locationId: null, subLocationId: sub.Id);

    Assert.True(state.Fixtures.ContainsKey(fixture.Id));
    Assert.Null(fixture.LocationId);
    Assert.Equal(sub.Id, fixture.SubLocationId);
}

[Fact]
public void CreateFixture_WithTags_HasTags()
{
    var (state, addr) = Setup();
    var loc = LocationBuilders.CreateLocation(state, addr, "Kitchen", new[] { "kitchen" });
    var fixture = LocationBuilders.CreateFixture(state, FixtureType.TrashCan, "Trash Can",
        locationId: loc.Id, subLocationId: null, tags: new[] { "kitchen", "waste" });

    Assert.True(fixture.HasTag("kitchen"));
    Assert.True(fixture.HasTag("waste"));
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test stakeout.tests/ --filter "FullyQualifiedName~LocationBuildersTests.CreateFixture" -v minimal --nologo`
Expected: FAIL — `CreateFixture` does not exist

- [ ] **Step 3: Add CreateFixture to LocationBuilders**

In `src/simulation/addresses/LocationBuilders.cs`:
- Add `using Stakeout.Simulation.Fixtures;` to imports
- Add this method:

```csharp
public static Fixture CreateFixture(SimulationState state, FixtureType type,
    string name, int? locationId, int? subLocationId, string[] tags = null)
{
    var fixture = new Fixture
    {
        Id = state.GenerateEntityId(),
        LocationId = locationId,
        SubLocationId = subLocationId,
        Name = name,
        Type = type,
        Tags = tags ?? Array.Empty<string>()
    };
    state.Fixtures[fixture.Id] = fixture;
    return fixture;
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test stakeout.tests/ --filter "FullyQualifiedName~LocationBuildersTests" -v minimal --nologo`
Expected: All LocationBuildersTests pass (existing + 3 new)

- [ ] **Step 5: Commit**

```
git add src/simulation/addresses/LocationBuilders.cs stakeout.tests/Simulation/Addresses/LocationBuildersTests.cs
git commit -m "feat: add CreateFixture helper to LocationBuilders"
```

---

### Task 4: Add Trash Can Fixtures to Address Templates

**Files:**
- Modify: `src/simulation/addresses/SuburbanHomeTemplate.cs`
- Modify: `src/simulation/addresses/DinerTemplate.cs`
- Modify: `src/simulation/addresses/DiveBarTemplate.cs`
- Modify: `src/simulation/addresses/ApartmentBuildingTemplate.cs`
- Modify: `src/simulation/addresses/OfficeTemplate.cs`
- Modify: `src/simulation/addresses/ParkTemplate.cs`
- Modify (no changes needed): `src/simulation/addresses/AirportTemplate.cs`
- Modify: `stakeout.tests/Simulation/Addresses/SuburbanHomeTemplateTests.cs`
- Modify: `stakeout.tests/Simulation/Addresses/DinerTemplateTests.cs`
- Modify: `stakeout.tests/Simulation/Addresses/DiveBarTemplateTests.cs`
- Modify: `stakeout.tests/Simulation/Addresses/ApartmentBuildingTemplateTests.cs`
- Modify: `stakeout.tests/Simulation/Addresses/OfficeTemplateTests.cs`
- Modify: `stakeout.tests/Simulation/Addresses/ParkTemplateTests.cs`

- [ ] **Step 1: Write failing tests for trash cans in templates**

Add one test per template test file (same pattern for each). Example for DinerTemplateTests:

```csharp
// Add import: using Stakeout.Simulation.Fixtures;

[Fact]
public void Generate_HasTrashCan()
{
    var (state, addr) = Generate();
    var allFixtures = state.Fixtures.Values.Where(f =>
        state.GetLocationsForAddress(addr.Id).Any(l => l.Id == f.LocationId) ||
        state.GetLocationsForAddress(addr.Id).SelectMany(l => state.GetSubLocationsForLocation(l.Id)).Any(s => s.Id == f.SubLocationId));
    Assert.Contains(allFixtures, f => f.Type == FixtureType.TrashCan);
}
```

Use the same pattern for all 6 template test files (not Airport — it's minimal). The test verifies at least one trash can fixture exists at the address.

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test stakeout.tests/ --filter "FullyQualifiedName~HasTrashCan" -v minimal --nologo`
Expected: FAIL — no fixtures generated yet

- [ ] **Step 3: Add trash can fixtures to each template**

Each template needs `using Stakeout.Simulation.Fixtures;` added to imports.

**SuburbanHomeTemplate** — add after the Kitchen sublocation is created:
```csharp
var kitchen = LocationBuilders.CreateSubLocation(state, interior, "Kitchen", new[] { "kitchen", "food" });
LocationBuilders.CreateFixture(state, FixtureType.TrashCan, "Trash Can",
    locationId: null, subLocationId: kitchen.Id);
```
Note: this requires capturing the return value from the Kitchen CreateSubLocation call (currently discarded).

**DinerTemplate** — add after the kitchen location is created:
```csharp
LocationBuilders.CreateFixture(state, FixtureType.TrashCan, "Trash Can",
    locationId: kitchen.Id, subLocationId: null);
```

**DiveBarTemplate** — add in the alley (where trash would naturally be):
```csharp
var alley = LocationBuilders.CreateLocation(state, address, "Alley", ...);
LocationBuilders.CreateFixture(state, FixtureType.TrashCan, "Trash Can",
    locationId: alley.Id, subLocationId: null);
```
Note: this requires capturing the return value from the Alley CreateLocation call (currently discarded).

**ApartmentBuildingTemplate** — add in lobby:
```csharp
var lobby = LocationBuilders.CreateLocation(state, address, "Lobby", ...);
LocationBuilders.CreateFixture(state, FixtureType.TrashCan, "Trash Can",
    locationId: lobby.Id, subLocationId: null);
```
Note: this requires capturing the return value from the Lobby CreateLocation call (currently discarded).

**OfficeTemplate** — add in each floor's Break Room:
```csharp
var breakRoom = LocationBuilders.CreateSubLocation(state, floor, "Break Room",
    new[] { "food", "social" });
LocationBuilders.CreateFixture(state, FixtureType.TrashCan, "Trash Can",
    locationId: null, subLocationId: breakRoom.Id);
```
Note: this requires capturing the return value from the Break Room CreateSubLocation call (currently discarded).

**ParkTemplate** — add at the Picnic Area:
```csharp
var picnic = LocationBuilders.CreateLocation(state, address, "Picnic Area", ...);
LocationBuilders.CreateFixture(state, FixtureType.TrashCan, "Trash Can",
    locationId: picnic.Id, subLocationId: null);
```
Note: this requires capturing the return value from the Picnic Area CreateLocation call (currently discarded).

**AirportTemplate** — no fixture (minimal template).

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test stakeout.tests/ --filter "FullyQualifiedName~TemplateTests" -v minimal --nologo`
Expected: All template tests pass (existing + new HasTrashCan tests)

- [ ] **Step 5: Commit**

```
git add src/simulation/addresses/SuburbanHomeTemplate.cs src/simulation/addresses/DinerTemplate.cs src/simulation/addresses/DiveBarTemplate.cs src/simulation/addresses/ApartmentBuildingTemplate.cs src/simulation/addresses/OfficeTemplate.cs src/simulation/addresses/ParkTemplate.cs
git add stakeout.tests/Simulation/Addresses/SuburbanHomeTemplateTests.cs stakeout.tests/Simulation/Addresses/DinerTemplateTests.cs stakeout.tests/Simulation/Addresses/DiveBarTemplateTests.cs stakeout.tests/Simulation/Addresses/ApartmentBuildingTemplateTests.cs stakeout.tests/Simulation/Addresses/OfficeTemplateTests.cs stakeout.tests/Simulation/Addresses/ParkTemplateTests.cs
git commit -m "feat: add trash can fixtures to address templates"
```

---

### Task 5: Redesign Trace Entity

**Files:**
- Modify: `src/simulation/traces/Trace.cs`
- Modify: `stakeout.tests/Simulation/Traces/TraceTests.cs`

- [ ] **Step 1: Rewrite TraceTests for the new Trace shape**

Replace the entire contents of `stakeout.tests/Simulation/Traces/TraceTests.cs`:

```csharp
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
            Id = 1, Type = TraceType.Condition,
            CreatedAt = new DateTime(1980, 1, 2, 1, 15, 0),
            CreatedByPersonId = 5, AttachedToPersonId = 3,
            Description = "Cause of death: stabbing"
        };
        Assert.Equal(TraceType.Condition, trace.Type);
        Assert.Equal(3, trace.AttachedToPersonId);
        Assert.Null(trace.LocationId);
    }

    [Fact]
    public void Trace_MarkType_BoundToLocation()
    {
        var trace = new Trace
        {
            Id = 2, Type = TraceType.Mark,
            CreatedAt = new DateTime(1980, 1, 2, 1, 15, 0),
            CreatedByPersonId = 5, LocationId = 10,
            Description = "Signs of forced entry"
        };
        Assert.Equal(TraceType.Mark, trace.Type);
        Assert.Equal(10, trace.LocationId);
        Assert.Null(trace.AttachedToPersonId);
    }

    [Fact]
    public void Trace_FingerprintType_OnFixture()
    {
        var trace = new Trace
        {
            Id = 3, Type = TraceType.Fingerprint,
            CreatedAt = new DateTime(1980, 1, 2, 8, 0, 0),
            CreatedByPersonId = 7,
            FixtureId = 42,
            Description = "Fingerprint on trash can",
            DecayDays = 7
        };
        Assert.Equal(TraceType.Fingerprint, trace.Type);
        Assert.Equal(42, trace.FixtureId);
        Assert.Equal(7, trace.DecayDays);
    }

    [Fact]
    public void Trace_IsActive_DefaultsToTrue()
    {
        var trace = new Trace { Id = 1, Type = TraceType.Mark, Description = "Blood" };
        Assert.True(trace.IsActive);
    }

    [Fact]
    public void Trace_DecayDays_NullMeansPermanent()
    {
        var trace = new Trace
        {
            Id = 1, Type = TraceType.Condition,
            Description = "Bullet wound",
            DecayDays = null
        };
        Assert.Null(trace.DecayDays);
    }

    [Fact]
    public void Trace_SubLocationId_Set()
    {
        var trace = new Trace
        {
            Id = 1, Type = TraceType.Mark,
            SubLocationId = 15,
            Description = "Scuff marks"
        };
        Assert.Equal(15, trace.SubLocationId);
        Assert.Null(trace.LocationId);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test stakeout.tests/ --filter "FullyQualifiedName~TraceTests" -v minimal --nologo`
Expected: FAIL — `Trace.Type` doesn't exist (old field is `TraceType`), `FixtureId` doesn't exist, etc.

- [ ] **Step 3: Rewrite the Trace entity**

Replace the entire contents of `src/simulation/traces/Trace.cs`:

```csharp
using System;

namespace Stakeout.Simulation.Traces;

public enum TraceType
{
    Mark,           // blood pool, scuff marks, broken glass
    Item,           // dropped receipt, forgotten jacket
    Sighting,       // witness report of someone being somewhere
    Record,         // email, letter, phone message
    Fingerprint,    // on a surface, door, fixture
    Condition,      // wound, cause of death — attached to a person
}

public class Trace
{
    public int Id { get; set; }
    public TraceType Type { get; set; }
    public string Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; } = true;

    // Where
    public int? LocationId { get; set; }
    public int? SubLocationId { get; set; }
    public int? FixtureId { get; set; }

    // Who
    public int? CreatedByPersonId { get; set; }
    public int? AttachedToPersonId { get; set; }

    // Decay
    public int? DecayDays { get; set; }
}
```

Key changes from the old Trace:
- `TraceType` property renamed to `Type` (avoids collision with the enum name)
- `CreatedByPersonId` changed from `int` to `int?` (not every trace has a known creator)
- `Data` dictionary removed — replaced by explicit `SubLocationId`, `FixtureId`, `IsActive`, `DecayDays` fields
- `IsActive` added (defaults true)
- `DecayDays` added (null = permanent)

- [ ] **Step 4: Fix compilation — update CrimeIntegrationTests**

`stakeout.tests/Simulation/Crimes/CrimeIntegrationTests.cs` references `t.TraceType` (the old property name) on lines 83-84. Rename to `t.Type`:

```csharp
// Line 83: change t.TraceType to t.Type
Assert.Contains(state.Traces.Values, t => t.Type == TraceType.Condition);
// Line 84: change t.TraceType to t.Type
Assert.Contains(state.Traces.Values, t => t.Type == TraceType.Mark);
```

Then verify the build compiles:

Run: `dotnet build stakeout.tests/ --nologo -v minimal`
Expected: Build succeeds (warnings OK)

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test stakeout.tests/ --filter "FullyQualifiedName~TraceTests" -v minimal --nologo`
Expected: 6 passed

- [ ] **Step 6: Commit**

```
git add src/simulation/traces/Trace.cs stakeout.tests/Simulation/Traces/TraceTests.cs stakeout.tests/Simulation/Crimes/CrimeIntegrationTests.cs
git commit -m "feat: redesign Trace entity with explicit fields, decay, IsActive"
```

---

### Task 6: Trace Query Helpers on SimulationState

**Files:**
- Modify: `src/simulation/SimulationState.cs`
- Test: `stakeout.tests/Simulation/Traces/TraceQueryTests.cs` (new file)

- [ ] **Step 1: Write failing tests for trace query helpers**

Create `stakeout.tests/Simulation/Traces/TraceQueryTests.cs`:

```csharp
using System;
using Stakeout.Simulation;
using Stakeout.Simulation.Traces;
using Xunit;

namespace Stakeout.Tests.Simulation.Traces;

public class TraceQueryTests
{
    private SimulationState StateWithTraces()
    {
        var state = new SimulationState();
        var now = new DateTime(1984, 6, 15);

        // Active trace at location 10
        state.Traces[1] = new Trace
        {
            Id = 1, Type = TraceType.Mark, LocationId = 10,
            Description = "Blood pool", CreatedAt = now, IsActive = true
        };

        // Inactive trace at location 10
        state.Traces[2] = new Trace
        {
            Id = 2, Type = TraceType.Mark, LocationId = 10,
            Description = "Cleaned up blood", CreatedAt = now, IsActive = false
        };

        // Trace at different location
        state.Traces[3] = new Trace
        {
            Id = 3, Type = TraceType.Mark, LocationId = 99,
            Description = "Other", CreatedAt = now, IsActive = true
        };

        // Expired trace at location 10 (decayed)
        state.Traces[4] = new Trace
        {
            Id = 4, Type = TraceType.Fingerprint, LocationId = 10,
            Description = "Old fingerprint", CreatedAt = now.AddDays(-10),
            DecayDays = 7, IsActive = true
        };

        // Non-expired trace at location 10
        state.Traces[5] = new Trace
        {
            Id = 5, Type = TraceType.Fingerprint, LocationId = 10,
            Description = "Fresh fingerprint", CreatedAt = now.AddDays(-3),
            DecayDays = 7, IsActive = true
        };

        // Trace on fixture 50
        state.Traces[6] = new Trace
        {
            Id = 6, Type = TraceType.Item, FixtureId = 50,
            Description = "Receipt", CreatedAt = now, IsActive = true
        };

        // Trace on sublocation 20
        state.Traces[7] = new Trace
        {
            Id = 7, Type = TraceType.Mark, SubLocationId = 20,
            Description = "Scuff marks", CreatedAt = now, IsActive = true
        };

        // Condition on person 30
        state.Traces[8] = new Trace
        {
            Id = 8, Type = TraceType.Condition, AttachedToPersonId = 30,
            Description = "Bullet wound", CreatedAt = now, IsActive = true
        };

        return state;
    }

    [Fact]
    public void GetTracesForLocation_FiltersInactiveAndExpired()
    {
        var state = StateWithTraces();
        var now = new DateTime(1984, 6, 15);
        var result = state.GetTracesForLocation(10, now);

        // Should include: 1 (active mark), 5 (fresh fingerprint)
        // Should exclude: 2 (inactive), 4 (expired)
        Assert.Equal(2, result.Count);
        Assert.Contains(result, t => t.Id == 1);
        Assert.Contains(result, t => t.Id == 5);
    }

    [Fact]
    public void GetTracesForSubLocation_ReturnsMatching()
    {
        var state = StateWithTraces();
        var now = new DateTime(1984, 6, 15);
        var result = state.GetTracesForSubLocation(20, now);
        Assert.Single(result);
        Assert.Equal(7, result[0].Id);
    }

    [Fact]
    public void GetTracesForFixture_ReturnsMatching()
    {
        var state = StateWithTraces();
        var now = new DateTime(1984, 6, 15);
        var result = state.GetTracesForFixture(50, now);
        Assert.Single(result);
        Assert.Equal(6, result[0].Id);
    }

    [Fact]
    public void GetTracesForPerson_ReturnsMatching()
    {
        var state = StateWithTraces();
        var now = new DateTime(1984, 6, 15);
        var result = state.GetTracesForPerson(30, now);
        Assert.Single(result);
        Assert.Equal(8, result[0].Id);
    }

    [Fact]
    public void GetTracesForLocation_NullDecayDays_NeverExpires()
    {
        var state = new SimulationState();
        var longAgo = new DateTime(1980, 1, 1);
        state.Traces[1] = new Trace
        {
            Id = 1, Type = TraceType.Condition, LocationId = 10,
            Description = "Permanent", CreatedAt = longAgo, DecayDays = null
        };

        var result = state.GetTracesForLocation(10, new DateTime(1984, 6, 15));
        Assert.Single(result);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test stakeout.tests/ --filter "FullyQualifiedName~TraceQueryTests" -v minimal --nologo`
Expected: FAIL — methods don't exist yet

- [ ] **Step 3: Add trace query helpers to SimulationState**

Add these methods to `src/simulation/SimulationState.cs` after the fixture query helpers:

```csharp
public List<Trace> GetTracesForLocation(int locationId, DateTime currentTime)
{
    return Traces.Values.Where(t => t.LocationId == locationId && IsTraceVisible(t, currentTime)).ToList();
}

public List<Trace> GetTracesForSubLocation(int subLocationId, DateTime currentTime)
{
    return Traces.Values.Where(t => t.SubLocationId == subLocationId && IsTraceVisible(t, currentTime)).ToList();
}

public List<Trace> GetTracesForFixture(int fixtureId, DateTime currentTime)
{
    return Traces.Values.Where(t => t.FixtureId == fixtureId && IsTraceVisible(t, currentTime)).ToList();
}

public List<Trace> GetTracesForPerson(int personId, DateTime currentTime)
{
    return Traces.Values.Where(t => t.AttachedToPersonId == personId && IsTraceVisible(t, currentTime)).ToList();
}

private static bool IsTraceVisible(Trace trace, DateTime currentTime)
{
    if (!trace.IsActive) return false;
    if (trace.DecayDays.HasValue && trace.CreatedAt.AddDays(trace.DecayDays.Value) < currentTime) return false;
    return true;
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test stakeout.tests/ --filter "FullyQualifiedName~TraceQueryTests" -v minimal --nologo`
Expected: 5 passed

- [ ] **Step 5: Commit**

```
git add src/simulation/SimulationState.cs stakeout.tests/Simulation/Traces/TraceQueryTests.cs
git commit -m "feat: add trace query helpers with decay and IsActive filtering"
```

---

### Task 7: TraceEmitter API

**Files:**
- Create: `src/simulation/traces/TraceEmitter.cs`
- Create: `stakeout.tests/Simulation/Traces/TraceEmitterTests.cs`

- [ ] **Step 1: Write failing tests for TraceEmitter**

Create `stakeout.tests/Simulation/Traces/TraceEmitterTests.cs`:

```csharp
using System;
using Stakeout.Simulation;
using Stakeout.Simulation.Fixtures;
using Stakeout.Simulation.Traces;
using Xunit;

namespace Stakeout.Tests.Simulation.Traces;

public class TraceEmitterTests
{
    private SimulationState Setup()
    {
        var state = new SimulationState();
        state.Fixtures[10] = new Fixture { Id = 10, LocationId = 1, Name = "Trash Can", Type = FixtureType.TrashCan };
        return state;
    }

    [Fact]
    public void EmitFingerprint_CreatesTraceWithDefaults()
    {
        var state = Setup();
        var id = TraceEmitter.EmitFingerprint(state, personId: 5, locationId: 1, "Fingerprint on door");

        var trace = state.Traces[id];
        Assert.Equal(TraceType.Fingerprint, trace.Type);
        Assert.Equal(5, trace.CreatedByPersonId);
        Assert.Equal(1, trace.LocationId);
        Assert.Equal(7, trace.DecayDays); // default fingerprint decay
        Assert.True(trace.IsActive);
    }

    [Fact]
    public void EmitFingerprintOnFixture_SetsFixtureId()
    {
        var state = Setup();
        var id = TraceEmitter.EmitFingerprintOnFixture(state, personId: 5, fixtureId: 10, "Fingerprint on trash can");

        var trace = state.Traces[id];
        Assert.Equal(TraceType.Fingerprint, trace.Type);
        Assert.Equal(10, trace.FixtureId);
        Assert.Equal(7, trace.DecayDays);
    }

    [Fact]
    public void EmitMark_CreatesMarkTrace()
    {
        var state = Setup();
        var id = TraceEmitter.EmitMark(state, locationId: 1, subLocationId: null, "Blood pool");

        var trace = state.Traces[id];
        Assert.Equal(TraceType.Mark, trace.Type);
        Assert.Equal(1, trace.LocationId);
        Assert.Null(trace.SubLocationId);
        Assert.Null(trace.DecayDays); // permanent by default
    }

    [Fact]
    public void EmitMark_WithDecay_SetsDecayDays()
    {
        var state = Setup();
        var id = TraceEmitter.EmitMark(state, locationId: 1, subLocationId: null, "Muddy footprints", decayDays: 3);

        var trace = state.Traces[id];
        Assert.Equal(3, trace.DecayDays);
    }

    [Fact]
    public void EmitMark_WithSubLocation_SetsSubLocationId()
    {
        var state = Setup();
        var id = TraceEmitter.EmitMark(state, locationId: 1, subLocationId: 20, "Scuff marks");

        var trace = state.Traces[id];
        Assert.Equal(1, trace.LocationId);
        Assert.Equal(20, trace.SubLocationId);
    }

    [Fact]
    public void EmitItem_CreatesItemTrace()
    {
        var state = Setup();
        var id = TraceEmitter.EmitItem(state, personId: 5, locationId: 1, fixtureId: 10, "Crumpled receipt");

        var trace = state.Traces[id];
        Assert.Equal(TraceType.Item, trace.Type);
        Assert.Equal(5, trace.CreatedByPersonId);
        Assert.Equal(1, trace.LocationId);
        Assert.Equal(10, trace.FixtureId);
    }

    [Fact]
    public void EmitCondition_CreatesPermanentTrace()
    {
        var state = Setup();
        var id = TraceEmitter.EmitCondition(state, personId: 3, "Bullet wound to the chest");

        var trace = state.Traces[id];
        Assert.Equal(TraceType.Condition, trace.Type);
        Assert.Equal(3, trace.AttachedToPersonId);
        Assert.Null(trace.DecayDays); // permanent
        Assert.Null(trace.LocationId);
    }

    [Fact]
    public void EmitRecord_CreatesRecordOnFixture()
    {
        var state = Setup();
        var id = TraceEmitter.EmitRecord(state, fixtureId: 10, personId: 5, "Threatening letter");

        var trace = state.Traces[id];
        Assert.Equal(TraceType.Record, trace.Type);
        Assert.Equal(10, trace.FixtureId);
        Assert.Equal(5, trace.CreatedByPersonId);
    }

    [Fact]
    public void EmitSighting_CreatesSightingTrace()
    {
        var state = Setup();
        var id = TraceEmitter.EmitSighting(state, personId: 5, locationId: 1, "Seen entering at 2am");

        var trace = state.Traces[id];
        Assert.Equal(TraceType.Sighting, trace.Type);
        Assert.Equal(5, trace.CreatedByPersonId);
        Assert.Equal(1, trace.LocationId);
    }

    [Fact]
    public void EmitSighting_WithDecay()
    {
        var state = Setup();
        var id = TraceEmitter.EmitSighting(state, personId: 5, locationId: 1, "Seen lurking", decayDays: 14);

        var trace = state.Traces[id];
        Assert.Equal(14, trace.DecayDays);
    }

    [Fact]
    public void AllEmitters_SetCreatedAtFromClock()
    {
        var clock = new GameClock(new DateTime(1984, 6, 15, 14, 30, 0));
        var state = new SimulationState(clock);
        state.Fixtures[10] = new Fixture { Id = 10, LocationId = 1, Name = "Trash Can", Type = FixtureType.TrashCan };

        var id = TraceEmitter.EmitMark(state, locationId: 1, subLocationId: null, "Test");
        Assert.Equal(new DateTime(1984, 6, 15, 14, 30, 0), state.Traces[id].CreatedAt);
    }
}
```

Note: This test assumes `GameClock` accepts an initial DateTime. Check if that's the case — if `GameClock` has a `CurrentTime` property, `TraceEmitter` should read `state.Clock.CurrentTime` for `CreatedAt`. If `GameClock` doesn't accept a start time constructor, you may need to set the clock time manually or adjust.

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test stakeout.tests/ --filter "FullyQualifiedName~TraceEmitterTests" -v minimal --nologo`
Expected: FAIL — `TraceEmitter` class doesn't exist

- [ ] **Step 3: Check GameClock for CurrentTime**

Read `src/simulation/GameClock.cs` to find how to get the current simulation time. The TraceEmitter needs to set `CreatedAt` from the game clock.

- [ ] **Step 4: Implement TraceEmitter**

Create `src/simulation/traces/TraceEmitter.cs`:

```csharp
using System;

namespace Stakeout.Simulation.Traces;

public static class TraceEmitter
{
    public static int EmitFingerprint(SimulationState state, int personId,
        int locationId, string description)
    {
        return AddTrace(state, new Trace
        {
            Type = TraceType.Fingerprint,
            CreatedByPersonId = personId,
            LocationId = locationId,
            Description = description,
            DecayDays = 7
        });
    }

    public static int EmitFingerprintOnFixture(SimulationState state, int personId,
        int fixtureId, string description)
    {
        return AddTrace(state, new Trace
        {
            Type = TraceType.Fingerprint,
            CreatedByPersonId = personId,
            FixtureId = fixtureId,
            Description = description,
            DecayDays = 7
        });
    }

    public static int EmitMark(SimulationState state, int locationId,
        int? subLocationId, string description, int? decayDays = null)
    {
        return AddTrace(state, new Trace
        {
            Type = TraceType.Mark,
            LocationId = locationId,
            SubLocationId = subLocationId,
            Description = description,
            DecayDays = decayDays
        });
    }

    public static int EmitItem(SimulationState state, int? personId,
        int locationId, int? fixtureId, string description, int? decayDays = null)
    {
        return AddTrace(state, new Trace
        {
            Type = TraceType.Item,
            CreatedByPersonId = personId,
            LocationId = locationId,
            FixtureId = fixtureId,
            Description = description,
            DecayDays = decayDays
        });
    }

    public static int EmitCondition(SimulationState state, int personId,
        string description)
    {
        return AddTrace(state, new Trace
        {
            Type = TraceType.Condition,
            AttachedToPersonId = personId,
            Description = description,
            DecayDays = null
        });
    }

    public static int EmitRecord(SimulationState state, int fixtureId,
        int? personId, string description)
    {
        return AddTrace(state, new Trace
        {
            Type = TraceType.Record,
            FixtureId = fixtureId,
            CreatedByPersonId = personId,
            Description = description
        });
    }

    public static int EmitSighting(SimulationState state, int personId,
        int locationId, string description, int? decayDays = null)
    {
        return AddTrace(state, new Trace
        {
            Type = TraceType.Sighting,
            CreatedByPersonId = personId,
            LocationId = locationId,
            Description = description,
            DecayDays = decayDays
        });
    }

    private static int AddTrace(SimulationState state, Trace trace)
    {
        trace.Id = state.GenerateEntityId();
        trace.CreatedAt = state.Clock.CurrentTime; // adjust based on GameClock API
        state.Traces[trace.Id] = trace;
        return trace.Id;
    }
}
```

Adjust `state.Clock.CurrentTime` based on whatever the actual GameClock property name is (found in step 3).

- [ ] **Step 5: Adjust tests if needed based on GameClock API**

If `GameClock` doesn't take a start time in its constructor, adjust the `AllEmitters_SetCreatedAtFromClock` test to work with however the clock is initialized.

- [ ] **Step 6: Run tests to verify they pass**

Run: `dotnet test stakeout.tests/ --filter "FullyQualifiedName~TraceEmitterTests" -v minimal --nologo`
Expected: 11 passed

- [ ] **Step 7: Commit**

```
git add src/simulation/traces/TraceEmitter.cs stakeout.tests/Simulation/Traces/TraceEmitterTests.cs
git commit -m "feat: add TraceEmitter API for trace generation"
```

---

### Task 8: InvestigationQuery and InvestigationResult

**Files:**
- Create: `src/simulation/traces/InvestigationResult.cs`
- Create: `src/simulation/traces/InvestigationQuery.cs`
- Create: `stakeout.tests/Simulation/Traces/InvestigationQueryTests.cs`

- [ ] **Step 1: Write failing tests for InvestigationQuery**

Create `stakeout.tests/Simulation/Traces/InvestigationQueryTests.cs`:

```csharp
using System;
using System.Linq;
using Stakeout.Simulation;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Fixtures;
using Stakeout.Simulation.Traces;
using Xunit;

namespace Stakeout.Tests.Simulation.Traces;

public class InvestigationQueryTests
{
    private static readonly DateTime Now = new(1984, 6, 15);

    private SimulationState SetupScene()
    {
        var state = new SimulationState();

        // Location 10 with a fixture
        state.Fixtures[1] = new Fixture { Id = 1, LocationId = 10, Name = "Trash Can", Type = FixtureType.TrashCan };

        // Trace at location 10 (not on fixture)
        state.Traces[1] = new Trace
        {
            Id = 1, Type = TraceType.Mark, LocationId = 10,
            Description = "Blood pool", CreatedAt = Now, IsActive = true
        };

        // Trace inside fixture 1
        state.Traces[2] = new Trace
        {
            Id = 2, Type = TraceType.Item, FixtureId = 1,
            Description = "Receipt", CreatedAt = Now, IsActive = true
        };

        // Inactive trace at location 10
        state.Traces[3] = new Trace
        {
            Id = 3, Type = TraceType.Mark, LocationId = 10,
            Description = "Cleaned", CreatedAt = Now, IsActive = false
        };

        // Trace at sublocation 20
        state.Traces[4] = new Trace
        {
            Id = 4, Type = TraceType.Mark, SubLocationId = 20,
            Description = "Scuff marks", CreatedAt = Now, IsActive = true
        };

        // Fixture at sublocation 20
        state.Fixtures[2] = new Fixture { Id = 2, SubLocationId = 20, Name = "Trash Can", Type = FixtureType.TrashCan };

        // Condition on person 30
        state.Traces[5] = new Trace
        {
            Id = 5, Type = TraceType.Condition, AttachedToPersonId = 30,
            Description = "Bullet wound", CreatedAt = Now, IsActive = true
        };

        return state;
    }

    [Fact]
    public void GetDiscoveriesForLocation_ReturnsFixturesAndTraces()
    {
        var state = SetupScene();
        var result = InvestigationQuery.GetDiscoveriesForLocation(state, 10, Now);

        Assert.Single(result.Fixtures);
        Assert.Equal(1, result.Fixtures[0].Id);

        // Only the blood pool (trace 1), not the cleaned one (trace 3)
        Assert.Single(result.Traces);
        Assert.Equal(1, result.Traces[0].Id);
    }

    [Fact]
    public void GetDiscoveriesForSubLocation_ReturnsFixturesAndTraces()
    {
        var state = SetupScene();
        var result = InvestigationQuery.GetDiscoveriesForSubLocation(state, 20, Now);

        Assert.Single(result.Fixtures);
        Assert.Equal(2, result.Fixtures[0].Id);

        Assert.Single(result.Traces);
        Assert.Equal(4, result.Traces[0].Id);
    }

    [Fact]
    public void GetFixtureTraces_ReturnsTracesInsideFixture()
    {
        var state = SetupScene();
        var result = InvestigationQuery.GetFixtureTraces(state, 1, Now);

        Assert.Single(result);
        Assert.Equal("Receipt", result[0].Description);
    }

    [Fact]
    public void GetPersonTraces_ReturnsConditions()
    {
        var state = SetupScene();
        var result = InvestigationQuery.GetPersonTraces(state, 30, Now);

        Assert.Single(result);
        Assert.Equal("Bullet wound", result[0].Description);
    }

    [Fact]
    public void GetDiscoveriesForLocation_ExcludesExpiredTraces()
    {
        var state = new SimulationState();
        state.Traces[1] = new Trace
        {
            Id = 1, Type = TraceType.Fingerprint, LocationId = 10,
            Description = "Old print", CreatedAt = Now.AddDays(-10),
            DecayDays = 7, IsActive = true
        };

        var result = InvestigationQuery.GetDiscoveriesForLocation(state, 10, Now);
        Assert.Empty(result.Traces);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test stakeout.tests/ --filter "FullyQualifiedName~InvestigationQueryTests" -v minimal --nologo`
Expected: FAIL — classes don't exist

- [ ] **Step 3: Create InvestigationResult**

Create `src/simulation/traces/InvestigationResult.cs`:

```csharp
using System.Collections.Generic;
using Stakeout.Simulation.Fixtures;

namespace Stakeout.Simulation.Traces;

public class InvestigationResult
{
    public List<Fixture> Fixtures { get; set; } = new();
    public List<Trace> Traces { get; set; } = new();
}
```

- [ ] **Step 4: Create InvestigationQuery**

Create `src/simulation/traces/InvestigationQuery.cs`:

```csharp
using System;
using System.Collections.Generic;
using Stakeout.Simulation.Fixtures;

namespace Stakeout.Simulation.Traces;

public static class InvestigationQuery
{
    public static InvestigationResult GetDiscoveriesForLocation(SimulationState state,
        int locationId, DateTime currentTime)
    {
        return new InvestigationResult
        {
            Fixtures = state.GetFixturesForLocation(locationId),
            Traces = state.GetTracesForLocation(locationId, currentTime)
        };
    }

    public static InvestigationResult GetDiscoveriesForSubLocation(SimulationState state,
        int subLocationId, DateTime currentTime)
    {
        return new InvestigationResult
        {
            Fixtures = state.GetFixturesForSubLocation(subLocationId),
            Traces = state.GetTracesForSubLocation(subLocationId, currentTime)
        };
    }

    public static List<Trace> GetFixtureTraces(SimulationState state,
        int fixtureId, DateTime currentTime)
    {
        return state.GetTracesForFixture(fixtureId, currentTime);
    }

    public static List<Trace> GetPersonTraces(SimulationState state,
        int personId, DateTime currentTime)
    {
        return state.GetTracesForPerson(personId, currentTime);
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test stakeout.tests/ --filter "FullyQualifiedName~InvestigationQueryTests" -v minimal --nologo`
Expected: 5 passed

- [ ] **Step 6: Commit**

```
git add src/simulation/traces/InvestigationResult.cs src/simulation/traces/InvestigationQuery.cs stakeout.tests/Simulation/Traces/InvestigationQueryTests.cs
git commit -m "feat: add InvestigationQuery and InvestigationResult"
```

---

### Task 9: Delete FingerprintService

**Files:**
- Delete: `src/simulation/traces/FingerprintService.cs`

- [ ] **Step 1: Verify no code references FingerprintService**

Run: `grep -r "FingerprintService" src/ stakeout.tests/ --include="*.cs"`
Expected: Only `src/simulation/traces/FingerprintService.cs` itself (no callers)

- [ ] **Step 2: Delete FingerprintService**

```
git rm src/simulation/traces/FingerprintService.cs
```

- [ ] **Step 3: Verify build still compiles**

Run: `dotnet build stakeout.tests/ --nologo -v minimal`
Expected: Build succeeds

- [ ] **Step 4: Run full test suite**

Run: `dotnet test stakeout.tests/ -v minimal --nologo`
Expected: All tests pass except the pre-existing `CrimeIntegrationTests` failure

- [ ] **Step 5: Commit**

```
git commit -m "remove: delete FingerprintService (replaced by TraceEmitter)"
```

---

### Task 10: Final Verification

- [ ] **Step 1: Run full test suite**

Run: `dotnet test stakeout.tests/ -v minimal --nologo`
Expected: Only the pre-existing `CrimeIntegrationTests.FullCrimePipeline_SerialKiller_VictimDiesAndTracesProduced` fails. All other tests pass.

- [ ] **Step 2: Verify build compiles clean**

Run: `dotnet build --nologo -v minimal`
Expected: Build succeeds (warnings OK, no errors)

- [ ] **Step 3: Review the diff**

Run: `git diff main --stat`
Verify the changed files match expectations from the spec.
