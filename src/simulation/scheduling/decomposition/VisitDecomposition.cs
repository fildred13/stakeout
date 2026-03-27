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
        var road = graph.GetRoad();
        var entryResult = graph.FindEntryPoint("entrance");
        var entrance = entryResult?.target;
        var entranceConnId = entryResult?.conn?.Id;
        if (entrance == null || road == null)
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
                    TargetSublocationId = entrance.Id,
                    ViaConnectionId = entranceConnId
                }
            };
        }

        // Meaningful stops: road (brief) → entrance → target (bulk) → entrance → road (brief)
        var totalDuration = endTime - startTime;
        if (totalDuration <= TimeSpan.Zero)
            totalDuration += TimeSpan.FromHours(24);

        var transitMinutes = ArrivalMinutes + DepartureMinutes + ArrivalMinutes + DepartureMinutes;
        var transitDuration = TimeSpan.FromMinutes(Math.Min(transitMinutes, totalDuration.TotalMinutes * 0.4));
        var perTransit = TimeSpan.FromTicks(transitDuration.Ticks / 4);
        var mainDuration = totalDuration - transitDuration;

        var current = startTime;
        var entries = new List<ScheduleEntry>();

        // Road arrival
        var roadEnd = current + perTransit;
        entries.Add(new ScheduleEntry
        {
            Action = task.ActionType, StartTime = current, EndTime = roadEnd,
            TargetAddressId = task.TargetAddressId, TargetSublocationId = road.Id
        });
        current = roadEnd;

        // Entrance arrival
        var entranceEnd = current + perTransit;
        entries.Add(new ScheduleEntry
        {
            Action = task.ActionType, StartTime = current, EndTime = entranceEnd,
            TargetAddressId = task.TargetAddressId, TargetSublocationId = entrance.Id,
            ViaConnectionId = entranceConnId
        });
        current = entranceEnd;

        // Main activity
        var mainEnd = current + mainDuration;
        entries.Add(new ScheduleEntry
        {
            Action = task.ActionType, StartTime = current, EndTime = mainEnd,
            TargetAddressId = task.TargetAddressId, TargetSublocationId = target.Id
        });
        current = mainEnd;

        // Entrance departure
        var entranceDepartEnd = current + perTransit;
        entries.Add(new ScheduleEntry
        {
            Action = task.ActionType, StartTime = current, EndTime = entranceDepartEnd,
            TargetAddressId = task.TargetAddressId, TargetSublocationId = entrance.Id,
            ViaConnectionId = entranceConnId
        });
        current = entranceDepartEnd;

        // Road departure
        entries.Add(new ScheduleEntry
        {
            Action = task.ActionType, StartTime = current, EndTime = endTime,
            TargetAddressId = task.TargetAddressId, TargetSublocationId = road.Id
        });

        return entries;
    }
}
