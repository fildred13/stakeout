using System;
using System.Collections.Generic;
using Stakeout.Simulation.Actions.Primitives;
using Stakeout.Simulation.Entities;

namespace Stakeout.Simulation.Objectives;

public class SleepObjective : Objective
{
    public override int Priority => 80;
    public override ObjectiveSource Source => ObjectiveSource.Universal;

    public override List<PlannedAction> GetActionsForToday(Person person, SimulationState state, DateTime currentDate)
    {
        var sleepTime = person.PreferredSleepTime;
        var wakeTime = person.PreferredWakeTime;
        var duration = wakeTime - sleepTime;
        if (duration < TimeSpan.Zero)
            duration += TimeSpan.FromHours(24);

        return new List<PlannedAction>
        {
            new()
            {
                Action = new WaitAction(duration, "sleeping"),
                TargetAddressId = person.HomeAddressId,
                TimeWindowStart = sleepTime,
                TimeWindowEnd = sleepTime,
                Duration = duration,
                DisplayText = "sleeping",
                SourceObjective = this
            }
        };
    }
}
