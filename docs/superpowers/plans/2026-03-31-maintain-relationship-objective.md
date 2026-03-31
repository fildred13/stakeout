# MaintainRelationshipObjective Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** When two people have a `Dating` relationship, each person automatically receives a `MaintainRelationshipObjective` that periodically adds an `OrganizeDateObjective` to schedule a date — no traits required.

**Architecture:** `SimulationState.AddRelationship` detects `RelationshipType.Dating` and adds a `MaintainRelationshipObjective` to both people. Each day when `NpcBrain.PlanDay` calls `GetActions` on this objective, it checks whether a date needs scheduling (no active date objective, no recent date, a diner exists) and, if so, adds an `OrganizeDateObjective` + sets `NeedsReplan = true`. `GetActions` always returns an empty list — this is a meta-objective that injects work, never schedules it directly.

**Tech Stack:** C# / .NET, xUnit, existing `SimulationState` / `NpcBrain` / `ActionRunner` pipeline.

---

## File Map

**New files:**
| File | Responsibility |
|---|---|
| `src/simulation/objectives/MaintainRelationshipObjective.cs` | Standing meta-objective; injects `OrganizeDateObjective` when conditions are met |
| `stakeout.tests/Simulation/Objectives/MaintainRelationshipObjectiveTests.cs` | Unit tests for the meta-objective logic |

**Modified files:**
| File | Change |
|---|---|
| `src/simulation/SimulationState.cs` | `AddRelationship` adds `MaintainRelationshipObjective` to both people for `Dating` relationships |
| `stakeout.tests/Simulation/Groups/DateCoordinationIntegrationTests.cs` | Add integration test: couple with Dating relationship (no pre-added objective) eventually goes on a date |

---

## Task 1: MaintainRelationshipObjective

**Files:**
- Create: `src/simulation/objectives/MaintainRelationshipObjective.cs`
- Create: `stakeout.tests/Simulation/Objectives/MaintainRelationshipObjectiveTests.cs`

- [ ] **Step 1: Write failing tests**

Create `stakeout.tests/Simulation/Objectives/MaintainRelationshipObjectiveTests.cs`:

