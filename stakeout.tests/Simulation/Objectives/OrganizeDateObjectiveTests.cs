using System;
using System.Collections.Generic;
using System.Linq;
using Stakeout.Simulation;
using Stakeout.Simulation.Actions.Telephone;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Objectives;
using Xunit;

namespace Stakeout.Tests.Simulation.Objectives;

public class OrganizeDateObjectiveTests
{
    [Fact]
    public void GetActions_InNeedToCallState_ReturnsPhoneCallAction()
    {
        var state = new SimulationState(new GameClock(new DateTime(1984, 1, 2, 8, 0, 0)));

        var callerHome = new Address { Id = state.GenerateEntityId(), GridX = 2, GridY = 2 };
        var recipientHome = new Address { Id = state.GenerateEntityId(), GridX = 8, GridY = 8 };
        var loc = new Location { Id = state.GenerateEntityId(), AddressId = recipientHome.Id };
        var phone = new Stakeout.Simulation.Fixtures.Fixture
        {
            Id = state.GenerateEntityId(),
            Type = Stakeout.Simulation.Fixtures.FixtureType.Telephone,
            LocationId = loc.Id
        };
        state.Addresses[callerHome.Id] = callerHome;
        state.Addresses[recipientHome.Id] = recipientHome;
        state.Locations[loc.Id] = loc;
        recipientHome.LocationIds.Add(loc.Id);
        state.Fixtures[phone.Id] = phone;

        var caller = new Person
        {
            Id = state.GenerateEntityId(),
            HomeAddressId = callerHome.Id,
            CurrentAddressId = callerHome.Id
        };
        var recipient = new Person
        {
            Id = state.GenerateEntityId(),
            HomeAddressId = recipientHome.Id,
            HomePhoneFixtureId = phone.Id
        };
        state.People[caller.Id] = caller;
        state.People[recipient.Id] = recipient;

        var callTime = new DateTime(1984, 1, 2, 12, 0, 0);
        var meetupTime = new DateTime(1984, 1, 2, 19, 0, 0);
        var pickupTime = new DateTime(1984, 1, 2, 17, 50, 0);

        var objective = new OrganizeDateObjective(recipient.Id, 99, callTime, meetupTime, pickupTime)
        { Id = state.GenerateEntityId() };
        caller.Objectives.Add(objective);

        var actions = objective.GetActions(caller, state,
            new DateTime(1984, 1, 2, 8, 0, 0), new DateTime(1984, 1, 2, 23, 59, 0));

        Assert.Single(actions);
        Assert.IsType<PhoneCallAction>(actions[0].Action);
        // Caller makes the call from their own home, not the recipient's home
        Assert.Equal(callerHome.Id, actions[0].TargetAddressId);
    }

