using System;
using System.Collections.Generic;
using System.Linq;
using Stakeout.Simulation.Actions.Telephone;
using Stakeout.Simulation.Entities;

namespace Stakeout.Simulation.Objectives;

public class OrganizeDateObjective : Objective
{
    private enum State { NeedToCall, AwaitingAnswer, MessageLeft, AwaitingCallback, DateOrganized }
    private State _state = State.NeedToCall;
    private int _groupId = -1;

    public int TargetPersonId { get; }
    private readonly int _proposedMeetupAddressId;
    private readonly DateTime _proposedCallTime;
    private readonly DateTime _proposedMeetupTime;
    private readonly DateTime _proposedPickupTime;

    public override int Priority => 50;
    public override ObjectiveSource Source => ObjectiveSource.Social;

    public OrganizeDateObjective(int targetPersonId, int proposedMeetupAddressId,
        DateTime proposedCallTime, DateTime proposedMeetupTime, DateTime proposedPickupTime)
    {
        TargetPersonId = targetPersonId;
        _proposedMeetupAddressId = proposedMeetupAddressId;
        _proposedCallTime = proposedCallTime;
        _proposedMeetupTime = proposedMeetupTime;
        _proposedPickupTime = proposedPickupTime;
    }

    public void SetGroupId(int groupId) => _groupId = groupId;

    public override List<PlannedAction> GetActions(Person person, SimulationState state,
        DateTime planStart, DateTime planEnd)
    {
        if (_state != State.NeedToCall) return new List<PlannedAction>();
        if (Status == ObjectiveStatus.Completed) return new List<PlannedAction>();

        var recipient = state.People[TargetPersonId];

        // Create the group (Forming) if not yet created
        if (_groupId < 0)
        {
            var group = new Group
            {
                Id = state.GenerateEntityId(),
                Type = GroupType.Date,
                Status = GroupStatus.Forming,
                DriverPersonId = person.Id,
                PickupAddressId = recipient.HomeAddressId,
                PickupTime = _proposedPickupTime,
                MeetupAddressId = _proposedMeetupAddressId,
                MeetupTime = _proposedMeetupTime,
                MemberPersonIds = new List<int> { person.Id, TargetPersonId }
            };
            state.Groups[group.Id] = group;
            _groupId = group.Id;
        }

        // Find recipient's home phone — required to place the call
        if (!recipient.HomePhoneFixtureId.HasValue) return new List<PlannedAction>();

        var callAction = new PhoneCallAction(
            targetAddressId: recipient.HomeAddressId,
            targetFixtureId: recipient.HomePhoneFixtureId.Value,
            proposedGroupId: _groupId,
            callerId: person.Id,
            recipientId: TargetPersonId);

        _state = State.AwaitingAnswer;

        return new List<PlannedAction>
        {
            new()
            {
                Action = callAction,
                TargetAddressId = recipient.HomeAddressId,
                TimeWindowStart = _proposedCallTime,
                TimeWindowEnd = _proposedCallTime + TimeSpan.FromHours(2),
                Duration = TimeSpan.FromMinutes(10),
                DisplayText = "calling to arrange a date",
                SourceObjective = this
            }
        };
    }

    public void OnMessageLeft()
    {
        _state = State.MessageLeft;
    }

    public void OnAccepted(DateTime acceptedAt, SimulationState state)
    {
        if (_groupId < 0) return;
        var group = state.Groups[_groupId];

        // Decide if today's time is still feasible (accepted + 2h buffer <= proposed meetup)
        if (acceptedAt.AddHours(2) > _proposedMeetupTime)
        {
            // Advance to next day, same time-of-day
            var nextDay = _proposedMeetupTime.Date.AddDays(1);
            group.MeetupTime = nextDay + _proposedMeetupTime.TimeOfDay;
            group.PickupTime = nextDay + _proposedPickupTime.TimeOfDay;
        }

        group.Status = GroupStatus.Active;

        // Clean up any competing OrganizeDateObjective that the recipient may have targeting the caller.
        // This prevents the recipient's orphaned objective from creating a second group.
        var recipient = state.People[TargetPersonId];
        foreach (var competing in recipient.Objectives.OfType<OrganizeDateObjective>()
            .Where(o => o.TargetPersonId == group.DriverPersonId && o.Status == ObjectiveStatus.Active)
            .ToList())
        {
            competing.Status = ObjectiveStatus.Completed;
        }

        // Create GoOnDateObjective for both members
        foreach (var memberId in group.MemberPersonIds)
        {
            var member = state.People[memberId];
            if (member.Objectives.OfType<GoOnDateObjective>().Any(o => o.GroupId == _groupId))
                continue;
            member.Objectives.Add(new GoOnDateObjective(_groupId) { Id = state.GenerateEntityId() });
            member.NeedsReplan = true;
        }

        Status = ObjectiveStatus.Completed;
        _state = State.DateOrganized;
    }
}
