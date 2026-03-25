using System;
using System.Collections.Generic;
using System.Linq;
using Stakeout.Simulation;
using Stakeout.Simulation.Actions;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Events;
using Stakeout.Simulation.Objectives;
using Stakeout.Simulation.Traces;
using Xunit;

namespace Stakeout.Tests.Simulation.Actions;

public class ActionExecutorTests
{
    private SimulationState MakeState()
    {
        return new SimulationState(new GameClock(new DateTime(1984, 6, 15, 22, 0, 0)));
    }

    private Person MakePerson(int id, int addressId, bool alive = true)
    {
        return new Person
        {
            Id = id,
            FirstName = "Test",
            LastName = $"Person{id}",
            IsAlive = alive,
            CurrentAddressId = addressId,
            CurrentAction = ActionType.Idle
        };
    }

    [Fact]
    public void KillPerson_SetsVictimIsAliveToFalse()
    {
        var state = MakeState();
        var killer = MakePerson(id: 1, addressId: 10);
        var victim = MakePerson(id: 2, addressId: 10);
        state.People[killer.Id] = killer;
        state.People[victim.Id] = victim;

        var objective = new Objective { Id = 1, Data = new Dictionary<string, object>() };
        var task = new SimTask
        {
            ObjectiveId = 1,
            ActionType = ActionType.KillPerson,
            ActionData = new Dictionary<string, object> { { "VictimId", 2 } }
        };

        ActionExecutor.Execute(task, killer, objective, state);

        Assert.False(victim.IsAlive);
    }

    [Fact]
    public void KillPerson_ProducesConditionTrace_AttachedToVictim()
    {
        var state = MakeState();
        var killer = MakePerson(id: 1, addressId: 10);
        var victim = MakePerson(id: 2, addressId: 10);
        state.People[killer.Id] = killer;
        state.People[victim.Id] = victim;

        var objective = new Objective { Id = 1, Data = new Dictionary<string, object>() };
        var task = new SimTask
        {
            ObjectiveId = 1,
            ActionType = ActionType.KillPerson,
            ActionData = new Dictionary<string, object> { { "VictimId", 2 } }
        };

        ActionExecutor.Execute(task, killer, objective, state);

        var conditionTrace = state.Traces.Values
            .FirstOrDefault(t => t.TraceType == TraceType.Condition);

        Assert.NotNull(conditionTrace);
        Assert.Equal(victim.Id, conditionTrace.AttachedToPersonId);
        Assert.Equal(killer.Id, conditionTrace.CreatedByPersonId);
        Assert.Equal("Cause of death: homicide", conditionTrace.Description);
    }

    [Fact]
    public void KillPerson_ProducesMarkTrace_AtKillersLocation()
    {
        var state = MakeState();
        var killer = MakePerson(id: 1, addressId: 10);
        var victim = MakePerson(id: 2, addressId: 10);
        state.People[killer.Id] = killer;
        state.People[victim.Id] = victim;

        var objective = new Objective { Id = 1, Data = new Dictionary<string, object>() };
        var task = new SimTask
        {
            ObjectiveId = 1,
            ActionType = ActionType.KillPerson,
            ActionData = new Dictionary<string, object> { { "VictimId", 2 } }
        };

        ActionExecutor.Execute(task, killer, objective, state);

        var markTrace = state.Traces.Values
            .FirstOrDefault(t => t.TraceType == TraceType.Mark);

        Assert.NotNull(markTrace);
        Assert.Equal(killer.CurrentAddressId, markTrace.LocationId);
        Assert.Equal(killer.Id, markTrace.CreatedByPersonId);
        Assert.Equal("Signs of violent struggle", markTrace.Description);
    }

