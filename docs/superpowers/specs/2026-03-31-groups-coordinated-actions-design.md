# Groups & Coordinated Actions — Design Spec (P5)

## Overview

This project adds the Group entity, telephone coordination, and a minimal relationship system to the simulation. The proof-of-concept is a complete "date" scenario: a guy calls his girlfriend to arrange a date, she may or may not be home to answer, they coordinate (possibly via answering machine and callback), and then execute a full pickup → diner → drop-off flow together. This exercises the entire stack and directly parallels the crime coordination system P7 will build on.

**Full design spec path:** `docs/superpowers/specs/2026-03-31-groups-coordinated-actions-design.md`

---

## Design Decisions

### Group Formation: Re-plan on Formation (not DayPlan takeover)

When a group forms, all members get a `GoOnDateObjective` (or equivalent) added to their `Objectives` list, and `NpcBrain.PlanDay(from: now)` is called for each member immediately. The existing planner handles the rest — the Group objective slots in at its priority level like any other objective. No second scheduler, no group-owned DayPlan.

This handles reactive formation cleanly: a group formed at 2pm for 6pm today is accommodated by a re-plan from 2pm. "Re-plan from now" is the same algorithm as morning planning, already implemented.

On disband, `GoOnDateObjective` is removed from both members and re-planning fires again for the remaining day.

### Telephone Coordination: Simulated as Actions

Phone calls are first-class simulated actions, not instant events. The caller has an `OrganizeDateObjective` that schedules a `PhoneCallAction` during the day. The call takes 10 sim-minutes to execute and emits phone record traces on both participants.

Phone calls are to an **address + telephone fixture**, not to a person. The recipient must be physically present at the target address to answer. If absent, the message is recorded on the telephone fixture itself — the fixture is both phone and answering machine. This matches the 1984 setting (landlines only) and makes phone records meaningful investigation evidence.

### Telephone Fixture Unified

A telephone fixture handles both answering and message recording — no separate answering machine fixture. Every telephone fixture always accepts messages. A missed call leaves a message trace on the fixture; the recipient checks the same fixture when they arrive home.

### Shared Vehicle Travel: Driver Sets Both TravelInfos

When two people travel together, the driver's `GoOnDateGroupAction` sets `TravelInfo` on both itself and the passenger via `ctx.State.Persons[passengerId]`. Same destination, same ETA. No separate group-level travel state. The Group entity's `CurrentPhase` is the coordination signal — the passenger's action waits for phase transitions driven by the driver's arrival events.

### OnSuspend / OnResume: Implement Now

`IAction.OnSuspend(ActionContext)` and `IAction.OnResume(ActionContext)` were explicitly deferred from P3 to P5. Implemented here for `IAction` (default no-op) and `ActionSequence` (saves/restores current step index). Required for future mid-action interrupts (crime arrests, urgent calls).

### Invitation Processing: At Action Boundaries

`ActionRunner` checks for pending invitations after each action completes, before calling `AdvanceToNext`. If a `PendingInvitation` is found, an `AcceptPhoneCallAction` is inserted as the immediate next activity. No mid-action interruption needed for P5.

Similarly, when a person arrives at their home address (`TravelInfo` resolves to `HomeAddressId`), `ActionRunner` checks for unread message traces on the home telephone fixture and inserts a `CheckAnsweringMachineAction` at the next action boundary if found.

---

## New Entities

### `Relationship`

```csharp
public class Relationship
{
    public int Id { get; set; }
    public int PersonAId { get; set; }
    public int PersonBId { get; set; }
    public RelationshipType Type { get; set; }
    public DateTime StartedAt { get; set; }
}

public enum RelationshipType { Dating, Friend, CriminalAssociate }
```

`SimulationState` gets: `Dictionary<int, List<Relationship>> RelationshipsByPersonId`

Helper: `GetRelationship(personAId, personBId)` — checks both directions.

### `Group`

```csharp
public class Group
{
    public int Id { get; set; }
    public GroupType Type { get; set; }
    public GroupStatus Status { get; set; }
    public List<int> MemberPersonIds { get; set; }
    public int? DriverPersonId { get; set; }
    public int PickupAddressId { get; set; }
    public DateTime PickupTime { get; set; }
    public int MeetupAddressId { get; set; }
    public DateTime MeetupTime { get; set; }
    public GroupPhase CurrentPhase { get; set; }
}

public enum GroupType { Date, CriminalMeeting }
public enum GroupStatus { Forming, Active, Disbanded }
public enum GroupPhase { DriverEnRoute, AtPickup, DrivingToVenue, AtVenue, DrivingBack, Complete }
```

