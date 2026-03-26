using System;
using System.Collections.Generic;
using System.Linq;
using Stakeout.Simulation.Actions;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Objectives;

namespace Stakeout.Simulation.Scheduling.Decomposition;

public class PatronizeDecomposition : IDecompositionStrategy
{
    public List<ScheduleEntry> Decompose(SimTask task, SublocationGraph graph,
        TimeSpan startTime, TimeSpan endTime, Random rng)
    {
        var entrance = graph.FindByTag("entrance");
        if (entrance == null)
            return new List<ScheduleEntry>();

        var serviceArea = graph.FindByTag("service_area") ?? graph.FindByTag("social");

        var sublocationSequence = new List<Sublocation>();

        if (serviceArea == null)
        {
            // Graceful fallback: stay at entrance
            sublocationSequence.Add(entrance);
        }
        else
        {
            // Enter: entrance → service/social area (via path)
            var arrivalPath = graph.FindPath(entrance.Id, serviceArea.Id);
            sublocationSequence.AddRange(arrivalPath);

            // 30% chance to visit restroom
            if (rng.NextDouble() < 0.3)
            {
                var restroom = graph.FindByTag("restroom");
                if (restroom != null)
                {
                    var toRestroom = graph.FindPath(serviceArea.Id, restroom.Id);
                    sublocationSequence.AddRange(toRestroom.Skip(1));
                    var backToService = graph.FindPath(restroom.Id, serviceArea.Id);
                    sublocationSequence.AddRange(backToService.Skip(1));
                }
            }

            // Exit: service/social area → entrance (via path)
            var departurePath = graph.FindPath(serviceArea.Id, entrance.Id);
            sublocationSequence.AddRange(departurePath.Skip(1));
        }

        return AssignTimes(sublocationSequence, task.TargetAddressId, startTime, endTime);
    }

    private List<ScheduleEntry> AssignTimes(List<Sublocation> sublocations, int? addressId,
        TimeSpan startTime, TimeSpan endTime)
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
                Action = ActionType.Idle,
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
