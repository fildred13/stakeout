# Progressive Apartment Unit Assignment

## Purpose

When a person is generated as living in an apartment building, assign them a specific apartment unit and generate that unit's floor if it doesn't exist yet. This enables progressive generation — only floors with residents get expanded.

## Data Model Changes

### Person

Add `HomeUnitTag` (nullable string). For apartment residents this holds their unit tag (e.g., `"unit_3"`). For suburban homes it stays null.

### SimTask

Add `UnitTag` (nullable string). Tasks targeting a specific unit within an address carry this tag so decomposition strategies can scope their room lookups.

## ExpandFloor Change

`ApartmentBuildingGenerator.ExpandFloor` currently creates rooms without unit-level grouping tags. Change: each room in a unit gets an additional `unit_N` tag alongside its existing tags (bedroom, kitchen, etc.).

Example: "Apt 3 Bedroom" gets tags `["bedroom", "private", "unit_3"]`.

## PersonGenerator Flow

When `homeType == ApartmentBuilding`, after generating the home address:

1. Pick a random floor placeholder from the building.
2. If `IsGenerated == false`, call `ApartmentBuildingGenerator.ExpandFloor`.
3. Collect all distinct `unit_N` tags on that floor.
4. Filter out units already claimed by existing people (matching `HomeAddressId` + `HomeUnitTag`).
5. Pick randomly from remaining vacant units.
6. If no vacancy on that floor, pick another random floor and repeat.
7. Set `Person.HomeUnitTag` to the chosen unit tag.

For `SuburbanHome`, skip all of the above — `HomeUnitTag` stays null.

## Decomposition Strategy Changes

### SleepDecomposition

Currently calls `graph.FindByTag("bedroom")` which matches any bedroom in the building. Change: when `SimTask.UnitTag` is set, find all sublocations with that unit tag, then pick the one also tagged `bedroom`.

Fallback: if no unit tag is set (suburban home), behavior is unchanged.

### InhabitDecomposition

Same pattern — scope `FindByTag` calls for bedroom, kitchen, restroom, living by intersecting with the unit tag when present.

Both strategies receive the unit tag via `SimTask.UnitTag`, which is populated from `Person.HomeUnitTag` when objectives targeting the home address are resolved.

## Objective/Task Wiring

`ObjectiveResolver` creates tasks with `TargetAddressId` set to the home address. It also needs to set `UnitTag` from the person's `HomeUnitTag` on home-targeting tasks (GetSleep, DefaultIdle).

## Vacancy Tracking

During person generation, vacancy is checked by scanning `state.People` for matching `HomeAddressId` + `HomeUnitTag`. This is only called at generation time, not a hot path.

## What This Does NOT Cover

- Roommates / cohabitation (future work)
- Clustering people on the same floor (future work — floor selection is purely random)
- Player investigation lookups (future work, but the data model supports unit-to-resident queries via the tag system)