```csharp
using System;
using System.Linq;
using Stakeout.Simulation;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Objectives;
using Xunit;

namespace Stakeout.Tests.Simulation.Objectives;

public class MaintainRelationshipObjectiveTests
{
    private static (SimulationState state, Person person, Person partner, Address diner)
        BuildScene()
    {
        var state = new SimulationState(new GameClock(new DateTime(1984, 1, 2, 9, 0, 0)));

        var personHome = new Address { Id = state.GenerateEntityId(), GridX = 2, GridY = 2 };
        var partnerHome = new Address { Id = state.GenerateEntityId(), GridX = 8, GridY = 8 };
        var diner = new Address { Id = state.GenerateEntityId(), GridX = 5, GridY = 5,
            Type = AddressType.Diner };
        state.Addresses[personHome.Id] = personHome;
        state.Addresses[partnerHome.Id] = partnerHome;
        state.Addresses[diner.Id] = diner;

        var person = new Person
        {
            Id = state.GenerateEntityId(),
            HomeAddressId = personHome.Id,
            CurrentAddressId = personHome.Id,
            PreferredSleepTime = TimeSpan.FromHours(23),
            PreferredWakeTime = TimeSpan.FromHours(7)
        };
        person.Objectives.Add(new SleepObjective { Id = state.GenerateEntityId() });

        var partner = new Person
        {
            Id = state.GenerateEntityId(),
            HomeAddressId = partnerHome.Id,
            CurrentAddressId = partnerHome.Id,
            HomePhoneFixtureId = null
        };

        state.People[person.Id] = person;
        state.People[partner.Id] = partner;

        return (state, person, partner, diner);
    }

    [Fact]
    public void GetActions_NoPriorDate_AddsOrganizeDateObjectiveAndSetsNeedsReplan()
    {
        var (state, person, partner, _) = BuildScene();
        var obj = new MaintainRelationshipObjective(partner.Id) { Id = state.GenerateEntityId() };

        var actions = obj.GetActions(person, state,
            new DateTime(1984, 1, 2, 9, 0, 0), new DateTime(1984, 1, 2, 23, 59, 0));

        Assert.Empty(actions);
        Assert.Contains(person.Objectives,
            o => o is OrganizeDateObjective od && od.TargetPersonId == partner.Id);
        Assert.True(person.NeedsReplan);
    }

    [Fact]
    public void GetActions_OrganizeDateObjectiveAlreadyActive_NoNewObjectiveAdded()
    {
        var (state, person, partner, diner) = BuildScene();
        var existing = new OrganizeDateObjective(partner.Id, diner.Id,
            new DateTime(1984, 1, 2, 12, 0, 0),
            new DateTime(1984, 1, 2, 19, 0, 0),
            new DateTime(1984, 1, 2, 17, 50, 0))
        { Id = state.GenerateEntityId() };
        person.Objectives.Add(existing);

        var obj = new MaintainRelationshipObjective(partner.Id) { Id = state.GenerateEntityId() };
        obj.GetActions(person, state,
            new DateTime(1984, 1, 2, 9, 0, 0), new DateTime(1984, 1, 2, 23, 59, 0));

        Assert.Single(person.Objectives.OfType<OrganizeDateObjective>());
        Assert.False(person.NeedsReplan);
    }

    [Fact]
    public void GetActions_GoOnDateObjectiveActive_NoNewObjectiveAdded()
    {
        var (state, person, partner, _) = BuildScene();
        var group = new Group
        {
            Id = state.GenerateEntityId(),
            Status = GroupStatus.Active,
            MemberPersonIds = new System.Collections.Generic.List<int> { person.Id, partner.Id }
        };
        state.Groups[group.Id] = group;
        person.Objectives.Add(new GoOnDateObjective(group.Id) { Id = state.GenerateEntityId() });

        var obj = new MaintainRelationshipObjective(partner.Id) { Id = state.GenerateEntityId() };
        obj.GetActions(person, state,
            new DateTime(1984, 1, 2, 9, 0, 0), new DateTime(1984, 1, 2, 23, 59, 0));

        Assert.DoesNotContain(person.Objectives, o => o is OrganizeDateObjective);
        Assert.False(person.NeedsReplan);
    }

    [Fact]
    public void GetActions_RecentDate_NoNewObjectiveAdded()
    {
        var (state, person, partner, diner) = BuildScene();

        // A date that happened 3 days ago — within the 7-day cooldown
        var recentGroup = new Group
        {
            Id = state.GenerateEntityId(),
            Type = GroupType.Date,
            Status = GroupStatus.Disbanded,
            MeetupTime = new DateTime(1984, 1, 2, 9, 0, 0).AddDays(-3),
            MemberPersonIds = new System.Collections.Generic.List<int> { person.Id, partner.Id }
        };
        state.Groups[recentGroup.Id] = recentGroup;

        var obj = new MaintainRelationshipObjective(partner.Id) { Id = state.GenerateEntityId() };
        obj.GetActions(person, state,
            new DateTime(1984, 1, 2, 9, 0, 0), new DateTime(1984, 1, 2, 23, 59, 0));

        Assert.DoesNotContain(person.Objectives, o => o is OrganizeDateObjective);
    }

    [Fact]
    public void GetActions_NoDinerInState_NoNewObjectiveAdded()
    {
        var (state, person, partner, diner) = BuildScene();
        // Remove the diner
        state.Addresses.Remove(diner.Id);

        var obj = new MaintainRelationshipObjective(partner.Id) { Id = state.GenerateEntityId() };
        obj.GetActions(person, state,
            new DateTime(1984, 1, 2, 9, 0, 0), new DateTime(1984, 1, 2, 23, 59, 0));

        Assert.DoesNotContain(person.Objectives, o => o is OrganizeDateObjective);
        Assert.False(person.NeedsReplan);
    }

    [Fact]
    public void GetActions_AlwaysReturnsEmptyList()
    {
        var (state, person, partner, _) = BuildScene();
        var obj = new MaintainRelationshipObjective(partner.Id) { Id = state.GenerateEntityId() };

        var actions = obj.GetActions(person, state,
            new DateTime(1984, 1, 2, 9, 0, 0), new DateTime(1984, 1, 2, 23, 59, 0));

        Assert.Empty(actions);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```
