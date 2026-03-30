using System;
using System.Collections.Generic;
using Stakeout.Simulation.Data;
using Stakeout.Simulation.Entities;

namespace Stakeout.Simulation.Businesses;

public class DinerBusinessTemplate : IBusinessTemplate
{
    public BusinessType Type => BusinessType.Diner;

    private static readonly DayOfWeek[] AllDays =
        { DayOfWeek.Sunday, DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
          DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday };

    public List<BusinessHours> GenerateHours()
    {
        var hours = new List<BusinessHours>();
        foreach (var day in AllDays)
        {
            hours.Add(new BusinessHours
            {
                Day = day,
                OpenTime = TimeSpan.Zero,
                CloseTime = new TimeSpan(23, 59, 59)
            });
        }
        return hours;
    }

    public List<Position> GeneratePositions(SimulationState state, Random random)
    {
        var positions = new List<Position>();

        // 3 shifts: morning (5-13), afternoon (13-21), night (21-5)
        var shifts = new[]
        {
            (Start: new TimeSpan(5, 0, 0), End: new TimeSpan(13, 0, 0)),
            (Start: new TimeSpan(13, 0, 0), End: new TimeSpan(21, 0, 0)),
            (Start: new TimeSpan(21, 0, 0), End: new TimeSpan(5, 0, 0)),
        };

        foreach (var shift in shifts)
        {
            // 1 cook per shift
            positions.Add(MakePosition(state, "cook", shift.Start, shift.End, AllDays));

            // 1-2 waiters per shift
            int waiterCount = 1 + random.Next(2);
            for (int i = 0; i < waiterCount; i++)
            {
                positions.Add(MakePosition(state, "waiter", shift.Start, shift.End, AllDays));
            }

            // 0-1 manager per shift
            if (random.Next(2) == 1)
            {
                positions.Add(MakePosition(state, "manager", shift.Start, shift.End, AllDays));
            }
        }

        return positions;
    }

    public string GenerateName(Random random)
    {
        return BusinessNameData.GenerateName(BusinessType.Diner, random);
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
