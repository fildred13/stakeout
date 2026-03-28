using System;
using System.Collections.Generic;
using System.Linq;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Objectives;

namespace Stakeout.Simulation.Scheduling.Decomposition;

public class SleepDecomposition : IDecompositionStrategy
{
    public List<ScheduleEntry> Decompose(SimTask task, SublocationGraph graph,
        TimeSpan startTime, TimeSpan endTime, Random rng)
    {
        var bedroom = FindRoom(graph, "bedroom", task.UnitTag);
        if (bedroom == null)
        {
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

    internal static Sublocation FindRoom(SublocationGraph graph, string roomTag, string unitTag)
    {
        if (unitTag != null)
        {
            var unitRooms = graph.FindAllByTag(unitTag);
            return unitRooms.FirstOrDefault(s => s.HasTag(roomTag));
        }
        return graph.FindByTag(roomTag);
    }
}
