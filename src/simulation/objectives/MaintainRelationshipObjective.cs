using System;
using System.Collections.Generic;
using System.Linq;
using Stakeout.Simulation.Entities;

namespace Stakeout.Simulation.Objectives;

public class MaintainRelationshipObjective : Objective
{
    public int PartnerPersonId { get; }
    private static readonly TimeSpan DateCooldown = TimeSpan.FromDays(7);

    public override int Priority => 45;
    public override ObjectiveSource Source => ObjectiveSource.Social;

    public MaintainRelationshipObjective(int partnerPersonId)
    {
        PartnerPersonId = partnerPersonId;
    }

    public override List<PlannedAction> GetActions(Person person, SimulationState state,
        DateTime planStart, DateTime planEnd)
    {
        // Don't schedule if already organizing a date with this partner
        if (person.Objectives.OfType<OrganizeDateObjective>()
            .Any(o => o.TargetPersonId == PartnerPersonId && o.Status == ObjectiveStatus.Active))
            return new List<PlannedAction>();

        // Don't schedule if currently on a date
        if (person.Objectives.OfType<GoOnDateObjective>()
            .Any(o => o.Status == ObjectiveStatus.Active))
            return new List<PlannedAction>();

        // Don't schedule if went on a date recently
        if (WentOnDateRecently(person, state, planStart))
            return new List<PlannedAction>();

        // Need a diner to propose as the meetup venue
        var diner = state.Addresses.Values.FirstOrDefault(a => a.Type == AddressType.Diner);
        if (diner == null) return new List<PlannedAction>();

        // Propose today at 7pm if at least 4 hours away, otherwise tomorrow at 7pm
        var meetupTime = planStart.Date.AddHours(19);
        if (meetupTime < planStart.AddHours(4))
            meetupTime = planStart.Date.AddDays(1).AddHours(19);

        var pickupTime = meetupTime - TimeSpan.FromMinutes(70);
        var callTime = planStart.AddHours(1);
        if (callTime > meetupTime - TimeSpan.FromHours(3))
            callTime = meetupTime - TimeSpan.FromHours(3);

        person.Objectives.Add(new OrganizeDateObjective(
            targetPersonId: PartnerPersonId,
            proposedMeetupAddressId: diner.Id,
            proposedCallTime: callTime,
            proposedMeetupTime: meetupTime,
            proposedPickupTime: pickupTime)
        {
            Id = state.GenerateEntityId()
        });
        person.NeedsReplan = true;

        return new List<PlannedAction>();
    }

    private bool WentOnDateRecently(Person person, SimulationState state, DateTime now)
    {
        return state.Groups.Values.Any(g =>
            g.Type == GroupType.Date &&
            g.Status == GroupStatus.Disbanded &&
            g.MemberPersonIds.Contains(person.Id) &&
            g.MemberPersonIds.Contains(PartnerPersonId) &&
            now - g.MeetupTime < DateCooldown);
    }
}
