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

        var road = graph.GetRoad();
        var bedroom = SleepDecomposition.FindRoom(graph, "bedroom", task.UnitTag);
        var restroom = SleepDecomposition.FindRoom(graph, "restroom", task.UnitTag);
        var kitchen = SleepDecomposition.FindRoom(graph, "kitchen", task.UnitTag);
        var entryResult = graph.FindEntryPoint("entrance");
        var entrance = entryResult?.target;

        // Build list of meaningful rooms only (no pathfinding intermediates)
        var rooms = new List<Sublocation>();

        if (isMorning)
        {
            // Morning routine: bedroom → restroom → kitchen → entrance → road (leaving)
            if (bedroom != null) rooms.Add(bedroom);
            if (restroom != null) rooms.Add(restroom);
            if (kitchen != null) rooms.Add(kitchen);
            if (entrance != null) rooms.Add(entrance);
            if (road != null) rooms.Add(road);
        }
        else
        {
            // Evening routine: road (arriving) → entrance → kitchen → living → restroom → bedroom
            var living = SleepDecomposition.FindRoom(graph, "living", task.UnitTag);
            if (road != null) rooms.Add(road);
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
