using System;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Scheduling;
using Xunit;

namespace Stakeout.Tests.Simulation.Scheduling;

public class SleepScheduleCalculatorTests
{
    [Fact]
    public void Compute_OfficeWorker_ReturnsDefaultSleepSchedule()
    {
        var position = new Position
        {
            ShiftStart = new TimeSpan(9, 0, 0),
            ShiftEnd = new TimeSpan(17, 0, 0)
        };

        var (sleepTime, wakeTime) = SleepScheduleCalculator.Compute(position, commuteHours: 0.5f);

        Assert.Equal(new TimeSpan(22, 0, 0), sleepTime);
        Assert.Equal(new TimeSpan(6, 0, 0), wakeTime);
    }

    [Fact]
    public void Compute_Bartender_ShiftsSleepToAfterShift()
    {
        var position = new Position
        {
            ShiftStart = new TimeSpan(16, 0, 0),
            ShiftEnd = new TimeSpan(2, 0, 0)
        };

        var (sleepTime, wakeTime) = SleepScheduleCalculator.Compute(position, commuteHours: 0.5f);

        // Work block ends at 02:00 + 30min commute = 02:30
        // Sleep should start at or after 02:30, wake 8hrs later
        Assert.True(sleepTime >= new TimeSpan(2, 30, 0),
            $"Sleep start {sleepTime} should be >= 02:30 (after shift end + commute)");
        Assert.Equal(sleepTime.Add(new TimeSpan(8, 0, 0)).TotalHours % 24,
            wakeTime.TotalHours % 24, precision: 2);
    }

    [Fact]
    public void Compute_EarlyDinerShift_PushesSleepEarlier()
    {
        var position = new Position
        {
            ShiftStart = new TimeSpan(5, 0, 0),
            ShiftEnd = new TimeSpan(17, 0, 0)
        };

        var (sleepTime, wakeTime) = SleepScheduleCalculator.Compute(position, commuteHours: 0.33f);

        // Wake must be before shift start minus commute (~04:40)
        Assert.True(wakeTime.TotalHours < 5.0,
            $"Wake time {wakeTime} should be before 05:00 to allow commute");
        // Sleep duration should be 8 hours
        var duration = (wakeTime - sleepTime).TotalHours;
        if (duration < 0) duration += 24;
        Assert.Equal(8.0, duration, precision: 1);
    }

    [Fact]
    public void Compute_AlwaysReturns8HourSleepDuration()
    {
        var positions = new[]
        {
            new Position { ShiftStart = new TimeSpan(9, 0, 0), ShiftEnd = new TimeSpan(17, 0, 0) },
            new Position { ShiftStart = new TimeSpan(16, 0, 0), ShiftEnd = new TimeSpan(2, 0, 0) },
            new Position { ShiftStart = new TimeSpan(5, 0, 0), ShiftEnd = new TimeSpan(17, 0, 0) },
            new Position { ShiftStart = new TimeSpan(21, 0, 0), ShiftEnd = new TimeSpan(9, 0, 0) },
        };

        foreach (var position in positions)
        {
            var (sleepTime, wakeTime) = SleepScheduleCalculator.Compute(position, commuteHours: 0.5f);
            var duration = (wakeTime - sleepTime).TotalHours;
            if (duration < 0) duration += 24;
            Assert.Equal(8.0, duration, precision: 1);
        }
    }
}
