using System;
using System.Linq;
using Stakeout.Simulation;
using Stakeout.Simulation.Actions;
using Stakeout.Simulation.Actions.Telephone;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Fixtures;
using Stakeout.Simulation.Traces;
using Xunit;

namespace Stakeout.Tests.Simulation.Actions;

public class PhoneCallActionTests
{
    private static (SimulationState state, Person caller, Person recipient, Fixture phone, Group group)
        BuildScene(bool recipientIsHome)
    {
        var state = new SimulationState(new GameClock(new DateTime(1984, 1, 2, 12, 0, 0)));

        var callerHome = new Address { Id = state.GenerateEntityId(), GridX = 2, GridY = 2 };
        var recipientHomeAddr = new Address { Id = state.GenerateEntityId(), GridX = 8, GridY = 8 };
        state.Addresses[callerHome.Id] = callerHome;
        state.Addresses[recipientHomeAddr.Id] = recipientHomeAddr;

        var loc = new Location { Id = state.GenerateEntityId(), AddressId = recipientHomeAddr.Id };
        state.Locations[loc.Id] = loc;
        recipientHomeAddr.LocationIds.Add(loc.Id);

        var phone = new Fixture { Id = state.GenerateEntityId(), Type = FixtureType.Telephone, LocationId = loc.Id };
        state.Fixtures[phone.Id] = phone;

        var callerPerson = new Person
        {
            Id = state.GenerateEntityId(),
            FirstName = "Guy",
            LastName = "Test",
            HomeAddressId = callerHome.Id,
            CurrentAddressId = callerHome.Id,
            HomePhoneFixtureId = null
        };
        state.People[callerPerson.Id] = callerPerson;

        var recipientPerson = new Person
        {
            Id = state.GenerateEntityId(),
            FirstName = "Girl",
            LastName = "Test",
            HomeAddressId = recipientHomeAddr.Id,
            CurrentAddressId = recipientIsHome ? recipientHomeAddr.Id : callerHome.Id
        };
        state.People[recipientPerson.Id] = recipientPerson;

        var group = new Group
        {
            Id = state.GenerateEntityId(),
            Status = GroupStatus.Forming,
            MemberPersonIds = new System.Collections.Generic.List<int> { callerPerson.Id, recipientPerson.Id }
        };
        state.Groups[group.Id] = group;

        return (state, callerPerson, recipientPerson, phone, group);
    }

    [Fact]
    public void PhoneCallAction_RecipientHome_CreatesPendingInvitation()
    {
        var (state, caller, recipient, phone, group) = BuildScene(recipientIsHome: true);

        var action = new PhoneCallAction(
            targetAddressId: recipient.HomeAddressId,
            targetFixtureId: phone.Id,
            proposedGroupId: group.Id,
            callerId: caller.Id,
            recipientId: recipient.Id);

        var ctx = new ActionContext
        {
            Person = caller,
            State = state,
            EventJournal = state.Journal,
            Random = new Random(1),
            CurrentTime = state.Clock.CurrentTime
        };

        action.OnStart(ctx);
        var status = ActionStatus.Running;
        for (int i = 0; i < 11 && status == ActionStatus.Running; i++)
            status = action.Tick(ctx, TimeSpan.FromMinutes(1));
        action.OnComplete(ctx);

        Assert.True(state.PendingInvitationsByPersonId.ContainsKey(recipient.Id));
        Assert.Single(state.PendingInvitationsByPersonId[recipient.Id]);

        var phoneTraces = state.GetTracesForFixture(phone.Id, state.Clock.CurrentTime);
        Assert.NotEmpty(phoneTraces);
    }

    [Fact]
    public void PhoneCallAction_RecipientAbsent_LeavesMessageTrace()
    {
        var (state, caller, recipient, phone, group) = BuildScene(recipientIsHome: false);

        // Give caller an OrganizeDateObjective so OnMessageLeft can be called
        var objective = new Stakeout.Simulation.Objectives.OrganizeDateObjective(
            recipient.Id, 99, DateTime.Now, DateTime.Now.AddHours(7), DateTime.Now.AddHours(5));
        objective.Id = state.GenerateEntityId();
        caller.Objectives.Add(objective);

        var action = new PhoneCallAction(
            targetAddressId: recipient.HomeAddressId,
            targetFixtureId: phone.Id,
            proposedGroupId: group.Id,
            callerId: caller.Id,
            recipientId: recipient.Id);

        var ctx = new ActionContext
        {
            Person = caller,
            State = state,
            EventJournal = state.Journal,
            Random = new Random(1),
            CurrentTime = state.Clock.CurrentTime
        };

        action.OnStart(ctx);
        for (int i = 0; i < 11; i++)
            action.Tick(ctx, TimeSpan.FromMinutes(1));
        action.OnComplete(ctx);

        Assert.False(state.PendingInvitationsByPersonId.ContainsKey(recipient.Id));

        var phoneTraces = state.GetTracesForFixture(phone.Id, state.Clock.CurrentTime);
        Assert.Contains(phoneTraces, t => t.Description != null && t.Description.Contains("please call back"));
    }
}
