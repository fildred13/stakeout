using System;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Events;
using Stakeout.Simulation.Objectives;
using Stakeout.Simulation.Traces;

namespace Stakeout.Simulation.Actions;

public static class ActionExecutor
{
    public static void Execute(SimTask task, Person actor, Objective objective, SimulationState state)
    {
        switch (task.ActionType)
        {
            case ActionType.KillPerson:
                ExecuteKillPerson(task, actor, objective, state);
                break;

            case ActionType.Work:
            case ActionType.Sleep:
            case ActionType.Idle:
            case ActionType.TravelByCar:
                // No-op for now
                break;
        }
    }

    private static void ExecuteKillPerson(SimTask task, Person killer, Objective objective, SimulationState state)
    {
        // Get victimId from task.ActionData, fall back to objective.Data
        int victimId;
        if (task.ActionData != null && task.ActionData.TryGetValue("VictimId", out var taskVictimId))
        {
            victimId = Convert.ToInt32(taskVictimId);
        }
        else
        {
            victimId = Convert.ToInt32(objective.Data["VictimId"]);
        }

        var victim = state.People[victimId];

        // 1. Kill the victim
        victim.IsAlive = false;
        victim.CurrentAction = ActionType.Idle;

        // 2. Log PersonDied event
        state.Journal.Append(new SimulationEvent
        {
            Timestamp = state.Clock.CurrentTime,
            PersonId = victim.Id,
            EventType = SimulationEventType.PersonDied,
            AddressId = victim.CurrentAddressId
        });

        // 3. Create Condition trace (attached to victim, created by killer)
        var conditionTrace = new Trace
        {
            Id = state.GenerateEntityId(),
            TraceType = TraceType.Condition,
            CreatedAt = state.Clock.CurrentTime,
            CreatedByPersonId = killer.Id,
            AttachedToPersonId = victim.Id,
            Description = "Cause of death: homicide"
        };
        state.Traces[conditionTrace.Id] = conditionTrace;

        // 4. Create Mark trace (at killer's current address, created by killer)
        var markTrace = new Trace
        {
            Id = state.GenerateEntityId(),
            TraceType = TraceType.Mark,
            CreatedAt = state.Clock.CurrentTime,
            CreatedByPersonId = killer.Id,
            LocationId = killer.CurrentAddressId,
            Description = "Signs of violent struggle"
        };
        state.Traces[markTrace.Id] = markTrace;

        // 5. Log CrimeCommitted event
        state.Journal.Append(new SimulationEvent
        {
            Timestamp = state.Clock.CurrentTime,
            PersonId = killer.Id,
            EventType = SimulationEventType.CrimeCommitted
        });
    }
}
