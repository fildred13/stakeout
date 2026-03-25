using System;
using System.Collections.Generic;
using Godot;
using Stakeout.Simulation;
using Stakeout.Simulation.Actions;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Objectives;
using Stakeout.Simulation.Scheduling;
using Xunit;

namespace Stakeout.Tests.Simulation.Scheduling;

public class PersonBehaviorTests
{
    private static (SimulationState state, Person person) CreateTestScenario()
    {
        var state = new SimulationState(new GameClock(new DateTime(1980, 1, 1, 0, 0, 0)));
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

        var objectives = new List<Objective>
        {
            ObjectiveResolver.CreateGetSleepObjective(sleepTime, wakeTime, home.Id),
            ObjectiveResolver.CreateMaintainJobObjective(job.ShiftStart, job.ShiftEnd, work.Id),
            ObjectiveResolver.CreateDefaultIdleObjective(home.Id)
        };

        var tasks = ObjectiveResolver.ResolveTasks(objectives, state);
        var addresses = new Dictionary<int, Address> { { home.Id, home }, { work.Id, work } };
        var schedule = ScheduleBuilder.BuildFromTasks(tasks, addresses, mapConfig);

        var person = new Person
        {
            Id = state.GenerateEntityId(),
            FirstName = "Test",
            LastName = "Person",
            HomeAddressId = home.Id,
            JobId = job.Id,
            CurrentAddressId = home.Id,
            CurrentPosition = home.Position,
            CurrentAction = ActionType.Sleep,
            PreferredSleepTime = sleepTime,
            PreferredWakeTime = wakeTime,
            Objectives = objectives,
            Schedule = schedule
        };
        state.People[person.Id] = person;

        return (state, person);
    }

    [Fact]
    public void Update_AtMiddayOnWorkday_PersonIsWorking()
    {
        var (state, person) = CreateTestScenario();
        state.Clock.Tick(12 * 3600); // noon

        // Pre-place person at work so travel is not needed
        var workAddress = state.Addresses[state.Jobs[person.JobId].WorkAddressId];
        person.CurrentAddressId = workAddress.Id;
        person.CurrentPosition = workAddress.Position;

        var behavior = new PersonBehavior(new MapConfig());

        behavior.Update(person, state);

        Assert.Equal(ActionType.Work, person.CurrentAction);
    }

    [Fact]
    public void Update_At3AM_PersonIsSleeping()
    {
        var (state, person) = CreateTestScenario();
        state.Clock.Tick(3 * 3600);
        var behavior = new PersonBehavior(new MapConfig());

        behavior.Update(person, state);

        Assert.Equal(ActionType.Sleep, person.CurrentAction);
    }

    [Fact]
    public void Update_TransitionAppendsJournalEvent()
    {
        var (state, person) = CreateTestScenario();
        state.Clock.Tick(7 * 3600); // 07:00 — should be AtHome after waking
        var behavior = new PersonBehavior(new MapConfig());

        behavior.Update(person, state);

        Assert.True(state.Journal.AllEvents.Count > 0);
    }

    [Fact]
    public void Update_DuringTravel_InterpolatesPosition()
    {
        var (state, person) = CreateTestScenario();
        var schedule = person.Schedule;
        var behavior = new PersonBehavior(new MapConfig());

        // Find a travel entry
        ScheduleEntry travelEntry = null;
        foreach (var entry in schedule.Entries)
        {
            if (entry.Action == ActionType.TravelByCar)
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

        behavior.Update(person, state);

        Assert.Equal(ActionType.TravelByCar, person.CurrentAction);
        Assert.Null(person.CurrentAddressId);
        Assert.NotNull(person.TravelInfo);
    }

    [Fact]
    public void Update_DeadPerson_DoesNothing()
    {
        var (state, person) = CreateTestScenario();
        person.IsAlive = false;
        person.CurrentAction = ActionType.Idle;
        state.Clock.Tick(12 * 3600);
        var behavior = new PersonBehavior(new MapConfig());
        behavior.Update(person, state);
        Assert.Equal(ActionType.Idle, person.CurrentAction);
    }
}
