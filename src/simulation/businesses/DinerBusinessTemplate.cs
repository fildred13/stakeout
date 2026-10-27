using System;
using System.Collections.Generic;
using System.Linq;
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
                CloseTime = TimeSpan.Zero
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

        // Staff work six days a week; days off are staggered so every shift always has someone on.
        for (int shiftIndex = 0; shiftIndex < shifts.Length; shiftIndex++)
        {
            var shift = shifts[shiftIndex];
            int slot = 0;
            DayOfWeek[] Days()
            {
                int off = (slot++ * 3 + shiftIndex * 2) % 7;
                return AllDays.Where((_, i) => i != off).ToArray();
            }

            positions.Add(MakePosition(state, "cook", shift.Start, shift.End, Days()));
            int waiterCount = 1 + random.Next(2);
            for (int i = 0; i < waiterCount; i++)
                positions.Add(MakePosition(state, "waiter", shift.Start, shift.End, Days()));
            if (random.Next(2) == 1)
                positions.Add(MakePosition(state, "manager", shift.Start, shift.End, Days()));
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
