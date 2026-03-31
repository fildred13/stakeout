using System;
using System.Linq;
using Stakeout.Simulation;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Objectives;
using Xunit;

namespace Stakeout.Tests.Simulation.Objectives;

public class MaintainRelationshipObjectiveTests
{
    private static (SimulationState state, Person person, Person partner, Address diner)
        BuildScene()
    {
        var state = new SimulationState(new GameClock(new DateTime(1984, 1, 2, 9, 0, 0)));

        var personHome = new Address { Id = state.GenerateEntityId(), GridX = 2, GridY = 2 };
        var partnerHome = new Address { Id = state.GenerateEntityId(), GridX = 8, GridY = 8 };
        var diner = new Address { Id = state.GenerateEntityId(), GridX = 5, GridY = 5,
            Type = AddressType.Diner };
        state.Addresses[personHome.Id] = personHome;
        state.Addresses[partnerHome.Id] = partnerHome;
        state.Addresses[diner.Id] = diner;

        var person = new Person
        {
            Id = state.GenerateEntityId(),
            HomeAddressId = personHome.Id,
            CurrentAddressId = personHome.Id,
            PreferredSleepTime = TimeSpan.FromHours(23),
            PreferredWakeTime = TimeSpan.FromHours(7)
        };
        person.Objectives.Add(new SleepObjective { Id = state.GenerateEntityId() });

        var partner = new Person
        {
            Id = state.GenerateEntityId(),
            HomeAddressId = partnerHome.Id,
            CurrentAddressId = partnerHome.Id,
            HomePhoneFixtureId = null
        };

        state.People[person.Id] = person;
        state.People[partner.Id] = partner;

        return (state, person, partner, diner);
    }

    [Fact]
    public void GetActions_NoPriorDate_AddsOrganizeDateObjectiveAndSetsNeedsReplan()
    {
        var (state, person, partner, _) = BuildScene();
        var obj = new MaintainRelationshipObjective(partner.Id) { Id = state.GenerateEntityId() };

        var actions = obj.GetActions(person, state,
            new DateTime(1984, 1, 2, 9, 0, 0), new DateTime(1984, 1, 2, 23, 59, 0));

        Assert.Empty(actions);
        Assert.Contains(person.Objectives,
            o => o is OrganizeDateObjective od && od.TargetPersonId == partner.Id);
        Assert.True(person.NeedsReplan);
    }

    [Fact]
    public void GetActions_OrganizeDateObjectiveAlreadyActive_NoNewObjectiveAdded()
    {
        var (state, person, partner, diner) = BuildScene();
        var existing = new OrganizeDateObjective(partner.Id, diner.Id,
            new DateTime(1984, 1, 2, 12, 0, 0),
            new DateTime(1984, 1, 2, 19, 0, 0),
            new DateTime(1984, 1, 2, 17, 50, 0))
        { Id = state.GenerateEntityId() };
        person.Objectives.Add(existing);

        var obj = new MaintainRelationshipObjective(partner.Id) { Id = state.GenerateEntityId() };
        obj.GetActions(person, state,
            new DateTime(1984, 1, 2, 9, 0, 0), new DateTime(1984, 1, 2, 23, 59, 0));

        Assert.Single(person.Objectives.OfType<OrganizeDateObjective>());
        Assert.False(person.NeedsReplan);
    }

    [Fact]
    public void GetActions_GoOnDateObjectiveActive_NoNewObjectiveAdded()
    {
        var (state, person, partner, _) = BuildScene();
        var group = new Group
        {
            Id = state.GenerateEntityId(),
            Status = GroupStatus.Active,
            MemberPersonIds = new System.Collections.Generic.List<int> { person.Id, partner.Id }
        };
        state.Groups[group.Id] = group;
        person.Objectives.Add(new GoOnDateObjective(group.Id) { Id = state.GenerateEntityId() });

        var obj = new MaintainRelationshipObjective(partner.Id) { Id = state.GenerateEntityId() };
        obj.GetActions(person, state,
            new DateTime(1984, 1, 2, 9, 0, 0), new DateTime(1984, 1, 2, 23, 59, 0));

        Assert.DoesNotContain(person.Objectives, o => o is OrganizeDateObjective);
        Assert.False(person.NeedsReplan);
    }

    [Fact]
    public void GetActions_RecentDate_NoNewObjectiveAdded()
    {
        var (state, person, partner, diner) = BuildScene();

        // A date that happened 3 days ago — within the 7-day cooldown
        var recentGroup = new Group
        {
            Id = state.GenerateEntityId(),
            Type = GroupType.Date,
            Status = GroupStatus.Disbanded,
            MeetupTime = new DateTime(1984, 1, 2, 9, 0, 0).AddDays(-3),
            MemberPersonIds = new System.Collections.Generic.List<int> { person.Id, partner.Id }
        };
        state.Groups[recentGroup.Id] = recentGroup;

        var obj = new MaintainRelationshipObjective(partner.Id) { Id = state.GenerateEntityId() };
        obj.GetActions(person, state,
            new DateTime(1984, 1, 2, 9, 0, 0), new DateTime(1984, 1, 2, 23, 59, 0));

        Assert.DoesNotContain(person.Objectives, o => o is OrganizeDateObjective);
    }

    [Fact]
    public void GetActions_NoDinerInState_NoNewObjectiveAdded()
    {
        var (state, person, partner, diner) = BuildScene();
        // Remove the diner
        state.Addresses.Remove(diner.Id);

        var obj = new MaintainRelationshipObjective(partner.Id) { Id = state.GenerateEntityId() };
        obj.GetActions(person, state,
            new DateTime(1984, 1, 2, 9, 0, 0), new DateTime(1984, 1, 2, 23, 59, 0));

        Assert.DoesNotContain(person.Objectives, o => o is OrganizeDateObjective);
        Assert.False(person.NeedsReplan);
    }

    [Fact]
    public void GetActions_AlwaysReturnsEmptyList()
    {
        var (state, person, partner, _) = BuildScene();
        var obj = new MaintainRelationshipObjective(partner.Id) { Id = state.GenerateEntityId() };

        var actions = obj.GetActions(person, state,
            new DateTime(1984, 1, 2, 9, 0, 0), new DateTime(1984, 1, 2, 23, 59, 0));

        Assert.Empty(actions);
    }
}
