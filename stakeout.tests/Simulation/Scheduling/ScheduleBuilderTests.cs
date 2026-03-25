using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Stakeout.Simulation;
using Stakeout.Simulation.Actions;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Objectives;
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

        var activities = schedule.Entries.Select(e => e.Action).ToList();
        Assert.Contains(ActionType.Sleep, activities);
        Assert.Contains(ActionType.Idle, activities);
        Assert.Contains(ActionType.Work, activities);
        Assert.Contains(ActionType.TravelByCar, activities);
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

        var travelEntries = schedule.Entries.Where(e => e.Action == ActionType.TravelByCar).ToList();
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
        Assert.Equal(ActionType.Work, midday.Action);

        var night = schedule.GetEntryAtTime(new TimeSpan(3, 0, 0));
        Assert.Equal(ActionType.Sleep, night.Action);
    }

    // --- BuildFromTasks tests ---

    private static List<SimTask> CreateOfficeWorkerTasks(Address home, Address work)
    {
        return new List<SimTask>
        {
            new SimTask { Id = 1, ActionType = ActionType.Sleep, Priority = 30,
                WindowStart = new TimeSpan(22, 0, 0), WindowEnd = new TimeSpan(6, 0, 0),
                TargetAddressId = home.Id },
            new SimTask { Id = 2, ActionType = ActionType.Work, Priority = 20,
                WindowStart = new TimeSpan(9, 0, 0), WindowEnd = new TimeSpan(17, 0, 0),
                TargetAddressId = work.Id },
            new SimTask { Id = 3, ActionType = ActionType.Idle, Priority = 10,
                WindowStart = TimeSpan.Zero, WindowEnd = TimeSpan.Zero,
                TargetAddressId = home.Id }
        };
    }

    [Fact]
    public void BuildFromTasks_OfficeWorker_HasCorrectActionSequence()
    {
        var (home, work) = CreateAddresses();
        var tasks = CreateOfficeWorkerTasks(home, work);
        var addresses = new Dictionary<int, Address> { { home.Id, home }, { work.Id, work } };
        var schedule = ScheduleBuilder.BuildFromTasks(tasks, addresses, DefaultConfig);
        var actions = schedule.Entries.Select(e => e.Action).ToList();
        Assert.Contains(ActionType.Sleep, actions);
        Assert.Contains(ActionType.Idle, actions);
        Assert.Contains(ActionType.Work, actions);
        Assert.Contains(ActionType.TravelByCar, actions);
    }

    [Fact]
    public void BuildFromTasks_ScheduleCovers24Hours()
    {
        var (home, work) = CreateAddresses();
        var tasks = CreateOfficeWorkerTasks(home, work);
        var addresses = new Dictionary<int, Address> { { home.Id, home }, { work.Id, work } };
        var schedule = ScheduleBuilder.BuildFromTasks(tasks, addresses, DefaultConfig);
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
    public void BuildFromTasks_ThirdAddress_KillPersonAppears()
    {
        var home = new Address { Id = 1, Position = new Vector2(100, 100), Type = AddressType.SuburbanHome };
        var work = new Address { Id = 2, Position = new Vector2(600, 100), Type = AddressType.Office };
        var crimeScene = new Address { Id = 3, Position = new Vector2(1000, 500), Type = AddressType.SuburbanHome };
        var addresses = new Dictionary<int, Address>
            { { 1, home }, { 2, work }, { 3, crimeScene } };
        var tasks = new List<SimTask>
        {
            new SimTask { Id = 1, ActionType = ActionType.Sleep, Priority = 30,
                WindowStart = new TimeSpan(22, 0, 0), WindowEnd = new TimeSpan(6, 0, 0),
                TargetAddressId = home.Id },
            new SimTask { Id = 2, ActionType = ActionType.Work, Priority = 20,
                WindowStart = new TimeSpan(9, 0, 0), WindowEnd = new TimeSpan(17, 0, 0),
                TargetAddressId = work.Id },
            new SimTask { Id = 3, ActionType = ActionType.KillPerson, Priority = 40,
                WindowStart = new TimeSpan(1, 0, 0), WindowEnd = new TimeSpan(1, 30, 0),
                TargetAddressId = crimeScene.Id },
            new SimTask { Id = 4, ActionType = ActionType.Idle, Priority = 10,
                WindowStart = TimeSpan.Zero, WindowEnd = TimeSpan.Zero,
                TargetAddressId = home.Id }
        };
        var schedule = ScheduleBuilder.BuildFromTasks(tasks, addresses, DefaultConfig);
        var killEntry = schedule.Entries.FirstOrDefault(e => e.Action == ActionType.KillPerson);
        Assert.NotNull(killEntry);
        var travelEntries = schedule.Entries.Where(e => e.Action == ActionType.TravelByCar).ToList();
        Assert.True(travelEntries.Count >= 2);
    }
}
