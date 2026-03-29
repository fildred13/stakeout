using System;
using Stakeout.Simulation;
using Stakeout.Simulation.Actions;
using Stakeout.Simulation.Actions.Primitives;
using Stakeout.Simulation.Brain;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Events;
using Stakeout.Simulation.Objectives;
using Xunit;

namespace Stakeout.Tests.Simulation.Actions;

public class ActionRunnerTests
{
    private static readonly DateTime BaseTime = new DateTime(1980, 1, 1, 8, 0, 0);

    private static (SimulationState state, Person person) Setup()
    {
        var state = new SimulationState(new GameClock(BaseTime));
        var home = new Address { Id = 1, GridX = 0, GridY = 0 };
        var park = new Address { Id = 2, GridX = 2, GridY = 2 };
        state.Addresses[home.Id] = home;
        state.Addresses[park.Id] = park;

        var person = new Person
        {
            Id = state.GenerateEntityId(),
            HomeAddressId = 1,
            CurrentAddressId = 1,
            CurrentPosition = new Godot.Vector2(0, 0),
            PreferredWakeTime = TimeSpan.FromHours(6),
            PreferredSleepTime = TimeSpan.FromHours(22)
        };
        state.People[person.Id] = person;

        // Create a simple day plan
        person.DayPlan = new DayPlan();
        person.DayPlan.Entries.Add(new DayPlanEntry
        {
            StartTime = BaseTime,
            EndTime = BaseTime.AddHours(1),
            PlannedAction = new PlannedAction
            {
                Action = new WaitAction(TimeSpan.FromHours(1), "relaxing at home"),
                TargetAddressId = 1,
                Duration = TimeSpan.FromHours(1),
                DisplayText = "relaxing at home"
            }
        });

        return (state, person);
    }

    [Fact]
    public void Tick_StartsFirstActivity_WhenAtTargetAddress()
    {
        var (state, person) = Setup();
        var runner = new ActionRunner(new MapConfig());

        runner.Tick(person, state, TimeSpan.FromSeconds(1));

        Assert.NotNull(person.CurrentActivity);
        Assert.Equal("relaxing at home", person.CurrentActivity.DisplayText);
    }

    [Fact]
    public void Tick_StartsTraveling_WhenNotAtTargetAddress()
    {
        var (state, person) = Setup();
        // Change plan to target park (address 2), person is at home (address 1)
        person.DayPlan.Entries[0] = new DayPlanEntry
        {
            StartTime = BaseTime,
            EndTime = BaseTime.AddHours(1),
            PlannedAction = new PlannedAction
            {
                Action = new WaitAction(TimeSpan.FromHours(1), "running"),
                TargetAddressId = 2,
                Duration = TimeSpan.FromHours(1),
                DisplayText = "running"
            }
        };

        var runner = new ActionRunner(new MapConfig());
        runner.Tick(person, state, TimeSpan.FromSeconds(1));

        Assert.NotNull(person.TravelInfo);
        Assert.Equal(2, person.TravelInfo.ToAddressId);
        Assert.Null(person.CurrentActivity);
    }

    [Fact]
    public void Tick_CompletesActivity_AdvancesPlan()
    {
        var (state, person) = Setup();
        // Add a second entry
        person.DayPlan.Entries.Add(new DayPlanEntry
        {
            StartTime = BaseTime.AddHours(1),
            EndTime = BaseTime.AddHours(2),
            PlannedAction = new PlannedAction
            {
                Action = new WaitAction(TimeSpan.FromHours(1), "second activity"),
                TargetAddressId = 1,
                Duration = TimeSpan.FromHours(1),
                DisplayText = "second activity"
            }
        });

        var runner = new ActionRunner(new MapConfig());
        // Start first activity
        runner.Tick(person, state, TimeSpan.FromSeconds(1));
        Assert.Equal("relaxing at home", person.CurrentActivity.DisplayText);

        // Complete first activity
        runner.Tick(person, state, TimeSpan.FromHours(1.1));
        Assert.Equal(1, person.DayPlan.CurrentIndex);
    }

    [Fact]
    public void Tick_LogsActivityStartedEvent()
    {
        var (state, person) = Setup();
        var runner = new ActionRunner(new MapConfig());

        runner.Tick(person, state, TimeSpan.FromSeconds(1));

        var events = state.Journal.GetEventsForPerson(person.Id);
        Assert.Contains(events, e => e.EventType == SimulationEventType.ActivityStarted);
    }

    [Fact]
    public void Tick_NoPlan_DoesNothing()
    {
        var (state, person) = Setup();
        person.DayPlan = null;
        var runner = new ActionRunner(new MapConfig());

        runner.Tick(person, state, TimeSpan.FromSeconds(1));

        Assert.Null(person.CurrentActivity);
        Assert.Null(person.TravelInfo);
    }
}
