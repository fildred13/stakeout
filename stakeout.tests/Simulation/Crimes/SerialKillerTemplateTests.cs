using System.Linq;
using Stakeout.Simulation;
using Stakeout.Simulation.Crimes;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Objectives;
using Xunit;

namespace Stakeout.Tests.Simulation.Crimes;

public class SerialKillerTemplateTests
{
    private static SimulationState CreateStateWithPeople(int count)
    {
        var state = new SimulationState();
        for (int i = 0; i < count; i++)
        {
            var addressId = state.GenerateEntityId();
            var address = new Address { Id = addressId };
            state.Addresses[addressId] = address;

            var personId = state.GenerateEntityId();
            var person = new Person
            {
                Id = personId,
                FirstName = $"Person{i}",
                LastName = "Test",
                HomeAddressId = addressId,
                IsAlive = true
            };
            state.People[personId] = person;
        }
        return state;
    }

    [Fact]
    public void Instantiate_CreatesCrime_WithKillerRoleNotNull()
    {
        var state = CreateStateWithPeople(3);
        var template = new SerialKillerTemplate();

        var crime = template.Instantiate(state);

        Assert.NotNull(crime);
        Assert.True(crime.Roles.ContainsKey("Killer"));
        Assert.NotNull(crime.Roles["Killer"]);
        Assert.True(state.People.ContainsKey(crime.Roles["Killer"].Value));
    }

    [Fact]
    public void Instantiate_VictimRole_StartsNull()
    {
        var state = CreateStateWithPeople(3);
        var template = new SerialKillerTemplate();

        var crime = template.Instantiate(state);

        Assert.NotNull(crime);
        Assert.True(crime.Roles.ContainsKey("Victim"));
        Assert.Null(crime.Roles["Victim"]);
    }

    [Fact]
    public void Instantiate_InjectsCommitMurderObjective_IntoKiller()
    {
        var state = CreateStateWithPeople(3);
        var template = new SerialKillerTemplate();

        var crime = template.Instantiate(state);

        Assert.NotNull(crime);
        var killerId = crime.Roles["Killer"].Value;
        var killer = state.People[killerId];
        Assert.Single(killer.Objectives);
        Assert.Equal(ObjectiveType.CommitMurder, killer.Objectives[0].Type);
        Assert.Equal(3, killer.Objectives[0].Steps.Count);
    }

    [Fact]
    public void Instantiate_AddsCrime_ToStateCrimes()
    {
        var state = CreateStateWithPeople(3);
        var template = new SerialKillerTemplate();

        var crime = template.Instantiate(state);

        Assert.NotNull(crime);
        Assert.True(state.Crimes.ContainsKey(crime.Id));
        Assert.Same(crime, state.Crimes[crime.Id]);
    }

    [Fact]
    public void Instantiate_WithOnlyOnePerson_ReturnsNull()
    {
        var state = CreateStateWithPeople(1);
        var template = new SerialKillerTemplate();

        var crime = template.Instantiate(state);

        Assert.Null(crime);
    }
}
