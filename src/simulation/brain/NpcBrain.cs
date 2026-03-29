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
    public static DayPlan PlanDay(Person person, SimulationState state,
        DateTime currentTime, MapConfig mapConfig = null)
    {
        var plan = new DayPlan();
        var planStart = currentTime;
        var planEnd = currentTime.AddHours(24);

        // Collect all objectives sorted by priority (descending)
        var objectives = person.Objectives
            .Where(o => o.Status == ObjectiveStatus.Active)
            .OrderByDescending(o => o.Priority)
            .ToList();

        // Schedule greedily by priority
        var scheduled = new List<(DateTime start, DateTime end, PlannedAction action)>();

        foreach (var objective in objectives)
        {
            var actions = objective.GetActions(person, state, planStart, planEnd);
            foreach (var action in actions)
            {
                var travelHours = EstimateTravelTime(
                    person, action.TargetAddressId, state, mapConfig);
                var totalDuration = action.Duration + TimeSpan.FromHours(travelHours);

                var slot = FindSlot(scheduled, action.TimeWindowStart,
                    action.TimeWindowEnd, totalDuration, planStart, planEnd);
                if (slot.HasValue)
                {
                    scheduled.Add((slot.Value, slot.Value + totalDuration, action));
                }
            }
        }

        // Sort by start time
        scheduled.Sort((a, b) => a.start.CompareTo(b.start));

        // Build plan entries, filling gaps with IdleAtHome
        var currentSlotTime = planStart;
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

        // Fill remaining time with idle
        if (currentSlotTime < planEnd)
        {
            AddIdleEntry(plan, currentSlotTime, planEnd, person.HomeAddressId);
        }

        return plan;
    }

    private static DateTime? FindSlot(
        List<(DateTime start, DateTime end, PlannedAction action)> scheduled,
        DateTime windowStart, DateTime windowEnd,
        TimeSpan totalDuration,
        DateTime planStart, DateTime planEnd)
    {
        // Clamp window to plan bounds
        var effectiveStart = windowStart < planStart ? planStart : windowStart;
        var effectiveEnd = windowEnd > planEnd ? planEnd : windowEnd;

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

    private static void AddIdleEntry(DayPlan plan, DateTime start, DateTime end,
        int homeAddressId)
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

    private static float EstimateTravelTime(Person person, int targetAddressId,
        SimulationState state, MapConfig mapConfig)
    {
        if (person.CurrentAddressId == targetAddressId) return 0f;
        if (!state.Addresses.TryGetValue(targetAddressId, out var target)) return 0f;
        if (mapConfig != null)
            return mapConfig.ComputeTravelTimeHours(person.CurrentPosition, target.Position);
        var diagonal = new Godot.Vector2(4800, 4800).Length();
        return person.CurrentPosition.DistanceTo(target.Position) / diagonal * 1.0f;
    }
}
