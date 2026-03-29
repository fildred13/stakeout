using System;
using System.Collections.Generic;
using Stakeout.Simulation;
using Stakeout.Simulation.Actions;
using Stakeout.Simulation.Actions.Primitives;
using Stakeout.Simulation.Brain;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Objectives;
using Xunit;

namespace Stakeout.Tests.Simulation.Brain;

public class NpcBrainTests
{
    private static SimulationState CreateState()
    {
        var state = new SimulationState();
        var home = new Address { Id = 1, GridX = 0, GridY = 0 };
        state.Addresses[home.Id] = home;
        return state;
    }

    private static Person CreatePerson(SimulationState state, TimeSpan wakeTime, TimeSpan sleepTime)
    {
        var person = new Person
        {
            Id = state.GenerateEntityId(),
            HomeAddressId = 1,
            CurrentAddressId = 1,
            PreferredWakeTime = wakeTime,
            PreferredSleepTime = sleepTime,
            CurrentPosition = new Godot.Vector2(0, 0)
        };
        state.People[person.Id] = person;
        return person;
    }

    [Fact]
    public void PlanDay_NoObjectives_OnlyIdleAtHome()
    {
        var state = CreateState();
        var person = CreatePerson(state, TimeSpan.FromHours(6), TimeSpan.FromHours(22));

        var plan = NpcBrain.PlanDay(person, state, state.Clock.CurrentTime);

        Assert.NotEmpty(plan.Entries);
        Assert.All(plan.Entries, e => Assert.Equal("relaxing at home", e.PlannedAction.DisplayText));
    }

    [Fact]
    public void PlanDay_HigherPriorityScheduledFirst()
    {
        var state = CreateState();
        var park = new Address { Id = 2, GridX = 0, GridY = 0 };
        state.Addresses[park.Id] = park;

        var person = CreatePerson(state, TimeSpan.FromHours(6), TimeSpan.FromHours(22));
        person.Objectives.Add(new TestObjective(80, new PlannedAction
        {
            Action = new WaitAction(TimeSpan.FromHours(1), "high priority"),
            TargetAddressId = 1,
            TimeWindowStart = TimeSpan.FromHours(8),
            TimeWindowEnd = TimeSpan.FromHours(12),
            Duration = TimeSpan.FromHours(1),
            DisplayText = "high priority"
        }));
        person.Objectives.Add(new TestObjective(20, new PlannedAction
        {
            Action = new WaitAction(TimeSpan.FromHours(1), "low priority"),
            TargetAddressId = 1,
            TimeWindowStart = TimeSpan.FromHours(8),
            TimeWindowEnd = TimeSpan.FromHours(12),
            Duration = TimeSpan.FromHours(1),
            DisplayText = "low priority"
        }));

        var plan = NpcBrain.PlanDay(person, state, state.Clock.CurrentTime);

        // Find the two non-idle entries
        var nonIdle = plan.Entries.FindAll(e => e.PlannedAction.DisplayText != "relaxing at home");
        Assert.Equal(2, nonIdle.Count);
        // High priority should be scheduled first (earlier time)
        Assert.True(nonIdle[0].StartTime <= nonIdle[1].StartTime);
        Assert.Equal("high priority", nonIdle[0].PlannedAction.DisplayText);
    }

    [Fact]
    public void PlanDay_GapsFilledWithIdleAtHome()
    {
        var state = CreateState();
        var person = CreatePerson(state, TimeSpan.FromHours(6), TimeSpan.FromHours(22));
        person.Objectives.Add(new TestObjective(40, new PlannedAction
        {
            Action = new WaitAction(TimeSpan.FromHours(1), "eating"),
            TargetAddressId = 1,
            TimeWindowStart = TimeSpan.FromHours(12),
            TimeWindowEnd = TimeSpan.FromHours(13),
            Duration = TimeSpan.FromHours(1),
            DisplayText = "eating"
        }));

        var plan = NpcBrain.PlanDay(person, state, state.Clock.CurrentTime);

        // Should have idle before eating, eating, idle after eating
        Assert.True(plan.Entries.Count >= 3);
        var eatingEntry = plan.Entries.Find(e => e.PlannedAction.DisplayText == "eating");
        Assert.NotNull(eatingEntry);
    }

    [Fact]
    public void PlanDay_ObjectiveThatDoesntFit_IsSkipped()
    {
        var state = CreateState();
        var person = CreatePerson(state, TimeSpan.FromHours(6), TimeSpan.FromHours(22));
        // Fill the whole day with high priority
        person.Objectives.Add(new TestObjective(80, new PlannedAction
        {
            Action = new WaitAction(TimeSpan.FromHours(16), "all day"),
            TargetAddressId = 1,
            TimeWindowStart = TimeSpan.FromHours(6),
            TimeWindowEnd = TimeSpan.FromHours(22),
            Duration = TimeSpan.FromHours(16),
            DisplayText = "all day"
        }));
        // Low priority can't fit
        person.Objectives.Add(new TestObjective(20, new PlannedAction
        {
            Action = new WaitAction(TimeSpan.FromHours(1), "cant fit"),
            TargetAddressId = 1,
            TimeWindowStart = TimeSpan.FromHours(6),
            TimeWindowEnd = TimeSpan.FromHours(22),
            Duration = TimeSpan.FromHours(1),
            DisplayText = "cant fit"
        }));

        var plan = NpcBrain.PlanDay(person, state, state.Clock.CurrentTime);

        Assert.DoesNotContain(plan.Entries, e => e.PlannedAction.DisplayText == "cant fit");
    }

    // Helper: a test objective that returns a fixed PlannedAction
    private class TestObjective : Objective
    {
        private readonly int _priority;
        private readonly PlannedAction _action;

        public override int Priority => _priority;
        public override ObjectiveSource Source => ObjectiveSource.Universal;

        public TestObjective(int priority, PlannedAction action)
        {
            _priority = priority;
            _action = action;
        }

        public override List<PlannedAction> GetActionsForToday(Person person, SimulationState state, DateTime currentDate)
        {
            return new List<PlannedAction> { _action };
        }
    }
}
