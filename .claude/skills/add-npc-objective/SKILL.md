---
name: add-npc-objective
description: Use when adding a new NPC behavior - a new Objective (something an NPC wants to do) and, if needed, a new IAction (what they do once there). Covers priorities, trait/job/social wiring, traces, and tests.
---

# Add an NPC Objective

Read `docs/architecture/npc-brain.md` first. In short: `NpcBrain.PlanDay` asks every active Objective for
`PlannedAction`s inside a 24h window, schedules them greedily by priority (travel is added by the brain), and
`ActionRunner` executes them. Objectives never move people; actions never plan.

## Files to touch

| Step | File | Change |
|---|---|---|
| 1 | `src/simulation/objectives/<Name>Objective.cs` | Subclass `Objective` |
| 2 | `src/simulation/actions/...` | Only if `WaitAction` / `ActionSequence` can't express it: a new `IAction` |
| 3 | Wiring (pick one) | Trait: `src/simulation/traits/TraitDefinitions.cs`. Job: `PersonGenerator`. Social: where the relationship/group is created (see `SimulationState.AddRelationship`). Crime: the crime template |
| 4 | `src/simulation/objectives/ObjectiveSource.cs` | Only if none of Universal/Trait/Job/Crime/Social fits |
| 5 | `stakeout.tests/Simulation/Objectives/<Name>ObjectiveTests.cs` | Unit tests on `GetActions` |
| 6 | Integration test | If it interacts with other NPCs, add one under `stakeout.tests/Simulation/` that runs real `PersonGenerator` people through `ActionRunner` (see `Groups/DateCoordinationIntegrationTests.cs`) |

## Objective pattern

```csharp
public class GoToChurchObjective : Objective
{
    private static readonly TimeSpan ServiceLength = TimeSpan.FromHours(1);

    public override int Priority => 35;
    public override ObjectiveSource Source => ObjectiveSource.Trait;

    public override List<PlannedAction> GetActions(Person person, SimulationState state,
        DateTime planStart, DateTime planEnd)
    {
        // Return an empty list when there is nothing to do this window.
        // Windows are absolute DateTimes inside [planStart, planEnd], never wall-clock TimeSpans.
        return new List<PlannedAction>
        {
            new()
            {
                Action = new WaitAction(ServiceLength, "attending a service"),
                TargetAddressId = churchId,
                TimeWindowStart = windowStart,
                TimeWindowEnd = windowEnd,
                Duration = ServiceLength,
                DisplayText = "attending a service",
                SourceObjective = this   // required, or the completion hooks below never fire
            }
        };
    }
}
```

## Priorities in use

Sleep 80 · GoOnDate 70 · WorkShift 60 · OrganizeDate 50 · MaintainRelationship 45 · EatOut 40 · GoForARun 20 ·
CallBack 10. Higher wins a contested slot; a lower-priority action is dropped for the day if no slot is left.

## Hooks you can override

- `OnActionCompleted` / `OnActionCompletedWithState` - advance a multi-step objective, set `Status = Completed`.
- `EmitTraces` - leave evidence via `TraceEmitter` after a successful action (sightings, records, marks).
- Set `person.NeedsReplan = true` (on yourself or another person) when plans must change mid-day; `ActionRunner`
  replans on the next tick.

## Rules

- Use `ctx.Random` in actions and never `new Random()` or `DateTime.Now`; use `state.Clock.CurrentTime`.
- Find places by `AddressType` / Location tags, not by names.
- Per-NPC state that must survive a save belongs on the Objective or `Person`, not in a static or in `ActionRunner`.
- Run `dotnet test stakeout.tests/`; scheduling changes often break `NpcSchedulingIntegrationTests`.
