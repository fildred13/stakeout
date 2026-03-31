using System;
using System.Collections.Generic;

namespace Stakeout.Simulation.Actions;

public class ActionSequence : IAction
{
    private readonly List<IStep> _steps = new();
    private int _currentStepIndex;
    private int _savedStepIndex;
    private IAction _currentAction;

    public string Name { get; }
    public string DisplayText => _currentAction?.DisplayText ?? Name;

    private ActionSequence(string name)
    {
        Name = name;
    }

    public static ActionSequenceBuilder Create(string name) => new(name);

    public void OnStart(ActionContext ctx)
    {
        _currentStepIndex = 0;
        AdvanceToNextRunnableStep(ctx);
    }

    public ActionStatus Tick(ActionContext ctx, TimeSpan delta)
    {
        if (_currentAction == null) return ActionStatus.Completed;

        var status = _currentAction.Tick(ctx, delta);
        if (status == ActionStatus.Completed)
        {
            _currentAction.OnComplete(ctx);
            _currentStepIndex++;
            AdvanceToNextRunnableStep(ctx);
            if (_currentAction == null) return ActionStatus.Completed;
        }
        else if (status == ActionStatus.Failed)
        {
            return ActionStatus.Failed;
        }
        return ActionStatus.Running;
    }

    public void OnComplete(ActionContext ctx) { }

    public void OnSuspend(ActionContext ctx)
    {
        _savedStepIndex = _currentStepIndex;
        _currentAction?.OnSuspend(ctx);
    }

    public void OnResume(ActionContext ctx)
    {
        _currentStepIndex = _savedStepIndex;
        AdvanceToNextRunnableStep(ctx);
    }

    private void AdvanceToNextRunnableStep(ActionContext ctx)
    {
        while (_currentStepIndex < _steps.Count)
        {
            var step = _steps[_currentStepIndex];
            var action = step.Resolve(ctx);
            if (action != null)
            {
                _currentAction = action;
                _currentAction.OnStart(ctx);
                return;
            }
            _currentStepIndex++;
        }
        _currentAction = null;
    }

    private interface IStep
    {
        IAction Resolve(ActionContext ctx);
    }

    private class DirectStep : IStep
    {
        private readonly IAction _action;
        public DirectStep(IAction action) { _action = action; }
        public IAction Resolve(ActionContext ctx) => _action;
    }

    private class ConditionalStep : IStep
    {
        private readonly Func<ActionContext, bool> _condition;
        private readonly IAction _action;
        public ConditionalStep(Func<ActionContext, bool> condition, IAction action)
        {
            _condition = condition;
            _action = action;
        }
        public IAction Resolve(ActionContext ctx) => _condition(ctx) ? _action : null;
    }

    private class MaybeStep : IStep
    {
        private readonly double _probability;
        private readonly IAction _action;
        public MaybeStep(double probability, IAction action)
        {
            _probability = probability;
            _action = action;
        }
        public IAction Resolve(ActionContext ctx) =>
            ctx.Random.NextDouble() < _probability ? _action : null;
    }

    public class ActionSequenceBuilder
    {
        private readonly ActionSequence _sequence;

        internal ActionSequenceBuilder(string name)
        {
            _sequence = new ActionSequence(name);
        }

        public ActionSequenceBuilder Wait(TimeSpan duration, string displayText)
        {
            _sequence._steps.Add(new DirectStep(
                new Primitives.WaitAction(duration, displayText)));
            return this;
        }

        public ActionSequenceBuilder Do(IAction action)
        {
            _sequence._steps.Add(new DirectStep(action));
            return this;
        }

        public ActionSequenceBuilder MoveTo(int targetLocationId, int? targetSubLocationId = null, string displayText = "moving")
        {
            _sequence._steps.Add(new DirectStep(
                new Primitives.MoveToAction(targetLocationId, targetSubLocationId, displayText)));
            return this;
        }

        public ActionSequenceBuilder If(Func<ActionContext, bool> condition,
            Func<ActionSequenceBuilder, ActionSequenceBuilder> buildInner)
        {
            var inner = new ActionSequenceBuilder("if-branch");
            buildInner(inner);
            var innerSeq = inner.Build();
            _sequence._steps.Add(new ConditionalStep(condition, innerSeq));
            return this;
        }

        public ActionSequenceBuilder Maybe(double probability,
            Func<ActionSequenceBuilder, ActionSequenceBuilder> buildInner)
        {
            var inner = new ActionSequenceBuilder("maybe-branch");
            buildInner(inner);
            var innerSeq = inner.Build();
            _sequence._steps.Add(new MaybeStep(probability, innerSeq));
            return this;
        }

        public ActionSequence Build() => _sequence;
    }
}
