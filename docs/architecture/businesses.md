# Businesses & Staffing

## Purpose
Gives commercial addresses an operator: a named Business with opening hours and staff Positions (role, shift,
work days). Every Position is filled at world creation.

## Key Files
| File | Role |
|------|------|
| `src/simulation/entities/Business.cs` | Business, BusinessHours (per weekday; 00:00-00:00 = 24h), Position |
| `src/simulation/businesses/IBusinessTemplate.cs` | Hours, positions and name generation for one BusinessType |
| `src/simulation/businesses/*BusinessTemplate.cs` | Diner (24/7, three shifts, staggered days off), DiveBar, Office |
| `src/simulation/businesses/BusinessTemplateRegistry.cs` | BusinessType to template |
| `src/simulation/businesses/BusinessGenerator.cs` | Creates a Business per Commercial address |
| `src/simulation/agents/Planner.cs` | `Shifts` (shift instances in a window), `IsOpen` (opening hours) |

## How It Works
1. `CityGenerator` calls `BusinessGenerator.CreateBusinessesForCity`.
2. `PopulationGenerator` creates one person per Position and derives their sleep schedule from the shift.
3. The planner puts each shift in the worker's agenda; `Places.Workplace` puts them in the room for their role.
4. Customers only plan visits while the business is open; the detective needs opening hours to enter public rooms.

## Key Decisions
- **Fully staffed up front:** open diners always have someone on site (an invariant checks this), and every worker
  is a potential witness.
- **Diner days off are staggered** across a shift's positions so nobody works seven days and no shift is empty.

## Connection Points
- **City generation** creates businesses; **agents** schedule the shifts; **witness memory** lets staff recognize regulars.
- **Adding a business type:** steps 9-11 of the `add-address-type` skill.
