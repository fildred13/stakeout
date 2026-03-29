using System;
using System.Collections.Generic;
using System.Linq;
using Stakeout.Simulation.Actions;
using Stakeout.Simulation.Actions.Primitives;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Objectives;

namespace Stakeout.Simulation.Brain;

public static class NpcBrain
{
    public static DayPlan PlanDay(Person person, SimulationState state, DateTime currentTime, MapConfig mapConfig = null)
    {
        var plan = new DayPlan();
        var wakeTime = person.PreferredWakeTime;
        var sleepTime = person.PreferredSleepTime;

        // Separate sleep from other objectives — sleep is appended at the end, not slot-found
        var objectives = person.Objectives
            .Where(o => o.Status == ObjectiveStatus.Active)
            .OrderByDescending(o => o.Priority)
            .ToList();

        PlannedAction sleepAction = null;
        var scheduled = new List<(TimeSpan start, TimeSpan end, PlannedAction action)>();

        foreach (var objective in objectives)
        {
            var actions = objective.GetActionsForToday(person, state, currentTime.Date);
            foreach (var action in actions)
            {
                // Sleep is special — it goes at the end of the day, not in a waking-hours slot
                if (objective is SleepObjective)
                {
                    sleepAction = action;
                    continue;
                }

                var travelHours = EstimateTravelTime(person, action.TargetAddressId, state, mapConfig);
                var totalDuration = action.Duration + TimeSpan.FromHours(travelHours);

                var slot = FindSlot(scheduled, action.TimeWindowStart, action.TimeWindowEnd,
                    totalDuration, wakeTime, sleepTime);
                if (slot.HasValue)
                {
                    scheduled.Add((slot.Value, slot.Value + totalDuration, action));
                }
            }
        }

        // Sort scheduled actions by start time
        scheduled.Sort((a, b) => a.start.CompareTo(b.start));

        // Build plan entries, filling gaps with IdleAtHome
        var currentSlotTime = wakeTime;
        foreach (var (start, end, action) in scheduled)
        {
            if (start > currentSlotTime)
            {
                AddIdleEntry(plan, currentSlotTime, start, person.HomeAddressId);
            }
            plan.Entries.Add(new DayPlanEntry
            {
                StartTime = start,
                EndTime = end,
                PlannedAction = action
            });
            currentSlotTime = end;
        }

        // Fill remaining waking time with idle
        if (currentSlotTime < sleepTime)
        {
            AddIdleEntry(plan, currentSlotTime, sleepTime, person.HomeAddressId);
        }

        // Append sleep at the end of the day
        if (sleepAction != null)
        {
            plan.Entries.Add(new DayPlanEntry
            {
                StartTime = sleepTime,
                EndTime = sleepTime + sleepAction.Duration,
                PlannedAction = sleepAction
            });
        }

        return plan;
    }

    private static TimeSpan? FindSlot(
        List<(TimeSpan start, TimeSpan end, PlannedAction action)> scheduled,
        TimeSpan windowStart, TimeSpan windowEnd,
        TimeSpan totalDuration,
        TimeSpan wakeTime, TimeSpan sleepTime)
    {
        // Clamp window to waking hours
        var effectiveStart = windowStart < wakeTime ? wakeTime : windowStart;
        var effectiveEnd = windowEnd > sleepTime ? sleepTime : windowEnd;

        if (effectiveEnd - effectiveStart < totalDuration)
            return null;

        // Try to fit starting from effectiveStart, skipping over existing slots
        var candidate = effectiveStart;
        foreach (var (start, end, _) in scheduled.OrderBy(s => s.start))
        {
            if (candidate + totalDuration <= start)
                return candidate;

            if (candidate < end)
                candidate = end;
        }

        if (candidate + totalDuration <= effectiveEnd)
            return candidate;

        return null;
    }

    private static void AddIdleEntry(DayPlan plan, TimeSpan start, TimeSpan end, int homeAddressId)
    {
        var duration = end - start;
        if (duration <= TimeSpan.Zero) return;

        plan.Entries.Add(new DayPlanEntry
        {
            StartTime = start,
            EndTime = end,
            PlannedAction = new PlannedAction
            {
                Action = new WaitAction(duration, "relaxing at home"),
                TargetAddressId = homeAddressId,
                TimeWindowStart = start,
                TimeWindowEnd = end,
                Duration = duration,
                DisplayText = "relaxing at home"
            }
        });
    }

    private static float EstimateTravelTime(Person person, int targetAddressId, SimulationState state, MapConfig mapConfig)
    {
        if (person.CurrentAddressId == targetAddressId) return 0f;
        if (!state.Addresses.TryGetValue(targetAddressId, out var target)) return 0f;
        if (mapConfig != null)
            return mapConfig.ComputeTravelTimeHours(person.CurrentPosition, target.Position);
        // Fallback: use same formula as MapConfig defaults
        var diagonal = new Godot.Vector2(4800, 4800).Length();
        return person.CurrentPosition.DistanceTo(target.Position) / diagonal * 1.0f;
    }
}
