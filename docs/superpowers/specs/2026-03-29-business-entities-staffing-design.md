# Project 4: Business Entities & Staffing — Design Spec

## Overview

Businesses become first-class entities in the simulation. Every commercial address gets a Business shell during city generation — with a name, operating hours, and empty position slots. Every NPC gets assigned to a real business position, giving them a concrete work schedule driven by WorkShiftObjective. Business resolution (spawning all remaining coworkers) is deferred until triggered by player interaction or narrative need.

This project also introduces a constrained person generation pattern: `SpawnRequirements` lets callers specify constraints (business, position, home address, etc.) when spawning NPCs. P4 implements the business constraint; future projects add more.

## Design Decisions

### Job Entity Removed

The `Job` entity, `JobType` enum, and `SimulationState.Jobs` dictionary are removed. All shift/role data lives on `Position`, which is owned by `Business`. `Person` gets `BusinessId` and `PositionId` fields instead of `JobId`. This eliminates redundant data — Position is the single source of truth for what a person does and when.

### Businesses Are Data Sources, Not Managers

A business defines what positions exist and what hours it operates. It does not actively manage NPCs at runtime. Workers show up via their `WorkShiftObjective`, which reads position data. The business is passive.

### Lazy Resolution

City gen creates business shells (name, hours, positions — all empty). When `PersonGenerator` spawns an NPC, it claims one open position at a business but does NOT fill the other positions. `BusinessResolver.Resolve()` fills all remaining positions on demand — triggered by player interaction, crime casting, or debug tools. This means most businesses in the simulation have 1-2 assigned workers (whoever happened to be spawned) with the rest empty until needed.

### Same-City Employment

NPCs always work in their home city. PersonGenerator filters to businesses within the NPC's home city when picking a position. Cross-city commuting is deferred.

### No CommuteAction Needed

The master plan mentions `CommuteAction` as a P4 deliverable. This is unnecessary — P3's `ActionRunner` already handles inter-address travel at the engine level. When `WorkShiftObjective` produces a `PlannedAction` targeting the business address, the engine automatically initiates travel if the NPC isn't already there. Commuting is implicit, not a separate action.

### Schedule-Driven Door Locking

Business entrance doors lock/unlock based on operating hours, not individual worker arrivals. Only resolved businesses participate in door locking. Unresolved business shells use default door state from their address template.

## Data Model

### Business

```csharp
public class Business
{
    public int Id { get; set; }
    public int AddressId { get; set; }
    public string Name { get; set; }
    public BusinessType Type { get; set; }
    public List<BusinessHours> Hours { get; set; }  // 7 entries, one per day
    public List<Position> Positions { get; set; }
    public bool IsResolved { get; set; }  // false = shell, true = all positions filled
}
```

### BusinessType

```csharp
public enum BusinessType { Diner, DiveBar, Office }
```

Maps 1:1 to existing commercial `AddressType` values.

### BusinessHours

```csharp
public class BusinessHours
{
    public DayOfWeek Day { get; set; }
    public TimeSpan? OpenTime { get; set; }   // null = closed that day
    public TimeSpan? CloseTime { get; set; }  // can cross midnight (e.g., 01:00 next day)
}
```

CloseTime crossing midnight uses the same pattern as `SleepScheduleCalculator` — a close time less than open time means next day.

### Position

```csharp
public class Position
{
    public int Id { get; set; }
    public int BusinessId { get; set; }
    public string Role { get; set; }          // "cook", "waiter", "bartender", "manager", etc.
    public TimeSpan ShiftStart { get; set; }
    public TimeSpan ShiftEnd { get; set; }
    public DayOfWeek[] WorkDays { get; set; }
    public int? AssignedPersonId { get; set; } // null until someone is assigned
}
```

Role is a string (not an enum) to keep it extensible for future business types.

### Person Entity Changes

```csharp
// Removed:
public int JobId { get; set; }

// Added:
public int? BusinessId { get; set; }
public int? PositionId { get; set; }
```

