using System;

namespace Stakeout.Simulation.Actions;

public interface IAction
{
    string Name { get; }
    string DisplayText { get; }
    ActionStatus Tick(ActionContext ctx, TimeSpan delta);
    void OnStart(ActionContext ctx);
    void OnComplete(ActionContext ctx);
    void OnSuspend(ActionContext ctx) { }
    void OnResume(ActionContext ctx) { }
}
