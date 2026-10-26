# Businesses & Staffing

## Purpose
Gives commercial addresses an operator: a named Business with opening hours and staff Positions (role, shift,
work days). NPCs get jobs by filling Positions, and shifts become WorkShiftObjectives in their day plans.

## Key Files
| File | Role |
|------|------|
| `src/simulation/entities/Business.cs` | Business, BusinessHours (per weekday, open/close; 00:00-00:00 = 24h), Position |
| `src/simulation/businesses/IBusinessTemplate.cs` | Hours, positions and name generation for one BusinessType |
| `src/simulation/businesses/*BusinessTemplate.cs` | Diner (24/7, three 8h shifts), DiveBar, Office |
| `src/simulation/businesses/BusinessTemplateRegistry.cs` | BusinessType to template |
| `src/simulation/businesses/BusinessGenerator.cs` | Creates a Business per Commercial address (AddressType to BusinessType map) |
| `src/simulation/BusinessResolver.cs` | Spawns NPCs for every unfilled Position and marks the business resolved |
| `src/simulation/objectives/WorkShiftObjective.cs` | Turns a Position's shifts into PlannedActions at the business address |
| `src/simulation/scheduling/DoorLockingService.cs` | Each tick, locks/unlocks the `main_entrance` of resolved businesses by opening hours |

## How It Works
1. `CityGenerator` calls `BusinessGenerator.CreateBusinessesForCity`: every Commercial address gets a Business with
   hours and Positions, but no staff (`IsResolved = false`).
2. `PersonGenerator.GeneratePerson` gives each new NPC a job: either the Position in its `SpawnRequirements`, or the
   first open Position in its city. It resolves the workplace interior, sets `Person.BusinessId/PositionId`, marks
   the Position assigned, and adds a `WorkShiftObjective`.
3. `BusinessResolver.Resolve` fills *all* remaining Positions of one business at once (used when a business becomes
   relevant, e.g. the player visits).
4. `WorkShiftObjective.GetActions` emits one entry per shift overlapping the plan window, including overnight shifts
   and a commute buffer when replanning mid-shift.

## Key Decisions
- **Businesses exist up front, staff on demand:** fits the progressive-growth model; only relevant workplaces are fully staffed.
- **Business separate from Address:** the building (Address/Locations) and its operator (Business) can change independently.
- **Door locking keyed to `IsResolved`:** unstaffed businesses are skipped. Note that nothing in the live game calls
  `BusinessResolver` yet, so in play no doors are locked by this service (only covered by tests).

## Connection Points
- **City generation** creates businesses; **location hierarchy** provides the `main_entrance` AccessPoint.
- **NPC brain** schedules WorkShiftObjective (priority 60).
- **Adding a business type:** steps 9-11 of the `add-address-type` skill.
