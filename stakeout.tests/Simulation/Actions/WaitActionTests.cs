using System;
using Stakeout.Simulation;
using Stakeout.Simulation.Actions;
using Stakeout.Simulation.Actions.Primitives;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Events;
using Xunit;

namespace Stakeout.Tests.Simulation.Actions;

public class WaitActionTests
{
    private static ActionContext CreateContext(Person person = null)
    {
        var state = new SimulationState();
        person ??= new Person { Id = 1 };
        state.People[person.Id] = person;
        return new ActionContext
        {
            Person = person,
            State = state,
            EventJournal = state.Journal,
            Random = new Random(42),
            CurrentTime = state.Clock.CurrentTime
        };
    }

    [Fact]
    public void WaitAction_HasCorrectName()
    {
        var action = new WaitAction(TimeSpan.FromMinutes(30), "resting");
        Assert.Equal("Wait", action.Name);
    }

    [Fact]
    public void WaitAction_HasCorrectDisplayText()
    {
        var action = new WaitAction(TimeSpan.FromMinutes(30), "running on the trails");
        Assert.Equal("running on the trails", action.DisplayText);
    }

    [Fact]
    public void WaitAction_ReturnsRunning_WhileTimeRemains()
    {
        var action = new WaitAction(TimeSpan.FromMinutes(30), "resting");
        var ctx = CreateContext();
        action.OnStart(ctx);

        var status = action.Tick(ctx, TimeSpan.FromMinutes(10));
        Assert.Equal(ActionStatus.Running, status);
    }

    [Fact]
    public void WaitAction_ReturnsCompleted_WhenTimeElapses()
    {
        var action = new WaitAction(TimeSpan.FromMinutes(30), "resting");
        var ctx = CreateContext();
        action.OnStart(ctx);

        action.Tick(ctx, TimeSpan.FromMinutes(20));
        var status = action.Tick(ctx, TimeSpan.FromMinutes(15));
        Assert.Equal(ActionStatus.Completed, status);
    }

    [Fact]
    public void WaitAction_ReturnsCompleted_OnExactDuration()
    {
        var action = new WaitAction(TimeSpan.FromMinutes(30), "resting");
        var ctx = CreateContext();
        action.OnStart(ctx);

        var status = action.Tick(ctx, TimeSpan.FromMinutes(30));
        Assert.Equal(ActionStatus.Completed, status);
    }

    [Fact]
    public void WaitAction_RemainingTime_DecreasesEachTick()
    {
        var action = new WaitAction(TimeSpan.FromMinutes(30), "resting");
        var ctx = CreateContext();
        action.OnStart(ctx);

        action.Tick(ctx, TimeSpan.FromMinutes(10));
        Assert.Equal(TimeSpan.FromMinutes(20), action.RemainingTime);
    }
}
