using System;
using System.Collections.Generic;
using System.Linq;
using Stakeout.Simulation.Actions.Primitives;
using Stakeout.Simulation.Entities;

namespace Stakeout.Simulation.Objectives;

public class WorkShiftObjective : Objective
{
    private readonly int _businessId;
    private readonly int _positionId;

    public override int Priority => 60;
    public override ObjectiveSource Source => ObjectiveSource.Job;

    public WorkShiftObjective(int businessId, int positionId)
    {
        _businessId = businessId;
        _positionId = positionId;
    }

    public override List<PlannedAction> GetActions(
        Person person, SimulationState state,
        DateTime planStart, DateTime planEnd)
    {
        var business = state.Businesses[_businessId];
        var position = business.Positions.First(p => p.Id == _positionId);

        var actions = new List<PlannedAction>();

        var day = planStart.Date;
        while (day < planEnd)
        {
            if (position.WorkDays.Contains(day.DayOfWeek))
            {
                var shiftStart = day + position.ShiftStart;
                var shiftEnd = day + position.ShiftEnd;

                // Handle overnight shifts (end < start means crosses midnight)
                if (position.ShiftEnd <= position.ShiftStart)
                    shiftEnd = shiftEnd.AddDays(1);

                if (shiftEnd > planStart && shiftStart < planEnd)
                {
                    var effectiveStart = shiftStart < planStart ? planStart : shiftStart;
                    var duration = shiftEnd - effectiveStart;
                    var displayText = $"working as {position.Role}";

                    // When replanning mid-shift (shiftStart < planStart), the person needs to
                    // travel to work and won't arrive exactly at planStart. Extend the window end
                    // by a commute buffer so NpcBrain can fit travel + remaining work in the slot.
                    // Without this, NpcBrain's (duration + travelTime) > (shiftEnd - planStart)
                    // causes work to be silently dropped even though the shift isn't over.
                    var windowEnd = shiftStart < planStart
                        ? shiftEnd + TimeSpan.FromHours(1)
                        : shiftEnd;

                    actions.Add(new PlannedAction
                    {
                        Action = new WaitAction(duration, displayText),
                        TargetAddressId = business.AddressId,
                        TimeWindowStart = effectiveStart,
                        TimeWindowEnd = windowEnd,
                        Duration = duration,
                        DisplayText = displayText,
                        SourceObjective = this
                    });
                }
            }

            day = day.AddDays(1);
        }

        return actions;
    }
}
