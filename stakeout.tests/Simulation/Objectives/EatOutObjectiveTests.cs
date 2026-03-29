using System;
using System.Collections.Generic;
using Stakeout.Simulation;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Objectives;
using Xunit;

namespace Stakeout.Tests.Simulation.Objectives;

public class EatOutObjectiveTests
{
    private static SimulationState CreateStateWithDiner()
    {
        var state = new SimulationState();
        state.Addresses[1] = new Address { Id = 1, Type = AddressType.SuburbanHome };
        state.Addresses[2] = new Address { Id = 2, Type = AddressType.Diner, CityId = 1 };
        state.Cities[1] = new Stakeout.Simulation.Entities.City { Id = 1, AddressIds = { 1, 2 } };
        return state;
    }

    [Fact]
    public void Priority_Is40()
    {
        var obj = new EatOutObjective();
        Assert.Equal(40, obj.Priority);
    }

    [Fact]
    public void Source_IsTrait()
    {
        var obj = new EatOutObjective();
        Assert.Equal(ObjectiveSource.Trait, obj.Source);
    }

    [Fact]
    public void GetActions_ReturnsEatAction()
    {
        var state = CreateStateWithDiner();
        var person = new Person { Id = 1, HomeAddressId = 1, CurrentCityId = 1 };
        var planStart = new DateTime(1980, 1, 1, 6, 0, 0);
        var planEnd = planStart.AddHours(24);

        var obj = new EatOutObjective();
        var actions = obj.GetActions(person, state, planStart, planEnd);

        Assert.Single(actions);
        Assert.Contains("eating", actions[0].DisplayText);
        Assert.Equal(2, actions[0].TargetAddressId);
    }

    [Fact]
    public void GetActions_NoDiner_ReturnsEmpty()
    {
        var state = new SimulationState();
        state.Addresses[1] = new Address { Id = 1, Type = AddressType.SuburbanHome };
        state.Cities[1] = new Stakeout.Simulation.Entities.City { Id = 1, AddressIds = { 1 } };
        var person = new Person { Id = 1, HomeAddressId = 1, CurrentCityId = 1 };
        var planStart = new DateTime(1980, 1, 1, 6, 0, 0);
        var planEnd = planStart.AddHours(24);

        var obj = new EatOutObjective();
        var actions = obj.GetActions(person, state, planStart, planEnd);

        Assert.Empty(actions);
    }

    [Fact]
    public void GetActions_Duration_Is30Minutes()
    {
        var state = CreateStateWithDiner();
        var person = new Person { Id = 1, HomeAddressId = 1, CurrentCityId = 1 };
        var planStart = new DateTime(1980, 1, 1, 6, 0, 0);
        var planEnd = planStart.AddHours(24);

        var obj = new EatOutObjective();
        var actions = obj.GetActions(person, state, planStart, planEnd);

        Assert.Equal(TimeSpan.FromMinutes(30), actions[0].Duration);
    }
}
