using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Stakeout.Simulation;
using Stakeout.Simulation.Actions;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Objectives;
using Stakeout.Simulation.Scheduling;
using Stakeout.Simulation.Sublocations;
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
    public void BuildFromTasks_TravelEntriesHaveAddressIds()
    {
        var (home, work) = CreateAddresses();
        var tasks = CreateOfficeWorkerTasks(home, work);
        var addresses = new Dictionary<int, Address> { { home.Id, home }, { work.Id, work } };
        var schedule = ScheduleBuilder.BuildFromTasks(tasks, addresses, DefaultConfig);
        var travelEntries = schedule.Entries.Where(e => e.Action == ActionType.TravelByCar).ToList();
        Assert.True(travelEntries.Count >= 2);
        foreach (var travel in travelEntries)
        {
            Assert.NotNull(travel.FromAddressId);
            Assert.NotNull(travel.TargetAddressId);
        }
    }

    [Fact]
    public void BuildFromTasks_GetEntryAtTime_ReturnsCorrectEntry()
    {
        var (home, work) = CreateAddresses();
        var tasks = CreateOfficeWorkerTasks(home, work);
        var addresses = new Dictionary<int, Address> { { home.Id, home }, { work.Id, work } };
        var schedule = ScheduleBuilder.BuildFromTasks(tasks, addresses, DefaultConfig);

        var midday = schedule.GetEntryAtTime(new TimeSpan(12, 0, 0));
        Assert.Equal(ActionType.Work, midday.Action);

        var night = schedule.GetEntryAtTime(new TimeSpan(3, 0, 0));
        Assert.Equal(ActionType.Sleep, night.Action);
    }

    [Fact]
    public void BuildFromTasks_WithSublocations_EntriesHaveSublocationIds()
    {
        var state = new SimulationState();
        var home = new Address { Id = 1, Position = new Vector2(100, 100), Type = AddressType.SuburbanHome };
        var work = new Address { Id = 2, Position = new Vector2(600, 100), Type = AddressType.Office };
        state.Addresses[1] = home;
        state.Addresses[2] = work;

        // Generate sublocations
        SublocationGeneratorRegistry.RegisterAll();
        new SuburbanHomeGenerator().Generate(home, state, new Random(42));
        new OfficeGenerator().Generate(work, state, new Random(42));

        var tasks = CreateOfficeWorkerTasks(home, work);
        var schedule = ScheduleBuilder.BuildFromTasks(tasks, state, DefaultConfig);

        // Non-travel entries at addresses with sublocations should have sublocation IDs
        var nonTravelEntries = schedule.Entries
            .Where(e => e.Action != ActionType.TravelByCar)
            .ToList();

        Assert.True(nonTravelEntries.Any(e => e.TargetSublocationId.HasValue));
    }

    [Fact]
    public void BuildFromTasks_ApartmentWithUnitTag_SublocationEntriesUseCorrectUnit()
    {
        var state = new SimulationState();
        var home = new Address { Id = 1, Position = new Vector2(100, 100), Type = AddressType.ApartmentBuilding };
        var work = new Address { Id = 2, Position = new Vector2(600, 100), Type = AddressType.Office };
        state.Addresses[1] = home;
        state.Addresses[2] = work;

        SublocationGeneratorRegistry.RegisterAll();
        new ApartmentBuildingGenerator().Generate(home, state, new Random(42));
        new OfficeGenerator().Generate(work, state, new Random(42));

        // Pick the SECOND unit tag — not the first, so FindByTag("bedroom")
        // without unit scoping would return the wrong bedroom
        var unitTag = home.Sublocations.Values
            .SelectMany(s => s.Tags)
            .Where(t => t.StartsWith("unit_f"))
            .Distinct()
            .Skip(1)
            .First();

        // Find the bedroom for this specific unit
        var unitBedroom = home.Sublocations.Values
            .First(s => s.HasTag(unitTag) && s.HasTag("bedroom"));

        var tasks = new List<SimTask>
        {
            new SimTask { Id = 1, ActionType = ActionType.Sleep, Priority = 30,
                WindowStart = new TimeSpan(22, 0, 0), WindowEnd = new TimeSpan(6, 0, 0),
                TargetAddressId = home.Id, UnitTag = unitTag },
            new SimTask { Id = 2, ActionType = ActionType.Work, Priority = 20,
                WindowStart = new TimeSpan(9, 0, 0), WindowEnd = new TimeSpan(17, 0, 0),
                TargetAddressId = work.Id },
            new SimTask { Id = 3, ActionType = ActionType.Idle, Priority = 10,
                WindowStart = TimeSpan.Zero, WindowEnd = TimeSpan.Zero,
                TargetAddressId = home.Id, UnitTag = unitTag }
        };

        var schedule = ScheduleBuilder.BuildFromTasks(tasks, state, DefaultConfig);

        // Sleep entries targeting the home address should use this unit's bedroom
        var sleepEntries = schedule.Entries
            .Where(e => e.Action == ActionType.Sleep && e.TargetSublocationId.HasValue)
            .ToList();
        Assert.NotEmpty(sleepEntries);
        Assert.All(sleepEntries, e => Assert.Equal(unitBedroom.Id, e.TargetSublocationId));
    }

    [Fact]
    public void BuildFromTasks_ThirdAddress_KillPersonAppears()
    {
        var home = new Address { Id = 1, Position = new Vector2(100, 100), Type = AddressType.SuburbanHome };
        var work = new Address { Id = 2, Position = new Vector2(600, 100), Type = AddressType.Office };
        var crimeScene = new Address { Id = 3, Position = new Vector2(400, 300), Type = AddressType.SuburbanHome };
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

    [Fact]
    public void BuildFromTasks_NightShiftWorker_NoOverlappingEntries()
    {
        // Night shift worker: work ends at 00:10, sleep starts at 00:25 (after 15-min commute window).
        // The idle gap between work-end and sleep-start is only 15 minutes.
        // If travel time > 15 minutes, the travel overflows the idle block,
        // creating a broken entry with StartTime > EndTime.
        var home = new Address { Id = 1, Position = new Vector2(100, 100), Type = AddressType.SuburbanHome };
        var work = new Address { Id = 2, Position = new Vector2(800, 100), Type = AddressType.Office };
        var addresses = new Dictionary<int, Address> { { 1, home }, { 2, work } };

        var tasks = new List<SimTask>
        {
            new SimTask { Id = 1, ActionType = ActionType.Sleep, Priority = 30,
                WindowStart = new TimeSpan(0, 25, 0), WindowEnd = new TimeSpan(8, 25, 0),
                TargetAddressId = home.Id },
            new SimTask { Id = 2, ActionType = ActionType.Work, Priority = 20,
                WindowStart = new TimeSpan(16, 0, 0), WindowEnd = new TimeSpan(0, 10, 0),
                TargetAddressId = work.Id },
            new SimTask { Id = 3, ActionType = ActionType.Idle, Priority = 10,
                WindowStart = TimeSpan.Zero, WindowEnd = TimeSpan.Zero,
                TargetAddressId = home.Id }
        };

        var schedule = ScheduleBuilder.BuildFromTasks(tasks, addresses, DefaultConfig);

        // Every entry should have StartTime < EndTime, or be a legitimate midnight wrap
        // (where the duration when accounting for wrap is positive and reasonable)
        foreach (var entry in schedule.Entries)
        {
            var duration = entry.EndTime - entry.StartTime;
            if (duration < TimeSpan.Zero)
                duration += TimeSpan.FromHours(24);

            // No entry should be near-24-hour duration (symptom of overflow)
            Assert.True(duration < TimeSpan.FromHours(23),
                $"Entry {entry.Action} has suspicious duration {duration} " +
                $"(StartTime={entry.StartTime}, EndTime={entry.EndTime})");
        }

        // Entries should not overlap when laid out on a 24-hour timeline
        var sorted = schedule.Entries.OrderBy(e => e.StartTime).ToList();
        for (int i = 0; i < sorted.Count - 1; i++)
        {
            var current = sorted[i];
            var next = sorted[i + 1];
            // Current entry's end should be <= next entry's start (no overlap)
            // Account for midnight wrap: if current wraps, its end is next day
            if (current.EndTime > current.StartTime)
            {
                Assert.True(current.EndTime <= next.StartTime,
                    $"Overlap: {current.Action} ends at {current.EndTime} but " +
                    $"{next.Action} starts at {next.StartTime}");
            }
        }
    }
}
