using System;
using Godot;
using Stakeout.Simulation;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Scheduling;
using Xunit;

namespace Stakeout.Tests.Simulation.Scheduling;

public class PersonBehaviorTests
{
    private static (SimulationState state, Person person, DailySchedule schedule) CreateTestScenario()
    {
        var state = new SimulationState();
        var home = new Address { Id = state.GenerateEntityId(), Position = new Vector2(100, 100), Type = AddressType.SuburbanHome, Number = 1, StreetId = 1 };
        var work = new Address { Id = state.GenerateEntityId(), Position = new Vector2(600, 100), Type = AddressType.Office, Number = 2, StreetId = 1 };
        state.Addresses[home.Id] = home;
        state.Addresses[work.Id] = work;

        var job = new Job
        {
            Id = state.GenerateEntityId(),
            Type = JobType.OfficeWorker,
            Title = "Office Worker",
            WorkAddressId = work.Id,
            ShiftStart = new TimeSpan(9, 0, 0),
            ShiftEnd = new TimeSpan(17, 0, 0)
        };
        state.Jobs[job.Id] = job;

        var mapConfig = new MapConfig();
        var commuteHours = mapConfig.ComputeTravelTimeHours(home.Position, work.Position);
        var (sleepTime, wakeTime) = SleepScheduleCalculator.Compute(job, commuteHours);

        var person = new Person
        {
            Id = state.GenerateEntityId(),
            FirstName = "Test",
            LastName = "Person",
            HomeAddressId = home.Id,
            JobId = job.Id,
            CurrentAddressId = home.Id,
            CurrentPosition = home.Position,
            CurrentActivity = ActivityType.Sleeping,
            PreferredSleepTime = sleepTime,
            PreferredWakeTime = wakeTime
        };
        state.People[person.Id] = person;

        var goalSet = GoalSetBuilder.Build(job, sleepTime, wakeTime);
        var schedule = ScheduleBuilder.Build(goalSet, home, work, mapConfig);

        return (state, person, schedule);
    }

    [Fact]
    public void Update_AtMiddayOnWorkday_PersonIsWorking()
    {
        var (state, person, schedule) = CreateTestScenario();
        state.Clock.Tick(12 * 3600); // noon
        var behavior = new PersonBehavior(new MapConfig());

        behavior.Update(person, schedule, state);

        Assert.Equal(ActivityType.Working, person.CurrentActivity);
    }

    [Fact]
    public void Update_At3AM_PersonIsSleeping()
    {
        var (state, person, schedule) = CreateTestScenario();
        state.Clock.Tick(3 * 3600);
        var behavior = new PersonBehavior(new MapConfig());

        behavior.Update(person, schedule, state);

        Assert.Equal(ActivityType.Sleeping, person.CurrentActivity);
    }

    [Fact]
    public void Update_TransitionAppendsJournalEvent()
    {
        var (state, person, schedule) = CreateTestScenario();
        state.Clock.Tick(7 * 3600); // 07:00 — should be AtHome after waking
        var behavior = new PersonBehavior(new MapConfig());

        behavior.Update(person, schedule, state);

        Assert.True(state.Journal.AllEvents.Count > 0);
    }

    [Fact]
    public void Update_DuringTravel_InterpolatesPosition()
    {
        var (state, person, schedule) = CreateTestScenario();
        var behavior = new PersonBehavior(new MapConfig());

        // Find a travel entry
        ScheduleEntry travelEntry = null;
        foreach (var entry in schedule.Entries)
        {
            if (entry.Activity == ActivityType.TravellingByCar)
            {
                travelEntry = entry;
                break;
            }
        }
        Assert.NotNull(travelEntry);

        // Set clock to midpoint of travel
        var midTime = travelEntry.StartTime + (travelEntry.EndTime - travelEntry.StartTime) / 2;
        if (midTime < TimeSpan.Zero) midTime += TimeSpan.FromHours(24);
        state.Clock.Tick(midTime.TotalSeconds);

        behavior.Update(person, schedule, state);

        Assert.Equal(ActivityType.TravellingByCar, person.CurrentActivity);
        Assert.Null(person.CurrentAddressId);
        Assert.NotNull(person.TravelInfo);
    }
}
