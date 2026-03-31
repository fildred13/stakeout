using System;
using System.Linq;
using Stakeout.Simulation;
using Stakeout.Simulation.Addresses;
using Stakeout.Simulation.Businesses;
using Stakeout.Simulation.City;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Objectives;
using Xunit;
using CityEntity = Stakeout.Simulation.Entities.City;

namespace Stakeout.Tests.Simulation.Objectives;

public class WorkShiftObjectiveTests
{
    private static (SimulationState state, Business biz, Person person) Setup()
    {
        AddressTemplateRegistry.RegisterAll();
        BusinessTemplateRegistry.RegisterAll();
        var state = new SimulationState();
        var city = new CityEntity { Id = state.GenerateEntityId(), Name = "Boston", CountryName = "US" };
        state.Cities[city.Id] = city;
        var cityGen = new CityGenerator(seed: 42);
        state.CityGrids[city.Id] = cityGen.Generate(state, city);

        var gen = new PersonGenerator(new MapConfig());
        var person = gen.GeneratePerson(state);
        var biz = state.Businesses[person.BusinessId.Value];

        return (state, biz, person);
    }

    [Fact]
    public void Priority_Is60()
    {
        var obj = new WorkShiftObjective(1, 1);
        Assert.Equal(60, obj.Priority);
    }

    [Fact]
    public void Source_IsJob()
    {
        var obj = new WorkShiftObjective(1, 1);
        Assert.Equal(ObjectiveSource.Job, obj.Source);
    }

    [Fact]
    public void GetActions_ReturnsActions_OnWorkDays()
    {
        var (state, biz, person) = Setup();
        var pos = biz.Positions.First(p => p.Id == person.PositionId);
        var obj = new WorkShiftObjective(biz.Id, pos.Id) { Id = state.GenerateEntityId() };

        var workDay = pos.WorkDays[0];
        var planStart = new DateTime(2026, 3, 30);
        while (planStart.DayOfWeek != workDay)
            planStart = planStart.AddDays(1);

        var actions = obj.GetActions(person, state, planStart, planStart.AddHours(24));

        Assert.NotEmpty(actions);
        Assert.All(actions, a => Assert.Equal(biz.AddressId, a.TargetAddressId));
    }

    [Fact]
    public void GetActions_ReturnsEmpty_OnDayOff()
    {
        var (state, biz, person) = Setup();
        var pos = biz.Positions.First(p => p.Id == person.PositionId);
        var obj = new WorkShiftObjective(biz.Id, pos.Id) { Id = state.GenerateEntityId() };

        var allDays = Enum.GetValues<DayOfWeek>();
        DayOfWeek dayOff = DayOfWeek.Sunday;
        foreach (var d in allDays)
        {
            if (!pos.WorkDays.Contains(d)) { dayOff = d; break; }
        }

        var planStart = new DateTime(2026, 3, 30);
        while (planStart.DayOfWeek != dayOff)
            planStart = planStart.AddDays(1);

        var actions = obj.GetActions(person, state, planStart, planStart.AddHours(24));

        Assert.Empty(actions);
    }

    [Fact]
    public void GetActions_ShiftTimes_MatchPosition()
    {
        var (state, biz, person) = Setup();
        var pos = biz.Positions.First(p => p.Id == person.PositionId);
        var obj = new WorkShiftObjective(biz.Id, pos.Id) { Id = state.GenerateEntityId() };

        var workDay = pos.WorkDays[0];
        var planStart = new DateTime(2026, 3, 30);
        while (planStart.DayOfWeek != workDay)
            planStart = planStart.AddDays(1);

        var actions = obj.GetActions(person, state, planStart, planStart.AddHours(24));
        if (actions.Count == 0) return;

        var action = actions[0];
        Assert.Equal(planStart.Date + pos.ShiftStart, action.TimeWindowStart);
    }
}
