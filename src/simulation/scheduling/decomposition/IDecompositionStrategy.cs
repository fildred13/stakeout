using System;
using System.Collections.Generic;
using Stakeout.Simulation.Objectives;
using Stakeout.Simulation.Scheduling;

namespace Stakeout.Simulation.Scheduling.Decomposition;

// TODO: Project 3 — this system will be rebuilt as part of the simulation overhaul.
public interface IDecompositionStrategy
{
    List<ScheduleEntry> Decompose(SimTask task, SublocationGraph graph,
        TimeSpan startTime, TimeSpan endTime, Random rng);
}
