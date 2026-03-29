using System;
using System.Collections.Generic;
using System.Linq;
using Stakeout.Simulation;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Objectives;
using Xunit;

namespace Stakeout.Tests.Simulation.Objectives;

public class GoForARunObjectiveTests
{
    private static SimulationState CreateStateWithPark()
    {
        var state = new SimulationState();
        state.Addresses[1] = new Address { Id = 1, Type = AddressType.SuburbanHome };
        state.Addresses[2] = new Address { Id = 2, Type = AddressType.Park, CityId = 1 };
        state.Cities[1] = new Stakeout.Simulation.Entities.City { Id = 1, AddressIds = { 1, 2 } };
        return state;
    }

    [Fact]
    public void Priority_Is20()
    {
        var obj = new GoForARunObjective();
        Assert.Equal(20, obj.Priority);
    }

    [Fact]
    public void Source_IsTrait()
    {
        var obj = new GoForARunObjective();
        Assert.Equal(ObjectiveSource.Trait, obj.Source);
    }

    [Fact]
    public void GetActionsForToday_ReturnsRunAction()
    {
        var state = CreateStateWithPark();
        var person = new Person { Id = 1, HomeAddressId = 1, CurrentCityId = 1 };

        var obj = new GoForARunObjective();
        var actions = obj.GetActionsForToday(person, state, DateTime.Today);

        Assert.Single(actions);
        Assert.Equal("running on the trails", actions[0].DisplayText);
        Assert.Equal(2, actions[0].TargetAddressId); // park
    }

    [Fact]
    public void GetActionsForToday_NoPark_ReturnsEmpty()
    {
        var state = new SimulationState();
        state.Addresses[1] = new Address { Id = 1, Type = AddressType.SuburbanHome };
        state.Cities[1] = new Stakeout.Simulation.Entities.City { Id = 1, AddressIds = { 1 } };
        var person = new Person { Id = 1, HomeAddressId = 1, CurrentCityId = 1 };

        var obj = new GoForARunObjective();
        var actions = obj.GetActionsForToday(person, state, DateTime.Today);

        Assert.Empty(actions);
    }

    [Fact]
    public void GetActionsForToday_Duration_Is45Minutes()
    {
        var state = CreateStateWithPark();
        var person = new Person { Id = 1, HomeAddressId = 1, CurrentCityId = 1 };

        var obj = new GoForARunObjective();
        var actions = obj.GetActionsForToday(person, state, DateTime.Today);

        Assert.Equal(TimeSpan.FromMinutes(45), actions[0].Duration);
    }
}
