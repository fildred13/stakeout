using System;
using System.Collections.Generic;
using Stakeout.Simulation.Objectives;

namespace Stakeout.Simulation.Scheduling.Decomposition;

// TODO: Project 3 — this system will be rebuilt as part of the simulation overhaul.
public class StaffShiftDecomposition : IDecompositionStrategy
{
    public List<ScheduleEntry> Decompose(SimTask task, SublocationGraph graph,
        TimeSpan startTime, TimeSpan endTime, Random rng)
    {
        throw new System.NotImplementedException();
    }
}
