using System;
using System.Collections.Generic;
using Stakeout.Simulation.Data;
using Stakeout.Simulation.Entities;

namespace Stakeout.Simulation.Businesses;

public class DiveBarBusinessTemplate : IBusinessTemplate
{
    public BusinessType Type => BusinessType.DiveBar;

    private static readonly DayOfWeek[] WorkDays =
        { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
          DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday };

    public List<BusinessHours> GenerateHours()
    {
        var hours = new List<BusinessHours>();
        foreach (DayOfWeek day in Enum.GetValues(typeof(DayOfWeek)))
        {
            if (day == DayOfWeek.Sunday)
            {
                hours.Add(new BusinessHours { Day = day, OpenTime = null, CloseTime = null });
            }
            else if (day == DayOfWeek.Friday || day == DayOfWeek.Saturday)
            {
                hours.Add(new BusinessHours
                {
                    Day = day,
                    OpenTime = new TimeSpan(12, 0, 0),
                    CloseTime = new TimeSpan(4, 0, 0)
                });
            }
            else
            {
                hours.Add(new BusinessHours
                {
                    Day = day,
                    OpenTime = new TimeSpan(12, 0, 0),
                    CloseTime = new TimeSpan(1, 0, 0)
                });
            }
        }
        return hours;
    }

    public List<Position> GeneratePositions(SimulationState state, Random random)
    {
        var positions = new List<Position>();

        var shiftStart = new TimeSpan(16, 0, 0);
        var shiftEnd = new TimeSpan(2, 0, 0);

        // 1-2 bartenders
        int bartenderCount = 1 + random.Next(2);
        for (int i = 0; i < bartenderCount; i++)
        {
            positions.Add(MakePosition(state, "bartender", shiftStart, shiftEnd, WorkDays));
        }

        // 0-1 manager
        if (random.Next(2) == 1)
        {
            positions.Add(MakePosition(state, "manager", shiftStart, shiftEnd, WorkDays));
        }

        return positions;
    }

    public string GenerateName(Random random)
    {
        return BusinessNameData.GenerateName(BusinessType.DiveBar, random);
    }

    private static Position MakePosition(SimulationState state, string role, TimeSpan start, TimeSpan end, DayOfWeek[] days)
    {
        return new Position
        {
            Id = state.GenerateEntityId(),
            Role = role,
            ShiftStart = start,
            ShiftEnd = end,
            WorkDays = days
        };
    }
}
