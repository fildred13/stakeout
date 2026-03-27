using System;
using System.Collections.Generic;
using Stakeout.Simulation.Actions;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Objectives;

namespace Stakeout.Simulation.Scheduling.Decomposition;

public class PatronizeDecomposition : IDecompositionStrategy
{
    private const int ArrivalMinutes = 5;
    private const int DepartureMinutes = 5;
    private const int RestroomMinutes = 10;

    public List<ScheduleEntry> Decompose(SimTask task, SublocationGraph graph,
        TimeSpan startTime, TimeSpan endTime, Random rng)
    {
        var entryResult = graph.FindEntryPoint("entrance");
        var entrance = entryResult?.target;
        var entranceConnId = entryResult?.conn?.Id;
        if (entrance == null)
            return new List<ScheduleEntry>();

        var serviceArea = graph.FindByTag("service_area") ?? graph.FindByTag("social");

        if (serviceArea == null)
        {
            return new List<ScheduleEntry>
            {
                new ScheduleEntry
                {
                    Action = ActionType.Idle,
                    StartTime = startTime,
                    EndTime = endTime,
                    TargetAddressId = task.TargetAddressId,
                    TargetSublocationId = entrance.Id
                }
            };
        }

        // Build meaningful stops: entrance → service area → optional restroom → entrance
        var stops = new List<(Sublocation sub, StopKind kind, int? viaConnId)>();

        stops.Add((entrance, StopKind.Transit, entranceConnId));
        stops.Add((serviceArea, StopKind.Main, null));

        if (rng.NextDouble() < 0.3)
        {
            var restroom = graph.FindByTag("restroom");
            if (restroom != null)
            {
                stops.Add((restroom, StopKind.Transit, null));
                stops.Add((serviceArea, StopKind.Main, null));
            }
        }

        stops.Add((entrance, StopKind.Transit, entranceConnId));

        return AllocateTimes(stops, task.TargetAddressId, startTime, endTime);
    }

    private enum StopKind { Transit, Main }

    private List<ScheduleEntry> AllocateTimes(
        List<(Sublocation sub, StopKind kind, int? viaConnId)> stops,
        int? addressId, TimeSpan startTime, TimeSpan endTime)
    {
        var totalDuration = endTime - startTime;
        if (totalDuration <= TimeSpan.Zero)
            totalDuration += TimeSpan.FromHours(24);

        int transitMinutes = 0;
        int mainCount = 0;
        foreach (var (_, kind, _) in stops)
        {
            if (kind == StopKind.Transit) transitMinutes += ArrivalMinutes;
            else mainCount++;
        }

        var transitDuration = TimeSpan.FromMinutes(Math.Min(transitMinutes, totalDuration.TotalMinutes));
        var mainDuration = totalDuration - transitDuration;
        var mainSlot = mainCount > 0
            ? TimeSpan.FromTicks(mainDuration.Ticks / mainCount)
            : TimeSpan.Zero;

        var entries = new List<ScheduleEntry>();
        var current = startTime;

        for (int i = 0; i < stops.Count; i++)
        {
            var (sub, kind, viaConnId) = stops[i];
            var duration = kind == StopKind.Transit
                ? TimeSpan.FromMinutes(ArrivalMinutes)
                : mainSlot;

            var slotEnd = (i == stops.Count - 1) ? endTime : current + duration;

            entries.Add(new ScheduleEntry
            {
                Action = ActionType.Idle,
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
