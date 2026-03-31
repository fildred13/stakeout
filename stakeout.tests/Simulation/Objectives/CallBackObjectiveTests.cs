using System;
using System.Linq;
using Stakeout.Simulation;
using Stakeout.Simulation.Actions.Telephone;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Objectives;
using Xunit;

namespace Stakeout.Tests.Simulation.Objectives;

public class CallBackObjectiveTests
{
    [Fact]
    public void GetActions_ReturnsPhoneCallAction_TargetingCallerPhone()
    {
        var state = new SimulationState(new GameClock(new DateTime(1984, 1, 2, 18, 30, 0)));

        var callerHome = new Address { Id = state.GenerateEntityId(), GridX = 2, GridY = 2 };
        var recipientHome = new Address { Id = state.GenerateEntityId(), GridX = 8, GridY = 2 };
        var loc = new Location { Id = state.GenerateEntityId(), AddressId = callerHome.Id };
        var phone = new Stakeout.Simulation.Fixtures.Fixture
        {
            Id = state.GenerateEntityId(),
            Type = Stakeout.Simulation.Fixtures.FixtureType.Telephone,
            LocationId = loc.Id
        };
        state.Addresses[callerHome.Id] = callerHome;
        state.Addresses[recipientHome.Id] = recipientHome;
        state.Locations[loc.Id] = loc;
        callerHome.LocationIds.Add(loc.Id);
        state.Fixtures[phone.Id] = phone;

        var caller = new Person { Id = state.GenerateEntityId(), HomeAddressId = callerHome.Id, HomePhoneFixtureId = phone.Id };
        var recipient = new Person { Id = state.GenerateEntityId(), HomeAddressId = recipientHome.Id };
        state.People[caller.Id] = caller;
        state.People[recipient.Id] = recipient;

        // Give caller an OrganizeDateObjective (the state machine target)
        var organizeObj = new OrganizeDateObjective(recipient.Id, 99,
            new DateTime(1984, 1, 2, 12, 0, 0),
            new DateTime(1984, 1, 2, 19, 0, 0),
            new DateTime(1984, 1, 2, 17, 50, 0))
        { Id = state.GenerateEntityId() };
        caller.Objectives.Add(organizeObj);

        var callbackObj = new CallBackObjective(caller.Id, phone.Id, callerHome.Id)
        { Id = state.GenerateEntityId() };

        var actions = callbackObj.GetActions(recipient, state,
            new DateTime(1984, 1, 2, 18, 30, 0), new DateTime(1984, 1, 2, 23, 59, 0));

        Assert.Single(actions);
        Assert.IsType<PhoneCallAction>(actions[0].Action);
        Assert.Equal(callerHome.Id, actions[0].TargetAddressId);
        Assert.Equal(ObjectiveSource.Social, callbackObj.Source);
        Assert.Equal(10, callbackObj.Priority);
    }
}