dotnet test stakeout.tests/ --filter "MaintainRelationshipObjectiveTests" -v minimal
```
Expected: FAIL (class doesn't exist).

- [ ] **Step 3: Create MaintainRelationshipObjective**

Create `src/simulation/objectives/MaintainRelationshipObjective.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Stakeout.Simulation.Entities;

namespace Stakeout.Simulation.Objectives;

public class MaintainRelationshipObjective : Objective
{
    public int PartnerPersonId { get; }
    private static readonly TimeSpan DateCooldown = TimeSpan.FromDays(7);

    public override int Priority => 45;
    public override ObjectiveSource Source => ObjectiveSource.Social;

    public MaintainRelationshipObjective(int partnerPersonId)
    {
        PartnerPersonId = partnerPersonId;
    }

    public override List<PlannedAction> GetActions(Person person, SimulationState state,
        DateTime planStart, DateTime planEnd)
    {
        // Don't schedule if already organizing a date with this partner
        if (person.Objectives.OfType<OrganizeDateObjective>()
            .Any(o => o.TargetPersonId == PartnerPersonId && o.Status == ObjectiveStatus.Active))
            return new List<PlannedAction>();

        // Don't schedule if currently on a date
        if (person.Objectives.OfType<GoOnDateObjective>()
            .Any(o => o.Status == ObjectiveStatus.Active))
            return new List<PlannedAction>();

        // Don't schedule if went on a date recently
        if (WentOnDateRecently(person, state, planStart))
            return new List<PlannedAction>();

        // Need a diner to propose as the meetup venue
        var diner = state.Addresses.Values.FirstOrDefault(a => a.Type == AddressType.Diner);
        if (diner == null) return new List<PlannedAction>();

        // Propose today at 7pm if at least 4 hours away, otherwise tomorrow at 7pm
        var meetupTime = planStart.Date.AddHours(19);
        if (meetupTime < planStart.AddHours(4))
            meetupTime = planStart.Date.AddDays(1).AddHours(19);

        var pickupTime = meetupTime - TimeSpan.FromMinutes(70);
        var callTime = planStart.AddHours(1);
        if (callTime > meetupTime - TimeSpan.FromHours(3))
            callTime = meetupTime - TimeSpan.FromHours(3);

        person.Objectives.Add(new OrganizeDateObjective(
            targetPersonId: PartnerPersonId,
            proposedMeetupAddressId: diner.Id,
            proposedCallTime: callTime,
            proposedMeetupTime: meetupTime,
            proposedPickupTime: pickupTime)
        {
            Id = state.GenerateEntityId()
        });
        person.NeedsReplan = true;

        return new List<PlannedAction>();
    }

