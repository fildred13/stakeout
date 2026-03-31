using System;
using System.Linq;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Objectives;
using Stakeout.Simulation.Traces;

namespace Stakeout.Simulation.Actions.Telephone;

public class PhoneCallAction : IAction
{
    private readonly int _targetAddressId;
    private readonly int _targetFixtureId;
    private readonly int _proposedGroupId;
    private readonly int _callerId;
    private readonly int _recipientId;
    private TimeSpan _elapsed = TimeSpan.Zero;
    private static readonly TimeSpan CallDuration = TimeSpan.FromMinutes(10);

    public string Name => "PhoneCall";
    public string DisplayText => "Making a phone call";

    public PhoneCallAction(int targetAddressId, int targetFixtureId, int proposedGroupId,
        int callerId, int recipientId)
    {
        _targetAddressId = targetAddressId;
        _targetFixtureId = targetFixtureId;
        _proposedGroupId = proposedGroupId;
        _callerId = callerId;
        _recipientId = recipientId;
    }

    public void OnStart(ActionContext ctx) { }

    public ActionStatus Tick(ActionContext ctx, TimeSpan delta)
    {
        _elapsed += delta;
        return _elapsed >= CallDuration ? ActionStatus.Completed : ActionStatus.Running;
    }

    public void OnComplete(ActionContext ctx)
    {
        // Outgoing record on caller's home phone
        if (ctx.Person.HomePhoneFixtureId.HasValue)
            TraceEmitter.EmitRecord(ctx.State, ctx.Person.HomePhoneFixtureId.Value,
                ctx.Person.Id, $"Outgoing call to {ctx.State.Addresses[_targetAddressId].Id}");

        // Incoming record on target phone
        TraceEmitter.EmitRecord(ctx.State, _targetFixtureId, ctx.Person.Id,
            $"Incoming call from {ctx.Person.FirstName} {ctx.Person.LastName}");

        var recipient = ctx.State.People[_recipientId];
        if (recipient.CurrentAddressId == _targetAddressId)
        {
            // Recipient is home — create pending invitation
            var inv = new PendingInvitation
            {
                Id = ctx.State.GenerateEntityId(),
                FromPersonId = _callerId,
                ToPersonId = _recipientId,
                Type = InvitationType.DateInvitation,
                ProposedGroupId = _proposedGroupId,
                CreatedAt = ctx.CurrentTime
            };
            ctx.State.AddPendingInvitation(inv);
        }
        else
        {
            // Recipient absent — leave message trace
            TraceEmitter.EmitRecord(ctx.State, _targetFixtureId, ctx.Person.Id,
                $"Message from {ctx.Person.FirstName} {ctx.Person.LastName}: please call back");

            // Notify OrganizeDateObjective on the caller
            var obj = ctx.Person.Objectives
                .OfType<OrganizeDateObjective>()
                .FirstOrDefault(o => o.TargetPersonId == _recipientId);
            obj?.OnMessageLeft();
        }
    }
}
