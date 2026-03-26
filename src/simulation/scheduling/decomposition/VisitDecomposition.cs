using System;
using System.Collections.Generic;
using System.Linq;
using Stakeout.Simulation.Actions;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Objectives;

namespace Stakeout.Simulation.Scheduling.Decomposition;

public class VisitDecomposition : IDecompositionStrategy
{
    public List<ScheduleEntry> Decompose(SimTask task, SublocationGraph graph,
        TimeSpan startTime, TimeSpan endTime, Random rng)
    {
        var entrance = graph.FindByTag("entrance");
        if (entrance == null)
            return new List<ScheduleEntry>();

        // Determine the target sublocation
        Sublocation target = null;

        // Check task.ActionData for TargetSublocationId override
        if (task.ActionData != null &&
            task.ActionData.TryGetValue("TargetSublocationId", out var rawId) &&
            rawId is int sublocationId)
        {
            target = graph.Get(sublocationId);
        }

        // Fall back to tag search if no explicit target
        if (target == null)
        {
            target = graph.FindByTag("social") ?? graph.FindByTag("service_area");
        }

        var sublocationSequence = new List<Sublocation>();

        if (target == null || target.Id == entrance.Id)
        {
            // No specific target: stay at entrance
            sublocationSequence.Add(entrance);
        }
        else
        {
            // Enter: entrance → target (via path)
            var arrivalPath = graph.FindPath(entrance.Id, target.Id);
            sublocationSequence.AddRange(arrivalPath);

            // Exit: target → entrance (via path)
            var departurePath = graph.FindPath(target.Id, entrance.Id);
            sublocationSequence.AddRange(departurePath.Skip(1));
        }

        return AssignTimes(sublocationSequence, task.TargetAddressId, task.ActionType, startTime, endTime);
    }

    private List<ScheduleEntry> AssignTimes(List<Sublocation> sublocations, int? addressId,
        ActionType actionType, TimeSpan startTime, TimeSpan endTime)
    {
        if (sublocations.Count == 0)
            return new List<ScheduleEntry>();

        var totalDuration = endTime - startTime;
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
