using System;
using System.Collections.Generic;
using Stakeout.Simulation.Data;
using Stakeout.Simulation.Entities;

namespace Stakeout.Simulation.Businesses;

public class OfficeBusinessTemplate : IBusinessTemplate
{
    public BusinessType Type => BusinessType.Office;

    private static readonly DayOfWeek[] Weekdays =
        { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
          DayOfWeek.Thursday, DayOfWeek.Friday };

    public List<BusinessHours> GenerateHours()
    {
        var hours = new List<BusinessHours>();
        foreach (DayOfWeek day in Enum.GetValues(typeof(DayOfWeek)))
        {
            if (day == DayOfWeek.Saturday || day == DayOfWeek.Sunday)
            {
                hours.Add(new BusinessHours { Day = day, OpenTime = null, CloseTime = null });
            }
            else
            {
                hours.Add(new BusinessHours
                {
                    Day = day,
                    OpenTime = new TimeSpan(7, 0, 0),
                    CloseTime = new TimeSpan(19, 0, 0)
                });
            }
        }
        return hours;
    }

    public List<Position> GeneratePositions(SimulationState state, Random random)
    {
        var positions = new List<Position>();

        var shiftStart = new TimeSpan(9, 0, 0);
        var shiftEnd = new TimeSpan(17, 0, 0);

        // 5-10 office workers
        int workerCount = 5 + random.Next(6);
        for (int i = 0; i < workerCount; i++)
        {
            positions.Add(MakePosition(state, "office_worker", shiftStart, shiftEnd, Weekdays));
        }

        // 1-2 managers
        int managerCount = 1 + random.Next(2);
        for (int i = 0; i < managerCount; i++)
        {
            positions.Add(MakePosition(state, "manager", shiftStart, shiftEnd, Weekdays));
        }

        // 1 CEO
        positions.Add(MakePosition(state, "ceo", shiftStart, shiftEnd, Weekdays));

        return positions;
    }

    public string GenerateName(Random random)
    {
        return BusinessNameData.GenerateName(BusinessType.Office, random);
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
