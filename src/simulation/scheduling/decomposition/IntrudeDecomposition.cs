using System;
using System.Collections.Generic;
using System.Linq;
using Stakeout.Simulation.Actions;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Objectives;

namespace Stakeout.Simulation.Scheduling.Decomposition;

public class IntrudeDecomposition : IDecompositionStrategy
{
    public List<ScheduleEntry> Decompose(SimTask task, SublocationGraph graph,
        TimeSpan startTime, TimeSpan endTime, Random rng)
    {
        // Use covert_entry if available; fall back to entrance
        var entryPoint = graph.FindByTag("covert_entry") ?? graph.FindByTag("entrance");

        if (entryPoint == null)
            return new List<ScheduleEntry>();

        // Determine target room: use task's TargetSublocationId if set, else first bedroom
        Sublocation targetRoom = null;
        if (task.TargetSublocationId.HasValue)
        {
            targetRoom = graph.Get(task.TargetSublocationId.Value);
        }
        if (targetRoom == null)
        {
            targetRoom = graph.FindByTag("bedroom");
        }

        var sublocationSequence = new List<Sublocation>();

        if (targetRoom == null || targetRoom.Id == entryPoint.Id)
        {
            // No distinct target: just stay at entry point
            sublocationSequence.Add(entryPoint);
        }
        else
        {
            // entry → target room
            var entryPath = graph.FindPath(entryPoint.Id, targetRoom.Id);
            sublocationSequence.AddRange(entryPath.Select(s => s.Location));

            // target room → entry (exit)
            var exitPath = graph.FindPath(targetRoom.Id, entryPoint.Id);
            sublocationSequence.AddRange(exitPath.Skip(1).Select(s => s.Location));
        }

        return AssignTimes(sublocationSequence, task.TargetAddressId, task.ActionType, startTime, endTime);
    }

    private List<ScheduleEntry> AssignTimes(List<Sublocation> sublocations, int? addressId,
        ActionType actionType, TimeSpan startTime, TimeSpan endTime)
    {
        if (sublocations.Count == 0) return new List<ScheduleEntry>();

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
