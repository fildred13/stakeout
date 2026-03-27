using System;
using System.Collections.Generic;
using System.Linq;
using Stakeout.Simulation.Actions;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Objectives;

namespace Stakeout.Simulation.Scheduling.Decomposition;

public class WorkDayDecomposition : IDecompositionStrategy
{
    private const int ArrivalMinutes = 5;
    private const int DepartureMinutes = 5;
    private const int BreakMinutes = 15;

    public List<ScheduleEntry> Decompose(SimTask task, SublocationGraph graph,
        TimeSpan startTime, TimeSpan endTime, Random rng)
    {
        var road = graph.GetRoad();
        var entryResult = graph.FindEntryPoint("entrance");
        var entrance = entryResult?.target;
        var entranceConnId = entryResult?.conn?.Id;
        if (entrance == null || road == null)
            return new List<ScheduleEntry>();

        var workArea = graph.FindByTag("work_area");
        if (workArea == null)
        {
            return new List<ScheduleEntry>
            {
                new ScheduleEntry
                {
                    Action = ActionType.Work,
                    StartTime = startTime,
                    EndTime = endTime,
                    TargetAddressId = task.TargetAddressId,
                    TargetSublocationId = entrance.Id
                }
            };
        }

        // Build sequence of meaningful stops: road → entrance → work → ... → entrance → road
        var stops = new List<(Sublocation sub, StopKind kind, int? viaConnId)>();

        stops.Add((road, StopKind.Arrival, null));
        stops.Add((entrance, StopKind.Arrival, entranceConnId));
        stops.Add((workArea, StopKind.Work, null));

        // Generate 1-3 break opportunities
        int breakCount = rng.Next(1, 4);
        for (int i = 0; i < breakCount; i++)
        {
            if (rng.NextDouble() < 0.5)
            {
                var food = graph.FindByTag("food");
                if (food != null)
                {
                    stops.Add((food, StopKind.Break, null));
                    stops.Add((workArea, StopKind.Work, null));
                }
            }

            if (rng.NextDouble() < 0.7)
            {
                var restroom = graph.FindByTag("restroom");
                if (restroom != null)
                {
                    stops.Add((restroom, StopKind.Break, null));
                    stops.Add((workArea, StopKind.Work, null));
                }
            }
        }

        stops.Add((entrance, StopKind.Departure, entranceConnId));
        stops.Add((road, StopKind.Departure, null));

        return AllocateTimes(stops, task.TargetAddressId, startTime, endTime);
    }

    private enum StopKind { Arrival, Departure, Break, Work }

    private List<ScheduleEntry> AllocateTimes(
        List<(Sublocation sub, StopKind kind, int? viaConnId)> stops,
        int? addressId, TimeSpan startTime, TimeSpan endTime)
    {
        var totalDuration = endTime - startTime;
        if (totalDuration <= TimeSpan.Zero)
            totalDuration += TimeSpan.FromHours(24);

        // Calculate fixed time for non-work stops
        int fixedMinutes = 0;
        int workCount = 0;
        foreach (var (_, kind, _) in stops)
        {
            switch (kind)
            {
                case StopKind.Arrival: fixedMinutes += ArrivalMinutes; break;
                case StopKind.Departure: fixedMinutes += DepartureMinutes; break;
                case StopKind.Break: fixedMinutes += BreakMinutes; break;
                case StopKind.Work: workCount++; break;
            }
        }

        var fixedDuration = TimeSpan.FromMinutes(Math.Min(fixedMinutes, totalDuration.TotalMinutes));
        var workDuration = totalDuration - fixedDuration;
        var workSlot = workCount > 0
            ? TimeSpan.FromTicks(workDuration.Ticks / workCount)
            : TimeSpan.Zero;

        var entries = new List<ScheduleEntry>();
        var current = startTime;

        for (int i = 0; i < stops.Count; i++)
        {
            var (sub, kind, viaConnId) = stops[i];
            var duration = kind switch
            {
                StopKind.Arrival => TimeSpan.FromMinutes(ArrivalMinutes),
                StopKind.Departure => TimeSpan.FromMinutes(DepartureMinutes),
                StopKind.Break => TimeSpan.FromMinutes(BreakMinutes),
                StopKind.Work => workSlot,
                _ => workSlot
            };

            // Last entry gets whatever time remains to avoid rounding gaps
            var slotEnd = (i == stops.Count - 1) ? endTime : current + duration;

            entries.Add(new ScheduleEntry
            {
                Action = ActionType.Work,
                StartTime = current,
                EndTime = slotEnd,
                TargetAddressId = addressId,
                TargetSublocationId = sub.Id,
                ViaConnectionId = viaConnId
            });
            current = slotEnd;
        }

        return entries;
    }
}
