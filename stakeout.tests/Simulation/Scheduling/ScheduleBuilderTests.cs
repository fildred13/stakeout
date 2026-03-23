using System;
using System.Linq;
using Godot;
using Stakeout.Simulation;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Scheduling;
using Xunit;

namespace Stakeout.Tests.Simulation.Scheduling;

public class ScheduleBuilderTests
{
    private static MapConfig DefaultConfig => new();

    private static (Address home, Address work) CreateAddresses(float distance = 500f)
    {
        var home = new Address { Id = 1, Position = new Vector2(100, 100), Type = AddressType.SuburbanHome };
        var work = new Address { Id = 2, Position = new Vector2(100 + distance, 100), Type = AddressType.Office };
        return (home, work);
    }

    [Fact]
    public void Build_OfficeWorker_HasCorrectActivitySequence()
    {
        var (home, work) = CreateAddresses();
        var job = new Job { ShiftStart = new TimeSpan(9, 0, 0), ShiftEnd = new TimeSpan(17, 0, 0), WorkAddressId = work.Id };
        var commuteHours = DefaultConfig.ComputeTravelTimeHours(home.Position, work.Position);
        var (sleepTime, wakeTime) = SleepScheduleCalculator.Compute(job, commuteHours);

        var goalSet = GoalSetBuilder.Build(job, sleepTime, wakeTime);
        var schedule = ScheduleBuilder.Build(goalSet, home, work, DefaultConfig);

        var activities = schedule.Entries.Select(e => e.Activity).ToList();
        Assert.Contains(ActivityType.Sleeping, activities);
        Assert.Contains(ActivityType.AtHome, activities);
        Assert.Contains(ActivityType.Working, activities);
        Assert.Contains(ActivityType.TravellingByCar, activities);
    }

    [Fact]
    public void Build_ScheduleCovers24Hours()
    {
        var (home, work) = CreateAddresses();
        var job = new Job { ShiftStart = new TimeSpan(9, 0, 0), ShiftEnd = new TimeSpan(17, 0, 0), WorkAddressId = work.Id };
        var commuteHours = DefaultConfig.ComputeTravelTimeHours(home.Position, work.Position);
        var (sleepTime, wakeTime) = SleepScheduleCalculator.Compute(job, commuteHours);

        var goalSet = GoalSetBuilder.Build(job, sleepTime, wakeTime);
        var schedule = ScheduleBuilder.Build(goalSet, home, work, DefaultConfig);

        double totalHours = 0;
        foreach (var entry in schedule.Entries)
        {
            var duration = (entry.EndTime - entry.StartTime).TotalHours;
            if (duration < 0) duration += 24;
            totalHours += duration;
        }
        Assert.Equal(24.0, totalHours, precision: 1);
    }

    [Fact]
    public void Build_TravelEntriesHaveAddressIds()
    {
        var (home, work) = CreateAddresses();
        var job = new Job { ShiftStart = new TimeSpan(9, 0, 0), ShiftEnd = new TimeSpan(17, 0, 0), WorkAddressId = work.Id };
        var commuteHours = DefaultConfig.ComputeTravelTimeHours(home.Position, work.Position);
        var (sleepTime, wakeTime) = SleepScheduleCalculator.Compute(job, commuteHours);

        var goalSet = GoalSetBuilder.Build(job, sleepTime, wakeTime);
        var schedule = ScheduleBuilder.Build(goalSet, home, work, DefaultConfig);

        var travelEntries = schedule.Entries.Where(e => e.Activity == ActivityType.TravellingByCar).ToList();
        Assert.True(travelEntries.Count >= 2);
        foreach (var travel in travelEntries)
        {
            Assert.NotNull(travel.FromAddressId);
            Assert.NotNull(travel.TargetAddressId);
        }
    }

    [Fact]
    public void GetEntryAtTime_ReturnsCorrectEntry()
    {
        var (home, work) = CreateAddresses();
        var job = new Job { ShiftStart = new TimeSpan(9, 0, 0), ShiftEnd = new TimeSpan(17, 0, 0), WorkAddressId = work.Id };
        var commuteHours = DefaultConfig.ComputeTravelTimeHours(home.Position, work.Position);
        var (sleepTime, wakeTime) = SleepScheduleCalculator.Compute(job, commuteHours);

        var goalSet = GoalSetBuilder.Build(job, sleepTime, wakeTime);
        var schedule = ScheduleBuilder.Build(goalSet, home, work, DefaultConfig);

        var midday = schedule.GetEntryAtTime(new TimeSpan(12, 0, 0));
        Assert.Equal(ActivityType.Working, midday.Activity);

        var night = schedule.GetEntryAtTime(new TimeSpan(3, 0, 0));
        Assert.Equal(ActivityType.Sleeping, night.Activity);
    }
}
