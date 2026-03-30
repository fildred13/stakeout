using System;
using System.Linq;
using Stakeout.Simulation;
using Stakeout.Simulation.Actions;
using Stakeout.Simulation.Addresses;
using Stakeout.Simulation.Brain;
using Stakeout.Simulation.Businesses;
using Stakeout.Simulation.City;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Objectives;
using Xunit;
using CityEntity = Stakeout.Simulation.Entities.City;

namespace Stakeout.Tests.Simulation.Businesses;

public class BusinessIntegrationTests
{
    private static SimulationState CreateState()
    {
        AddressTemplateRegistry.RegisterAll();
        BusinessTemplateRegistry.RegisterAll();
        var state = new SimulationState();
        var city = new CityEntity { Id = state.GenerateEntityId(), Name = "Boston", CountryName = "US" };
        state.Cities[city.Id] = city;
        var cityGen = new CityGenerator(seed: 42);
        state.CityGrids[city.Id] = cityGen.Generate(state, city);
        return state;
    }

    [Fact]
    public void FullDaySimulation_NpcGoesToWork()
    {
        var state = CreateState();
        var gen = new PersonGenerator(new MapConfig());
        var person = gen.GeneratePerson(state);

        Assert.Contains(person.Objectives, o => o is WorkShiftObjective);

        var biz = state.Businesses[person.BusinessId.Value];
        var pos = biz.Positions.First(p => p.Id == person.PositionId);
        var workDay = pos.WorkDays[0];

        var planDate = new DateTime(2026, 3, 30);
        while (planDate.DayOfWeek != workDay)
            planDate = planDate.AddDays(1);

        var startTime = planDate + person.PreferredWakeTime;

        // Place person at work to eliminate travel time from planning slot calculations,
        // ensuring WorkShiftObjective can always fit within the shift window.
        person.CurrentAddressId = biz.AddressId;
        person.CurrentPosition = state.Addresses[biz.AddressId].Position;

        var plan = NpcBrain.PlanDay(person, state, startTime);
        person.DayPlan = plan;

        var workEntry = plan.Entries.FirstOrDefault(e =>
            e.PlannedAction.DisplayText.Contains("working as"));
        Assert.NotNull(workEntry);
        Assert.Equal(biz.AddressId, workEntry.PlannedAction.TargetAddressId);
    }

    [Fact]
    public void FullDaySimulation_WithActionRunner_CompletesActivities()
    {
        var state = CreateState();
        var gen = new PersonGenerator(new MapConfig());
        var person = gen.GeneratePerson(state);

        var biz = state.Businesses[person.BusinessId.Value];
        var pos = biz.Positions.First(p => p.Id == person.PositionId);
        var workDay = pos.WorkDays[0];

        var planDate = new DateTime(2026, 3, 30);
        while (planDate.DayOfWeek != workDay)
            planDate = planDate.AddDays(1);

        var startTime = planDate + person.PreferredWakeTime;

        // GameClock has no SetTime — advance to startTime from current position
        var elapsed = (startTime - state.Clock.CurrentTime).TotalSeconds;
        if (elapsed > 0) state.Clock.Tick(elapsed);

        person.DayPlan = NpcBrain.PlanDay(person, state, startTime);

        var runner = new ActionRunner(new MapConfig());
        for (int i = 0; i < 18 * 60; i++)
        {
            state.Clock.Tick(60.0);
            runner.Tick(person, state, TimeSpan.FromMinutes(1));
        }

        var events = state.Journal.GetEventsForPerson(person.Id);
        Assert.Contains(events, e =>
            e.EventType == Stakeout.Simulation.Events.SimulationEventType.ActivityCompleted);
    }

    [Fact]
    public void BusinessResolver_SpawnedWorkers_AllHaveSchedules()
    {
        var state = CreateState();
        var gen = new PersonGenerator(new MapConfig());
        var biz = state.Businesses.Values.First();

        var spawned = BusinessResolver.Resolve(state, biz, gen);

        foreach (var person in spawned)
        {
            Assert.Contains(person.Objectives, o => o is WorkShiftObjective);
            Assert.Contains(person.Objectives, o => o is SleepObjective);
            Assert.NotNull(person.BusinessId);
            Assert.NotNull(person.PositionId);
        }
    }
}
