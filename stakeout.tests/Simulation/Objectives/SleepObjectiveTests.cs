using System;
using System.Linq;
using Stakeout.Simulation;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Objectives;
using Xunit;

namespace Stakeout.Tests.Simulation.Objectives;

public class SleepObjectiveTests
{
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
    public void GetActionsForToday_ReturnsSleepAction()
    {
        var state = new SimulationState();
        state.Addresses[1] = new Address { Id = 1 };
        var person = new Person
        {
            Id = 1,
            HomeAddressId = 1,
            PreferredSleepTime = TimeSpan.FromHours(22),
            PreferredWakeTime = TimeSpan.FromHours(6)
        };

        var obj = new SleepObjective();
        var actions = obj.GetActionsForToday(person, state, DateTime.Today);

        Assert.Single(actions);
        Assert.Equal("sleeping", actions[0].DisplayText);
        Assert.Equal(1, actions[0].TargetAddressId);
    }

    [Fact]
    public void GetActionsForToday_SleepWindowMatchesPreferences()
    {
        var state = new SimulationState();
        state.Addresses[1] = new Address { Id = 1 };
        var person = new Person
        {
            Id = 1,
            HomeAddressId = 1,
            PreferredSleepTime = TimeSpan.FromHours(22),
            PreferredWakeTime = TimeSpan.FromHours(6)
        };

        var obj = new SleepObjective();
        var actions = obj.GetActionsForToday(person, state, DateTime.Today);

        Assert.Equal(TimeSpan.FromHours(22), actions[0].TimeWindowStart);
    }

    [Fact]
    public void GetActionsForToday_DurationIs8Hours()
    {
        var state = new SimulationState();
        state.Addresses[1] = new Address { Id = 1 };
        var person = new Person
        {
            Id = 1,
            HomeAddressId = 1,
            PreferredSleepTime = TimeSpan.FromHours(22),
            PreferredWakeTime = TimeSpan.FromHours(6)
        };

        var obj = new SleepObjective();
        var actions = obj.GetActionsForToday(person, state, DateTime.Today);

        Assert.Equal(TimeSpan.FromHours(8), actions[0].Duration);
    }
}
