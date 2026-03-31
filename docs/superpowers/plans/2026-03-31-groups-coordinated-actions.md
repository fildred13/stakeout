# Groups & Coordinated Actions Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement the Group entity, telephone coordination system, relationship model, and a GoOnDate scenario that drives two NPCs through a complete call-arrange-pickup-dine-return flow — proving the full P5 stack with two integration tests.

**Architecture:** Group formation adds a `GoOnDateObjective` (phase-aware) to both members and sets `NeedsReplan` on them; `ActionRunner` handles the re-plan on the next tick. Phone calls target address + fixture (1984 landlines); `ActionRunner` injects `AcceptPhoneCallAction` at action boundaries when a `PendingInvitation` is waiting.

**Design note vs spec:** The spec described a `GoOnDateGroupAction` IAction. This plan uses a phase-aware `GoOnDateObjective` instead. `IAction.Tick` is not called while `TravelInfo` is set, making an action-based phase coordinator impractical with the current `ActionRunner` architecture. Observable behavior is identical.

**Tech Stack:** C# / .NET, xUnit, existing `SimulationState` / `NpcBrain` / `ActionRunner` pipeline.

---

## File Map

**New files:**
| File | Responsibility |
|---|---|
| `src/simulation/entities/Relationship.cs` | `Relationship` entity + `RelationshipType` enum |
| `src/simulation/entities/Group.cs` | `Group` entity + `GroupType/GroupStatus/GroupPhase` enums |
| `src/simulation/entities/PendingInvitation.cs` | `PendingInvitation` entity + `InvitationType` enum |
| `src/simulation/entities/Vehicle.cs` | `Vehicle` entity + `VehicleType` enum |
| `src/simulation/actions/telephone/PhoneCallAction.cs` | Timed action, emits traces, creates invitation or message |
| `src/simulation/actions/telephone/AcceptPhoneCallAction.cs` | Picks up phone, notifies `OrganizeDateObjective` |
| `src/simulation/actions/telephone/CheckAnsweringMachineAction.cs` | Reads messages, creates `CallBackObjective` |
| `src/simulation/objectives/OrganizeDateObjective.cs` | State-machine objective driving phone coordination |
| `src/simulation/objectives/GoOnDateObjective.cs` | Phase-aware objective producing date actions |
| `src/simulation/objectives/CallBackObjective.cs` | Schedules a return phone call |
| `stakeout.tests/Simulation/Groups/DateCoordinationIntegrationTests.cs` | Two integration tests |

**Modified files:**
| File | Change |
|---|---|
| `src/simulation/entities/Person.cs` | Add `HomePhoneFixtureId`, `VehicleId`, `NeedsReplan` |
| `src/simulation/SimulationState.cs` | Add `Groups`, `RelationshipsByPersonId`, `PendingInvitationsByPersonId`, `Vehicles` + helpers |
| `src/simulation/actions/IAction.cs` | Add `OnSuspend` / `OnResume` default interface methods |
| `src/simulation/actions/ActionSequence.cs` | Implement `OnSuspend` / `OnResume` |
| `src/simulation/actions/ActionRunner.cs` | `NeedsReplan` handling, invitation interrupt, arriving-home check, `OnActionCompletedWithState` call |
| `src/simulation/objectives/Objective.cs` | Add `OnActionCompletedWithState` virtual method |
| `src/simulation/objectives/ObjectiveSource.cs` | Add `Social` |
| `src/simulation/fixtures/FixtureType.cs` | Add `Telephone` |
| `src/simulation/addresses/SuburbanHomeTemplate.cs` | Add kitchen telephone fixture |
| `src/simulation/addresses/ApartmentBuildingTemplate.cs` | Add living room telephone fixture per unit |
| `src/simulation/addresses/DiveBarTemplate.cs` | Add back office telephone fixture |
| `src/simulation/addresses/OfficeTemplate.cs` | Add reception telephone fixture |
| `src/simulation/PersonGenerator.cs` | Set `HomePhoneFixtureId` after home is resolved |

---

## Task 1: Core Entities + SimulationState + Person Additions

**Files:**
- Create: `src/simulation/entities/Relationship.cs`
- Create: `src/simulation/entities/Group.cs`
- Create: `src/simulation/entities/PendingInvitation.cs`
- Create: `src/simulation/entities/Vehicle.cs`
- Modify: `src/simulation/entities/Person.cs`
- Modify: `src/simulation/SimulationState.cs`

- [ ] **Step 1: Create entity files**

`src/simulation/entities/Relationship.cs`:
```csharp
using System;
namespace Stakeout.Simulation.Entities;

public enum RelationshipType { Dating, Friend, CriminalAssociate }

public class Relationship
{
    public int Id { get; set; }
    public int PersonAId { get; set; }
    public int PersonBId { get; set; }
    public RelationshipType Type { get; set; }
    public DateTime StartedAt { get; set; }
}
```

`src/simulation/entities/Group.cs`:
```csharp
using System;
using System.Collections.Generic;
namespace Stakeout.Simulation.Entities;

public enum GroupType { Date, CriminalMeeting }
public enum GroupStatus { Forming, Active, Disbanded }
public enum GroupPhase { DriverEnRoute, AtPickup, DrivingToVenue, AtVenue, DrivingBack, Complete }

public class Group
{
    public int Id { get; set; }
    public GroupType Type { get; set; }
    public GroupStatus Status { get; set; }
    public List<int> MemberPersonIds { get; set; } = new();
    public int? DriverPersonId { get; set; }
    public int PickupAddressId { get; set; }
    public DateTime PickupTime { get; set; }
    public int MeetupAddressId { get; set; }
    public DateTime MeetupTime { get; set; }
    public GroupPhase CurrentPhase { get; set; }
}
```

`src/simulation/entities/PendingInvitation.cs`:
```csharp
using System;
namespace Stakeout.Simulation.Entities;

public enum InvitationType { DateInvitation, MeetingRequest }

public class PendingInvitation
{
    public int Id { get; set; }
    public int FromPersonId { get; set; }
    public int ToPersonId { get; set; }
    public InvitationType Type { get; set; }
    public int ProposedGroupId { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

`src/simulation/entities/Vehicle.cs`:
```csharp
namespace Stakeout.Simulation.Entities;

public enum VehicleType { Car }

public class Vehicle
{
    public int Id { get; set; }
    public int OwnerPersonId { get; set; }
    public int CurrentAddressId { get; set; }
    public VehicleType Type { get; set; }
}
```

- [ ] **Step 2: Add fields to Person.cs**

In `src/simulation/entities/Person.cs`, add after the existing `InventoryItemIds` property:
```csharp
public int? HomePhoneFixtureId { get; set; }
public int? VehicleId { get; set; }
public bool NeedsReplan { get; set; }
```

- [ ] **Step 3: Add collections and helpers to SimulationState.cs**

In `src/simulation/SimulationState.cs`, add after `public Dictionary<int, CityGrid> CityGrids`:
```csharp
public Dictionary<int, Group> Groups { get; } = new();
public Dictionary<int, Relationship> Relationships { get; } = new();
public Dictionary<int, List<Relationship>> RelationshipsByPersonId { get; } = new();
public Dictionary<int, List<PendingInvitation>> PendingInvitationsByPersonId { get; } = new();
public Dictionary<int, Vehicle> Vehicles { get; } = new();
```

Add these helper methods at the end of the class, before the closing `}`:
```csharp
public void AddRelationship(Relationship rel)
{
    Relationships[rel.Id] = rel;
    if (!RelationshipsByPersonId.ContainsKey(rel.PersonAId))
        RelationshipsByPersonId[rel.PersonAId] = new();
    RelationshipsByPersonId[rel.PersonAId].Add(rel);
    if (!RelationshipsByPersonId.ContainsKey(rel.PersonBId))
        RelationshipsByPersonId[rel.PersonBId] = new();
    RelationshipsByPersonId[rel.PersonBId].Add(rel);
}

public Relationship GetRelationship(int personAId, int personBId)
{
    if (RelationshipsByPersonId.TryGetValue(personAId, out var list))
        return list.FirstOrDefault(r => r.PersonBId == personBId || r.PersonAId == personBId);
    return null;
}

public void AddPendingInvitation(PendingInvitation inv)
{
    if (!PendingInvitationsByPersonId.ContainsKey(inv.ToPersonId))
        PendingInvitationsByPersonId[inv.ToPersonId] = new();
    PendingInvitationsByPersonId[inv.ToPersonId].Add(inv);
}

public List<Fixture> GetFixturesForAddress(int addressId)
{
    var locationIds = GetLocationsForAddress(addressId).Select(l => l.Id).ToHashSet();
    return Fixtures.Values.Where(f => f.LocationId.HasValue && locationIds.Contains(f.LocationId.Value)).ToList();
}
```

- [ ] **Step 4: Build to verify**

```
dotnet build stakeout.tests/
```
Expected: build succeeds, 0 errors.

- [ ] **Step 5: Commit**

```
git add src/simulation/entities/Relationship.cs src/simulation/entities/Group.cs src/simulation/entities/PendingInvitation.cs src/simulation/entities/Vehicle.cs src/simulation/entities/Person.cs src/simulation/SimulationState.cs
git commit -m "feat: add Group, Relationship, PendingInvitation, Vehicle entities + SimulationState additions"
```

---

## Task 2: ObjectiveSource + FixtureType + Telephone Fixtures in Templates

**Files:**
- Modify: `src/simulation/objectives/ObjectiveSource.cs`
- Modify: `src/simulation/fixtures/FixtureType.cs`
- Modify: `src/simulation/addresses/SuburbanHomeTemplate.cs`
- Modify: `src/simulation/addresses/ApartmentBuildingTemplate.cs`
- Modify: `src/simulation/addresses/DiveBarTemplate.cs`
- Modify: `src/simulation/addresses/OfficeTemplate.cs`
- Modify: `src/simulation/PersonGenerator.cs`
- Test: `stakeout.tests/Simulation/PersonGeneratorTests.cs` (add a test)

- [ ] **Step 1: Add Social to ObjectiveSource and Telephone to FixtureType**

In `src/simulation/objectives/ObjectiveSource.cs`, replace file contents:
```csharp
namespace Stakeout.Simulation.Objectives;

public enum ObjectiveSource
{
    Universal,
    Trait,
    Job,
    Crime,
    Social
}
```

In `src/simulation/fixtures/FixtureType.cs`, replace file contents:
```csharp
namespace Stakeout.Simulation.Fixtures;

