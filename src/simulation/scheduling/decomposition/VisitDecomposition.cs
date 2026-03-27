using System;
using System.Collections.Generic;
using Stakeout.Simulation.Actions;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Objectives;

namespace Stakeout.Simulation.Scheduling.Decomposition;

public class VisitDecomposition : IDecompositionStrategy
{
    private const int ArrivalMinutes = 5;
    private const int DepartureMinutes = 5;

    public List<ScheduleEntry> Decompose(SimTask task, SublocationGraph graph,
        TimeSpan startTime, TimeSpan endTime, Random rng)
    {
        var entrance = graph.FindEntryPoint("entrance")?.target;
        if (entrance == null)
            return new List<ScheduleEntry>();

        // Determine the target sublocation
        Sublocation target = null;

        if (task.ActionData != null &&
            task.ActionData.TryGetValue("TargetSublocationId", out var rawId) &&
            rawId is int sublocationId)
        {
            target = graph.Get(sublocationId);
        }

        if (target == null)
        {
            target = graph.FindByTag("social") ?? graph.FindByTag("service_area");
        }

        if (target == null || target.Id == entrance.Id)
        {
            // No specific target: stay at entrance
            return new List<ScheduleEntry>
            {
                new ScheduleEntry
                {
                    Action = task.ActionType,
                    StartTime = startTime,
                    EndTime = endTime,
                    TargetAddressId = task.TargetAddressId,
                    TargetSublocationId = entrance.Id
                }
            };
        }

        // Meaningful stops: entrance (brief) → target (bulk) → entrance (brief)
        var totalDuration = endTime - startTime;
        if (totalDuration <= TimeSpan.Zero)
            totalDuration += TimeSpan.FromHours(24);

        var arrivalDuration = TimeSpan.FromMinutes(Math.Min(ArrivalMinutes, totalDuration.TotalMinutes / 3));
        var departureDuration = TimeSpan.FromMinutes(Math.Min(DepartureMinutes, totalDuration.TotalMinutes / 3));

        var arrivalEnd = startTime + arrivalDuration;
        var departureStart = endTime - departureDuration;

        return new List<ScheduleEntry>
        {
            new ScheduleEntry
            {
                Action = task.ActionType,
                StartTime = startTime,
                EndTime = arrivalEnd,
                TargetAddressId = task.TargetAddressId,
                TargetSublocationId = entrance.Id
            },
            new ScheduleEntry
            {
                Action = task.ActionType,
                StartTime = arrivalEnd,
                EndTime = departureStart,
                TargetAddressId = task.TargetAddressId,
                TargetSublocationId = target.Id
            },
            new ScheduleEntry
            {
                Action = task.ActionType,
                StartTime = departureStart,
                EndTime = endTime,
                TargetAddressId = task.TargetAddressId,
                TargetSublocationId = entrance.Id
            }
        };
    }
}
