using System;
using System.Collections.Generic;
using System.Linq;
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
    private static SimulationState CreateState(DateTime? startTime = null)
    {
        var state = new SimulationState(
            new GameClock(startTime ?? new DateTime(1980, 1, 1, 6, 0, 0)));
        var home = new Address { Id = 1, GridX = 0, GridY = 0 };
        state.Addresses[home.Id] = home;
        return state;
    }

    private static Person CreatePerson(SimulationState state,
        TimeSpan wakeTime, TimeSpan sleepTime)
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
        person.Objectives.Add(new SleepObjective { Id = state.GenerateEntityId() });
        state.People[person.Id] = person;
        return person;
    }

    [Fact]
    public void PlanDay_DayShift_HasSleepAndIdle()
    {
        var state = CreateState();
        var person = CreatePerson(state,
            TimeSpan.FromHours(6), TimeSpan.FromHours(22));

        var plan = NpcBrain.PlanDay(person, state, state.Clock.CurrentTime);

        Assert.NotEmpty(plan.Entries);
        Assert.Contains(plan.Entries, e => e.PlannedAction.DisplayText == "sleeping");
        Assert.Contains(plan.Entries, e => e.PlannedAction.DisplayText == "relaxing at home");
    }

    [Fact]
    public void PlanDay_NightShift_HasSleepAndIdle()
    {
        // Night worker: wakes 15:30, sleeps 07:30
        var state = CreateState(new DateTime(1980, 1, 1, 15, 30, 0));
        var person = CreatePerson(state,
            TimeSpan.FromHours(15.5), TimeSpan.FromHours(7.5));

        var plan = NpcBrain.PlanDay(person, state, state.Clock.CurrentTime);

        Assert.NotEmpty(plan.Entries);
        Assert.Contains(plan.Entries, e => e.PlannedAction.DisplayText == "sleeping");
        Assert.Contains(plan.Entries, e => e.PlannedAction.DisplayText == "relaxing at home");
    }

    [Fact]
    public void PlanDay_MidSleep_StartsWithSleep()
    {
        // Day worker (sleep 22:00-06:00), but plan starts at 02:00 (mid-sleep)
        var state = CreateState(new DateTime(1980, 1, 1, 2, 0, 0));
        var person = CreatePerson(state,
            TimeSpan.FromHours(6), TimeSpan.FromHours(22));

        var plan = NpcBrain.PlanDay(person, state, state.Clock.CurrentTime);

        Assert.NotEmpty(plan.Entries);
        // First entry should be sleep
        Assert.Equal("sleeping", plan.Entries[0].PlannedAction.DisplayText);
        // Should start at plan start (02:00), not at 22:00
        Assert.Equal(state.Clock.CurrentTime, plan.Entries[0].StartTime);
    }

    [Fact]
    public void PlanDay_AllEntriesUseDateTime()
    {
        var state = CreateState();
        var person = CreatePerson(state,
            TimeSpan.FromHours(6), TimeSpan.FromHours(22));

        var plan = NpcBrain.PlanDay(person, state, state.Clock.CurrentTime);

        foreach (var entry in plan.Entries)
        {
            Assert.True(entry.StartTime > DateTime.MinValue);
            Assert.True(entry.EndTime > entry.StartTime);
        }
    }

    [Fact]
    public void PlanDay_EntriesAreChronological()
    {
        var state = CreateState();
        var person = CreatePerson(state,
            TimeSpan.FromHours(6), TimeSpan.FromHours(22));

        var plan = NpcBrain.PlanDay(person, state, state.Clock.CurrentTime);

        for (int i = 1; i < plan.Entries.Count; i++)
        {
            Assert.True(plan.Entries[i].StartTime >= plan.Entries[i - 1].EndTime,
                $"Entry {i} starts before entry {i - 1} ends");
        }
    }

    [Fact]
    public void PlanDay_HigherPriorityScheduledFirst()
    {
        var now = new DateTime(1980, 1, 1, 6, 0, 0);
        var state = CreateState(now);
        var person = CreatePerson(state,
            TimeSpan.FromHours(6), TimeSpan.FromHours(22));

        person.Objectives.Add(new TestObjective(50, new PlannedAction
        {
            Action = new WaitAction(TimeSpan.FromHours(1), "high priority"),
            TargetAddressId = 1,
            TimeWindowStart = now + TimeSpan.FromHours(2),
            TimeWindowEnd = now + TimeSpan.FromHours(6),
            Duration = TimeSpan.FromHours(1),
            DisplayText = "high priority"
        }));
        person.Objectives.Add(new TestObjective(20, new PlannedAction
        {
            Action = new WaitAction(TimeSpan.FromHours(1), "low priority"),
            TargetAddressId = 1,
            TimeWindowStart = now + TimeSpan.FromHours(2),
            TimeWindowEnd = now + TimeSpan.FromHours(6),
            Duration = TimeSpan.FromHours(1),
            DisplayText = "low priority"
        }));

        var plan = NpcBrain.PlanDay(person, state, now);

        var nonIdle = plan.Entries
            .Where(e => e.PlannedAction.DisplayText != "relaxing at home"
                     && e.PlannedAction.DisplayText != "sleeping")
            .ToList();
        Assert.Equal(2, nonIdle.Count);
        Assert.True(nonIdle[0].StartTime <= nonIdle[1].StartTime);
        Assert.Equal("high priority", nonIdle[0].PlannedAction.DisplayText);
    }

    [Fact]
    public void PlanDay_GapsFilledWithIdleAtHome()
    {
        var now = new DateTime(1980, 1, 1, 6, 0, 0);
        var state = CreateState(now);
        var person = CreatePerson(state,
            TimeSpan.FromHours(6), TimeSpan.FromHours(22));

        person.Objectives.Add(new TestObjective(40, new PlannedAction
        {
            Action = new WaitAction(TimeSpan.FromHours(1), "eating"),
            TargetAddressId = 1,
            TimeWindowStart = now + TimeSpan.FromHours(6),
            TimeWindowEnd = now + TimeSpan.FromHours(7),
            Duration = TimeSpan.FromHours(1),
            DisplayText = "eating"
        }));

        var plan = NpcBrain.PlanDay(person, state, now);

        Assert.True(plan.Entries.Count >= 3);
        Assert.Contains(plan.Entries, e => e.PlannedAction.DisplayText == "eating");
        Assert.Contains(plan.Entries, e => e.PlannedAction.DisplayText == "relaxing at home");
    }

    [Fact]
    public void PlanDay_PlanSpans24Hours()
    {
        var now = new DateTime(1980, 1, 1, 6, 0, 0);
        var state = CreateState(now);
        var person = CreatePerson(state,
            TimeSpan.FromHours(6), TimeSpan.FromHours(22));

        var plan = NpcBrain.PlanDay(person, state, now);

        Assert.Equal(now, plan.Entries.First().StartTime);
        var lastEnd = plan.Entries.Last().EndTime;
        Assert.Equal(now.AddHours(24), lastEnd);
    }

    [Fact]
    public void PlanDay_ObjectiveThatDoesntFit_IsSkipped()
    {
        var now = new DateTime(1980, 1, 1, 6, 0, 0);
        var state = CreateState(now);
        var person = CreatePerson(state,
            TimeSpan.FromHours(6), TimeSpan.FromHours(22));

        // Fill most of the 24h window with high priority
        person.Objectives.Add(new TestObjective(90, new PlannedAction
        {
            Action = new WaitAction(TimeSpan.FromHours(23), "all day"),
            TargetAddressId = 1,
            TimeWindowStart = now,
            TimeWindowEnd = now.AddHours(24),
            Duration = TimeSpan.FromHours(23),
            DisplayText = "all day"
        }));
        // Low priority can't fit
        person.Objectives.Add(new TestObjective(20, new PlannedAction
        {
            Action = new WaitAction(TimeSpan.FromHours(2), "cant fit"),
            TargetAddressId = 1,
            TimeWindowStart = now,
            TimeWindowEnd = now.AddHours(24),
            Duration = TimeSpan.FromHours(2),
            DisplayText = "cant fit"
        }));

        var plan = NpcBrain.PlanDay(person, state, now);

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

        public override List<PlannedAction> GetActions(
            Person person, SimulationState state,
            DateTime planStart, DateTime planEnd)
        {
            return new List<PlannedAction> { _action };
        }
    }
}