public enum FixtureType
{
    TrashCan,
    Telephone
}
```

- [ ] **Step 2: Add telephone fixture to SuburbanHomeTemplate**

In `src/simulation/addresses/SuburbanHomeTemplate.cs`, after the line that creates the kitchen sublocation:
```csharp
var kitchen = LocationBuilders.CreateSubLocation(state, interior, "Kitchen", new[] { "kitchen", "food" });
LocationBuilders.CreateFixture(state, FixtureType.TrashCan, "Trash Can", locationId: null, subLocationId: kitchen.Id);
```
Add after:
```csharp
LocationBuilders.CreateFixture(state, FixtureType.Telephone, "Kitchen Telephone", locationId: null, subLocationId: kitchen.Id);
```

- [ ] **Step 3: Add telephone fixture to ApartmentBuildingTemplate**

Read `src/simulation/addresses/ApartmentBuildingTemplate.cs` to find where unit sublocations are created. Add a telephone fixture to the living room sublocation (or the unit location if there's no living room). Pattern:
```csharp
// After creating living room sublocation for a unit:
LocationBuilders.CreateFixture(state, FixtureType.Telephone, "Telephone", locationId: null, subLocationId: livingRoom.Id);
```
If units don't have a living room sublocation, add it to the unit location directly:
```csharp
LocationBuilders.CreateFixture(state, FixtureType.Telephone, "Telephone", locationId: unitLocation.Id, subLocationId: null);
```

- [ ] **Step 4: Add telephone fixture to DiveBarTemplate and OfficeTemplate**

Read both templates. In `DiveBarTemplate.cs`, find the back office or staff area location and add:
```csharp
LocationBuilders.CreateFixture(state, FixtureType.Telephone, "Back Office Telephone", locationId: officeLocation.Id, subLocationId: null);
```

In `OfficeTemplate.cs`, find the reception or lobby location and add:
```csharp
LocationBuilders.CreateFixture(state, FixtureType.Telephone, "Reception Telephone", locationId: receptionLocation.Id, subLocationId: null);
```

- [ ] **Step 5: Wire HomePhoneFixtureId in PersonGenerator**

In `src/simulation/PersonGenerator.cs`, after the line `state.People[person.Id] = person;`, add:
```csharp
// Set home phone fixture
var homeFixtures = state.GetFixturesForAddress(homeAddress.Id);
var homeTelephone = homeFixtures.FirstOrDefault(f => f.Type == Stakeout.Simulation.Fixtures.FixtureType.Telephone);
person.HomePhoneFixtureId = homeTelephone?.Id;
```

- [ ] **Step 6: Write a failing test for HomePhoneFixtureId wiring**

In `stakeout.tests/PersonGeneratorTests.cs`, add:
```csharp
[Fact]
public void GeneratedPerson_AtSuburbanHome_HasHomePhoneFixtureId()
{
    AddressTemplateRegistry.RegisterAll();
    BusinessTemplateRegistry.RegisterAll();
    var state = new SimulationState(new GameClock());
    var city = new CityEntity { Id = state.GenerateEntityId(), Name = "Test", CountryName = "US" };
    state.Cities[city.Id] = city;
    state.CityGrids[city.Id] = new CityGenerator(seed: 1).Generate(state, city);

    var person = new PersonGenerator(new MapConfig()).GeneratePerson(state);

    Assert.NotNull(person.HomePhoneFixtureId);
    var fixture = state.Fixtures[person.HomePhoneFixtureId.Value];
    Assert.Equal(Stakeout.Simulation.Fixtures.FixtureType.Telephone, fixture.Type);
}
```

- [ ] **Step 7: Run the test to verify it fails**

```
dotnet test stakeout.tests/ --filter "GeneratedPerson_AtSuburbanHome_HasHomePhoneFixtureId" -v minimal
```
Expected: FAIL (HomePhoneFixtureId is null — GetFixturesForAddress may need adjustment or templates not yet adding telephone).

- [ ] **Step 8: Fix any issues and run until green**

```
dotnet test stakeout.tests/ --filter "GeneratedPerson_AtSuburbanHome_HasHomePhoneFixtureId" -v minimal
```
Expected: PASS.

- [ ] **Step 9: Run full test suite**

```
dotnet test stakeout.tests/ -v minimal
```
Expected: all existing tests still pass.

- [ ] **Step 10: Commit**

```
git add src/simulation/objectives/ObjectiveSource.cs src/simulation/fixtures/FixtureType.cs src/simulation/addresses/SuburbanHomeTemplate.cs src/simulation/addresses/ApartmentBuildingTemplate.cs src/simulation/addresses/DiveBarTemplate.cs src/simulation/addresses/OfficeTemplate.cs src/simulation/PersonGenerator.cs stakeout.tests/PersonGeneratorTests.cs
git commit -m "feat: add Telephone fixture to address templates, wire HomePhoneFixtureId in PersonGenerator"
```

---

## Task 3: IAction.OnSuspend/OnResume + ActionSequence + Objective.OnActionCompletedWithState

**Files:**
- Modify: `src/simulation/actions/IAction.cs`
- Modify: `src/simulation/actions/ActionSequence.cs`
- Modify: `src/simulation/objectives/Objective.cs`
- Modify: `src/simulation/actions/ActionRunner.cs`

- [ ] **Step 1: Add OnSuspend/OnResume to IAction interface**

Replace `src/simulation/actions/IAction.cs`:
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
    void OnSuspend(ActionContext ctx) { }
    void OnResume(ActionContext ctx) { }
}
```

- [ ] **Step 2: Implement OnSuspend/OnResume in ActionSequence**

In `src/simulation/actions/ActionSequence.cs`, add a field after `private IAction _currentAction;`:
```csharp
private int _savedStepIndex;
```

Add these methods after `public void OnComplete(ActionContext ctx) { }`:
```csharp
public void OnSuspend(ActionContext ctx)
{
    _savedStepIndex = _currentStepIndex;
    _currentAction?.OnSuspend(ctx);
}

public void OnResume(ActionContext ctx)
{
    _currentStepIndex = _savedStepIndex;
    AdvanceToNextRunnableStep(ctx);
}
```

- [ ] **Step 3: Add OnActionCompletedWithState to Objective base**

In `src/simulation/objectives/Objective.cs`, add after `public virtual void OnActionCompleted(PlannedAction action, bool success) { }`:
```csharp
public virtual void OnActionCompletedWithState(
    PlannedAction action, Person person, SimulationState state, bool success) { }
```

- [ ] **Step 4: Call OnActionCompletedWithState from ActionRunner**

In `src/simulation/actions/ActionRunner.cs`, find the block that calls `obj.OnActionCompleted`:
```csharp
obj.OnActionCompleted(entry.PlannedAction, status == ActionStatus.Completed);
if (status == ActionStatus.Completed)
    obj.EmitTraces(entry.PlannedAction, person, state);
```
Change to:
```csharp
var success = status == ActionStatus.Completed;
obj.OnActionCompleted(entry.PlannedAction, success);
obj.OnActionCompletedWithState(entry.PlannedAction, person, state, success);
if (success)
    obj.EmitTraces(entry.PlannedAction, person, state);
```

- [ ] **Step 5: Build and run tests**

```
dotnet test stakeout.tests/ -v minimal
```
Expected: all pass (default no-op implementations keep existing tests green).

- [ ] **Step 6: Commit**

```
git add src/simulation/actions/IAction.cs src/simulation/actions/ActionSequence.cs src/simulation/objectives/Objective.cs src/simulation/actions/ActionRunner.cs
git commit -m "feat: add OnSuspend/OnResume to IAction, OnActionCompletedWithState to Objective"
```

---

## Task 4: ActionRunner — NeedsReplan + Invitation Interrupt + Arriving-Home Check

**Files:**
- Modify: `src/simulation/actions/ActionRunner.cs`
- Test: `stakeout.tests/Simulation/Actions/ActionRunnerTests.cs` (new test file or add to existing)

- [ ] **Step 1: Write failing test for NeedsReplan**