### SimulationState Changes

```csharp
// Removed:
Dictionary<int, Job> Jobs

// Added:
Dictionary<int, Business> Businesses
```

## Business Templates

### IBusinessTemplate

```csharp
public interface IBusinessTemplate
{
    BusinessType Type { get; }
    List<BusinessHours> GenerateHours();
    List<Position> GeneratePositions(SimulationState state);
    string GenerateName(Random random);
}
```

### DinerTemplate

- **Hours:** 24/7 — all 7 days, open 00:00 to 00:00
- **Positions:** Per shift: 1 cook, 1-2 waiters, 0-1 manager. A 24/7 diner needs ~3 shifts to cover the day, so total positions are 3 cooks, 3-6 waiters, 0-3 managers. Each position gets a specific shift window (e.g., cook 5am-1pm, cook 1pm-9pm, cook 9pm-5am). Position counts randomized within ranges per instance.
- **Names:** Pool-based patterns — "{FirstName}'s Diner", "{LastName}'s", "The {Adjective} Spoon"

### DiveBarTemplate

- **Hours:** Mon–Thu noon–1am, Fri–Sat noon–4am, Sun closed
- **Positions:** 1-2 bartenders, 0-1 manager. All shifts aligned to operating hours.
- **Names:** "The {Adjective} {Noun}" — "The Rusty Nail", "The Blind Pig", "The Brass Monkey"

### OfficeTemplate

- **Hours:** Mon–Fri 7am–7pm, Sat–Sun closed
- **Positions:** 5-10 office workers, 1-2 managers, 1 CEO. All work 9-5 weekdays.
- **Names:** "{LastName} & {LastName}", "{LastName} {Suffix}" — "Parker & Shaw", "Meridian Associates"

### Name Generation

`BusinessNameGenerator` static class with per-type word pools. Simple pattern: pick a template string, fill slots from pools. Sits alongside existing `NameData`.

### City Generation Integration

When the city grid places a commercial address, `BusinessGenerator.CreateBusiness()` is called:

1. Look up the `IBusinessTemplate` for the address type
2. Generate hours, positions (with entity IDs, no assigned people), and name
3. Create Business entity with `IsResolved = false`
4. Add to `state.Businesses`

This hooks into the existing city generation flow alongside address placement.

## Constrained Person Generation

### SpawnRequirements

```csharp
public class SpawnRequirements
{
    public int? BusinessId { get; set; }
    public int? PositionId { get; set; }
    public int? HomeAddressId { get; set; }
    public int? HomeLocationId { get; set; }
    // Future: relationships, proximity constraints, required traits, etc.
}
```

A flat bag of optional constraints. Null means "pick randomly." P4 uses `BusinessId` + `PositionId`. Future projects add fields without changing the interface.

### PersonGenerator Changes

New overload:

```csharp
public Person GeneratePerson(SimulationState state)  // existing — calls new overload with empty requirements
public Person GeneratePerson(SimulationState state, SpawnRequirements requirements)
```

**All paths** produce a fully-scheduled NPC with a real business position:

