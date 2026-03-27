using System;
using System.Collections.Generic;
using Stakeout.Simulation.Actions;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Objectives;

namespace Stakeout.Simulation.Scheduling.Decomposition;

public class InhabitDecomposition : IDecompositionStrategy
{
    public List<ScheduleEntry> Decompose(SimTask task, SublocationGraph graph,
        TimeSpan startTime, TimeSpan endTime, Random rng)
    {
        bool isMorning = startTime.Hours < 12;

        var bedroom = graph.FindByTag("bedroom");
        var restroom = graph.FindByTag("restroom");
        var kitchen = graph.FindByTag("kitchen");
        var entryResult = graph.FindEntryPoint("entrance");
        var entrance = entryResult?.target;

        // Build list of meaningful rooms only (no pathfinding intermediates)
        var rooms = new List<Sublocation>();

        if (isMorning)
        {
            // Morning routine: bedroom → restroom → kitchen → entrance
            if (bedroom != null) rooms.Add(bedroom);
            if (restroom != null) rooms.Add(restroom);
            if (kitchen != null) rooms.Add(kitchen);
            if (entrance != null) rooms.Add(entrance);
        }
        else
        {
            // Evening routine: entrance → kitchen → living → restroom → bedroom
            var living = graph.FindByTag("living");
            if (entrance != null) rooms.Add(entrance);
            if (kitchen != null) rooms.Add(kitchen);
            if (living != null) rooms.Add(living);
            if (restroom != null) rooms.Add(restroom);
            if (bedroom != null) rooms.Add(bedroom);
        }

        if (rooms.Count == 0) return new List<ScheduleEntry>();

        return AssignTimes(rooms, task.TargetAddressId, task.ActionType, startTime, endTime);
    }

    private List<ScheduleEntry> AssignTimes(List<Sublocation> sublocations, int? addressId,
        ActionType actionType, TimeSpan startTime, TimeSpan endTime)
    {
        var totalDuration = endTime - startTime;
        if (totalDuration <= TimeSpan.Zero)
            totalDuration += TimeSpan.FromHours(24);

        var slotDuration = TimeSpan.FromTicks(totalDuration.Ticks / sublocations.Count);

        var entries = new List<ScheduleEntry>();
        var current = startTime;

        for (int i = 0; i < sublocations.Count; i++)
        {
            var slotEnd = (i == sublocations.Count - 1) ? endTime : current + slotDuration;
            entries.Add(new ScheduleEntry
            {
                Action = actionType,
                StartTime = current,
                EndTime = slotEnd,
                TargetAddressId = addressId,
                TargetSublocationId = sublocations[i].Id
            });
            current = slotEnd;
        }

        return entries;
    }
}
