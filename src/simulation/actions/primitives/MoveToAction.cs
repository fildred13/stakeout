using System;
using Stakeout.Simulation.Entities;

namespace Stakeout.Simulation.Actions.Primitives;

/// <summary>
/// Intra-address movement — changes the person's current Location/SubLocation.
/// Completes immediately (no travel time for room-to-room movement).
/// </summary>
public class MoveToAction : IAction
{
    private readonly int _targetLocationId;
    private readonly int? _targetSubLocationId;

    public string Name => "MoveTo";
    public string DisplayText { get; }

    public MoveToAction(int targetLocationId, int? targetSubLocationId, string displayText)
    {
        _targetLocationId = targetLocationId;
        _targetSubLocationId = targetSubLocationId;
        DisplayText = displayText;
    }

    public void OnStart(ActionContext ctx) { }

    public ActionStatus Tick(ActionContext ctx, TimeSpan delta)
    {
        ctx.Person.CurrentLocationId = _targetLocationId;
        ctx.Person.CurrentSubLocationId = _targetSubLocationId;
        return ActionStatus.Completed;
    }

    public void OnComplete(ActionContext ctx) { }
}
