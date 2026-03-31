using System;
using System.Collections.Generic;
using System.Linq;
using Stakeout.Simulation.Actions.Telephone;
using Stakeout.Simulation.Entities;

namespace Stakeout.Simulation.Objectives;

public class CallBackObjective : Objective
{
    public int TargetPersonId { get; }
    private readonly int _targetFixtureId;
    private readonly int _targetAddressId;

    public override int Priority => 10;
    public override ObjectiveSource Source => ObjectiveSource.Social;

    public CallBackObjective(int targetPersonId, int targetFixtureId, int targetAddressId)
    {
        TargetPersonId = targetPersonId;
        _targetFixtureId = targetFixtureId;
        _targetAddressId = targetAddressId;
    }

    public override List<PlannedAction> GetActions(Person person, SimulationState state,
        DateTime planStart, DateTime planEnd)
    {
        if (Status == ObjectiveStatus.Completed) return new List<PlannedAction>();

        // Reuse the existing Forming group if any, or create a new one
        var existingGroup = state.Groups.Values
            .FirstOrDefault(g => g.Status == GroupStatus.Forming
                              && g.MemberPersonIds.Contains(person.Id)
                              && g.MemberPersonIds.Contains(TargetPersonId));

        int groupId;
        if (existingGroup != null)
        {
            groupId = existingGroup.Id;
        }
        else
        {
            var group = new Group
            {
                Id = state.GenerateEntityId(),
                Type = GroupType.Date,
                Status = GroupStatus.Forming,
                MemberPersonIds = new List<int> { TargetPersonId, person.Id }
            };
            state.Groups[group.Id] = group;
            groupId = group.Id;
        }

        var callAction = new PhoneCallAction(
            targetAddressId: _targetAddressId,
            targetFixtureId: _targetFixtureId,
            proposedGroupId: groupId,
            callerId: person.Id,
            recipientId: TargetPersonId);

        Status = ObjectiveStatus.Completed;

        return new List<PlannedAction>
        {
            new()
            {
                Action = callAction,
                TargetAddressId = _targetAddressId,
                TimeWindowStart = planStart,
                TimeWindowEnd = planEnd,
                Duration = TimeSpan.FromMinutes(10),
                DisplayText = "returning a phone call",
                SourceObjective = this
            }
        };
    }
}
