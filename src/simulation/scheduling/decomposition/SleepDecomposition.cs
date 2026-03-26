using System;
using System.Collections.Generic;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Objectives;

namespace Stakeout.Simulation.Scheduling.Decomposition;

public class SleepDecomposition : IDecompositionStrategy
{
    public List<ScheduleEntry> Decompose(SimTask task, SublocationGraph graph,
        TimeSpan startTime, TimeSpan endTime, Random rng)
    {
        var bedroom = graph.FindByTag("bedroom");
        if (bedroom == null)
        {
            // Fallback: sleep at whatever sublocation is available
            return new List<ScheduleEntry>
            {
                new ScheduleEntry
                {
                    Action = task.ActionType,
                    StartTime = startTime,
                    EndTime = endTime,
                    TargetAddressId = task.TargetAddressId
                }
            };
        }

        return new List<ScheduleEntry>
        {
            new ScheduleEntry
            {
                Action = task.ActionType,
                StartTime = startTime,
                EndTime = endTime,
                TargetAddressId = task.TargetAddressId,
                TargetSublocationId = bedroom.Id
            }
        };
    }
}