    [Fact]
    public void OnAccepted_TodayStillFeasible_CreatesGoOnDateObjectiveForBoth()
    {
        var state = new SimulationState(new GameClock(new DateTime(1984, 1, 2, 14, 0, 0)));

        var callerHome = new Address { Id = state.GenerateEntityId(), GridX = 2, GridY = 2 };
        var recipientHome = new Address { Id = state.GenerateEntityId(), GridX = 8, GridY = 8 };
        var diner = new Address { Id = state.GenerateEntityId(), GridX = 5, GridY = 5 };
        state.Addresses[callerHome.Id] = callerHome;
        state.Addresses[recipientHome.Id] = recipientHome;
        state.Addresses[diner.Id] = diner;

        var caller = new Person
        {
            Id = state.GenerateEntityId(),
            HomeAddressId = callerHome.Id,
            CurrentAddressId = callerHome.Id,
            PreferredSleepTime = TimeSpan.FromHours(23),
            PreferredWakeTime = TimeSpan.FromHours(7)
        };
        caller.Objectives.Add(new SleepObjective { Id = state.GenerateEntityId() });
        var recipient = new Person
        {
            Id = state.GenerateEntityId(),
            HomeAddressId = recipientHome.Id,
            CurrentAddressId = recipientHome.Id,
            PreferredSleepTime = TimeSpan.FromHours(23),
            PreferredWakeTime = TimeSpan.FromHours(7)
        };
        recipient.Objectives.Add(new SleepObjective { Id = state.GenerateEntityId() });
        state.People[caller.Id] = caller;
        state.People[recipient.Id] = recipient;

        var meetupTime = new DateTime(1984, 1, 2, 19, 0, 0);  // 7pm — 5 hrs after 2pm, > 2hr buffer
        var objective = new OrganizeDateObjective(recipient.Id, diner.Id,
            new DateTime(1984, 1, 2, 12, 0, 0), meetupTime,
            new DateTime(1984, 1, 2, 17, 50, 0))
        { Id = state.GenerateEntityId() };
        caller.Objectives.Add(objective);

        // Call GetActions first to trigger Group creation
        objective.GetActions(caller, state, new DateTime(1984, 1, 2, 8, 0, 0), new DateTime(1984, 1, 2, 23, 59, 0));

        objective.OnAccepted(new DateTime(1984, 1, 2, 14, 0, 0), state);

        Assert.Contains(caller.Objectives, o => o is GoOnDateObjective);
        Assert.Contains(recipient.Objectives, o => o is GoOnDateObjective);
        Assert.True(caller.NeedsReplan);
        Assert.True(recipient.NeedsReplan);
        Assert.Equal(ObjectiveStatus.Completed, objective.Status);

        var group = state.Groups.Values.Single();
        Assert.Equal(GroupStatus.Active, group.Status);
        Assert.Equal(meetupTime, group.MeetupTime);
    }

    [Fact]
    public void OnAccepted_TooLateToday_AdvancesToNextDay()
    {
        var state = new SimulationState(new GameClock(new DateTime(1984, 1, 2, 20, 0, 0)));

        var callerHome = new Address { Id = state.GenerateEntityId(), GridX = 2, GridY = 2 };
        var recipientHome = new Address { Id = state.GenerateEntityId(), GridX = 8, GridY = 8 };
        var diner = new Address { Id = state.GenerateEntityId(), GridX = 5, GridY = 5 };
        state.Addresses[callerHome.Id] = callerHome;
        state.Addresses[recipientHome.Id] = recipientHome;
        state.Addresses[diner.Id] = diner;

        var caller = new Person { Id = state.GenerateEntityId(), HomeAddressId = callerHome.Id, CurrentAddressId = callerHome.Id, PreferredSleepTime = TimeSpan.FromHours(23), PreferredWakeTime = TimeSpan.FromHours(7) };
        caller.Objectives.Add(new SleepObjective { Id = state.GenerateEntityId() });
        var recipient = new Person { Id = state.GenerateEntityId(), HomeAddressId = recipientHome.Id, CurrentAddressId = recipientHome.Id, PreferredSleepTime = TimeSpan.FromHours(23), PreferredWakeTime = TimeSpan.FromHours(7) };
        recipient.Objectives.Add(new SleepObjective { Id = state.GenerateEntityId() });
        state.People[caller.Id] = caller;
        state.People[recipient.Id] = recipient;

        var meetupTime = new DateTime(1984, 1, 2, 19, 0, 0);  // 7pm, but it's already 8pm
        var objective = new OrganizeDateObjective(recipient.Id, diner.Id,
            new DateTime(1984, 1, 2, 12, 0, 0), meetupTime,
            new DateTime(1984, 1, 2, 17, 50, 0))
        { Id = state.GenerateEntityId() };
        caller.Objectives.Add(objective);
        objective.GetActions(caller, state, state.Clock.CurrentTime, state.Clock.CurrentTime.AddHours(4));

        objective.OnAccepted(new DateTime(1984, 1, 2, 20, 0, 0), state);

        var group = state.Groups.Values.Single();
        Assert.Equal(new DateTime(1984, 1, 3, 19, 0, 0), group.MeetupTime);  // next day same time
    }
}
