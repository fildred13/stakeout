using System;
using System.Collections.Generic;
using System.Linq;
using Stakeout.Simulation.Actions;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Objectives;

namespace Stakeout.Simulation.Scheduling.Decomposition;

public class StaffShiftDecomposition : IDecompositionStrategy
{
    public List<ScheduleEntry> Decompose(SimTask task, SublocationGraph graph,
        TimeSpan startTime, TimeSpan endTime, Random rng)
    {
        // Use staff_entry if available, fall back to entrance
        var staffEntry = graph.FindByTag("staff_entry") ?? graph.FindByTag("entrance");
        var workArea = graph.FindByTag("work_area");

        var sublocationSequence = new List<Sublocation>();

        if (staffEntry == null)
            return new List<ScheduleEntry>();

        // No work area: stay at staff entry the whole shift
        if (workArea == null)
        {
            sublocationSequence.Add(staffEntry);
        }
        else
        {
            // Arrival: staff entry → work area (via path)
            var arrivalPath = graph.FindPath(staffEntry.Id, workArea.Id);
            sublocationSequence.AddRange(arrivalPath);

            // Periodic breaks (same logic as WorkDay: 50% food, 70% restroom, 1-3 breaks)
            sublocationSequence.AddRange(BuildBreakSequence(workArea, graph, rng));

            // Departure: work area → staff entry (via path)
            var departurePath = graph.FindPath(workArea.Id, staffEntry.Id);
            sublocationSequence.AddRange(departurePath.Skip(1));
        }

        return AssignTimes(sublocationSequence, task.TargetAddressId, startTime, endTime);
    }

    private List<Sublocation> BuildBreakSequence(Sublocation workArea, SublocationGraph graph, Random rng)
    {
        var sequence = new List<Sublocation>();
        int breakCount = rng.Next(1, 4);

        for (int i = 0; i < breakCount; i++)
        {
            sequence.Add(workArea);

            // 50% chance to visit food area
            if (rng.NextDouble() < 0.5)
            {
                var food = graph.FindByTag("food");
                if (food != null)
                {
                    var toFood = graph.FindPath(workArea.Id, food.Id);
                    sequence.AddRange(toFood.Skip(1));
                    var backToWork = graph.FindPath(food.Id, workArea.Id);
                    sequence.AddRange(backToWork.Skip(1));
                }
            }

            // 70% chance to visit restroom
            if (rng.NextDouble() < 0.7)
            {
                var restroom = graph.FindByTag("restroom");
                if (restroom != null)
                {
                    var toRestroom = graph.FindPath(workArea.Id, restroom.Id);
                    sequence.AddRange(toRestroom.Skip(1));
                    var backToWork = graph.FindPath(restroom.Id, workArea.Id);
                    sequence.AddRange(backToWork.Skip(1));
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
