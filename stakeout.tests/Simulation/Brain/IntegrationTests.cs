using System;
using System.Linq;
using Stakeout.Simulation;
using Stakeout.Simulation.Actions;
using Stakeout.Simulation.Addresses;
using Stakeout.Simulation.Brain;
using Stakeout.Simulation.City;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Events;
using Stakeout.Simulation.Objectives;
using Xunit;
using CityEntity = Stakeout.Simulation.Entities.City;

namespace Stakeout.Tests.Simulation.Brain;

public class IntegrationTests
{
    private static (SimulationState state, Person person) Setup()
    {
        AddressTemplateRegistry.RegisterAll();
        var state = new SimulationState(new GameClock(new DateTime(1980, 1, 1, 6, 0, 0)));

        var city = new CityEntity { Id = state.GenerateEntityId(), Name = "Boston", CountryName = "United States" };
        state.Cities[city.Id] = city;
        var cityGen = new CityGenerator(seed: 42);
        state.CityGrids[city.Id] = cityGen.Generate(state, city);

        var gen = new PersonGenerator(new MapConfig());
        var person = gen.GeneratePerson(state);

        return (state, person);
    }

    [Fact]
    public void PersonGetsPlannedDay()
    {
        var (state, person) = Setup();

        var plan = NpcBrain.PlanDay(person, state, state.Clock.CurrentTime);

        Assert.NotNull(plan);
        Assert.NotEmpty(plan.Entries);
    }

    [Fact]
    public void PersonHasSleepInPlan()
    {
        var (state, person) = Setup();

        var plan = NpcBrain.PlanDay(person, state, state.Clock.CurrentTime);

        Assert.Contains(plan.Entries, e => e.PlannedAction.DisplayText == "sleeping");
    }

    [Fact]
    public void ActionRunnerExecutesPlan()
    {
        var (state, person) = Setup();
        person.DayPlan = NpcBrain.PlanDay(person, state, state.Clock.CurrentTime);

        var runner = new ActionRunner(new MapConfig());

        // Tick for a bit — first entry should start
        runner.Tick(person, state, TimeSpan.FromSeconds(1));

        // Person should have an activity or be traveling
        Assert.True(person.CurrentActivity != null || person.TravelInfo != null);
    }

    [Fact]
    public void FullDaySimulation_ProducesEvents()
    {
        var (state, person) = Setup();
        person.DayPlan = NpcBrain.PlanDay(person, state, state.Clock.CurrentTime);

        var runner = new ActionRunner(new MapConfig());

        // Simulate 18 hours in 1-minute increments
        for (int i = 0; i < 18 * 60; i++)
        {
            state.Clock.Tick(60); // 1 minute
            runner.Tick(person, state, TimeSpan.FromMinutes(1));
        }

        // Should have generated some events
        var events = state.Journal.GetEventsForPerson(person.Id);
        Assert.True(events.Count > 1, $"Expected multiple events, got {events.Count}");
        Assert.Contains(events, e => e.EventType == SimulationEventType.ActivityStarted);
    }

    [Fact]
    public void MultiplePersons_EachGetOwnPlan()
    {
        AddressTemplateRegistry.RegisterAll();
        var state = new SimulationState(new GameClock(new DateTime(1980, 1, 1, 6, 0, 0)));
        var city = new CityEntity { Id = state.GenerateEntityId(), Name = "Boston", CountryName = "United States" };
        state.Cities[city.Id] = city;
        var cityGen = new CityGenerator(seed: 42);
        state.CityGrids[city.Id] = cityGen.Generate(state, city);

        var gen = new PersonGenerator(new MapConfig());
        for (int i = 0; i < 5; i++)
        {
            var person = gen.GeneratePerson(state);
            person.DayPlan = NpcBrain.PlanDay(person, state, state.Clock.CurrentTime);
            Assert.NotNull(person.DayPlan);
            Assert.NotEmpty(person.DayPlan.Entries);
        }
    }
}
