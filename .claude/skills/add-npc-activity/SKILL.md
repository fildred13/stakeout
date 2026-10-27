---
name: add-npc-activity
description: Use when adding a new NPC behavior - a solo outing in the daily planner (e.g. going to church, shopping) or a joint activity several people do together (e.g. a poker night, a heist, a drug deal). Covers planner options, joint-activity scripts, phone arrangements, facts, invariants and tests.
---

# Add an NPC Activity

Read `docs/architecture/agents.md` and `social-coordination.md` first. People never move themselves: the
planner writes appointments, the executor travels to them, joint activities move groups through `Movement`.

## A solo outing (planner option)

| Step | File | Change |
|---|---|---|
| 1 | `src/simulation/history/Facts.cs` | Add an `ActivityKind` value if none fits |
| 2 | `src/simulation/agents/Planner.cs` | Add a candidate in `ChooseOuting`: weight (traits, weekday), venue, time window, min/max minutes, verb. Mark the day in `DayContext` so it happens once |
| 3 | `src/simulation/agents/Places.cs` | If the venue needs a new `Spot` (which room), resolve it in `Resolve` |
| 4 | `src/simulation/generation/PopulationGenerator.cs` | If people need a favourite venue or a trait for it (`AssignFavorites`, trait table in `NewPerson`) |

Respect opening hours (`Planner.IsOpen`) and travel feasibility (the candidate must leave time to reach the next block).

## A joint activity

| Step | File | Change |
|---|---|---|
| 1 | `src/simulation/agents/<Name>Activity.cs` | Subclass `JointActivity`: participants, `MeetingAddressId`, `Start`, `ExpectedDuration`, `WaitingSpot`, `Run` |
| 2 | Arrangement | Create it only after a call (`PhoneSystem.Call` with a `CallPurpose`) or face to face, then `Commit()` |
| 3 | Facts | Record what observers need: `Touch` for visitors' prints, `NoiseFact`, `PurchaseFact`, `ItemMovedFact` |
| 4 | `src/simulation/diagnostics/InvariantChecker.cs` | Add a check that participants really were together if the activity has new physics |

Script rules: move groups with `MoveTogether` (one trip: the leader's car, else on foot or cab); settle with
`World.Movement.GoInside`; wait with `After`; end with `Finish()` (or `Release` one participant early). Override
`CanBeginWith` and `OnLateArrival` for partial groups, `ParticipantLost` for deaths.

## Randomness and determinism

Use `Rng.For(world.Seed, Purpose.X, keys...)` keyed by the people and the day, never `new Random()` or
`DateTime.Now`. Never iterate a `HashSet` or `Dictionary` without ordering when the order affects the outcome.

## Tests

- Run the city for a week (`WorldWeekTests` pattern) and assert the activity happens and the invariants hold.
- If it should leave evidence, assert an observer finds it (`WitnessMemory`, `Cameras`, `Fingerprints`).
- `DeterminismTests` must still pass.