`SimulationState` gets: `Dictionary<int, Group> Groups`

### `PendingInvitation`

```csharp
public class PendingInvitation
{
    public int Id { get; set; }
    public int FromPersonId { get; set; }
    public int ToPersonId { get; set; }
    public InvitationType Type { get; set; }
    public int ProposedGroupId { get; set; }
    public DateTime CreatedAt { get; set; }
}

public enum InvitationType { DateInvitation, MeetingRequest }
```

`SimulationState` gets: `Dictionary<int, List<PendingInvitation>> PendingInvitationsByPersonId`

### `Vehicle`

```csharp
public class Vehicle
{
    public int Id { get; set; }
    public int OwnerPersonId { get; set; }
    public int CurrentAddressId { get; set; }
    public VehicleType Type { get; set; }
}

public enum VehicleType { Car }
```

`Person` gets: `int? VehicleId`

`SimulationState` gets: `Dictionary<int, Vehicle> Vehicles`

### `Person` additions

```csharp
public int? HomePhoneFixtureId { get; set; }   // nullable — not everyone has a phone
public int? VehicleId { get; set; }
```

---

## Telephone System

### Telephone Fixture

A `telephone` fixture type is added to address templates. It serves as both the phone you answer and the machine that records messages — always. Address templates that get a telephone fixture:

- `SuburbanHome` — kitchen telephone (sub-location: kitchen)
- `Apartment` — living room telephone
- `DiveBar` — back office telephone
- `Office` — reception telephone, manager's office telephone

`Person.HomePhoneFixtureId` points to the telephone fixture at their home address.

### `PhoneCallAction`

Parameters: `targetAddressId`, `targetFixtureId`, `proposedGroupId`, `callerId`

- Duration: 10 sim-minutes
- `OnComplete`:
  - Emits `TraceEmitter.EmitRecord` on `targetFixtureId` (phone record, no decay)
  - Emits `TraceEmitter.EmitRecord` on caller's home phone fixture (outgoing record, no decay)
  - If recipient currently at `targetAddressId`:
    - Creates `PendingInvitation` in `state.PendingInvitationsByPersonId[recipientId]`
  - If recipient absent:
    - Emits message trace on `targetFixtureId` (type: `Record`, description includes caller name + callback request)
    - Notifies `OrganizeDateObjective` → transitions to `MessageLeft`

### `AcceptPhoneCallAction`

Parameters: `invitationId`

- Duration: 2 sim-minutes (brief conversation)
- `OnComplete`:
  - Removes processed invitation from `state.PendingInvitationsByPersonId`
  - Looks up the caller's `OrganizeDateObjective` on `ctx.State.Persons[invitation.FromPersonId]` (scenario 1: guy called girl, objective is on guy) OR on `ctx.Person` (scenario 2: girl called back, guy's `OrganizeDateObjective` targets `invitation.FromPersonId`)
  - Calls `objective.OnAccepted(ctx.CurrentTime)` — the objective handles all date creation, Group activation, and re-planning (see `OrganizeDateObjective` below)

`AcceptPhoneCallAction` does NOT directly create `GoOnDateObjective` or trigger re-plans — that is exclusively `OrganizeDateObjective`'s responsibility.

### `CheckAnsweringMachineAction`

- Duration: 2 sim-minutes
- Queries telephone fixture at `person.HomeAddressId` for message traces
- For each message trace from a known contact (via `RelationshipsByPersonId`) that contains a callback request:
  - "Unread" is determined by checking whether a `CallBackObjective` already exists on this person targeting that caller — no `IsRead` flag needed on `Trace`
  - If no existing `CallBackObjective` for that caller: creates one

### ActionRunner extensions

After each action completes, before `AdvanceToNext`, `ActionRunner` runs two checks:

1. **Pending invitations** — if `state.PendingInvitationsByPersonId[person.Id]` has entries, insert `AcceptPhoneCallAction` as next activity, clear the entry
2. **Answering machine** (only when arriving home) — when `TravelInfo` resolves to `person.HomeAddressId`, check for unread message traces on `person.HomePhoneFixtureId`. If found, insert `CheckAnsweringMachineAction` at next boundary

