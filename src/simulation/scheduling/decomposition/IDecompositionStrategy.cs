using System.Collections.Generic;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Objectives;
using Stakeout.Simulation.Scheduling;

namespace Stakeout.Simulation.Scheduling.Decomposition;

public interface IDecompositionStrategy
{
    List<ScheduleEntry> Decompose(SimTask task, SublocationGraph graph,
        System.TimeSpan startTime, System.TimeSpan endTime, System.Random rng);
}
