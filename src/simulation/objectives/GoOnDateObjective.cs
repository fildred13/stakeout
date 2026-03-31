using System;
using System.Collections.Generic;
using Stakeout.Simulation.Entities;

namespace Stakeout.Simulation.Objectives;

// Stub — full implementation in Task 8
public class GoOnDateObjective : Objective
{
    public int GroupId { get; }

    public override int Priority => 70;
    public override ObjectiveSource Source => ObjectiveSource.Social;

    public GoOnDateObjective(int groupId)
    {
        GroupId = groupId;
    }

    public override List<PlannedAction> GetActions(Person person, SimulationState state,
        DateTime planStart, DateTime planEnd) => new();
}
