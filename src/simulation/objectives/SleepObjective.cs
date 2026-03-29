using System;
using System.Collections.Generic;
using Stakeout.Simulation.Actions.Primitives;
using Stakeout.Simulation.Entities;

namespace Stakeout.Simulation.Objectives;

public class SleepObjective : Objective
{
    public override int Priority => 80;
    public override ObjectiveSource Source => ObjectiveSource.Universal;

    public override List<PlannedAction> GetActions(
        Person person, SimulationState state,
        DateTime planStart, DateTime planEnd)
    {
        var sleepTimeOfDay = person.PreferredSleepTime;
        var wakeTimeOfDay = person.PreferredWakeTime;

        var sleepDuration = wakeTimeOfDay - sleepTimeOfDay;
        if (sleepDuration < TimeSpan.Zero)
            sleepDuration += TimeSpan.FromHours(24);

        var actions = new List<PlannedAction>();

        // Check if planStart is mid-sleep
        if (IsInSleepWindow(planStart.TimeOfDay, sleepTimeOfDay, wakeTimeOfDay))
        {
            var wakeUpToday = planStart.Date + wakeTimeOfDay;
            if (wakeUpToday <= planStart)
                wakeUpToday = wakeUpToday.AddDays(1);
            var remaining = wakeUpToday - planStart;

            actions.Add(MakeSleepAction(person, planStart, remaining));
        }

        // Find next preferred sleep time within window
        var nextSleep = planStart.Date + sleepTimeOfDay;
        // Advance until nextSleep is after planStart (and after any mid-sleep wake)
        while (nextSleep <= planStart)
            nextSleep = nextSleep.AddDays(1);
        // If mid-sleep, also skip past the remaining sleep window
        if (actions.Count > 0 && nextSleep < actions[0].TimeWindowStart + actions[0].Duration)
            nextSleep = nextSleep.AddDays(1);

        if (nextSleep < planEnd)
        {
            actions.Add(MakeSleepAction(person, nextSleep, sleepDuration));
        }

        return actions;
    }

    private static bool IsInSleepWindow(TimeSpan timeOfDay, TimeSpan sleepStart, TimeSpan wakeEnd)
    {
        if (sleepStart < wakeEnd)
        {
            // Sleep window doesn't cross midnight (e.g., 01:00-09:00)
            return timeOfDay >= sleepStart && timeOfDay < wakeEnd;
        }
        else
        {
            // Wraps midnight: e.g., sleep 22:00, wake 06:00
            return timeOfDay >= sleepStart || timeOfDay < wakeEnd;
        }
    }

    private PlannedAction MakeSleepAction(Person person, DateTime start, TimeSpan duration)
    {
        return new PlannedAction
        {
            Action = new WaitAction(duration, "sleeping"),
            TargetAddressId = person.HomeAddressId,
            TimeWindowStart = start,
            TimeWindowEnd = start + duration,
            Duration = duration,
            DisplayText = "sleeping",
            SourceObjective = this
        };
    }
}
