using System;
using System.Linq;
using Stakeout.Simulation;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Objectives;
using Xunit;

namespace Stakeout.Tests.Simulation.Objectives;

public class SleepObjectiveTests
{
    private static (SimulationState state, Person person) CreateSetup(
        TimeSpan sleepTime, TimeSpan wakeTime)
    {
        var state = new SimulationState();
        state.Addresses[1] = new Address { Id = 1 };
        var person = new Person
        {
            Id = 1,
            HomeAddressId = 1,
            PreferredSleepTime = sleepTime,
            PreferredWakeTime = wakeTime
        };
        return (state, person);
    }

    [Fact]
    public void Priority_Is80()
    {
        var obj = new SleepObjective();
        Assert.Equal(80, obj.Priority);
    }

    [Fact]
    public void Source_IsUniversal()
    {
        var obj = new SleepObjective();
        Assert.Equal(ObjectiveSource.Universal, obj.Source);
    }

    [Fact]
    public void GetActions_ReturnsSleepAction()
    {
        var (state, person) = CreateSetup(
            TimeSpan.FromHours(22), TimeSpan.FromHours(6));
        var planStart = new DateTime(1980, 1, 1, 6, 0, 0);
        var planEnd = planStart.AddHours(24);

        var obj = new SleepObjective();
        var actions = obj.GetActions(person, state, planStart, planEnd);

        Assert.Single(actions);
        Assert.Equal("sleeping", actions[0].DisplayText);
        Assert.Equal(1, actions[0].TargetAddressId);
    }

    [Fact]
    public void GetActions_DurationIs8Hours()
    {
        var (state, person) = CreateSetup(
            TimeSpan.FromHours(22), TimeSpan.FromHours(6));
        var planStart = new DateTime(1980, 1, 1, 6, 0, 0);
        var planEnd = planStart.AddHours(24);

        var obj = new SleepObjective();
        var actions = obj.GetActions(person, state, planStart, planEnd);

        Assert.Equal(TimeSpan.FromHours(8), actions[0].Duration);
    }

    [Fact]
    public void GetActions_SleepStartsAtPreferredTime()
    {
        var (state, person) = CreateSetup(
            TimeSpan.FromHours(22), TimeSpan.FromHours(6));
        // Plan starts at 06:00 — sleep should be at 22:00 same day
        var planStart = new DateTime(1980, 1, 1, 6, 0, 0);
        var planEnd = planStart.AddHours(24);

        var obj = new SleepObjective();
        var actions = obj.GetActions(person, state, planStart, planEnd);

        Assert.Equal(new DateTime(1980, 1, 1, 22, 0, 0), actions[0].TimeWindowStart);
    }

    [Fact]
    public void GetActions_MidSleep_ReturnsRemainingSleep()
    {
        // NPC sleeps 22:00-06:00. Plan starts at 02:00 (mid-sleep).
        var (state, person) = CreateSetup(
            TimeSpan.FromHours(22), TimeSpan.FromHours(6));
        var planStart = new DateTime(1980, 1, 2, 2, 0, 0); // 02:00 day 2
        var planEnd = planStart.AddHours(24);

        var obj = new SleepObjective();
        var actions = obj.GetActions(person, state, planStart, planEnd);

        // Should get TWO sleep actions:
        // 1. Remaining sleep: 02:00 to 06:00 (4h)
        // 2. Next night's sleep: 22:00 to 06:00 (8h)
        Assert.Equal(2, actions.Count);

        // First: remaining sleep starting now
        Assert.Equal(planStart, actions[0].TimeWindowStart);
        Assert.Equal(TimeSpan.FromHours(4), actions[0].Duration);

        // Second: next full sleep
        Assert.Equal(new DateTime(1980, 1, 2, 22, 0, 0), actions[1].TimeWindowStart);
        Assert.Equal(TimeSpan.FromHours(8), actions[1].Duration);
    }

    [Fact]
    public void GetActions_NightShift_SleepAtCorrectTime()
    {
        // Night worker: sleeps 07:30-15:30. Plan starts at 15:30.
        var (state, person) = CreateSetup(
            TimeSpan.FromHours(7.5), TimeSpan.FromHours(15.5));
        var planStart = new DateTime(1980, 1, 1, 15, 30, 0);
        var planEnd = planStart.AddHours(24);

        var obj = new SleepObjective();
        var actions = obj.GetActions(person, state, planStart, planEnd);

        // Sleep should be at 07:30 next day
        Assert.Single(actions);
        Assert.Equal(new DateTime(1980, 1, 2, 7, 30, 0), actions[0].TimeWindowStart);
        Assert.Equal(TimeSpan.FromHours(8), actions[0].Duration);
    }

    [Fact]
    public void GetActions_NightShift_MidSleep_ReturnsRemaining()
    {
        // Night worker: sleeps 07:30-15:30. Plan starts at 10:00 (mid-sleep).
        var (state, person) = CreateSetup(
            TimeSpan.FromHours(7.5), TimeSpan.FromHours(15.5));
        var planStart = new DateTime(1980, 1, 1, 10, 0, 0);
        var planEnd = planStart.AddHours(24);

        var obj = new SleepObjective();
        var actions = obj.GetActions(person, state, planStart, planEnd);

        // Should get TWO sleep actions:
        // 1. Remaining: 10:00 to 15:30 (5.5h)
        // 2. Next sleep: 07:30 next day (8h)
        Assert.Equal(2, actions.Count);
        Assert.Equal(planStart, actions[0].TimeWindowStart);
        Assert.Equal(TimeSpan.FromHours(5.5), actions[0].Duration);
        Assert.Equal(new DateTime(1980, 1, 2, 7, 30, 0), actions[1].TimeWindowStart);
    }
}
