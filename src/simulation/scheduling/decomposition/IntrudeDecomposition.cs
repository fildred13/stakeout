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
        var covertResult = graph.FindEntryPoint("covert_entry") ?? graph.FindEntryPoint("entrance");
        var entryPoint = covertResult?.target;
        var entryConnId = covertResult?.conn?.Id;

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

        var steps = new List<(Sublocation sub, int? viaConnId)>();

        if (targetRoom == null || targetRoom.Id == entryPoint.Id)
        {
            // No distinct target: just stay at entry point
            steps.Add((entryPoint, entryConnId));
        }
        else
        {
            // entry → target room
            var entryPath = graph.FindPath(entryPoint.Id, targetRoom.Id);
            foreach (var step in entryPath)
                steps.Add((step.Location, step.Via?.Id));

            // target room → entry (exit)
            var exitPath = graph.FindPath(targetRoom.Id, entryPoint.Id);
            foreach (var step in exitPath.Skip(1))
                steps.Add((step.Location, step.Via?.Id));
        }

        return AssignTimes(steps, task.TargetAddressId, task.ActionType, startTime, endTime);
    }

    private List<ScheduleEntry> AssignTimes(List<(Sublocation sub, int? viaConnId)> steps,
        int? addressId, ActionType actionType, TimeSpan startTime, TimeSpan endTime)
    {
        if (steps.Count == 0) return new List<ScheduleEntry>();

        var totalDuration = endTime - startTime;
        var slotDuration = TimeSpan.FromTicks(totalDuration.Ticks / steps.Count);

        var entries = new List<ScheduleEntry>();
        var current = startTime;

        for (int i = 0; i < steps.Count; i++)
        {
            var (sub, viaConnId) = steps[i];
            var slotEnd = (i == steps.Count - 1) ? endTime : current + slotDuration;
            entries.Add(new ScheduleEntry
            {
                Action = actionType,
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