### `IAction` interface additions

```csharp
void OnSuspend(ActionContext ctx);   // default: no-op
void OnResume(ActionContext ctx);    // default: no-op
```

`ActionSequence` implements both: `OnSuspend` saves current step index; `OnResume` restores it.

---

## Objectives

Priority ladder (updated):

| Priority | Objective | Source |
|---|---|---|
| 100 | *(Crime — P7)* | Crime |
| 80 | SleepObjective | Universal |
| 70 | GoOnDateObjective | Social |
| 60 | WorkShiftObjective | Job |
| 50 | OrganizeDateObjective | Social |
| 40 | EatOutObjective | Trait |
| 20 | GoForARunObjective | Trait |
| 10 | CallBackObjective | Social |
| 0 | IdleAtHome | Universal |

`ObjectiveSource` gets a `Social` variant added alongside `Universal/Trait/Job/Crime`.

### `OrganizeDateObjective`

State machine: `NeedToCall → AwaitingAnswer → MessageLeft → AwaitingCallback → DateOrganized`

Constructor parameters: `targetPersonId`, `proposedMeetupAddressId`, `proposedMeetupTime`, `proposedPickupTime`

`GetActions()` behavior by state:
- `NeedToCall` → creates a `Group` (Status=Forming), returns `PlannedAction` wrapping `PhoneCallAction` targeting recipient's `HomePhoneFixtureId` at the proposed call time
- `AwaitingAnswer` / `AwaitingCallback` / `MessageLeft` → returns empty (gaps filled by planner with idle)
- `DateOrganized` → objective completes itself

`OnAccepted(DateTime acceptedAt)` — called by `AcceptPhoneCallAction`:
- Sets `group.Status = Active`
- Checks if `proposedMeetupTime` is still achievable: `acceptedAt + 2 hours <= proposedMeetupTime`
  - If yes → use today's proposed time
  - If no → advance to next available evening (next day at same proposed time-of-day)
- Updates `group.MeetupTime` and `group.PickupTime` to the final decided time
- Adds `GoOnDateObjective` to both people's `Objectives`
- Calls `NpcBrain.PlanDay(from: acceptedAt)` for both people
- Sets new `DayPlan`s on both
- Transitions to `DateOrganized`

### `GoOnDateObjective`

Constructor parameters: `groupId`

`GetActions()`: reads `group.PickupTime` and `group.MeetupAddressId`. Returns a single `PlannedAction` wrapping `GoOnDateGroupAction`, time-windowed from `PickupTime` to estimated return (meetup duration + travel back). On complete, calls `Group.Disband()`.

### `CallBackObjective`

Constructor parameters: `targetPersonId`, `targetFixtureId`, `targetAddressId`

Priority: 10. `GetActions()` returns `PlannedAction` wrapping `PhoneCallAction` to the original caller's home telephone fixture. When the call is answered, the caller's `OrganizeDateObjective` (in `AwaitingCallback` state) handles the negotiation and date creation.

---

## Group Coordination

### `GoOnDateGroupAction`

Instantiated separately for driver and passenger. Both hold `groupId` and check `group.CurrentPhase` each tick. The driver advances phases; the passenger reacts.

**Driver tick behavior by phase:**

| Phase | Driver behavior | Transition |
|---|---|---|
| `DriverEnRoute` | Normal travel to `group.PickupAddressId` (own TravelInfo) | On arrival → `AtPickup` |
| `AtPickup` | Wait 1 sim-minute; set TravelInfo on self AND `ctx.State.Persons[passengerId]` to `MeetupAddressId` with same ETA | Advance to `DrivingToVenue` |
| `DrivingToVenue` | Wait for own TravelInfo to resolve | On arrival → `AtVenue` |
| `AtVenue` | Wait for date duration (2 hours); set TravelInfo on self AND passenger back to `PickupAddressId` | Advance to `DrivingBack` |
| `DrivingBack` | Wait for own TravelInfo to resolve | On arrival → `Complete`; call `Group.Disband()` |
| `Complete` | Action ends | — |

**Passenger tick behavior by phase:**

