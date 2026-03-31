using System;
using System.Linq;
using Stakeout.Simulation;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Objectives;
using Xunit;

namespace Stakeout.Tests.Simulation;

public class SimulationStateRelationshipTests
{
    private static (SimulationState state, Person personA, Person personB) BuildTwoPeople()
    {
        var state = new SimulationState(new GameClock(new DateTime(1984, 1, 2, 9, 0, 0)));

        var homeA = new Address { Id = state.GenerateEntityId(), GridX = 2, GridY = 2 };
        var homeB = new Address { Id = state.GenerateEntityId(), GridX = 8, GridY = 8 };
        state.Addresses[homeA.Id] = homeA;
        state.Addresses[homeB.Id] = homeB;

        var personA = new Person
        {
            Id = state.GenerateEntityId(),
            HomeAddressId = homeA.Id,
            CurrentAddressId = homeA.Id
        };
        var personB = new Person
        {
            Id = state.GenerateEntityId(),
            HomeAddressId = homeB.Id,
            CurrentAddressId = homeB.Id
        };
        state.People[personA.Id] = personA;
        state.People[personB.Id] = personB;

        return (state, personA, personB);
    }

    [Fact]
    public void AddRelationship_Dating_AddsMaintainObjectiveToBothPeople()
    {
        var (state, personA, personB) = BuildTwoPeople();

        state.AddRelationship(new Relationship
        {
            Id = state.GenerateEntityId(),
            PersonAId = personA.Id,
            PersonBId = personB.Id,
            Type = RelationshipType.Dating,
            StartedAt = state.Clock.CurrentTime
        });

        Assert.Contains(personA.Objectives,
            o => o is MaintainRelationshipObjective m && m.PartnerPersonId == personB.Id);
        Assert.Contains(personB.Objectives,
            o => o is MaintainRelationshipObjective m && m.PartnerPersonId == personA.Id);
    }

    [Fact]
    public void AddRelationship_Dating_CalledTwice_DoesNotDuplicateObjective()
    {
        var (state, personA, personB) = BuildTwoPeople();

        var rel = new Relationship
        {
            Id = state.GenerateEntityId(),
            PersonAId = personA.Id,
            PersonBId = personB.Id,
            Type = RelationshipType.Dating,
            StartedAt = state.Clock.CurrentTime
        };
        state.AddRelationship(rel);
        state.AddRelationship(rel);

        Assert.Single(personA.Objectives.OfType<MaintainRelationshipObjective>(),
            m => m.PartnerPersonId == personB.Id);
        Assert.Single(personB.Objectives.OfType<MaintainRelationshipObjective>(),
            m => m.PartnerPersonId == personA.Id);
    }

    [Fact]
    public void AddRelationship_Friend_DoesNotAddMaintainObjective()
    {
        var (state, personA, personB) = BuildTwoPeople();

        state.AddRelationship(new Relationship
        {
            Id = state.GenerateEntityId(),
            PersonAId = personA.Id,
            PersonBId = personB.Id,
            Type = RelationshipType.Friend,
            StartedAt = state.Clock.CurrentTime
        });

        Assert.DoesNotContain(personA.Objectives, o => o is MaintainRelationshipObjective);
        Assert.DoesNotContain(personB.Objectives, o => o is MaintainRelationshipObjective);
    }

    [Fact]
    public void AddRelationship_PersonNotInState_DoesNotThrow()
    {
        var state = new SimulationState(new GameClock());

        // PersonAId = 999 doesn't exist in state.People — should not throw
        var ex = Record.Exception(() => state.AddRelationship(new Relationship
        {
            Id = state.GenerateEntityId(),
            PersonAId = 999,
            PersonBId = 998,
            Type = RelationshipType.Dating,
            StartedAt = state.Clock.CurrentTime
        }));

        Assert.Null(ex);
    }
}