    private bool WentOnDateRecently(Person person, SimulationState state, DateTime now)
    {
        return state.Groups.Values.Any(g =>
            g.Type == GroupType.Date &&
            g.Status == GroupStatus.Disbanded &&
            g.MemberPersonIds.Contains(person.Id) &&
            g.MemberPersonIds.Contains(PartnerPersonId) &&
            now - g.MeetupTime < DateCooldown);
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

```
dotnet test stakeout.tests/ --filter "MaintainRelationshipObjectiveTests" -v minimal
```
Expected: all 6 pass.

- [ ] **Step 5: Run full suite**

```
dotnet test stakeout.tests/ -v minimal
```
Expected: all pass.

- [ ] **Step 6: Commit**

```
git add src/simulation/objectives/MaintainRelationshipObjective.cs stakeout.tests/Simulation/Objectives/MaintainRelationshipObjectiveTests.cs
```
```
git commit -m "feat: MaintainRelationshipObjective — schedules dates for people in Dating relationships"
```

---

## Task 2: Wire AddRelationship to Inject Objectives

**Files:**
- Modify: `src/simulation/SimulationState.cs`
- Test: `stakeout.tests/Simulation/SimulationStateRelationshipTests.cs` (new file)

- [ ] **Step 1: Write failing tests**

Create `stakeout.tests/Simulation/SimulationStateRelationshipTests.cs`:

```csharp
using System;
using System.Linq;
using Stakeout.Simulation;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Objectives;
using Xunit;

namespace Stakeout.Tests.Simulation;

public class SimulationStateRelationshipTests
{
    private static (SimulationState state, Person personA, Person personB) BuildTwoPeople()
    {
        var state = new SimulationState(new GameClock(new DateTime(1984, 1, 2, 9, 0, 0)));

        var homeA = new Address { Id = state.GenerateEntityId(), GridX = 2, GridY = 2 };
        var homeB = new Address { Id = state.GenerateEntityId(), GridX = 8, GridY = 8 };
        state.Addresses[homeA.Id] = homeA;
        state.Addresses[homeB.Id] = homeB;

        var personA = new Person
        {
            Id = state.GenerateEntityId(),
            HomeAddressId = homeA.Id,
            CurrentAddressId = homeA.Id
        };
        var personB = new Person
        {
            Id = state.GenerateEntityId(),
            HomeAddressId = homeB.Id,
            CurrentAddressId = homeB.Id
        };
        state.People[personA.Id] = personA;
        state.People[personB.Id] = personB;

        return (state, personA, personB);
    }

    [Fact]
    public void AddRelationship_Dating_AddsMaintainObjectiveToBothPeople()
    {
        var (state, personA, personB) = BuildTwoPeople();

        state.AddRelationship(new Relationship
        {
            Id = state.GenerateEntityId(),
            PersonAId = personA.Id,
            PersonBId = personB.Id,
            Type = RelationshipType.Dating,
            StartedAt = state.Clock.CurrentTime
        });

        Assert.Contains(personA.Objectives,
            o => o is MaintainRelationshipObjective m && m.PartnerPersonId == personB.Id);
        Assert.Contains(personB.Objectives,
            o => o is MaintainRelationshipObjective m && m.PartnerPersonId == personA.Id);
    }

    [Fact]
    public void AddRelationship_Dating_CalledTwice_DoesNotDuplicateObjective()
    {
        var (state, personA, personB) = BuildTwoPeople();

        var rel = new Relationship
        {
            Id = state.GenerateEntityId(),
            PersonAId = personA.Id,
            PersonBId = personB.Id,
            Type = RelationshipType.Dating,
            StartedAt = state.Clock.CurrentTime
        };
        state.AddRelationship(rel);
        state.AddRelationship(rel);

        Assert.Single(personA.Objectives.OfType<MaintainRelationshipObjective>()
            .Where(m => m.PartnerPersonId == personB.Id));
    }

    [Fact]
    public void AddRelationship_Friend_DoesNotAddMaintainObjective()
    {
        var (state, personA, personB) = BuildTwoPeople();

        state.AddRelationship(new Relationship
        {
            Id = state.GenerateEntityId(),
            PersonAId = personA.Id,
            PersonBId = personB.Id,
            Type = RelationshipType.Friend,
            StartedAt = state.Clock.CurrentTime
        });

        Assert.DoesNotContain(personA.Objectives, o => o is MaintainRelationshipObjective);
        Assert.DoesNotContain(personB.Objectives, o => o is MaintainRelationshipObjective);
    }

    [Fact]
    public void AddRelationship_PersonNotInState_DoesNotThrow()
    {
        var state = new SimulationState(new GameClock());

        // PersonAId = 999 doesn't exist in state.People — should not throw
        var ex = Record.Exception(() => state.AddRelationship(new Relationship
        {
            Id = state.GenerateEntityId(),
            PersonAId = 999,
            PersonBId = 998,
            Type = RelationshipType.Dating,
            StartedAt = state.Clock.CurrentTime
        }));

        Assert.Null(ex);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```
dotnet test stakeout.tests/ --filter "SimulationStateRelationshipTests" -v minimal
```
Expected: FAIL (AddRelationship doesn't add objectives yet).

- [ ] **Step 3: Modify AddRelationship in SimulationState.cs**

In `src/simulation/SimulationState.cs`, add this using at the top of the file:
```csharp
using Stakeout.Simulation.Objectives;
```

Then replace the `AddRelationship` method (currently lines 128–137) with:
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

    if (rel.Type == RelationshipType.Dating)
    {
        AddMaintainRelationshipObjectiveIfMissing(rel.PersonAId, rel.PersonBId);
        AddMaintainRelationshipObjectiveIfMissing(rel.PersonBId, rel.PersonAId);
    }
}

private void AddMaintainRelationshipObjectiveIfMissing(int personId, int partnerId)
{
    if (!People.TryGetValue(personId, out var person)) return;
    if (People.ContainsKey(partnerId) &&
        person.Objectives.OfType<MaintainRelationshipObjective>()
            .Any(o => o.PartnerPersonId == partnerId))
        return;
    person.Objectives.Add(new MaintainRelationshipObjective(partnerId)
    {
        Id = GenerateEntityId()
    });
}
```

- [ ] **Step 4: Run tests to verify they pass**

```
dotnet test stakeout.tests/ --filter "SimulationStateRelationshipTests" -v minimal
```
Expected: all 4 pass.

- [ ] **Step 5: Run full suite**

```
dotnet test stakeout.tests/ -v minimal
```
Expected: all pass.

- [ ] **Step 6: Commit**

```
git add src/simulation/SimulationState.cs stakeout.tests/Simulation/SimulationStateRelationshipTests.cs
```
```
git commit -m "feat: AddRelationship injects MaintainRelationshipObjective for Dating pairs"
```

---

## Task 3: Integration Test — Couple Goes on Date Without Pre-Added Objective

**Files:**
- Modify: `stakeout.tests/Simulation/Groups/DateCoordinationIntegrationTests.cs`

- [ ] **Step 1: Add a vehicle to the test helpers**

Read `stakeout.tests/Simulation/Groups/DateCoordinationIntegrationTests.cs` to understand the existing helpers. The `CreatePerson` helper accepts a `hasVehicle` parameter. The integration test below needs the guy (person with the `MaintainRelationshipObjective`) to have a vehicle so he can pick up the girl.

- [ ] **Step 2: Write the integration test**

Add this test method inside the existing `DateCoordinationIntegrationTests` class in `stakeout.tests/Simulation/Groups/DateCoordinationIntegrationTests.cs`:

```csharp
/// <summary>
/// Verifies the full chain: Dating relationship → MaintainRelationshipObjective (auto-added
/// by AddRelationship) → OrganizeDateObjective injected on first planning pass → phone call
/// → date organized → both people end up at the diner.
/// No OrganizeDateObjective is pre-added; it must emerge from the simulation.
/// </summary>
[Fact]
public void DatingCouple_NoPreAddedObjective_EventuallyGoOnDate()
{
    AddressTemplateRegistry.RegisterAll();
    var state = new SimulationState(new GameClock(SimStart));

    var guyHome = CreateAddress(state, AddressType.SuburbanHome, 2, 2);
    var girlHome = CreateAddress(state, AddressType.SuburbanHome, 10, 2);
    var diner = CreateAddress(state, AddressType.Diner, 6, 6);

    var guyPhone = GetTelephone(state, guyHome.Id);
    var girlPhone = GetTelephone(state, girlHome.Id);

    // Create people — neither gets an OrganizeDateObjective pre-added
    var guy = CreatePerson(state, guyHome, guyPhone, "Jack", hasVehicle: true);
    var girl = CreatePerson(state, girlHome, girlPhone, "Susan");

    // AddRelationship automatically adds MaintainRelationshipObjective to both
    state.AddRelationship(new Relationship
    {
        Id = state.GenerateEntityId(),
        PersonAId = guy.Id,
        PersonBId = girl.Id,
        Type = RelationshipType.Dating,
        StartedAt = SimStart.AddDays(-30)
    });

    // Verify the objective was injected before simulation starts
    Assert.Contains(guy.Objectives,
        o => o is MaintainRelationshipObjective m && m.PartnerPersonId == girl.Id);

    // Run for 24 hours — the MaintainRelationshipObjective should trigger
    // an OrganizeDateObjective, which calls the girl, which leads to a date
    RunSimulation(state, 24 * 60, guy, girl);

    // Group was formed and disbanded
    Assert.Single(state.Groups);
    var group = state.Groups.Values.Single();
    Assert.Equal(GroupStatus.Disbanded, group.Status);

    // Both attended the diner
    var guyEvents = state.Journal.GetEventsForPerson(guy.Id);
    var girlEvents = state.Journal.GetEventsForPerson(girl.Id);
    Assert.Contains(guyEvents, e =>
        e.EventType == SimulationEventType.ActivityStarted && e.Description == "having dinner");
    Assert.Contains(girlEvents, e =>
        e.EventType == SimulationEventType.ActivityStarted && e.Description == "having dinner");
}
```

- [ ] **Step 3: Run the test**

```
dotnet test stakeout.tests/ --filter "DatingCouple_NoPreAddedObjective_EventuallyGoOnDate" -v minimal
```
Expected: PASS if implementation is correct end-to-end. If it fails, diagnose from the assertion message and fix the issue.

Common failure modes to check:
- `MaintainRelationshipObjective.GetActions` proposing a meetup time that's already past (if the planning loop runs late in the day — check `ProposeMeetupTime` logic)
- `OrganizeDateObjective` not finding the recipient's phone fixture (verify `girlPhone` is wired to `girl.HomePhoneFixtureId`)
- `NeedsReplan` not triggering a second planning pass (verify ActionRunner.Tick handles it before the existing plan runs out)

- [ ] **Step 4: Run full suite**

```
dotnet test stakeout.tests/ -v minimal
```
Expected: all pass.

- [ ] **Step 5: Commit**

```
git add stakeout.tests/Simulation/Groups/DateCoordinationIntegrationTests.cs
```
```
git commit -m "test: integration test — dating couple goes on date without pre-added objective"
```

---

## Self-Review Checklist

**Spec coverage:**
- [x] `MaintainRelationshipObjective` created with `PartnerPersonId` property — Task 1
- [x] Returns empty list from `GetActions` (meta-objective) — Task 1
- [x] Injects `OrganizeDateObjective` when no active one exists — Task 1
- [x] Skips if `GoOnDateObjective` active (already on a date) — Task 1
- [x] Skips if dated within last 7 days — Task 1
- [x] Skips if no `Diner` address found in state — Task 1
- [x] Sets `NeedsReplan = true` after injecting — Task 1
- [x] `AddRelationship` adds objective to both parties for `Dating` type — Task 2
- [x] Idempotent: no duplicate objectives if called twice — Task 2
- [x] Non-Dating relationships do not add objective — Task 2
- [x] Safe if person not in `state.People` — Task 2
- [x] Integration test: full chain from relationship to date without pre-seeded objective — Task 3

**Placeholder scan:** None found.

**Type consistency:**
- `MaintainRelationshipObjective(int partnerPersonId)` constructor used consistently
- `PartnerPersonId` property name used in all tests
- `OrganizeDateObjective.TargetPersonId` matches existing definition
- `GoOnDateObjective` referenced from existing implementation
