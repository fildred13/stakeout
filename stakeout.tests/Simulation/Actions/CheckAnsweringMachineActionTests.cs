using System;
using System.Collections.Generic;
using System.Linq;
using Stakeout.Simulation;
using Stakeout.Simulation.Actions;
using Stakeout.Simulation.Actions.Telephone;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Fixtures;
using Stakeout.Simulation.Objectives;
using Stakeout.Simulation.Traces;
using Xunit;

namespace Stakeout.Tests.Simulation.Actions;

public class CheckAnsweringMachineActionTests
{
    [Fact]
    public void CheckAnsweringMachine_MessageFromKnownContact_CreatesCallBackObjective()
    {
        var state = new SimulationState(new GameClock(new DateTime(1984, 1, 2, 18, 0, 0)));

        var callerHome = new Address { Id = state.GenerateEntityId(), GridX = 2, GridY = 2 };
        var recipientHome = new Address { Id = state.GenerateEntityId(), GridX = 8, GridY = 2 };
        foreach (var a in new[] { callerHome, recipientHome })
            state.Addresses[a.Id] = a;

        var loc = new Location { Id = state.GenerateEntityId(), AddressId = recipientHome.Id };
        state.Locations[loc.Id] = loc;
        recipientHome.LocationIds.Add(loc.Id);

        var phone = new Fixture { Id = state.GenerateEntityId(), Type = FixtureType.Telephone, LocationId = loc.Id };
        state.Fixtures[phone.Id] = phone;

        var callerPhoneLoc = new Location { Id = state.GenerateEntityId(), AddressId = callerHome.Id };
        state.Locations[callerPhoneLoc.Id] = callerPhoneLoc;
        callerHome.LocationIds.Add(callerPhoneLoc.Id);
        var callerPhone = new Fixture { Id = state.GenerateEntityId(), Type = FixtureType.Telephone, LocationId = callerPhoneLoc.Id };
        state.Fixtures[callerPhone.Id] = callerPhone;

        var caller = new Person
        {
            Id = state.GenerateEntityId(),
            HomeAddressId = callerHome.Id,
            HomePhoneFixtureId = callerPhone.Id
        };
        var recipient = new Person
        {
            Id = state.GenerateEntityId(),
            HomeAddressId = recipientHome.Id,
            HomePhoneFixtureId = phone.Id
        };
        state.People[caller.Id] = caller;
        state.People[recipient.Id] = recipient;

        // Establish Dating relationship
        var rel = new Relationship
        {
            Id = state.GenerateEntityId(),
            PersonAId = caller.Id, PersonBId = recipient.Id,
            Type = RelationshipType.Dating
        };
        state.AddRelationship(rel);

        // Leave a message trace on recipient's phone
        TraceEmitter.EmitRecord(state, phone.Id, caller.Id,
            $"Message from {caller.FirstName} {caller.LastName}: please call back");

        var action = new CheckAnsweringMachineAction();
        var ctx = new ActionContext
        {
            Person = recipient,
            State = state,
            EventJournal = state.Journal,
            Random = new Random(1),
            CurrentTime = state.Clock.CurrentTime
        };

        action.OnStart(ctx);
        action.Tick(ctx, TimeSpan.FromMinutes(2));
        action.OnComplete(ctx);

        Assert.Contains(recipient.Objectives, o => o is CallBackObjective cb && cb.TargetPersonId == caller.Id);
    }

    [Fact]
    public void CheckAnsweringMachine_NoMessages_NoCallBackObjective()
    {
        var state = new SimulationState(new GameClock(new DateTime(1984, 1, 2, 18, 0, 0)));
        var home = new Address { Id = state.GenerateEntityId(), GridX = 5, GridY = 5 };
        state.Addresses[home.Id] = home;
        var loc = new Location { Id = state.GenerateEntityId(), AddressId = home.Id };
        state.Locations[loc.Id] = loc;
        home.LocationIds.Add(loc.Id);
        var phone = new Fixture { Id = state.GenerateEntityId(), Type = FixtureType.Telephone, LocationId = loc.Id };
        state.Fixtures[phone.Id] = phone;

        var person = new Person { Id = state.GenerateEntityId(), HomeAddressId = home.Id, HomePhoneFixtureId = phone.Id };
        state.People[person.Id] = person;

        var action = new CheckAnsweringMachineAction();
        var ctx = new ActionContext { Person = person, State = state, EventJournal = state.Journal, Random = new Random(1), CurrentTime = state.Clock.CurrentTime };
        action.OnStart(ctx);
        action.Tick(ctx, TimeSpan.FromMinutes(2));
        action.OnComplete(ctx);

        Assert.DoesNotContain(person.Objectives, o => o is CallBackObjective);
    }
}
