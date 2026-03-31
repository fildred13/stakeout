using System;

namespace Stakeout.Simulation.Actions.Telephone;

public class AcceptPhoneCallAction : IAction
{
    private readonly int _invitationId;
    private TimeSpan _elapsed = TimeSpan.Zero;
    private static readonly TimeSpan CallDuration = TimeSpan.FromMinutes(2);

    public string Name => "AcceptPhoneCall";
    public string DisplayText => "Talking on the phone";

    public AcceptPhoneCallAction(int invitationId)
    {
        _invitationId = invitationId;
    }

    public void OnStart(ActionContext ctx) { }

    public ActionStatus Tick(ActionContext ctx, TimeSpan delta)
    {
        _elapsed += delta;
        return _elapsed >= CallDuration ? ActionStatus.Completed : ActionStatus.Running;
    }

    public void OnComplete(ActionContext ctx) { }
}
