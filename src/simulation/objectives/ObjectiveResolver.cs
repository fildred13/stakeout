using System;
using System.Collections.Generic;
using Stakeout.Simulation.Actions;

namespace Stakeout.Simulation.Objectives;

// TODO: Project 3 — this system will be rebuilt as part of the simulation overhaul.
public static class ObjectiveResolver
{
    public static List<SimTask> ResolveTasks(List<Objective> objectives, SimulationState state)
    {
        throw new System.NotImplementedException();
    }

    public static Objective CreateGetSleepObjective(TimeSpan sleepTime, TimeSpan wakeTime, int homeAddressId, string unitTag = null)
    {
        throw new System.NotImplementedException();
    }

    public static Objective CreateMaintainJobObjective(TimeSpan shiftStart, TimeSpan shiftEnd, int workAddressId)
    {
        throw new System.NotImplementedException();
    }

    public static Objective CreateDefaultIdleObjective(int homeAddressId, string unitTag = null)
    {
        throw new System.NotImplementedException();
    }
}
