# Apartment Unit Assignment

## Purpose

When a person is generated as living in an apartment building, assign them a specific apartment unit. Apartment buildings are generated eagerly (all floors, units, and rooms at once). People get a 50% chance to reuse an existing apartment building vs. creating a new one.

## Data Model Changes

### Person

Add `HomeUnitTag` (nullable string). For apartment residents this holds their unit tag (e.g., `"unit_f2_3"` — floor 2, unit 3). For suburban homes it stays null.

### SimTask

Add `UnitTag` (nullable string). Tasks targeting a specific unit within an address carry this tag so decomposition strategies can scope their room lookups.

## ApartmentBuildingGenerator Changes

### Eager Full Generation

Replace the current two-phase approach (skeleton + lazy `ExpandFloor`) with a single `Generate` method that creates the entire building upfront:

- Road, lobby, elevator (as before)
- For each floor: hallway connected to elevator and stairs
- For each unit on each floor: bedroom, kitchen, living room, bathroom connected to the hallway

This eliminates `ExpandFloor`, floor placeholders, and the `IsGenerated` flag from this generator.

### Unit Tags

Each room in a unit gets a floor-scoped unit tag: `unit_f{floor}_{unitNumber}`. Example: "Apt 3 Bedroom" on floor 2 gets tags `["bedroom", "private", "unit_f2_3"]`.

Floor-scoping prevents collisions — without it, `FindAllByTag("unit_3")` would match rooms across every floor since the sublocation graph is flat per address.

## PersonGenerator Flow

When `homeType == ApartmentBuilding`:

1. **50% chance to reuse** an existing apartment building: scan `state.Addresses` for addresses with `Type == ApartmentBuilding`, pick one at random.
2. If reusing, check for vacancy (see below). If no vacancy found, fall through to creating a new building.
3. If creating new: call `LocationGenerator.GenerateAddress(state, ApartmentBuilding)` as before.
4. **Assign a unit**: collect all distinct `unit_f*` tags in the building's sublocations, filter out any already claimed by existing people (matching `HomeAddressId` + `HomeUnitTag`), pick randomly from remaining.
5. Set `Person.HomeUnitTag` to the chosen tag.

For `SuburbanHome`, skip all of the above — `HomeUnitTag` stays null.

## Objective/Task Wiring

`ObjectiveResolver.CreateGetSleepObjective` and `CreateDefaultIdleObjective` gain a `string unitTag` parameter (nullable). The `ResolveFunc` closures set `SimTask.UnitTag` from this parameter alongside the existing `TargetAddressId`.

`CreateMaintainJobObjective` does not need a unit tag — work addresses don't have per-person units.

In `PersonGenerator`, these factory methods are called after unit assignment, so the `HomeUnitTag` value is available to pass in.

## Decomposition Strategy Changes

### SleepDecomposition

When `task.UnitTag` is set: call `graph.FindAllByTag(unitTag)` to get the unit's rooms, then pick the one also tagged `bedroom`.

When `task.UnitTag` is null (suburban home): behavior unchanged — `graph.FindByTag("bedroom")` as before.

### InhabitDecomposition

Same scoping pattern for room-type lookups (bedroom, kitchen, restroom, living). Structural lookups (road, entrance via `FindEntryPoint`) remain unscoped — these are shared building infrastructure, not per-unit.

## Vacancy Tracking

During person generation, vacancy is checked by scanning `state.People` for matching `HomeAddressId` + `HomeUnitTag`. This is only called at generation time, not a hot path.

If a reused building has zero vacancy across all units, fall through to creating a new building.

## What This Does NOT Cover

- Roommates / cohabitation (future work)
- Clustering people on the same floor (future work — floor selection is purely random)
- Player investigation lookups (future work, but the data model supports unit-to-resident queries via the tag system)
- Removing the `IsGenerated` field from `Sublocation` (other generators may still use it; clean up separately if not needed)