1. **If requirements specify business/position:** use those directly
2. **If unconstrained:** pick a random business (in the NPC's home city) with an open position
3. Claim the position: `position.AssignedPersonId = person.Id`
4. Set `person.BusinessId` and `person.PositionId`
5. Compute commute time from home-to-work distance (same as current), then compute sleep schedule from position shift times + commute via `SleepScheduleCalculator`
6. Create `WorkShiftObjective(businessId, positionId)`
7. Everything else unchanged: home, traits, home key, etc.

### SleepScheduleCalculator

Signature changes from `Compute(Job, float commuteHours)` to `Compute(Position, float commuteHours)`. Reads `ShiftStart` and `ShiftEnd` from Position instead of Job. Commute hours still passed in from `MapConfig.ComputeTravelTimeHours()`. Same algorithm.

## Business Resolution

### BusinessResolver

Pseudocode (not final signatures — refer to actual `LocationGenerator` and `PersonGenerator` APIs during implementation):

```
Resolve(state, business, generator):
    if business.IsResolved: return empty
    ensure address interior is resolved (via LocationGenerator)
    for each position in business.Positions:
        if position already assigned: skip
        spawn person with SpawnRequirements { BusinessId, PositionId }
        collect spawned person
    set business.IsResolved = true
    return spawned list
```

### Resolution Triggers (P4 Scope)

P4 does not build an automatic trigger system. Resolution is called explicitly:

- Test code
- Debug tools
- Future: player visits address, player calls business, crime system needs a coworker

The returned `List<Person>` lets callers reference the spawned NPCs.

## WorkShiftObjective

```csharp
public class WorkShiftObjective : Objective
{
    public override int Priority => 60;
    public override ObjectiveSource Source => ObjectiveSource.Job;

    private readonly int _businessId;
    private readonly int _positionId;
}
```

`GetActions` implementation:

1. Look up the Position from state (via business → positions list)
2. For each day in the plan window that matches `Position.WorkDays`:
   - Create a `PlannedAction` targeting the business's address
   - Time window: shift start to shift end
   - Action: `WaitAction` with role-based display text ("working as cook", "tending bar", "working in office")
3. Return the list

Follows the exact pattern of `SleepObjective` — produces `PlannedAction` entries that the brain's greedy scheduler slots at priority 60.

## DoorLockingService

Schedule-driven, resolved businesses only:

```csharp
public static class DoorLockingService
{
    public static void UpdateDoorStates(SimulationState state, DateTime currentTime)
    {
        foreach (var business in state.Businesses.Values)
        {
            if (!business.IsResolved) continue;

            var hours = GetHoursForDay(business, currentTime.DayOfWeek);
            var isOpen = IsWithinOperatingHours(hours, currentTime.TimeOfDay);

            SetEntranceLockState(state, business.AddressId, locked: !isOpen);
        }
    }
}
```

**Finding entrances:** Iterates the business address's locations, finds access points tagged `"main_entrance"`, sets `IsLocked`.

**Execution:** Called from `SimulationManager._Process` at a reasonable interval (e.g., once per game-minute). Cheap operation — just iterates resolved businesses and compares times.

**Replaces** the old per-person stub signature. The old `LockEntrances(state, person)` / `UnlockEntrances(state, person)` methods are removed.

## Debug Inspector Updates

At the existing P4 TODO marker in GameShell, restore the job section showing:

- Business name and type
- Role (from position)
- Shift times and work days
- Current status: on shift / off duty / day off

Always available since every NPC has a business and position.

## Removals

- `Job` entity class
- `JobType` enum
- `SimulationState.Jobs` dictionary
- `PersonGenerator.CreateJob()` method
- `PersonGenerator.PickJobType()` method
- `PersonGenerator.JobTypeToAddressType()` method
- `PersonGenerator.GenerateDinerShiftStart()` method
- Old `DoorLockingService` stub signatures

## Test Coverage

- **Business template tests:** Each template generates valid hours, correct position counts/roles, non-empty names
- **City generation tests:** Commercial addresses get business shells, businesses link to correct addresses
- **PersonGenerator tests:** Constrained path fills specified position; unconstrained path claims an open position; all NPCs get WorkShiftObjective; same-city constraint holds
- **BusinessResolver tests:** Fills all empty positions, sets IsResolved, skips already-assigned positions, resolves address interior if needed
- **WorkShiftObjective tests:** Produces actions only on work days, correct shift times, correct target address, role-based display text
- **DoorLockingService tests:** Doors lock outside hours, unlock during hours, midnight-crossing hours work, unresolved businesses skipped
- **SleepScheduleCalculator tests:** Works with Position instead of Job (same behavior, new signature)
- **Integration test:** Full day simulation with business-employed NPC — plans day with sleep + work + hobbies, travels to work, returns home
