using System;
using System.Collections.Generic;
using Stakeout.Simulation.Entities;

namespace Stakeout.Simulation.Objectives;

// Stub — full implementation in Task 7
public class OrganizeDateObjective : Objective
{
    public int TargetPersonId { get; }
    private int _groupId = -1;

    public override int Priority => 50;
    public override ObjectiveSource Source => ObjectiveSource.Social;

    public OrganizeDateObjective(int targetPersonId, int proposedMeetupAddressId,
        DateTime proposedCallTime, DateTime proposedMeetupTime, DateTime proposedPickupTime)
    {
        TargetPersonId = targetPersonId;
    }

    public void SetGroupId(int groupId) => _groupId = groupId;

    public void OnMessageLeft() { }

    public void OnAccepted(DateTime acceptedAt, SimulationState state) { }

    public override List<PlannedAction> GetActions(Person person, SimulationState state,
        DateTime planStart, DateTime planEnd) => new();
}
