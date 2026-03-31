using System;
using Stakeout.Simulation;
using Stakeout.Simulation.Actions;
using Stakeout.Simulation.Actions.Primitives;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Events;
using Xunit;

namespace Stakeout.Tests.Simulation.Actions;

public class ActionSequenceTests
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
    public void Create_SetsName()
    {
        var seq = ActionSequence.Create("TestAction").Wait(TimeSpan.FromMinutes(10), "waiting").Build();
        Assert.Equal("TestAction", seq.Name);
    }

    [Fact]
    public void SingleWait_RunsThenCompletes()
    {
        var seq = ActionSequence.Create("Test")
            .Wait(TimeSpan.FromMinutes(10), "waiting")
            .Build();
        var ctx = CreateContext();
        seq.OnStart(ctx);

        Assert.Equal(ActionStatus.Running, seq.Tick(ctx, TimeSpan.FromMinutes(5)));
        Assert.Equal(ActionStatus.Completed, seq.Tick(ctx, TimeSpan.FromMinutes(6)));
    }

    [Fact]
    public void MultipleSteps_ExecuteSequentially()
    {
        var seq = ActionSequence.Create("TwoStep")
            .Wait(TimeSpan.FromMinutes(10), "step one")
            .Wait(TimeSpan.FromMinutes(5), "step two")
            .Build();
        var ctx = CreateContext();
        seq.OnStart(ctx);

        // Step 1 in progress
        Assert.Equal("step one", seq.DisplayText);
        Assert.Equal(ActionStatus.Running, seq.Tick(ctx, TimeSpan.FromMinutes(10)));

        // Step 2 in progress
        Assert.Equal("step two", seq.DisplayText);
        Assert.Equal(ActionStatus.Running, seq.Tick(ctx, TimeSpan.FromMinutes(3)));

        // Step 2 completes
        Assert.Equal(ActionStatus.Completed, seq.Tick(ctx, TimeSpan.FromMinutes(3)));
    }

    [Fact]
    public void DisplayText_ReflectsCurrentStep()
    {
        var seq = ActionSequence.Create("Test")
            .Wait(TimeSpan.FromMinutes(5), "first")
            .Wait(TimeSpan.FromMinutes(5), "second")
            .Build();
        var ctx = CreateContext();
        seq.OnStart(ctx);

        Assert.Equal("first", seq.DisplayText);
        seq.Tick(ctx, TimeSpan.FromMinutes(5)); // completes first step
        Assert.Equal("second", seq.DisplayText);
    }

    [Fact]
    public void Do_RunsCustomAction()
    {
        var customRan = false;
        var seq = ActionSequence.Create("Test")
            .Do(new TestAction(() => customRan = true))
            .Build();
        var ctx = CreateContext();
        seq.OnStart(ctx);
        seq.Tick(ctx, TimeSpan.FromSeconds(1));

        Assert.True(customRan);
    }

    [Fact]
    public void If_ConditionTrue_RunsStep()
    {
        var seq = ActionSequence.Create("Test")
            .If(_ => true, b => b.Wait(TimeSpan.FromMinutes(5), "ran"))
            .Build();
        var ctx = CreateContext();
        seq.OnStart(ctx);

        Assert.Equal("ran", seq.DisplayText);
        Assert.Equal(ActionStatus.Running, seq.Tick(ctx, TimeSpan.FromMinutes(3)));
    }

    [Fact]
    public void If_ConditionFalse_SkipsStep()
    {
        var seq = ActionSequence.Create("Test")
            .If(_ => false, b => b.Wait(TimeSpan.FromMinutes(5), "skipped"))
            .Wait(TimeSpan.FromMinutes(5), "ran instead")
            .Build();
        var ctx = CreateContext();
        seq.OnStart(ctx);

        Assert.Equal("ran instead", seq.DisplayText);
    }

    [Fact]
    public void Maybe_ProbabilityZero_SkipsStep()
    {
        // Seed 42 Random — but probability 0 always skips
        var seq = ActionSequence.Create("Test")
            .Maybe(0.0, b => b.Wait(TimeSpan.FromMinutes(5), "skipped"))
            .Wait(TimeSpan.FromMinutes(5), "ran")
            .Build();
        var ctx = CreateContext();
        seq.OnStart(ctx);

        Assert.Equal("ran", seq.DisplayText);
    }

    [Fact]
    public void Maybe_ProbabilityOne_RunsStep()
    {
        var seq = ActionSequence.Create("Test")
            .Maybe(1.0, b => b.Wait(TimeSpan.FromMinutes(5), "ran"))
            .Build();
        var ctx = CreateContext();
        seq.OnStart(ctx);

        Assert.Equal("ran", seq.DisplayText);
    }

    // Helper: an IAction that completes immediately and calls a callback
    private class TestAction : IAction
    {
        private readonly Action _onTick;
        public string Name => "Test";
        public string DisplayText => "testing";

        public TestAction(Action onTick) { _onTick = onTick; }
        public void OnStart(ActionContext ctx) { }
        public ActionStatus Tick(ActionContext ctx, TimeSpan delta) { _onTick(); return ActionStatus.Completed; }
        public void OnComplete(ActionContext ctx) { }
    }
}