Create `stakeout.tests/Simulation/Actions/ActionRunnerNeedsReplanTests.cs`:
```csharp
using System;
using Stakeout.Simulation;
using Stakeout.Simulation.Actions;
using Stakeout.Simulation.Brain;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Objectives;
using Xunit;

namespace Stakeout.Tests.Simulation.Actions;

public class ActionRunnerNeedsReplanTests
{
    [Fact]
    public void NeedsReplan_True_CausesNewDayPlan_OnNextTick()
    {
        var state = new SimulationState(new GameClock(new DateTime(1984, 1, 2, 12, 0, 0)));
        var mapConfig = new MapConfig();

        var homeAddr = new Address { Id = state.GenerateEntityId(), Type = AddressType.SuburbanHome, GridX = 5, GridY = 5 };
        state.Addresses[homeAddr.Id] = homeAddr;

        var person = new Person
        {
            Id = state.GenerateEntityId(),
            HomeAddressId = homeAddr.Id,
            CurrentAddressId = homeAddr.Id,
            CurrentPosition = homeAddr.Position,
            PreferredSleepTime = TimeSpan.FromHours(23),
            PreferredWakeTime = TimeSpan.FromHours(7)
        };
        person.Objectives.Add(new SleepObjective { Id = state.GenerateEntityId() });
        state.People[person.Id] = person;

        person.DayPlan = NpcBrain.PlanDay(person, state, state.Clock.CurrentTime, mapConfig);
        var originalPlan = person.DayPlan;

        person.NeedsReplan = true;

        var runner = new ActionRunner(mapConfig);
        state.Clock.Tick(60);
        runner.Tick(person, state, TimeSpan.FromMinutes(1));

        Assert.False(person.NeedsReplan);
        Assert.NotSame(originalPlan, person.DayPlan);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

```
dotnet test stakeout.tests/ --filter "NeedsReplan_True_CausesNewDayPlan_OnNextTick" -v minimal
```
Expected: FAIL (ActionRunner doesn't handle NeedsReplan yet).

- [ ] **Step 3: Add NeedsReplan handling to ActionRunner.Tick**

In `src/simulation/actions/ActionRunner.cs`, add at the very start of the `Tick` method, before `if (person.DayPlan == null) return;`:
```csharp
if (person.NeedsReplan)
{
    person.NeedsReplan = false;
    person.CurrentActivity = null;  // interrupt current activity without completing
    person.DayPlan = NpcBrain.PlanDay(person, state, state.Clock.CurrentTime, _mapConfig);
}
```

Also add the required using at the top of the file if not present:
```csharp
using Stakeout.Simulation.Brain;
```

- [ ] **Step 4: Run NeedsReplan test to verify it passes**

```
dotnet test stakeout.tests/ --filter "NeedsReplan_True_CausesNewDayPlan_OnNextTick" -v minimal
```
Expected: PASS.

- [ ] **Step 5: Write failing test for invitation interrupt**

Add to `stakeout.tests/Simulation/Actions/ActionRunnerNeedsReplanTests.cs`:
```csharp
[Fact]
public void PendingInvitation_AtActionBoundary_InjectsAcceptPhoneCallAction()
{
    var state = new SimulationState(new GameClock(new DateTime(1984, 1, 2, 12, 0, 0)));
    var mapConfig = new MapConfig();

    var homeAddr = new Address { Id = state.GenerateEntityId(), Type = AddressType.SuburbanHome, GridX = 5, GridY = 5 };
    state.Addresses[homeAddr.Id] = homeAddr;

    var person = new Person
    {
        Id = state.GenerateEntityId(),
        HomeAddressId = homeAddr.Id,
        CurrentAddressId = homeAddr.Id,
        CurrentPosition = homeAddr.Position,
        PreferredSleepTime = TimeSpan.FromHours(23),
        PreferredWakeTime = TimeSpan.FromHours(7)
    };
    person.Objectives.Add(new SleepObjective { Id = state.GenerateEntityId() });
    state.People[person.Id] = person;

    // Create a forming group so AcceptPhoneCallAction can find it
    var group = new Stakeout.Simulation.Entities.Group
    {
        Id = state.GenerateEntityId(),
        Type = Stakeout.Simulation.Entities.GroupType.Date,
        Status = Stakeout.Simulation.Entities.GroupStatus.Forming,
        MemberPersonIds = new System.Collections.Generic.List<int> { 999, person.Id }
    };
    state.Groups[group.Id] = group;

    var inv = new Stakeout.Simulation.Entities.PendingInvitation
    {
        Id = state.GenerateEntityId(),
        FromPersonId = 999,
        ToPersonId = person.Id,
        Type = Stakeout.Simulation.Entities.InvitationType.DateInvitation,
        ProposedGroupId = group.Id,
        CreatedAt = state.Clock.CurrentTime
    };
    state.AddPendingInvitation(inv);

    person.DayPlan = NpcBrain.PlanDay(person, state, state.Clock.CurrentTime, mapConfig);

    var runner = new ActionRunner(mapConfig);
    // Tick enough for the first plan entry to start and complete
    for (int i = 0; i < 120; i++)
    {
        state.Clock.Tick(60);
        runner.Tick(person, state, TimeSpan.FromMinutes(1));
        if (person.CurrentActivity?.Name == "AcceptPhoneCall")
            break;
    }

    Assert.Equal("AcceptPhoneCall", person.CurrentActivity?.Name);
    Assert.Empty(state.PendingInvitationsByPersonId.GetValueOrDefault(person.Id) 
        ?? new System.Collections.Generic.List<Stakeout.Simulation.Entities.PendingInvitation>());
}
```

- [ ] **Step 6: Run test to verify it fails**

```
dotnet test stakeout.tests/ --filter "PendingInvitation_AtActionBoundary_InjectsAcceptPhoneCallAction" -v minimal
```
Expected: FAIL (invitation interrupt not implemented yet).

- [ ] **Step 7: Add invitation interrupt to ActionRunner**

In `src/simulation/actions/ActionRunner.cs`, add this private method:
```csharp
private bool TryInjectPendingInvitation(Person person, SimulationState state)
{
    if (!state.PendingInvitationsByPersonId.TryGetValue(person.Id, out var invitations)
        || invitations.Count == 0)
        return false;

    var inv = invitations[0];
    invitations.RemoveAt(0);

    var action = new Telephone.AcceptPhoneCallAction(inv.Id);
    var ctx = CreateContext(person, state);
    action.OnStart(ctx);
    person.CurrentActivity = action;

    state.Journal.Append(new SimulationEvent
    {
        Timestamp = state.Clock.CurrentTime,
        PersonId = person.Id,
        EventType = SimulationEventType.ActivityStarted,
        AddressId = person.CurrentAddressId,
        Description = action.DisplayText
    });

    return true;
}
```

Add `using Stakeout.Simulation.Actions.Telephone;` at the top of `ActionRunner.cs`.

In `StartNextEntry`, add a call to `TryInjectPendingInvitation` at the very start:
```csharp
private void StartNextEntry(Person person, SimulationState state)
{
    if (TryInjectPendingInvitation(person, state))
        return;

    var entry = person.DayPlan.Current;
    // ... rest of existing code unchanged
```

- [ ] **Step 8: Add arriving-home check to ActionRunner**

In `UpdateTravel`, after `person.TravelInfo = null;` and before the journal append, add:
```csharp
// Check for answering machine messages when arriving home
if (travel.ToAddressId == person.HomeAddressId && person.HomePhoneFixtureId.HasValue)
{
    var messageTraces = state.GetTracesForFixture(person.HomePhoneFixtureId.Value, state.Clock.CurrentTime)
        .Where(t => t.Type == Stakeout.Simulation.Traces.TraceType.Record
                 && t.Description != null
                 && t.Description.Contains("please call back"))
        .ToList();

    if (messageTraces.Count > 0)
    {
        // Queue a CheckAnsweringMachineAction by adding a pending "check machine" flag
        // We piggyback on the invitation mechanism: inject directly into StartNextEntry
        _pendingAnsweringMachineCheck[person.Id] = true;
    }
}
```

Add a field to `ActionRunner`: `private readonly Dictionary<int, bool> _pendingAnsweringMachineCheck = new();`

Update `TryInjectPendingInvitation` to check for answering machine first (or add a separate method). Add after the invitation check in `StartNextEntry`:
```csharp
private void StartNextEntry(Person person, SimulationState state)
{
    if (TryInjectPendingInvitation(person, state))
        return;

    if (TryInjectAnsweringMachineCheck(person, state))
        return;

    // ... existing code
}

private bool TryInjectAnsweringMachineCheck(Person person, SimulationState state)
{
    if (!_pendingAnsweringMachineCheck.TryGetValue(person.Id, out var pending) || !pending)
        return false;

    _pendingAnsweringMachineCheck[person.Id] = false;

    var action = new Telephone.CheckAnsweringMachineAction();
    var ctx = CreateContext(person, state);
    action.OnStart(ctx);
    person.CurrentActivity = action;

    state.Journal.Append(new SimulationEvent
    {
        Timestamp = state.Clock.CurrentTime,
        PersonId = person.Id,
        EventType = SimulationEventType.ActivityStarted,
        AddressId = person.CurrentAddressId,
        Description = action.DisplayText
    });

    return true;
}
```

- [ ] **Step 9: Run all tests**

```
dotnet test stakeout.tests/ -v minimal
```
Expected: all pass (including the new ActionRunner tests).

- [ ] **Step 10: Commit**

```
git add src/simulation/actions/ActionRunner.cs stakeout.tests/Simulation/Actions/ActionRunnerNeedsReplanTests.cs
git commit -m "feat: ActionRunner NeedsReplan handling, invitation interrupt, arriving-home answering machine check"
```

---

## Task 5: PhoneCallAction

**Files:**
- Create: `src/simulation/actions/telephone/PhoneCallAction.cs`
- Test: `stakeout.tests/Simulation/Actions/PhoneCallActionTests.cs`

- [ ] **Step 1: Write failing tests**

Create `stakeout.tests/Simulation/Actions/PhoneCallActionTests.cs`:
```csharp
using System;
using System.Linq;
using Stakeout.Simulation;
using Stakeout.Simulation.Actions;
using Stakeout.Simulation.Actions.Telephone;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Fixtures;
using Stakeout.Simulation.Traces;
using Xunit;

namespace Stakeout.Tests.Simulation.Actions;

public class PhoneCallActionTests
{
    private static (SimulationState state, Person caller, Person recipient, Fixture phone, Group group)
        BuildScene(bool recipientHome)
    {
        var state = new SimulationState(new GameClock(new DateTime(1984, 1, 2, 12, 0, 0)));

        var callerHome = new Address { Id = state.GenerateEntityId(), GridX = 2, GridY = 2 };
        var recipientHome = new Address { Id = state.GenerateEntityId(), GridX = 8, GridY = 8 };
        state.Addresses[callerHome.Id] = callerHome;
        state.Addresses[recipientHome.Id] = recipientHome;

        var loc = new Location { Id = state.GenerateEntityId(), AddressId = recipientHome.Id };
        state.Locations[loc.Id] = loc;
        recipientHome.LocationIds.Add(loc.Id);

        var phone = new Fixture { Id = state.GenerateEntityId(), Type = FixtureType.Telephone, LocationId = loc.Id };
        state.Fixtures[phone.Id] = phone;

        var callerPerson = new Person
        {
            Id = state.GenerateEntityId(),
            FirstName = "Guy",
            LastName = "Test",
            HomeAddressId = callerHome.Id,
            CurrentAddressId = callerHome.Id,
            HomePhoneFixtureId = null  // caller's phone not needed for these tests
        };
        state.People[callerPerson.Id] = callerPerson;

        var recipientPerson = new Person
        {
            Id = state.GenerateEntityId(),
            FirstName = "Girl",
            LastName = "Test",
            HomeAddressId = recipientHome.Id,
            CurrentAddressId = recipientHome ? recipientHome.Id : callerHome.Id  // away or home
        };
        state.People[recipientPerson.Id] = recipientPerson;

        var group = new Group
        {
            Id = state.GenerateEntityId(),
            Status = GroupStatus.Forming,
            MemberPersonIds = new System.Collections.Generic.List<int> { callerPerson.Id, recipientPerson.Id }
        };
        state.Groups[group.Id] = group;

        return (state, callerPerson, recipientPerson, phone, group);
    }

    [Fact]
    public void PhoneCallAction_RecipientHome_CreatesPendingInvitation()
    {
        var (state, caller, recipient, phone, group) = BuildScene(recipientHome: true);

        var action = new PhoneCallAction(
            targetAddressId: recipient.HomeAddressId,
            targetFixtureId: phone.Id,
            proposedGroupId: group.Id,
            callerId: caller.Id,
            recipientId: recipient.Id);

        var ctx = new ActionContext
        {
            Person = caller,
            State = state,
            EventJournal = state.Journal,
            Random = new Random(1),
            CurrentTime = state.Clock.CurrentTime
        };

        action.OnStart(ctx);
        // Tick 10 minutes to complete the call
        var status = ActionStatus.Running;
        for (int i = 0; i < 11 && status == ActionStatus.Running; i++)
            status = action.Tick(ctx, TimeSpan.FromMinutes(1));
        action.OnComplete(ctx);

        Assert.True(state.PendingInvitationsByPersonId.ContainsKey(recipient.Id));
        Assert.Single(state.PendingInvitationsByPersonId[recipient.Id]);

        var phoneTraces = state.GetTracesForFixture(phone.Id, state.Clock.CurrentTime);
        Assert.NotEmpty(phoneTraces);
    }

    [Fact]
    public void PhoneCallAction_RecipientAbsent_LeavesMessageTrace()
    {
        var (state, caller, recipient, phone, group) = BuildScene(recipientHome: false);

        // Give caller an OrganizeDateObjective so OnMessageLeft can be called
        var objective = new Stakeout.Simulation.Objectives.OrganizeDateObjective(
            recipient.Id, 99, DateTime.Now, DateTime.Now.AddHours(7), DateTime.Now.AddHours(5.833));
        objective.Id = state.GenerateEntityId();
        caller.Objectives.Add(objective);

        var action = new PhoneCallAction(
            targetAddressId: recipient.HomeAddressId,
            targetFixtureId: phone.Id,
            proposedGroupId: group.Id,
            callerId: caller.Id,
            recipientId: recipient.Id);

        var ctx = new ActionContext
        {
            Person = caller,
            State = state,
            EventJournal = state.Journal,
            Random = new Random(1),
            CurrentTime = state.Clock.CurrentTime
        };

        action.OnStart(ctx);
        for (int i = 0; i < 11; i++)
            action.Tick(ctx, TimeSpan.FromMinutes(1));
        action.OnComplete(ctx);

        Assert.False(state.PendingInvitationsByPersonId.ContainsKey(recipient.Id));

        var phoneTraces = state.GetTracesForFixture(phone.Id, state.Clock.CurrentTime);
        Assert.Contains(phoneTraces, t => t.Description != null && t.Description.Contains("please call back"));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```
dotnet test stakeout.tests/ --filter "PhoneCallActionTests" -v minimal
```
Expected: FAIL (PhoneCallAction doesn't exist).

- [ ] **Step 3: Create PhoneCallAction**

Create `src/simulation/actions/telephone/PhoneCallAction.cs`:
```csharp
using System;
using System.Linq;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Objectives;
using Stakeout.Simulation.Traces;

namespace Stakeout.Simulation.Actions.Telephone;

public class PhoneCallAction : IAction
{
    private readonly int _targetAddressId;
    private readonly int _targetFixtureId;
    private readonly int _proposedGroupId;
    private readonly int _callerId;
    private readonly int _recipientId;
    private TimeSpan _elapsed = TimeSpan.Zero;
    private static readonly TimeSpan CallDuration = TimeSpan.FromMinutes(10);

    public string Name => "PhoneCall";
    public string DisplayText => "Making a phone call";

    public PhoneCallAction(int targetAddressId, int targetFixtureId, int proposedGroupId,
        int callerId, int recipientId)
    {
        _targetAddressId = targetAddressId;
        _targetFixtureId = targetFixtureId;
        _proposedGroupId = proposedGroupId;
        _callerId = callerId;
        _recipientId = recipientId;
    }

    public void OnStart(ActionContext ctx) { }

    public ActionStatus Tick(ActionContext ctx, TimeSpan delta)
    {
        _elapsed += delta;
        return _elapsed >= CallDuration ? ActionStatus.Completed : ActionStatus.Running;
    }

    public void OnComplete(ActionContext ctx)
    {
        // Outgoing record on caller's home phone
        if (ctx.Person.HomePhoneFixtureId.HasValue)
            TraceEmitter.EmitRecord(ctx.State, ctx.Person.HomePhoneFixtureId.Value,
                ctx.Person.Id, $"Outgoing call to {ctx.State.Addresses[_targetAddressId].Id}");

        // Incoming record on target phone
        TraceEmitter.EmitRecord(ctx.State, _targetFixtureId, ctx.Person.Id,
            $"Incoming call from {ctx.Person.FirstName} {ctx.Person.LastName}");

        var recipient = ctx.State.People[_recipientId];
        if (recipient.CurrentAddressId == _targetAddressId)
        {
            // Recipient is home — create pending invitation
            var inv = new PendingInvitation
            {
                Id = ctx.State.GenerateEntityId(),
                FromPersonId = _callerId,
                ToPersonId = _recipientId,
                Type = InvitationType.DateInvitation,
                ProposedGroupId = _proposedGroupId,
                CreatedAt = ctx.CurrentTime
            };
            ctx.State.AddPendingInvitation(inv);
        }
        else
        {
            // Recipient absent — leave message trace
            TraceEmitter.EmitRecord(ctx.State, _targetFixtureId, ctx.Person.Id,
                $"Message from {ctx.Person.FirstName} {ctx.Person.LastName}: please call back");

            // Notify OrganizeDateObjective
            var obj = ctx.Person.Objectives
                .OfType<OrganizeDateObjective>()
                .FirstOrDefault(o => o.TargetPersonId == _recipientId);
            obj?.OnMessageLeft();
        }
    }
}
```

- [ ] **Step 4: Run tests**

```
dotnet test stakeout.tests/ --filter "PhoneCallActionTests" -v minimal
```
Expected: PASS (both tests). If `OrganizeDateObjective.OnMessageLeft()` doesn't exist yet, the test may fail — add a placeholder `OnMessageLeft()` stub to `OrganizeDateObjective` (see Task 7) or stub it as an empty partial class to unblock.

- [ ] **Step 5: Commit**

```
git add src/simulation/actions/telephone/PhoneCallAction.cs stakeout.tests/Simulation/Actions/PhoneCallActionTests.cs
git commit -m "feat: PhoneCallAction — creates pending invitation or leaves message trace"
```

---

## Task 6: AcceptPhoneCallAction

**Files:**
- Create: `src/simulation/actions/telephone/AcceptPhoneCallAction.cs`
- Test: `stakeout.tests/Simulation/Actions/AcceptPhoneCallActionTests.cs`

- [ ] **Step 1: Write failing test**

Create `stakeout.tests/Simulation/Actions/AcceptPhoneCallActionTests.cs`:
```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Stakeout.Simulation;
using Stakeout.Simulation.Actions;
using Stakeout.Simulation.Actions.Telephone;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Objectives;
using Xunit;

namespace Stakeout.Tests.Simulation.Actions;

public class AcceptPhoneCallActionTests
{
    [Fact]
    public void AcceptPhoneCallAction_CallsOnAccepted_OnCallerOrganizeDateObjective()
    {
        var state = new SimulationState(new GameClock(new DateTime(1984, 1, 2, 14, 0, 0)));

        var callerHome = new Address { Id = state.GenerateEntityId(), GridX = 2, GridY = 2 };
        var recipientHome = new Address { Id = state.GenerateEntityId(), GridX = 8, GridY = 8 };
        var diner = new Address { Id = state.GenerateEntityId(), GridX = 5, GridY = 5 };
        state.Addresses[callerHome.Id] = callerHome;
        state.Addresses[recipientHome.Id] = recipientHome;
        state.Addresses[diner.Id] = diner;

        var caller = new Person
        {
            Id = state.GenerateEntityId(),
            HomeAddressId = callerHome.Id,
            CurrentAddressId = callerHome.Id
        };
        var recipient = new Person
        {
            Id = state.GenerateEntityId(),
            HomeAddressId = recipientHome.Id,
            CurrentAddressId = recipientHome.Id
        };
        state.People[caller.Id] = caller;
        state.People[recipient.Id] = recipient;

        var group = new Group
        {
            Id = state.GenerateEntityId(),
            Type = GroupType.Date,
            Status = GroupStatus.Forming,
            DriverPersonId = caller.Id,
            PickupAddressId = recipientHome.Id,
            PickupTime = new DateTime(1984, 1, 2, 17, 50, 0),
            MeetupAddressId = diner.Id,
            MeetupTime = new DateTime(1984, 1, 2, 19, 0, 0),
            MemberPersonIds = new List<int> { caller.Id, recipient.Id }
        };
        state.Groups[group.Id] = group;

        var organizeObj = new OrganizeDateObjective(
            recipient.Id, diner.Id,
            new DateTime(1984, 1, 2, 12, 0, 0),
            new DateTime(1984, 1, 2, 19, 0, 0),
            new DateTime(1984, 1, 2, 17, 50, 0))
        {
            Id = state.GenerateEntityId()
        };
        // Manually set the group ID on the objective (it creates one in GetActions but we need it pre-set)
        organizeObj.SetGroupId(group.Id);
        caller.Objectives.Add(organizeObj);

        var inv = new PendingInvitation
        {
            Id = state.GenerateEntityId(),
            FromPersonId = caller.Id,
            ToPersonId = recipient.Id,
            Type = InvitationType.DateInvitation,
            ProposedGroupId = group.Id,
            CreatedAt = state.Clock.CurrentTime
        };
        state.AddPendingInvitation(inv);

        var action = new AcceptPhoneCallAction(inv.Id);
        var ctx = new ActionContext
        {
            Person = recipient,
            State = state,
            EventJournal = state.Journal,
            Random = new Random(1),
            CurrentTime = state.Clock.CurrentTime
        };

        action.OnStart(ctx);
        for (int i = 0; i < 3; i++)
            action.Tick(ctx, TimeSpan.FromMinutes(1));
        action.OnComplete(ctx);

        // Invitation should be consumed
        Assert.Empty(state.PendingInvitationsByPersonId.GetValueOrDefault(recipient.Id)
            ?? new List<PendingInvitation>());

        // Group should be active
        Assert.Equal(GroupStatus.Active, group.Status);

        // Both people should have GoOnDateObjective
        Assert.Contains(caller.Objectives, o => o is GoOnDateObjective);
        Assert.Contains(recipient.Objectives, o => o is GoOnDateObjective);

        // Both should have NeedsReplan
        Assert.True(caller.NeedsReplan);
        Assert.True(recipient.NeedsReplan);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

```
dotnet test stakeout.tests/ --filter "AcceptPhoneCallActionTests" -v minimal
```
Expected: FAIL.

- [ ] **Step 3: Create AcceptPhoneCallAction**

Create `src/simulation/actions/telephone/AcceptPhoneCallAction.cs`:
```csharp
using System;
using System.Linq;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Objectives;

namespace Stakeout.Simulation.Actions.Telephone;

public class AcceptPhoneCallAction : IAction
{
    private readonly int _invitationId;
    private TimeSpan _elapsed = TimeSpan.Zero;
    private static readonly TimeSpan CallDuration = TimeSpan.FromMinutes(2);

    public string Name => "AcceptPhoneCall";
    public string DisplayText => "Talking on the phone";

    public AcceptPhoneCallAction(int invitationId)
    {
        _invitationId = invitationId;
    }

    public void OnStart(ActionContext ctx) { }

    public ActionStatus Tick(ActionContext ctx, TimeSpan delta)
    {
        _elapsed += delta;
        return _elapsed >= CallDuration ? ActionStatus.Completed : ActionStatus.Running;
    }

    public void OnComplete(ActionContext ctx)
    {
        // Find and consume the invitation
        if (!ctx.State.PendingInvitationsByPersonId.TryGetValue(ctx.Person.Id, out var invitations))
            return;
        var inv = invitations.FirstOrDefault(i => i.Id == _invitationId);
        if (inv == null) return;
        invitations.Remove(inv);

        // Find the OrganizeDateObjective on the caller that targets this person
        var caller = ctx.State.People[inv.FromPersonId];
        var objective = caller.Objectives
            .OfType<OrganizeDateObjective>()
            .FirstOrDefault(o => o.TargetPersonId == ctx.Person.Id)
            // Callback scenario: current person (caller) has objective targeting the FromPerson
            ?? ctx.Person.Objectives
                .OfType<OrganizeDateObjective>()
                .FirstOrDefault(o => o.TargetPersonId == inv.FromPersonId);

        objective?.OnAccepted(ctx.CurrentTime, ctx.State);
    }
}
```

- [ ] **Step 4: Run test**

```
dotnet test stakeout.tests/ --filter "AcceptPhoneCallActionTests" -v minimal
```
Expected: PASS (once OrganizeDateObjective.OnAccepted and SetGroupId are implemented in Task 7).

- [ ] **Step 5: Commit (after Task 7 makes the test pass)**

```
git add src/simulation/actions/telephone/AcceptPhoneCallAction.cs stakeout.tests/Simulation/Actions/AcceptPhoneCallActionTests.cs
git commit -m "feat: AcceptPhoneCallAction — notifies OrganizeDateObjective, triggers NeedsReplan on both"
```

---

## Task 7: OrganizeDateObjective

**Files:**
- Create: `src/simulation/objectives/OrganizeDateObjective.cs`
- Test: `stakeout.tests/Simulation/Objectives/OrganizeDateObjectiveTests.cs`

- [ ] **Step 1: Write failing tests**

Create `stakeout.tests/Simulation/Objectives/OrganizeDateObjectiveTests.cs`:
```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Stakeout.Simulation;
using Stakeout.Simulation.Actions.Telephone;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Objectives;
using Xunit;

namespace Stakeout.Tests.Simulation.Objectives;

public class OrganizeDateObjectiveTests
{
    [Fact]
    public void GetActions_InNeedToCallState_ReturnsPhoneCallAction()
    {
        var state = new SimulationState(new GameClock(new DateTime(1984, 1, 2, 8, 0, 0)));

        var callerHome = new Address { Id = state.GenerateEntityId(), GridX = 2, GridY = 2 };
        var recipientHome = new Address { Id = state.GenerateEntityId(), GridX = 8, GridY = 8 };
        var loc = new Location { Id = state.GenerateEntityId(), AddressId = recipientHome.Id };
        var phone = new Stakeout.Simulation.Fixtures.Fixture
        {
            Id = state.GenerateEntityId(),
            Type = Stakeout.Simulation.Fixtures.FixtureType.Telephone,
            LocationId = loc.Id
        };
        state.Addresses[callerHome.Id] = callerHome;
        state.Addresses[recipientHome.Id] = recipientHome;
        state.Locations[loc.Id] = loc;
        recipientHome.LocationIds.Add(loc.Id);
        state.Fixtures[phone.Id] = phone;

        var caller = new Person
        {
            Id = state.GenerateEntityId(),
            HomeAddressId = callerHome.Id,
            CurrentAddressId = callerHome.Id
        };
        var recipient = new Person
        {
            Id = state.GenerateEntityId(),
            HomeAddressId = recipientHome.Id,
            HomePhoneFixtureId = phone.Id
        };
        state.People[caller.Id] = caller;
        state.People[recipient.Id] = recipient;

        var callTime = new DateTime(1984, 1, 2, 12, 0, 0);
        var meetupTime = new DateTime(1984, 1, 2, 19, 0, 0);
        var pickupTime = new DateTime(1984, 1, 2, 17, 50, 0);

        var objective = new OrganizeDateObjective(recipient.Id, 99, callTime, meetupTime, pickupTime)
        { Id = state.GenerateEntityId() };
        caller.Objectives.Add(objective);

        var actions = objective.GetActions(caller, state,
            new DateTime(1984, 1, 2, 8, 0, 0), new DateTime(1984, 1, 2, 23, 59, 0));

        Assert.Single(actions);
        Assert.IsType<PhoneCallAction>(actions[0].Action);
        Assert.Equal(recipientHome.Id, actions[0].TargetAddressId);
    }

    [Fact]
    public void OnAccepted_TodayStillFeasible_CreatesGoOnDateObjectiveForBoth()
    {
        var state = new SimulationState(new GameClock(new DateTime(1984, 1, 2, 14, 0, 0)));

        var callerHome = new Address { Id = state.GenerateEntityId(), GridX = 2, GridY = 2 };
        var recipientHome = new Address { Id = state.GenerateEntityId(), GridX = 8, GridY = 8 };
        var diner = new Address { Id = state.GenerateEntityId(), GridX = 5, GridY = 5 };
        state.Addresses[callerHome.Id] = callerHome;
        state.Addresses[recipientHome.Id] = recipientHome;
        state.Addresses[diner.Id] = diner;

        var caller = new Person
        {
            Id = state.GenerateEntityId(),
            HomeAddressId = callerHome.Id,
            CurrentAddressId = callerHome.Id,
            PreferredSleepTime = TimeSpan.FromHours(23),
            PreferredWakeTime = TimeSpan.FromHours(7)
        };
        caller.Objectives.Add(new SleepObjective { Id = state.GenerateEntityId() });
        var recipient = new Person
        {
            Id = state.GenerateEntityId(),
            HomeAddressId = recipientHome.Id,
            CurrentAddressId = recipientHome.Id,
            PreferredSleepTime = TimeSpan.FromHours(23),
            PreferredWakeTime = TimeSpan.FromHours(7)
        };
        recipient.Objectives.Add(new SleepObjective { Id = state.GenerateEntityId() });
        state.People[caller.Id] = caller;
        state.People[recipient.Id] = recipient;

        var meetupTime = new DateTime(1984, 1, 2, 19, 0, 0);  // 7pm — 5 hrs after 2pm, > 2hr buffer
        var objective = new OrganizeDateObjective(recipient.Id, diner.Id,
            new DateTime(1984, 1, 2, 12, 0, 0), meetupTime,
            new DateTime(1984, 1, 2, 17, 50, 0))
        { Id = state.GenerateEntityId() };
        caller.Objectives.Add(objective);

        // First call GetActions so group is created
        objective.GetActions(caller, state, new DateTime(1984, 1, 2, 8, 0, 0), new DateTime(1984, 1, 2, 23, 59, 0));

        objective.OnAccepted(new DateTime(1984, 1, 2, 14, 0, 0), state);

        Assert.Contains(caller.Objectives, o => o is GoOnDateObjective);
        Assert.Contains(recipient.Objectives, o => o is GoOnDateObjective);
        Assert.True(caller.NeedsReplan);
        Assert.True(recipient.NeedsReplan);
        Assert.Equal(ObjectiveStatus.Completed, objective.Status);

        var group = state.Groups.Values.Single();
        Assert.Equal(GroupStatus.Active, group.Status);
        Assert.Equal(meetupTime, group.MeetupTime);
    }

    [Fact]
    public void OnAccepted_TooLateToday_AdvancesToNextDay()
    {
        var state = new SimulationState(new GameClock(new DateTime(1984, 1, 2, 20, 0, 0)));

        var callerHome = new Address { Id = state.GenerateEntityId(), GridX = 2, GridY = 2 };
        var recipientHome = new Address { Id = state.GenerateEntityId(), GridX = 8, GridY = 8 };
        var diner = new Address { Id = state.GenerateEntityId(), GridX = 5, GridY = 5 };
        state.Addresses[callerHome.Id] = callerHome;
        state.Addresses[recipientHome.Id] = recipientHome;
        state.Addresses[diner.Id] = diner;

        var caller = new Person { Id = state.GenerateEntityId(), HomeAddressId = callerHome.Id, CurrentAddressId = callerHome.Id, PreferredSleepTime = TimeSpan.FromHours(23), PreferredWakeTime = TimeSpan.FromHours(7) };
        caller.Objectives.Add(new SleepObjective { Id = state.GenerateEntityId() });
        var recipient = new Person { Id = state.GenerateEntityId(), HomeAddressId = recipientHome.Id, CurrentAddressId = recipientHome.Id, PreferredSleepTime = TimeSpan.FromHours(23), PreferredWakeTime = TimeSpan.FromHours(7) };
        recipient.Objectives.Add(new SleepObjective { Id = state.GenerateEntityId() });
        state.People[caller.Id] = caller;
        state.People[recipient.Id] = recipient;

        var meetupTime = new DateTime(1984, 1, 2, 19, 0, 0);  // 7pm, but it's already 8pm
        var objective = new OrganizeDateObjective(recipient.Id, diner.Id,
            new DateTime(1984, 1, 2, 12, 0, 0), meetupTime,
            new DateTime(1984, 1, 2, 17, 50, 0))
        { Id = state.GenerateEntityId() };
        caller.Objectives.Add(objective);
        objective.GetActions(caller, state, state.Clock.CurrentTime, state.Clock.CurrentTime.AddHours(4));

        objective.OnAccepted(new DateTime(1984, 1, 2, 20, 0, 0), state);

        var group = state.Groups.Values.Single();
        Assert.Equal(new DateTime(1984, 1, 3, 19, 0, 0), group.MeetupTime);  // next day same time
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```
dotnet test stakeout.tests/ --filter "OrganizeDateObjectiveTests" -v minimal
```
Expected: FAIL.

- [ ] **Step 3: Create OrganizeDateObjective**

Create `src/simulation/objectives/OrganizeDateObjective.cs`:
```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Stakeout.Simulation.Actions.Telephone;
using Stakeout.Simulation.Brain;
using Stakeout.Simulation.Entities;

namespace Stakeout.Simulation.Objectives;

public class OrganizeDateObjective : Objective
{
    private enum State { NeedToCall, AwaitingAnswer, MessageLeft, AwaitingCallback, DateOrganized }
    private State _state = State.NeedToCall;
    private int _groupId = -1;

    public int TargetPersonId { get; }
    private readonly int _proposedMeetupAddressId;
    private readonly DateTime _proposedCallTime;
    private readonly DateTime _proposedMeetupTime;
    private readonly DateTime _proposedPickupTime;

    public override int Priority => 50;
    public override ObjectiveSource Source => ObjectiveSource.Social;

    public OrganizeDateObjective(int targetPersonId, int proposedMeetupAddressId,
        DateTime proposedCallTime, DateTime proposedMeetupTime, DateTime proposedPickupTime)
    {
        TargetPersonId = targetPersonId;
        _proposedMeetupAddressId = proposedMeetupAddressId;
        _proposedCallTime = proposedCallTime;
        _proposedMeetupTime = proposedMeetupTime;
        _proposedPickupTime = proposedPickupTime;
    }

    // Used in tests to pre-set the group ID
    public void SetGroupId(int groupId) => _groupId = groupId;

    public override List<PlannedAction> GetActions(Person person, SimulationState state,
        DateTime planStart, DateTime planEnd)
    {
        if (_state != State.NeedToCall) return new List<PlannedAction>();
        if (Status == ObjectiveStatus.Completed) return new List<PlannedAction>();

        // Find recipient's home phone
        var recipient = state.People[TargetPersonId];
        if (!recipient.HomePhoneFixtureId.HasValue) return new List<PlannedAction>();
        var phoneFixture = state.Fixtures[recipient.HomePhoneFixtureId.Value];

        // Create the group (Forming) if not yet created
        if (_groupId < 0)
        {
            var group = new Group
            {
                Id = state.GenerateEntityId(),
                Type = GroupType.Date,
                Status = GroupStatus.Forming,
                DriverPersonId = person.Id,
                PickupAddressId = recipient.HomeAddressId,
                PickupTime = _proposedPickupTime,
                MeetupAddressId = _proposedMeetupAddressId,
                MeetupTime = _proposedMeetupTime,
                MemberPersonIds = new List<int> { person.Id, TargetPersonId }
            };
            state.Groups[group.Id] = group;
            _groupId = group.Id;
        }

        var callAction = new PhoneCallAction(
            targetAddressId: recipient.HomeAddressId,
            targetFixtureId: phoneFixture.Id,
            proposedGroupId: _groupId,
            callerId: person.Id,
            recipientId: TargetPersonId);

        _state = State.AwaitingAnswer;

        return new List<PlannedAction>
        {
            new()
            {
                Action = callAction,
                TargetAddressId = person.HomeAddressId,
                TimeWindowStart = _proposedCallTime,
                TimeWindowEnd = _proposedCallTime + TimeSpan.FromHours(2),
                Duration = TimeSpan.FromMinutes(10),
                DisplayText = "calling to arrange a date",
                SourceObjective = this
            }
        };
    }

    public void OnMessageLeft()
    {
        _state = State.MessageLeft;
    }

    public void OnAccepted(DateTime acceptedAt, SimulationState state)
    {
        if (_groupId < 0) return;
        var group = state.Groups[_groupId];

        // Decide if today's time is still feasible (accepted + 2h buffer <= proposed meetup)
        if (acceptedAt.AddHours(2) > _proposedMeetupTime)
        {
            // Advance to next day, same time-of-day
            var nextDay = _proposedMeetupTime.Date.AddDays(1);
            group.MeetupTime = nextDay + _proposedMeetupTime.TimeOfDay;
            group.PickupTime = nextDay + _proposedPickupTime.TimeOfDay;
        }

        group.Status = GroupStatus.Active;

        // Create GoOnDateObjective for both members
        foreach (var memberId in group.MemberPersonIds)
        {
            var member = state.People[memberId];
            if (member.Objectives.OfType<GoOnDateObjective>().Any(o => o.GroupId == _groupId))
                continue;
            member.Objectives.Add(new GoOnDateObjective(_groupId) { Id = state.GenerateEntityId() });
            member.NeedsReplan = true;
        }

        Status = ObjectiveStatus.Completed;
        _state = State.DateOrganized;
    }
}
```

- [ ] **Step 4: Run tests**

```
dotnet test stakeout.tests/ --filter "OrganizeDateObjectiveTests" -v minimal
```
Expected: PASS.

- [ ] **Step 5: Run full suite**

```
dotnet test stakeout.tests/ -v minimal
```
Expected: all pass.

- [ ] **Step 6: Commit**

```
git add src/simulation/objectives/OrganizeDateObjective.cs stakeout.tests/Simulation/Objectives/OrganizeDateObjectiveTests.cs
git commit -m "feat: OrganizeDateObjective — phone coordination state machine"
```

---

## Task 8: GoOnDateObjective

**Files:**
- Create: `src/simulation/objectives/GoOnDateObjective.cs`
- Test: `stakeout.tests/Simulation/Objectives/GoOnDateObjectiveTests.cs`

- [ ] **Step 1: Write failing tests**

Create `stakeout.tests/Simulation/Objectives/GoOnDateObjectiveTests.cs`:
```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Stakeout.Simulation;
using Stakeout.Simulation.Actions.Primitives;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Objectives;
using Xunit;

namespace Stakeout.Tests.Simulation.Objectives;

public class GoOnDateObjectiveTests
{
    private static (SimulationState state, Person driver, Person passenger, Group group, Address diner)
        BuildScene(GroupPhase phase)
    {
        var state = new SimulationState(new GameClock(new DateTime(1984, 1, 2, 14, 0, 0)));

        var driverHome = new Address { Id = state.GenerateEntityId(), GridX = 2, GridY = 2 };
        var passengerHome = new Address { Id = state.GenerateEntityId(), GridX = 8, GridY = 2 };
        var diner = new Address { Id = state.GenerateEntityId(), GridX = 5, GridY = 5 };
        foreach (var a in new[] { driverHome, passengerHome, diner })
            state.Addresses[a.Id] = a;

        var driver = new Person
        {
            Id = state.GenerateEntityId(),
            HomeAddressId = driverHome.Id,
            CurrentAddressId = driverHome.Id,
            CurrentPosition = driverHome.Position,
            PreferredSleepTime = TimeSpan.FromHours(23),
            PreferredWakeTime = TimeSpan.FromHours(7)
        };
        driver.Objectives.Add(new SleepObjective { Id = state.GenerateEntityId() });
        var passenger = new Person
        {
            Id = state.GenerateEntityId(),
            HomeAddressId = passengerHome.Id,
            CurrentAddressId = passengerHome.Id,
            CurrentPosition = passengerHome.Position,
            PreferredSleepTime = TimeSpan.FromHours(23),
            PreferredWakeTime = TimeSpan.FromHours(7)
        };
        passenger.Objectives.Add(new SleepObjective { Id = state.GenerateEntityId() });
        state.People[driver.Id] = driver;
        state.People[passenger.Id] = passenger;

        var group = new Group
        {
            Id = state.GenerateEntityId(),
            Type = GroupType.Date,
            Status = GroupStatus.Active,
            DriverPersonId = driver.Id,
            PickupAddressId = passengerHome.Id,
            PickupTime = new DateTime(1984, 1, 2, 17, 50, 0),
            MeetupAddressId = diner.Id,
            MeetupTime = new DateTime(1984, 1, 2, 19, 0, 0),
            MemberPersonIds = new List<int> { driver.Id, passenger.Id },
            CurrentPhase = phase
        };
        state.Groups[group.Id] = group;

        return (state, driver, passenger, group, diner);
    }

    [Fact]
    public void GetActions_DriverEnRoute_Driver_TargetsPickupAddress()
    {
        var (state, driver, _, group, _) = BuildScene(GroupPhase.DriverEnRoute);
        var obj = new GoOnDateObjective(group.Id) { Id = state.GenerateEntityId() };

        var actions = obj.GetActions(driver, state,
            new DateTime(1984, 1, 2, 14, 0, 0), new DateTime(1984, 1, 2, 23, 59, 0));

        Assert.Single(actions);
        Assert.Equal(group.PickupAddressId, actions[0].TargetAddressId);
        Assert.IsType<WaitAction>(actions[0].Action);
    }

    [Fact]
    public void GetActions_DriverEnRoute_Passenger_TargetsHome()
    {
        var (state, _, passenger, group, _) = BuildScene(GroupPhase.DriverEnRoute);
        var obj = new GoOnDateObjective(group.Id) { Id = state.GenerateEntityId() };

        var actions = obj.GetActions(passenger, state,
            new DateTime(1984, 1, 2, 14, 0, 0), new DateTime(1984, 1, 2, 23, 59, 0));

        Assert.Single(actions);
        Assert.Equal(passenger.HomeAddressId, actions[0].TargetAddressId);
    }

    [Fact]
    public void OnActionCompletedWithState_DriverEnRoute_AdvancesToAtPickup()
    {
        var (state, driver, passenger, group, _) = BuildScene(GroupPhase.DriverEnRoute);
        driver.CurrentAddressId = group.PickupAddressId;  // driver has arrived
        var obj = new GoOnDateObjective(group.Id) { Id = state.GenerateEntityId() };
        driver.Objectives.Add(obj);
        passenger.Objectives.Add(new GoOnDateObjective(group.Id) { Id = state.GenerateEntityId() });

        var dummyAction = new PlannedAction { SourceObjective = obj };
        obj.OnActionCompletedWithState(dummyAction, driver, state, success: true);

        Assert.Equal(GroupPhase.AtPickup, group.CurrentPhase);
        Assert.True(driver.NeedsReplan);
        Assert.True(passenger.NeedsReplan);
    }

    [Fact]
    public void OnActionCompletedWithState_DrivingBack_DisbandsGroup()
    {
        var (state, driver, passenger, group, _) = BuildScene(GroupPhase.DrivingBack);
        group.CurrentPhase = GroupPhase.DrivingBack;
        var driverObj = new GoOnDateObjective(group.Id) { Id = state.GenerateEntityId() };
        var passengerObj = new GoOnDateObjective(group.Id) { Id = state.GenerateEntityId() };
        driver.Objectives.Add(driverObj);
        passenger.Objectives.Add(passengerObj);

        driverObj.OnActionCompletedWithState(new PlannedAction { SourceObjective = driverObj }, driver, state, true);

        Assert.Equal(GroupPhase.Complete, group.CurrentPhase);
        Assert.Equal(GroupStatus.Disbanded, group.Status);
        Assert.DoesNotContain(driver.Objectives, o => o is GoOnDateObjective);
        Assert.DoesNotContain(passenger.Objectives, o => o is GoOnDateObjective);
        Assert.True(driver.NeedsReplan);
        Assert.True(passenger.NeedsReplan);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```
dotnet test stakeout.tests/ --filter "GoOnDateObjectiveTests" -v minimal
```
Expected: FAIL.

- [ ] **Step 3: Create GoOnDateObjective**

Create `src/simulation/objectives/GoOnDateObjective.cs`:
```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Stakeout.Simulation.Actions.Primitives;
using Stakeout.Simulation.Entities;

namespace Stakeout.Simulation.Objectives;

public class GoOnDateObjective : Objective
{
    public int GroupId { get; }

    public override int Priority => 70;
    public override ObjectiveSource Source => ObjectiveSource.Social;

    public GoOnDateObjective(int groupId)
    {
        GroupId = groupId;
    }

    public override List<PlannedAction> GetActions(Person person, SimulationState state,
        DateTime planStart, DateTime planEnd)
    {
        if (!state.Groups.TryGetValue(GroupId, out var group)) return new List<PlannedAction>();
        if (group.Status == GroupStatus.Disbanded) return new List<PlannedAction>();

        bool isDriver = person.Id == group.DriverPersonId;

        return group.CurrentPhase switch
        {
            GroupPhase.DriverEnRoute => GetDriverEnRouteActions(person, group, isDriver, planStart, planEnd),
            GroupPhase.AtPickup => GetAtPickupActions(group, planStart),
            GroupPhase.DrivingToVenue => GetDrivingToVenueActions(group, planStart),
            GroupPhase.AtVenue => GetAtVenueActions(group, planStart),
            GroupPhase.DrivingBack => GetDrivingBackActions(person, group, planStart),
            _ => new List<PlannedAction>()
        };
    }

    private List<PlannedAction> GetDriverEnRouteActions(Person person, Group group, bool isDriver,
        DateTime planStart, DateTime planEnd)
    {
        if (isDriver)
        {
            return new List<PlannedAction>
            {
                new()
                {
                    Action = new WaitAction(TimeSpan.FromMinutes(1), "picking up date"),
                    TargetAddressId = group.PickupAddressId,
                    TimeWindowStart = group.PickupTime,
                    TimeWindowEnd = group.PickupTime + TimeSpan.FromHours(2),
                    Duration = TimeSpan.FromMinutes(1),
                    DisplayText = "picking up date",
                    SourceObjective = this
                }
            };
        }
        else
        {
            // Passenger: wait at home until pickup time
            var waitDuration = group.PickupTime - planStart;
            if (waitDuration <= TimeSpan.Zero) waitDuration = TimeSpan.FromMinutes(1);
            return new List<PlannedAction>
            {
                new()
                {
                    Action = new WaitAction(waitDuration, "waiting to be picked up"),
                    TargetAddressId = person.HomeAddressId,
                    TimeWindowStart = planStart,
                    TimeWindowEnd = group.PickupTime + TimeSpan.FromHours(1),
                    Duration = waitDuration,
                    DisplayText = "waiting to be picked up",
                    SourceObjective = this
                }
            };
        }
    }

    private List<PlannedAction> GetAtPickupActions(Group group, DateTime planStart)
    {
        return new List<PlannedAction>
        {
            new()
            {
                Action = new WaitAction(TimeSpan.FromMinutes(1), "getting in the car"),
                TargetAddressId = group.PickupAddressId,
                TimeWindowStart = planStart,
                TimeWindowEnd = planStart + TimeSpan.FromHours(2),
                Duration = TimeSpan.FromMinutes(1),
                DisplayText = "getting in the car",
                SourceObjective = this
            }
        };
    }

    private List<PlannedAction> GetDrivingToVenueActions(Group group, DateTime planStart)
    {
        return new List<PlannedAction>
        {
            new()
            {
                Action = new WaitAction(TimeSpan.FromMinutes(1), "driving to dinner"),
                TargetAddressId = group.MeetupAddressId,
                TimeWindowStart = planStart,
                TimeWindowEnd = planStart + TimeSpan.FromHours(3),
                Duration = TimeSpan.FromMinutes(1),
                DisplayText = "driving to dinner",
                SourceObjective = this
            }
        };
    }

    private List<PlannedAction> GetAtVenueActions(Group group, DateTime planStart)
    {
        return new List<PlannedAction>
        {
            new()
            {
                Action = new WaitAction(TimeSpan.FromHours(2), "having dinner"),
                TargetAddressId = group.MeetupAddressId,
                TimeWindowStart = group.MeetupTime,
                TimeWindowEnd = group.MeetupTime + TimeSpan.FromHours(4),
                Duration = TimeSpan.FromHours(2),
                DisplayText = "having dinner",
                SourceObjective = this
            }
        };
    }

    private List<PlannedAction> GetDrivingBackActions(Person person, Group group, DateTime planStart)
    {
        return new List<PlannedAction>
        {
            new()
            {
                Action = new WaitAction(TimeSpan.FromMinutes(1), "heading home"),
                TargetAddressId = group.PickupAddressId,  // passenger's home = pickup address
                TimeWindowStart = planStart,
                TimeWindowEnd = planStart + TimeSpan.FromHours(4),
                Duration = TimeSpan.FromMinutes(1),
                DisplayText = "heading home",
                SourceObjective = this
            }
        };
    }

    public override void OnActionCompletedWithState(PlannedAction action, Person person,
        SimulationState state, bool success)
    {
        if (!success) return;
        if (!state.Groups.TryGetValue(GroupId, out var group)) return;

        bool isDriver = person.Id == group.DriverPersonId;
        if (!isDriver) return;  // only driver advances the phase

        var passengerId = group.MemberPersonIds.First(id => id != person.Id);
        var passenger = state.People[passengerId];

        switch (group.CurrentPhase)
        {
            case GroupPhase.DriverEnRoute:
                group.CurrentPhase = GroupPhase.AtPickup;
                person.NeedsReplan = true;
                passenger.NeedsReplan = true;
                break;

            case GroupPhase.AtPickup:
                group.CurrentPhase = GroupPhase.DrivingToVenue;
                person.NeedsReplan = true;
                passenger.NeedsReplan = true;
                break;

            case GroupPhase.DrivingToVenue:
                group.CurrentPhase = GroupPhase.AtVenue;
                person.NeedsReplan = true;
                passenger.NeedsReplan = true;
                break;

            case GroupPhase.AtVenue:
                group.CurrentPhase = GroupPhase.DrivingBack;
                person.NeedsReplan = true;
                passenger.NeedsReplan = true;
                break;

            case GroupPhase.DrivingBack:
                group.CurrentPhase = GroupPhase.Complete;
                group.Status = GroupStatus.Disbanded;
                person.Objectives.RemoveAll(o => o is GoOnDateObjective g && g.GroupId == GroupId);
                passenger.Objectives.RemoveAll(o => o is GoOnDateObjective g && g.GroupId == GroupId);
                person.NeedsReplan = true;
                passenger.NeedsReplan = true;
                break;
        }
    }
}
```

- [ ] **Step 4: Run tests**

```
dotnet test stakeout.tests/ --filter "GoOnDateObjectiveTests" -v minimal
```
Expected: PASS.

- [ ] **Step 5: Run full suite**

```
dotnet test stakeout.tests/ -v minimal
```
Expected: all pass.

- [ ] **Step 6: Commit**

```
git add src/simulation/objectives/GoOnDateObjective.cs stakeout.tests/Simulation/Objectives/GoOnDateObjectiveTests.cs
git commit -m "feat: GoOnDateObjective — phase-aware date coordination via NeedsReplan"
```

---

## Task 9: CheckAnsweringMachineAction

**Files:**
- Create: `src/simulation/actions/telephone/CheckAnsweringMachineAction.cs`
- Test: `stakeout.tests/Simulation/Actions/CheckAnsweringMachineActionTests.cs`

- [ ] **Step 1: Write failing test**

Create `stakeout.tests/Simulation/Actions/CheckAnsweringMachineActionTests.cs`:
```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Stakeout.Simulation;
using Stakeout.Simulation.Actions;
using Stakeout.Simulation.Actions.Telephone;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Fixtures;
using Stakeout.Simulation.Objectives;
using Stakeout.Simulation.Traces;
using Xunit;

namespace Stakeout.Tests.Simulation.Actions;

public class CheckAnsweringMachineActionTests
{
    [Fact]
    public void CheckAnsweringMachine_MessageFromKnownContact_CreatesCallBackObjective()
    {
        var state = new SimulationState(new GameClock(new DateTime(1984, 1, 2, 18, 0, 0)));

        var callerHome = new Address { Id = state.GenerateEntityId(), GridX = 2, GridY = 2 };
        var recipientHome = new Address { Id = state.GenerateEntityId(), GridX = 8, GridY = 2 };
        foreach (var a in new[] { callerHome, recipientHome })
            state.Addresses[a.Id] = a;

        var loc = new Location { Id = state.GenerateEntityId(), AddressId = recipientHome.Id };
        state.Locations[loc.Id] = loc;
        recipientHome.LocationIds.Add(loc.Id);

        var phone = new Fixture { Id = state.GenerateEntityId(), Type = FixtureType.Telephone, LocationId = loc.Id };
        state.Fixtures[phone.Id] = phone;

        var callerPhoneLoc = new Location { Id = state.GenerateEntityId(), AddressId = callerHome.Id };
        state.Locations[callerPhoneLoc.Id] = callerPhoneLoc;
        callerHome.LocationIds.Add(callerPhoneLoc.Id);
        var callerPhone = new Fixture { Id = state.GenerateEntityId(), Type = FixtureType.Telephone, LocationId = callerPhoneLoc.Id };
        state.Fixtures[callerPhone.Id] = callerPhone;

        var caller = new Person
        {
            Id = state.GenerateEntityId(),
            HomeAddressId = callerHome.Id,
            HomePhoneFixtureId = callerPhone.Id
        };
        var recipient = new Person
        {
            Id = state.GenerateEntityId(),
            HomeAddressId = recipientHome.Id,
            HomePhoneFixtureId = phone.Id
        };
        state.People[caller.Id] = caller;
        state.People[recipient.Id] = recipient;

        // Establish Dating relationship
        var rel = new Relationship
        {
            Id = state.GenerateEntityId(),
            PersonAId = caller.Id, PersonBId = recipient.Id,
            Type = RelationshipType.Dating
        };
        state.AddRelationship(rel);

        // Leave a message trace on recipient's phone
        TraceEmitter.EmitRecord(state, phone.Id, caller.Id,
            $"Message from {caller.FirstName} {caller.LastName}: please call back");

        var action = new CheckAnsweringMachineAction();
        var ctx = new ActionContext
        {
            Person = recipient,
            State = state,
            EventJournal = state.Journal,
            Random = new Random(1),
            CurrentTime = state.Clock.CurrentTime
        };

        action.OnStart(ctx);
        action.Tick(ctx, TimeSpan.FromMinutes(2));
        action.OnComplete(ctx);

        Assert.Contains(recipient.Objectives, o => o is CallBackObjective cb && cb.TargetPersonId == caller.Id);
    }

    [Fact]
    public void CheckAnsweringMachine_NoMessages_NoCallBackObjective()
    {
        var state = new SimulationState(new GameClock(new DateTime(1984, 1, 2, 18, 0, 0)));
        var home = new Address { Id = state.GenerateEntityId(), GridX = 5, GridY = 5 };
        state.Addresses[home.Id] = home;
        var loc = new Location { Id = state.GenerateEntityId(), AddressId = home.Id };
        state.Locations[loc.Id] = loc;
        home.LocationIds.Add(loc.Id);
        var phone = new Fixture { Id = state.GenerateEntityId(), Type = FixtureType.Telephone, LocationId = loc.Id };
        state.Fixtures[phone.Id] = phone;

        var person = new Person { Id = state.GenerateEntityId(), HomeAddressId = home.Id, HomePhoneFixtureId = phone.Id };
        state.People[person.Id] = person;

        var action = new CheckAnsweringMachineAction();
        var ctx = new ActionContext { Person = person, State = state, EventJournal = state.Journal, Random = new Random(1), CurrentTime = state.Clock.CurrentTime };
        action.OnStart(ctx);
        action.Tick(ctx, TimeSpan.FromMinutes(2));
        action.OnComplete(ctx);

        Assert.DoesNotContain(person.Objectives, o => o is CallBackObjective);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```
dotnet test stakeout.tests/ --filter "CheckAnsweringMachineActionTests" -v minimal
```
Expected: FAIL.

- [ ] **Step 3: Create CheckAnsweringMachineAction**

Create `src/simulation/actions/telephone/CheckAnsweringMachineAction.cs`:
```csharp
using System;
using System.Linq;
using Stakeout.Simulation.Objectives;
using Stakeout.Simulation.Traces;

namespace Stakeout.Simulation.Actions.Telephone;

public class CheckAnsweringMachineAction : IAction
{
    private TimeSpan _elapsed = TimeSpan.Zero;
    private static readonly TimeSpan CheckDuration = TimeSpan.FromMinutes(2);

    public string Name => "CheckAnsweringMachine";
    public string DisplayText => "Checking answering machine";

    public void OnStart(ActionContext ctx) { }

    public ActionStatus Tick(ActionContext ctx, TimeSpan delta)
    {
        _elapsed += delta;
        return _elapsed >= CheckDuration ? ActionStatus.Completed : ActionStatus.Running;
    }

    public void OnComplete(ActionContext ctx)
    {
        if (!ctx.Person.HomePhoneFixtureId.HasValue) return;

        var messageTraces = ctx.State.GetTracesForFixture(
            ctx.Person.HomePhoneFixtureId.Value, ctx.CurrentTime)
            .Where(t => t.Type == TraceType.Record
                     && t.Description != null
                     && t.Description.Contains("please call back")
                     && t.CreatedByPersonId.HasValue)
            .ToList();

        if (!ctx.State.RelationshipsByPersonId.TryGetValue(ctx.Person.Id, out var relationships))
            return;
        var knownContactIds = relationships.Select(r =>
            r.PersonAId == ctx.Person.Id ? r.PersonBId : r.PersonAId).ToHashSet();

        foreach (var trace in messageTraces)
        {
            var callerId = trace.CreatedByPersonId!.Value;
            if (!knownContactIds.Contains(callerId)) continue;

            // "Unread" = no existing CallBackObjective for this caller
            if (ctx.Person.Objectives.OfType<CallBackObjective>().Any(o => o.TargetPersonId == callerId))
                continue;

            var caller = ctx.State.People[callerId];
            if (!caller.HomePhoneFixtureId.HasValue) continue;

            ctx.Person.Objectives.Add(new CallBackObjective(callerId,
                caller.HomePhoneFixtureId.Value, caller.HomeAddressId)
            {
                Id = ctx.State.GenerateEntityId()
            });
        }
    }
}
```

- [ ] **Step 4: Run tests**

```
dotnet test stakeout.tests/ --filter "CheckAnsweringMachineActionTests" -v minimal
```
Expected: PASS.

- [ ] **Step 5: Commit**

```
git add src/simulation/actions/telephone/CheckAnsweringMachineAction.cs stakeout.tests/Simulation/Actions/CheckAnsweringMachineActionTests.cs
git commit -m "feat: CheckAnsweringMachineAction — creates CallBackObjective from message traces"
```

---

## Task 10: CallBackObjective

**Files:**
- Create: `src/simulation/objectives/CallBackObjective.cs`
- Test: `stakeout.tests/Simulation/Objectives/CallBackObjectiveTests.cs`

- [ ] **Step 1: Write failing test**

Create `stakeout.tests/Simulation/Objectives/CallBackObjectiveTests.cs`:
```csharp
using System;
using System.Linq;
using Stakeout.Simulation;
using Stakeout.Simulation.Actions.Telephone;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Objectives;
using Xunit;

namespace Stakeout.Tests.Simulation.Objectives;

public class CallBackObjectiveTests
{
    [Fact]
    public void GetActions_ReturnsPhoneCallAction_TargetingCallerPhone()
    {
        var state = new SimulationState(new GameClock(new DateTime(1984, 1, 2, 18, 30, 0)));

        var callerHome = new Address { Id = state.GenerateEntityId(), GridX = 2, GridY = 2 };
        var recipientHome = new Address { Id = state.GenerateEntityId(), GridX = 8, GridY = 2 };
        var loc = new Location { Id = state.GenerateEntityId(), AddressId = callerHome.Id };
        var phone = new Stakeout.Simulation.Fixtures.Fixture
        {
            Id = state.GenerateEntityId(),
            Type = Stakeout.Simulation.Fixtures.FixtureType.Telephone,
            LocationId = loc.Id
        };
        state.Addresses[callerHome.Id] = callerHome;
        state.Addresses[recipientHome.Id] = recipientHome;
        state.Locations[loc.Id] = loc;
        callerHome.LocationIds.Add(loc.Id);
        state.Fixtures[phone.Id] = phone;

        var caller = new Person { Id = state.GenerateEntityId(), HomeAddressId = callerHome.Id, HomePhoneFixtureId = phone.Id };
        var recipient = new Person { Id = state.GenerateEntityId(), HomeAddressId = recipientHome.Id };
        state.People[caller.Id] = caller;
        state.People[recipient.Id] = recipient;

        // Give caller an OrganizeDateObjective in AwaitingCallback state
        var organizeObj = new OrganizeDateObjective(recipient.Id, 99,
            new DateTime(1984, 1, 2, 12, 0, 0),
            new DateTime(1984, 1, 2, 19, 0, 0),
            new DateTime(1984, 1, 2, 17, 50, 0))
        { Id = state.GenerateEntityId() };
        caller.Objectives.Add(organizeObj);

        var callbackObj = new CallBackObjective(caller.Id, phone.Id, callerHome.Id)
        { Id = state.GenerateEntityId() };

        var actions = callbackObj.GetActions(recipient, state,
            new DateTime(1984, 1, 2, 18, 30, 0), new DateTime(1984, 1, 2, 23, 59, 0));

        Assert.Single(actions);
        Assert.IsType<PhoneCallAction>(actions[0].Action);
        Assert.Equal(callerHome.Id, actions[0].TargetAddressId);
        Assert.Equal(ObjectiveSource.Social, callbackObj.Source);
        Assert.Equal(10, callbackObj.Priority);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

```
dotnet test stakeout.tests/ --filter "CallBackObjectiveTests" -v minimal
```
Expected: FAIL.

- [ ] **Step 3: Create CallBackObjective**

Create `src/simulation/objectives/CallBackObjective.cs`:
```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Stakeout.Simulation.Actions.Telephone;
using Stakeout.Simulation.Entities;

namespace Stakeout.Simulation.Objectives;

public class CallBackObjective : Objective
{
    public int TargetPersonId { get; }
    private readonly int _targetFixtureId;
    private readonly int _targetAddressId;

    public override int Priority => 10;
    public override ObjectiveSource Source => ObjectiveSource.Social;

    public CallBackObjective(int targetPersonId, int targetFixtureId, int targetAddressId)
    {
        TargetPersonId = targetPersonId;
        _targetFixtureId = targetFixtureId;
        _targetAddressId = targetAddressId;
    }

    public override List<PlannedAction> GetActions(Person person, SimulationState state,
        DateTime planStart, DateTime planEnd)
    {
        if (Status == ObjectiveStatus.Completed) return new List<PlannedAction>();

        // Build a group ID for the return call — reuse the existing Forming group if any
        int groupId = -1;
        var existingGroup = state.Groups.Values
            .FirstOrDefault(g => g.Status == GroupStatus.Forming
                              && g.MemberPersonIds.Contains(person.Id)
                              && g.MemberPersonIds.Contains(TargetPersonId));
        if (existingGroup != null)
            groupId = existingGroup.Id;

        if (groupId < 0)
        {
            // Create a new forming group; OrganizeDateObjective.OnAccepted will fill params
            var group = new Group
            {
                Id = state.GenerateEntityId(),
                Type = GroupType.Date,
                Status = GroupStatus.Forming,
                MemberPersonIds = new List<int> { TargetPersonId, person.Id }
            };
            state.Groups[group.Id] = group;
            groupId = group.Id;
        }

        var callAction = new PhoneCallAction(
            targetAddressId: _targetAddressId,
            targetFixtureId: _targetFixtureId,
            proposedGroupId: groupId,
            callerId: person.Id,
            recipientId: TargetPersonId);

        Status = ObjectiveStatus.Completed;

        return new List<PlannedAction>
        {
            new()
            {
                Action = callAction,
                TargetAddressId = person.HomeAddressId,
                TimeWindowStart = planStart,
                TimeWindowEnd = planEnd,
                Duration = TimeSpan.FromMinutes(10),
                DisplayText = "returning a phone call",
                SourceObjective = this
            }
        };
    }
}
```

- [ ] **Step 4: Run tests**

```
dotnet test stakeout.tests/ --filter "CallBackObjectiveTests" -v minimal
```
Expected: PASS.

- [ ] **Step 5: Run full suite**

```
dotnet test stakeout.tests/ -v minimal
```
Expected: all pass.

- [ ] **Step 6: Commit**

```
git add src/simulation/objectives/CallBackObjective.cs stakeout.tests/Simulation/Objectives/CallBackObjectiveTests.cs
git commit -m "feat: CallBackObjective — schedules return phone call action"
```

---

## Task 11: Integration Test 1 — She Answers

**Files:**
- Create: `stakeout.tests/Simulation/Groups/DateCoordinationIntegrationTests.cs`

- [ ] **Step 1: Write the integration test**

Create `stakeout.tests/Simulation/Groups/DateCoordinationIntegrationTests.cs`:
```csharp
using System;
using System.Linq;
using Stakeout.Simulation;
using Stakeout.Simulation.Actions;
using Stakeout.Simulation.Addresses;
using Stakeout.Simulation.Brain;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Events;
using Stakeout.Simulation.Fixtures;
using Stakeout.Simulation.Objectives;
using Xunit;

namespace Stakeout.Tests.Simulation.Groups;

/// <summary>
/// Integration tests for the full date coordination flow.
/// Assertions are on SimulationState (person locations, traces, group status) and
/// journal events — not on internal plan structure or method call sequences.
/// </summary>
public class DateCoordinationIntegrationTests
{
    // 1984-01-02 Monday, start at 8am
    private static readonly DateTime SimStart = new(1984, 1, 2, 8, 0, 0);

    private static Address CreateAddress(SimulationState state, AddressType type, int gridX, int gridY)
    {
        var addr = new Address
        {
            Id = state.GenerateEntityId(),
            Type = type,
            GridX = gridX,
            GridY = gridY
        };
        state.Addresses[addr.Id] = addr;
        AddressTemplateRegistry.Get(type).Generate(addr, state, new Random(addr.Id));
        return addr;
    }

    private static Fixture GetTelephone(SimulationState state, int addressId)
    {
        return state.GetFixturesForAddress(addressId)
            .First(f => f.Type == FixtureType.Telephone);
    }

    private static Person CreatePerson(SimulationState state, Address home, Fixture phone, bool hasVehicle = false)
    {
        var person = new Person
        {
            Id = state.GenerateEntityId(),
            FirstName = hasVehicle ? "Guy" : "Girl",
            LastName = "Test",
            CreatedAt = state.Clock.CurrentTime,
            HomeAddressId = home.Id,
            HomePhoneFixtureId = phone.Id,
            CurrentAddressId = home.Id,
            CurrentPosition = home.Position,
            PreferredSleepTime = TimeSpan.FromHours(23),
            PreferredWakeTime = TimeSpan.FromHours(7)
        };
        person.Objectives.Add(new SleepObjective { Id = state.GenerateEntityId() });

        if (hasVehicle)
        {
            var vehicle = new Vehicle
            {
                Id = state.GenerateEntityId(),
                OwnerPersonId = person.Id,
                CurrentAddressId = home.Id,
                Type = VehicleType.Car
            };
            state.Vehicles[vehicle.Id] = vehicle;
            person.VehicleId = vehicle.Id;
        }

        state.People[person.Id] = person;
        return person;
    }

    private static void RunSimulation(SimulationState state, int minutes, params Person[] people)
    {
        var mapConfig = new MapConfig();
        var runner = new ActionRunner(mapConfig);

        for (int i = 0; i < minutes; i++)
        {
            state.Clock.Tick(60);
            foreach (var person in people)
            {
                if (person.DayPlan == null || person.DayPlan.IsExhausted)
                    person.DayPlan = NpcBrain.PlanDay(person, state, state.Clock.CurrentTime, mapConfig);
                runner.Tick(person, state, TimeSpan.FromMinutes(1));
            }
        }
    }

    /// <summary>
    /// Full date flow: guy calls, girl is home, she answers, they arrange a date.
    /// Guy drives to her house, they travel together to the diner, have dinner, return.
    /// Asserts on observable state: traces, group status, journal arrival events.
    /// </summary>
    [Fact]
    public void Date_SheAnswers_BothEndUpAtDinerAndReturnHome()
    {
        AddressTemplateRegistry.RegisterAll();
        var state = new SimulationState(new GameClock(SimStart));

        var guyHome = CreateAddress(state, AddressType.SuburbanHome, 2, 2);
        var girlHome = CreateAddress(state, AddressType.SuburbanHome, 10, 2);
        var diner = CreateAddress(state, AddressType.Diner, 6, 6);

        var guyPhone = GetTelephone(state, guyHome.Id);
        var girlPhone = GetTelephone(state, girlHome.Id);

        var guy = CreatePerson(state, guyHome, guyPhone, hasVehicle: true);
        var girl = CreatePerson(state, girlHome, girlPhone, hasVehicle: false);

        var rel = new Relationship
        {
            Id = state.GenerateEntityId(),
            PersonAId = guy.Id, PersonBId = girl.Id,
            Type = RelationshipType.Dating,
            StartedAt = SimStart.AddDays(-30)
        };
        state.AddRelationship(rel);

        // Guy will call at noon, propose dinner at 7pm (pickup 5:50pm)
        guy.Objectives.Add(new OrganizeDateObjective(
            targetPersonId: girl.Id,
            proposedMeetupAddressId: diner.Id,
            proposedCallTime: SimStart.Date.AddHours(12),
            proposedMeetupTime: SimStart.Date.AddHours(19),
            proposedPickupTime: SimStart.Date.AddHours(17).AddMinutes(50))
        {
            Id = state.GenerateEntityId()
        });

        // Run for 24 hours
        RunSimulation(state, 24 * 60, guy, girl);

        // --- Assertions ---

        // Phone record traces: call was received on girl's phone
        var girlPhoneTraces = state.GetTracesForFixture(girlPhone.Id, state.Clock.CurrentTime);
        Assert.NotEmpty(girlPhoneTraces);

        // Group was formed and disbanded
        Assert.Single(state.Groups);
        var group = state.Groups.Values.Single();
        Assert.Equal(GroupStatus.Disbanded, group.Status);
        Assert.Contains(guy.Id, group.MemberPersonIds);
        Assert.Contains(girl.Id, group.MemberPersonIds);

        // Both attended the diner — journal shows "having dinner" for both
        var guyEvents = state.Journal.GetEventsForPerson(guy.Id);
        var girlEvents = state.Journal.GetEventsForPerson(girl.Id);
        Assert.Contains(guyEvents, e =>
            e.EventType == SimulationEventType.ActivityStarted && e.Description == "having dinner");
        Assert.Contains(girlEvents, e =>
            e.EventType == SimulationEventType.ActivityStarted && e.Description == "having dinner");

        // They arrived at the diner within 30 minutes of each other
        var guyArrival = guyEvents.FirstOrDefault(e =>
            e.EventType == SimulationEventType.ArrivedAtAddress && e.AddressId == diner.Id);
        var girlArrival = girlEvents.FirstOrDefault(e =>
            e.EventType == SimulationEventType.ArrivedAtAddress && e.AddressId == diner.Id);
        Assert.NotNull(guyArrival);
        Assert.NotNull(girlArrival);
        Assert.True(Math.Abs((guyArrival.Timestamp - girlArrival.Timestamp).TotalMinutes) < 30,
            $"Guy arrived at diner at {guyArrival.Timestamp:HH:mm}, girl at {girlArrival.Timestamp:HH:mm} — too far apart");

        // Girl ended the day at her home
        Assert.Equal(girlHome.Id, girl.CurrentAddressId);
    }
}
```

- [ ] **Step 2: Run test to see initial result**

```
dotnet test stakeout.tests/ --filter "Date_SheAnswers_BothEndUpAtDinerAndReturnHome" -v minimal
```
Expected: FAIL (likely compilation or logic errors — diagnose and fix iteratively).

- [ ] **Step 3: Fix any issues and re-run until green**

Common issues to look for:
- `AddressTemplateRegistry.Get(type)` — verify this method exists; check `AddressTemplateRegistry.cs` for the correct API
- `state.GetFixturesForAddress` — verify the new helper returns fixtures in sublocations too (check the implementation in Task 1)
- `SimulationEvent.AddressId` field name — verify against actual field names in `SimulationEvent.cs`

Run after each fix:
```
dotnet test stakeout.tests/ --filter "Date_SheAnswers_BothEndUpAtDinerAndReturnHome" -v minimal
```

- [ ] **Step 4: Run full test suite**

```
dotnet test stakeout.tests/ -v minimal
```
Expected: all pass.

- [ ] **Step 5: Commit**

```
git add stakeout.tests/Simulation/Groups/DateCoordinationIntegrationTests.cs
git commit -m "test: integration test 1 — she answers, full date flow verified"
```

---

## Task 12: Integration Test 2 — Answering Machine Path

**Files:**
- Modify: `stakeout.tests/Simulation/Groups/DateCoordinationIntegrationTests.cs`

- [ ] **Step 1: Add the second integration test**

In `stakeout.tests/Simulation/Groups/DateCoordinationIntegrationTests.cs`, add after the first test method:
```csharp
/// <summary>
/// Answering machine path: guy calls at noon, girl is away from home, message left.
/// Girl returns home in the evening, checks machine, calls back.
/// Too late for today, so date is organized for the following day.
/// Next day both end up at the diner.
/// </summary>
[Fact]
public void Date_AnsweringMachinePath_DateOrganizedForNextDay()
{
    AddressTemplateRegistry.RegisterAll();
    var state = new SimulationState(new GameClock(SimStart));

    var guyHome = CreateAddress(state, AddressType.SuburbanHome, 2, 2);
    var girlHome = CreateAddress(state, AddressType.SuburbanHome, 10, 2);
    var diner = CreateAddress(state, AddressType.Diner, 6, 6);
    var awayAddr = CreateAddress(state, AddressType.Park, 15, 15);  // girl is here at noon

    var guyPhone = GetTelephone(state, guyHome.Id);
    var girlPhone = GetTelephone(state, girlHome.Id);

    var guy = CreatePerson(state, guyHome, guyPhone, hasVehicle: true);
    var girl = CreatePerson(state, girlHome, girlPhone, hasVehicle: false);

    var rel = new Relationship
    {
        Id = state.GenerateEntityId(),
        PersonAId = guy.Id, PersonBId = girl.Id,
        Type = RelationshipType.Dating,
        StartedAt = SimStart.AddDays(-30)
    };
    state.AddRelationship(rel);

    // Guy calls at noon
    guy.Objectives.Add(new OrganizeDateObjective(
        targetPersonId: girl.Id,
        proposedMeetupAddressId: diner.Id,
        proposedCallTime: SimStart.Date.AddHours(12),
        proposedMeetupTime: SimStart.Date.AddHours(19),    // 7pm today — will be too late after callback
        proposedPickupTime: SimStart.Date.AddHours(17).AddMinutes(50))
    {
        Id = state.GenerateEntityId()
    });

    // Girl is away at noon: place her at the park address until 6pm via a WaitAction in her plan.
    // We do this by pre-setting her CurrentAddressId to the away address and adding a
    // timed return — easier: just move her to the away address and let the sim run.
    girl.CurrentAddressId = awayAddr.Id;
    girl.CurrentPosition = awayAddr.Position;

    // Girl returns home via a GoForARunObjective replacement: give her a WaitAction via a
    // custom AdHocObjective that puts her back home after 6pm.
    // Simplest: add a direct PlannedAction by using a helper Objective.
    girl.Objectives.Add(new WaitAwayUntilObjective(awayAddr.Id, SimStart.Date.AddHours(18))
    {
        Id = state.GenerateEntityId()
    });

    // Run 48 hours (2 days)
    RunSimulation(state, 48 * 60, guy, girl);

    // --- Assertions ---

    // Message was left on girl's phone (not an invitation — no group active after first tick)
    var girlPhoneTraces = state.GetTracesForFixture(girlPhone.Id, state.Clock.CurrentTime);
    Assert.Contains(girlPhoneTraces, t => t.Description != null && t.Description.Contains("please call back"));

    // Group was eventually formed and disbanded
    Assert.Single(state.Groups);
    var group = state.Groups.Values.Single();
    Assert.Equal(GroupStatus.Disbanded, group.Status);

    // Date happened on day 2
    Assert.Equal(SimStart.Date.AddDays(1), group.MeetupTime.Date);

    // Both attended the diner
    var guyEvents = state.Journal.GetEventsForPerson(guy.Id);
    var girlEvents = state.Journal.GetEventsForPerson(girl.Id);
    Assert.Contains(guyEvents, e =>
        e.EventType == SimulationEventType.ActivityStarted && e.Description == "having dinner");
    Assert.Contains(girlEvents, e =>
        e.EventType == SimulationEventType.ActivityStarted && e.Description == "having dinner");

    // Both at diner on day 2 (not day 1)
    var guyDinerDay2 = guyEvents.FirstOrDefault(e =>
        e.EventType == SimulationEventType.ArrivedAtAddress
        && e.AddressId == diner.Id
        && e.Timestamp.Date == SimStart.Date.AddDays(1));
    var girlDinerDay2 = girlEvents.FirstOrDefault(e =>
        e.EventType == SimulationEventType.ArrivedAtAddress
        && e.AddressId == diner.Id
        && e.Timestamp.Date == SimStart.Date.AddDays(1));
    Assert.NotNull(guyDinerDay2);
    Assert.NotNull(girlDinerDay2);
}
```

Also add the test helper objective class at the bottom of the same file, inside the namespace:
```csharp
/// <summary>
/// Test helper: places an NPC at a specific address until a given return time,
/// then lets normal planning take over. Used to simulate "girl is away from home."
/// </summary>
internal class WaitAwayUntilObjective : Objective
{
    private readonly int _awayAddressId;
    private readonly DateTime _returnTime;

    public override int Priority => 30;  // above hobby, below work
    public override ObjectiveSource Source => ObjectiveSource.Universal;

    public WaitAwayUntilObjective(int awayAddressId, DateTime returnTime)
    {
        _awayAddressId = awayAddressId;
        _returnTime = returnTime;
    }

    public override List<PlannedAction> GetActions(Person person, SimulationState state,
        DateTime planStart, DateTime planEnd)
    {
        if (planStart >= _returnTime || Status == ObjectiveStatus.Completed)
            return new System.Collections.Generic.List<PlannedAction>();

        var duration = _returnTime - planStart;
        return new System.Collections.Generic.List<PlannedAction>
        {
            new()
            {
                Action = new Stakeout.Simulation.Actions.Primitives.WaitAction(duration, "out and about"),
                TargetAddressId = _awayAddressId,
                TimeWindowStart = planStart,
                TimeWindowEnd = _returnTime,
                Duration = duration,
                DisplayText = "out and about",
                SourceObjective = this
            }
        };
    }

    public override void OnActionCompleted(PlannedAction action, bool success)
    {
        Status = ObjectiveStatus.Completed;
    }
}
```

- [ ] **Step 2: Run test to see initial result**

```
dotnet test stakeout.tests/ --filter "Date_AnsweringMachinePath_DateOrganizedForNextDay" -v minimal
```
Expected: FAIL — diagnose and fix iteratively.

- [ ] **Step 3: Fix issues and re-run until green**

```
dotnet test stakeout.tests/ --filter "Date_AnsweringMachinePath_DateOrganizedForNextDay" -v minimal
```

- [ ] **Step 4: Run full test suite**

```
dotnet test stakeout.tests/ -v minimal
```
Expected: all 2 integration tests and all existing tests pass.

- [ ] **Step 5: Commit**

```
git add stakeout.tests/Simulation/Groups/DateCoordinationIntegrationTests.cs
git commit -m "test: integration test 2 — answering machine path, date organized for next day"
```

---

## Self-Review Checklist

- [x] All spec entities delivered: `Relationship`, `Group`, `PendingInvitation`, `Vehicle`
- [x] `ObjectiveSource.Social`, `FixtureType.Telephone` added
- [x] Telephone fixtures in 4 address templates + PersonGenerator wiring
- [x] `IAction.OnSuspend`/`OnResume` + `ActionSequence` implementation
- [x] `Objective.OnActionCompletedWithState` added, called from `ActionRunner`
- [x] `ActionRunner`: `NeedsReplan`, invitation interrupt, arriving-home answering machine check
- [x] `PhoneCallAction`, `AcceptPhoneCallAction`, `CheckAnsweringMachineAction` with unit tests
- [x] `OrganizeDateObjective`, `GoOnDateObjective`, `CallBackObjective` with unit tests
- [x] Two integration tests with assertions on observable state only (traces, group status, journal events)
- [x] `GoOnDateGroupAction` replaced by phase-aware `GoOnDateObjective` — documented deviation from spec
- [x] `SimulationState.GetFixturesForAddress` helper added (needed by templates + PersonGenerator)
- [x] All code references consistent: `GroupId`, `TargetPersonId`, `NeedsReplan`, `HomePhoneFixtureId`
