using System;
using System.Linq;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Objectives;

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

    public void OnComplete(ActionContext ctx)
    {
        // Find and consume the invitation
        if (!ctx.State.PendingInvitationsByPersonId.TryGetValue(ctx.Person.Id, out var invitations))
            return;
        var inv = invitations.FirstOrDefault(i => i.Id == _invitationId);
        if (inv == null) return;
        invitations.Remove(inv);

        // Find the OrganizeDateObjective on the caller that targets this person
        var caller = ctx.State.People[inv.FromPersonId];
        var objective = caller.Objectives
            .OfType<OrganizeDateObjective>()
            .FirstOrDefault(o => o.TargetPersonId == ctx.Person.Id)
            // Callback scenario: current person called back, their OrganizeDateObjective targets the caller
            ?? ctx.Person.Objectives
                .OfType<OrganizeDateObjective>()
                .FirstOrDefault(o => o.TargetPersonId == inv.FromPersonId);

        objective?.OnAccepted(ctx.CurrentTime, ctx.State);
    }
}