    [Fact]
    public void KillPerson_LogsPersonDiedEvent()
    {
        var state = MakeState();
        var killer = MakePerson(id: 1, addressId: 10);
        var victim = MakePerson(id: 2, addressId: 15);
        state.People[killer.Id] = killer;
        state.People[victim.Id] = victim;

        var objective = new Objective { Id = 1, Data = new Dictionary<string, object>() };
        var task = new SimTask
        {
            ObjectiveId = 1,
            ActionType = ActionType.KillPerson,
            ActionData = new Dictionary<string, object> { { "VictimId", 2 } }
        };

        ActionExecutor.Execute(task, killer, objective, state);

        var diedEvent = state.Journal.AllEvents
            .FirstOrDefault(e => e.EventType == SimulationEventType.PersonDied);

        Assert.NotNull(diedEvent);
        Assert.Equal(victim.Id, diedEvent.PersonId);
        Assert.Equal(victim.CurrentAddressId, diedEvent.AddressId);
    }

    [Fact]
    public void KillPerson_LogsCrimeCommittedEvent()
    {
        var state = MakeState();
        var killer = MakePerson(id: 1, addressId: 10);
        var victim = MakePerson(id: 2, addressId: 10);
        state.People[killer.Id] = killer;
        state.People[victim.Id] = victim;

        var objective = new Objective { Id = 1, Data = new Dictionary<string, object>() };
        var task = new SimTask
        {
            ObjectiveId = 1,
            ActionType = ActionType.KillPerson,
            ActionData = new Dictionary<string, object> { { "VictimId", 2 } }
        };

        ActionExecutor.Execute(task, killer, objective, state);

        var crimeEvent = state.Journal.AllEvents
            .FirstOrDefault(e => e.EventType == SimulationEventType.CrimeCommitted);

        Assert.NotNull(crimeEvent);
        Assert.Equal(killer.Id, crimeEvent.PersonId);
    }

    [Fact]
    public void KillPerson_FallsBackToObjectiveData_WhenTaskActionDataMissing()
    {
        var state = MakeState();
        var killer = MakePerson(id: 1, addressId: 10);
        var victim = MakePerson(id: 2, addressId: 10);
        state.People[killer.Id] = killer;
        state.People[victim.Id] = victim;

        var objective = new Objective
        {
            Id = 1,
            Data = new Dictionary<string, object> { { "VictimId", 2 } }
        };
        var task = new SimTask
        {
            ObjectiveId = 1,
            ActionType = ActionType.KillPerson,
            ActionData = null
        };

        ActionExecutor.Execute(task, killer, objective, state);

        Assert.False(victim.IsAlive);
    }

    [Fact]
    public void KillPerson_SetsVictimCurrentActionToIdle()
    {
        var state = MakeState();
        var killer = MakePerson(id: 1, addressId: 10);
        var victim = MakePerson(id: 2, addressId: 10);
        victim.CurrentAction = ActionType.Work;
        state.People[killer.Id] = killer;
        state.People[victim.Id] = victim;

        var objective = new Objective { Id = 1, Data = new Dictionary<string, object>() };
        var task = new SimTask
        {
            ObjectiveId = 1,
            ActionType = ActionType.KillPerson,
            ActionData = new Dictionary<string, object> { { "VictimId", 2 } }
        };

        ActionExecutor.Execute(task, killer, objective, state);

        Assert.Equal(ActionType.Idle, victim.CurrentAction);
    }

    [Fact]
    public void NoOpActions_DoNotThrow()
    {
        var state = MakeState();
        var actor = MakePerson(id: 1, addressId: 10);
        state.People[actor.Id] = actor;

        var objective = new Objective { Id = 1, Data = new Dictionary<string, object>() };

        foreach (var actionType in new[] { ActionType.Work, ActionType.Sleep, ActionType.Idle, ActionType.TravelByCar })
        {
            var task = new SimTask
            {
                ObjectiveId = 1,
                ActionType = actionType
            };

            // Should not throw
            ActionExecutor.Execute(task, actor, objective, state);
        }

        // No traces or events should have been created
        Assert.Empty(state.Traces);
        Assert.Empty(state.Journal.AllEvents);
    }
}
