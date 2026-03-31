using System;
using System.Collections.Generic;
using System.Linq;
using Stakeout.Simulation;
using Stakeout.Simulation.Actions.Primitives;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Objectives;
using Xunit;

namespace Stakeout.Tests.Simulation.Objectives;

public class GoOnDateObjectiveTests
{
    private static (SimulationState state, Person driver, Person passenger, Group group, Address diner)
        BuildScene(GroupPhase phase)
    {
        var state = new SimulationState(new GameClock(new DateTime(1984, 1, 2, 14, 0, 0)));

        var driverHome = new Address { Id = state.GenerateEntityId(), GridX = 2, GridY = 2 };
        var passengerHome = new Address { Id = state.GenerateEntityId(), GridX = 8, GridY = 2 };
        var diner = new Address { Id = state.GenerateEntityId(), GridX = 5, GridY = 5 };
        foreach (var a in new[] { driverHome, passengerHome, diner })
            state.Addresses[a.Id] = a;

        var driver = new Person
        {
            Id = state.GenerateEntityId(),
            HomeAddressId = driverHome.Id,
            CurrentAddressId = driverHome.Id,
            CurrentPosition = driverHome.Position,
            PreferredSleepTime = TimeSpan.FromHours(23),
            PreferredWakeTime = TimeSpan.FromHours(7)
        };
        driver.Objectives.Add(new SleepObjective { Id = state.GenerateEntityId() });
        var passenger = new Person
        {
            Id = state.GenerateEntityId(),
            HomeAddressId = passengerHome.Id,
            CurrentAddressId = passengerHome.Id,
            CurrentPosition = passengerHome.Position,
            PreferredSleepTime = TimeSpan.FromHours(23),
            PreferredWakeTime = TimeSpan.FromHours(7)
        };
        passenger.Objectives.Add(new SleepObjective { Id = state.GenerateEntityId() });
        state.People[driver.Id] = driver;
        state.People[passenger.Id] = passenger;

        var group = new Group
        {
            Id = state.GenerateEntityId(),
            Type = GroupType.Date,
            Status = GroupStatus.Active,
            DriverPersonId = driver.Id,
            PickupAddressId = passengerHome.Id,
            PickupTime = new DateTime(1984, 1, 2, 17, 50, 0),
            MeetupAddressId = diner.Id,
            MeetupTime = new DateTime(1984, 1, 2, 19, 0, 0),
            MemberPersonIds = new List<int> { driver.Id, passenger.Id },
            CurrentPhase = phase
        };
        state.Groups[group.Id] = group;

        return (state, driver, passenger, group, diner);
    }

    [Fact]
    public void GetActions_DriverEnRoute_Driver_TargetsPickupAddress()
    {
        var (state, driver, _, group, _) = BuildScene(GroupPhase.DriverEnRoute);
        var obj = new GoOnDateObjective(group.Id) { Id = state.GenerateEntityId() };

        var actions = obj.GetActions(driver, state,
            new DateTime(1984, 1, 2, 14, 0, 0), new DateTime(1984, 1, 2, 23, 59, 0));

        Assert.Single(actions);
        Assert.Equal(group.PickupAddressId, actions[0].TargetAddressId);
        Assert.IsType<WaitAction>(actions[0].Action);
    }

    [Fact]
    public void GetActions_DriverEnRoute_Passenger_TargetsHome()
    {
        var (state, _, passenger, group, _) = BuildScene(GroupPhase.DriverEnRoute);
        var obj = new GoOnDateObjective(group.Id) { Id = state.GenerateEntityId() };

        var actions = obj.GetActions(passenger, state,
            new DateTime(1984, 1, 2, 14, 0, 0), new DateTime(1984, 1, 2, 23, 59, 0));

        Assert.Single(actions);
        Assert.Equal(passenger.HomeAddressId, actions[0].TargetAddressId);
    }

    [Fact]
    public void OnActionCompletedWithState_DriverEnRoute_AdvancesToAtPickup()
    {
        var (state, driver, passenger, group, _) = BuildScene(GroupPhase.DriverEnRoute);
        driver.CurrentAddressId = group.PickupAddressId;  // driver has arrived
        var obj = new GoOnDateObjective(group.Id) { Id = state.GenerateEntityId() };
        driver.Objectives.Add(obj);
        passenger.Objectives.Add(new GoOnDateObjective(group.Id) { Id = state.GenerateEntityId() });

        var dummyAction = new PlannedAction { SourceObjective = obj };
        obj.OnActionCompletedWithState(dummyAction, driver, state, success: true);

        Assert.Equal(GroupPhase.AtPickup, group.CurrentPhase);
        Assert.True(driver.NeedsReplan);
        Assert.True(passenger.NeedsReplan);
    }

    [Fact]
    public void OnActionCompletedWithState_DrivingBack_DisbandsGroup()
    {
        var (state, driver, passenger, group, _) = BuildScene(GroupPhase.DrivingBack);
        group.CurrentPhase = GroupPhase.DrivingBack;
        var driverObj = new GoOnDateObjective(group.Id) { Id = state.GenerateEntityId() };
        var passengerObj = new GoOnDateObjective(group.Id) { Id = state.GenerateEntityId() };
        driver.Objectives.Add(driverObj);
        passenger.Objectives.Add(passengerObj);

        driverObj.OnActionCompletedWithState(new PlannedAction { SourceObjective = driverObj }, driver, state, true);

        Assert.Equal(GroupPhase.Complete, group.CurrentPhase);
        Assert.Equal(GroupStatus.Disbanded, group.Status);
        Assert.DoesNotContain(driver.Objectives, o => o is GoOnDateObjective);
        Assert.DoesNotContain(passenger.Objectives, o => o is GoOnDateObjective);
        Assert.True(driver.NeedsReplan);
        Assert.True(passenger.NeedsReplan);
    }
}
