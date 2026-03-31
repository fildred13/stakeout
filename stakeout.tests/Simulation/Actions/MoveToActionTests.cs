using System;
using Stakeout.Simulation;
using Stakeout.Simulation.Actions;
using Stakeout.Simulation.Actions.Primitives;
using Stakeout.Simulation.Entities;
using Xunit;

namespace Stakeout.Tests.Simulation.Actions;

public class MoveToActionTests
{
    private static ActionContext CreateContext(Person person = null)
    {
        var state = new SimulationState();
        person ??= new Person { Id = 1, CurrentLocationId = 10 };
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
    public void MoveToAction_HasCorrectName()
    {
        var action = new MoveToAction(5, null, "heading to bedroom");
        Assert.Equal("MoveTo", action.Name);
    }

    [Fact]
    public void MoveToAction_SetsPersonLocation_OnComplete()
    {
        var person = new Person { Id = 1, CurrentLocationId = 10 };
        var action = new MoveToAction(20, null, "heading to bedroom");
        var ctx = CreateContext(person);
        action.OnStart(ctx);
        action.Tick(ctx, TimeSpan.FromSeconds(1)); // completes immediately (intra-address)
        Assert.Equal(20, person.CurrentLocationId);
    }

    [Fact]
    public void MoveToAction_SetsSubLocation_WhenProvided()
    {
        var person = new Person { Id = 1, CurrentLocationId = 10 };
        var action = new MoveToAction(20, 30, "heading to kitchen");
        var ctx = CreateContext(person);
        action.OnStart(ctx);
        action.Tick(ctx, TimeSpan.FromSeconds(1));
        Assert.Equal(20, person.CurrentLocationId);
        Assert.Equal(30, person.CurrentSubLocationId);
    }

    [Fact]
    public void MoveToAction_CompletesImmediately()
    {
        var action = new MoveToAction(20, null, "moving");
        var ctx = CreateContext();
        action.OnStart(ctx);
        var status = action.Tick(ctx, TimeSpan.FromSeconds(1));
        Assert.Equal(ActionStatus.Completed, status);
    }
}
