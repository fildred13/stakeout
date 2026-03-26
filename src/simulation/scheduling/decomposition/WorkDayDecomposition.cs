using System;
using System.Collections.Generic;
using System.Linq;
using Stakeout.Simulation.Actions;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Objectives;

namespace Stakeout.Simulation.Scheduling.Decomposition;

public class WorkDayDecomposition : IDecompositionStrategy
{
    public List<ScheduleEntry> Decompose(SimTask task, SublocationGraph graph,
        TimeSpan startTime, TimeSpan endTime, Random rng)
    {
        var entrance = graph.FindByTag("entrance");
        var workArea = graph.FindByTag("work_area");

        // Build the list of sublocations to visit in order
        var sublocationSequence = new List<Sublocation>();

        if (entrance == null)
            return new List<ScheduleEntry>();

        // No work area: just stay at entrance the whole day
        if (workArea == null)
        {
            sublocationSequence.Add(entrance);
        }
        else
        {
            // Arrival: entrance → work area (via path)
            var arrivalPath = graph.FindPath(entrance.Id, workArea.Id);
            sublocationSequence.AddRange(arrivalPath);

            // Periodic breaks during the workday
            sublocationSequence.AddRange(BuildBreakSequence(workArea, graph, rng));

            // Departure: work area → entrance (via path)
            var departurePath = graph.FindPath(workArea.Id, entrance.Id);
            // Skip first element (work area) to avoid duplication
            sublocationSequence.AddRange(departurePath.Skip(1));
        }

        return AssignTimes(sublocationSequence, task.TargetAddressId, startTime, endTime);
    }

    private List<Sublocation> BuildBreakSequence(Sublocation workArea, SublocationGraph graph, Random rng)
    {
        var sequence = new List<Sublocation>();

        // We plan breaks at roughly 1-3 hour intervals; we'll create 1-3 break opportunities
        int breakCount = rng.Next(1, 4);

        for (int i = 0; i < breakCount; i++)
        {
            // Stay at work area between breaks
            sequence.Add(workArea);

            // 50% chance to visit food area
            if (rng.NextDouble() < 0.5)
            {
                var food = graph.FindByTag("food");
                if (food != null)
                {
                    var toFood = graph.FindPath(workArea.Id, food.Id);
                    sequence.AddRange(toFood.Skip(1)); // skip work area (already added)
                    var backToWork = graph.FindPath(food.Id, workArea.Id);
                    sequence.AddRange(backToWork.Skip(1)); // skip food
                }
            }

            // 70% chance to visit restroom
            if (rng.NextDouble() < 0.7)
            {
                var restroom = graph.FindByTag("restroom");
                if (restroom != null)
                {
                    var toRestroom = graph.FindPath(workArea.Id, restroom.Id);
                    sequence.AddRange(toRestroom.Skip(1)); // skip work area
                    var backToWork = graph.FindPath(restroom.Id, workArea.Id);
                    sequence.AddRange(backToWork.Skip(1)); // skip restroom
                }
            }
        }

        // Final work stint after last break
        sequence.Add(workArea);

        return sequence;
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
                Action = ActionType.Work,
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
