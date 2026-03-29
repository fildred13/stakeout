using System;

namespace Stakeout.Simulation.Actions.Primitives;

public class WaitAction : IAction
{
    private readonly TimeSpan _duration;
    private TimeSpan _elapsed;

    public string Name => "Wait";
    public string DisplayText { get; }
    public TimeSpan RemainingTime => _duration - _elapsed;

    public WaitAction(TimeSpan duration, string displayText)
    {
        _duration = duration;
        DisplayText = displayText;
    }

    public void OnStart(ActionContext ctx) { _elapsed = TimeSpan.Zero; }

    public ActionStatus Tick(ActionContext ctx, TimeSpan delta)
    {
        _elapsed += delta;
        return _elapsed >= _duration ? ActionStatus.Completed : ActionStatus.Running;
    }

    public void OnComplete(ActionContext ctx) { }
}
