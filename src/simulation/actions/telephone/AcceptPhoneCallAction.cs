using System;
using System.Linq;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Objectives;

namespace Stakeout.Simulation.Actions.Telephone;

public class AcceptPhoneCallAction : IAction
{
    private readonly PendingInvitation _invitation;
    private TimeSpan _elapsed = TimeSpan.Zero;
    private static readonly TimeSpan CallDuration = TimeSpan.FromMinutes(2);

    public string Name => "AcceptPhoneCall";
    public string DisplayText => "Talking on the phone";

    public AcceptPhoneCallAction(PendingInvitation invitation)
    {
        _invitation = invitation;
    }

    public void OnStart(ActionContext ctx) { }

    public ActionStatus Tick(ActionContext ctx, TimeSpan delta)
    {
        _elapsed += delta;
        return _elapsed >= CallDuration ? ActionStatus.Completed : ActionStatus.Running;
    }

    public void OnComplete(ActionContext ctx)
    {
        // Find the OrganizeDateObjective on the caller that targets this person
        var caller = ctx.State.People[_invitation.FromPersonId];
        var objective = caller.Objectives
            .OfType<OrganizeDateObjective>()
            .FirstOrDefault(o => o.TargetPersonId == ctx.Person.Id)
            // Callback scenario: current person called back, their OrganizeDateObjective targets the caller
            ?? ctx.Person.Objectives
                .OfType<OrganizeDateObjective>()
                .FirstOrDefault(o => o.TargetPersonId == _invitation.FromPersonId);

        objective?.OnAccepted(ctx.CurrentTime, ctx.State);
    }
}