| Phase | Passenger behavior |
|---|---|
| `DriverEnRoute` | Wait at home |
| `AtPickup` | Wait 1 sim-minute (gets in car) |
| `DrivingToVenue` | Wait for own TravelInfo to resolve (set by driver) |
| `AtVenue` | Wait for date duration (2 hours) |
| `DrivingBack` | Wait for own TravelInfo to resolve (set by driver) |
| `Complete` | Action ends |

**`Group.Disband()`:**
1. Sets `group.Status = Disbanded`
2. Removes `GoOnDateObjective` from both members' `Objectives`
3. Calls `NpcBrain.PlanDay(from: now)` for each member
4. Sets new `DayPlan`s on both

---

## Integration Tests

Both tests exercise the real simulation engine against real `SimulationState`. No mocked services. All assertions are on `SimulationState` — person locations, trace presence, group status — not on method call sequences.

### Test 1: She Answers

**Minimal setup:**
- `guy` and `girl` with `Dating` relationship
- `girl.HomeAddressId` has a kitchen telephone fixture; `girl.HomePhoneFixtureId` set
- A diner address exists in state
- `guy` has `OrganizeDateObjective` (call at noon, propose diner at 7pm, pickup at 5:50pm)
- `guy` has a vehicle (`VehicleId` set)
- Both start the day at their respective homes

**Assertions (player-observable):**
- After advancing to 12:10pm: phone record trace exists on girl's telephone fixture
- After advancing past girl's next action boundary: `Group` in state with both member IDs, `Status = Active`
- At ~5:50pm: `guy.CurrentAddressId` == `girl.HomeAddressId`
- At ~7pm: both `guy` and `girl` have `CurrentAddressId` == diner address
- After 9pm: `group.Status == Disbanded`, `girl.CurrentAddressId` == her home, `guy` traveling toward his home

### Test 2: Answering Machine Path

**Minimal setup:**
- Same `guy` and `girl`, same diner, same telephone fixture on girl's home
- `guy.HomePhoneFixtureId` set (needed for callback)
- `guy` has `OrganizeDateObjective` (call at noon)
- `girl` starts at a different address at noon (away from home) and is scheduled to return home at 6pm (give her a single `WaitAction` at another address until 6pm, then she travels home)
- `guy` has a vehicle

**Assertions:**
- After advancing to 12:10pm: message trace exists on girl's telephone fixture; no `Group` in state yet
- After advancing to ~6:10pm (girl arrives home + action boundary): `girl.CurrentActivity.Name` briefly shows check action (or simply: `CallBackObjective` in `girl.Objectives`)
- After advancing past girl's callback call: phone record trace on `guy`'s telephone fixture
- `Group` exists with `MeetupTime.Date == tomorrow`
- Next day at `MeetupTime`: both `guy` and `girl` have `CurrentAddressId` == diner address
- Next day after date: `group.Status == Disbanded`

---

## What This Delivers

- `Relationship` entity + `RelationshipsByPersonId` on SimulationState
- `Group` entity + lifecycle (`Forming → Active → Disbanded`) + phase coordination
- `Vehicle` entity + `Person.VehicleId`
- `PendingInvitation` + `PendingInvitationsByPersonId` on SimulationState
- Telephone fixture type added to address templates (SuburbanHome, Apartment, DiveBar, Office)
- `PhoneCallAction`, `AcceptPhoneCallAction`, `CheckAnsweringMachineAction`
- `OrganizeDateObjective` (state machine), `GoOnDateObjective`, `CallBackObjective`
- `GoOnDateGroupAction` (driver + passenger coordination via shared Group phase)
- `IAction.OnSuspend` / `IAction.OnResume` (default no-op + ActionSequence implementation)
- ActionRunner extensions: invitation interrupt + arriving-home answering machine check
- `ObjectiveSource.Social` added
- Two integration tests: she answers / answering machine path

## What This Does NOT Deliver (deferred)

- Split/Regroup for parallel sub-actions (P7 heist complexity)
- Mid-action interrupts (OnSuspend/OnResume added to interface, not exercised in P5)
- Reactive group interruption (person mid-action when invitation arrives — P5 only processes at action boundaries)
- Future-day date scheduling beyond same-day/next-day
- `CriminalMeeting` group action template (scaffolded via GroupType enum, implemented in P7)
- Trait-driven social objectives (e.g. "loner" trait reducing GoOnDate frequency)
