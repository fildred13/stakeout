using System;

namespace Stakeout.Simulation.Actions.Telephone;

public class CheckAnsweringMachineAction : IAction
{
    private TimeSpan _elapsed = TimeSpan.Zero;
    private static readonly TimeSpan CheckDuration = TimeSpan.FromMinutes(2);

    public string Name => "CheckAnsweringMachine";
    public string DisplayText => "Checking answering machine";

    public void OnStart(ActionContext ctx) { }

    public ActionStatus Tick(ActionContext ctx, TimeSpan delta)
    {
        _elapsed += delta;
        return _elapsed >= CheckDuration ? ActionStatus.Completed : ActionStatus.Running;
    }

    public void OnComplete(ActionContext ctx) { }
}
