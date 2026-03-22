using System.Linq;
using Stakeout.Simulation;
using Stakeout.Simulation.Data;
using Stakeout.Simulation.Entities;
using Xunit;

namespace Stakeout.Tests.Simulation;

public class PersonGeneratorTests
{
    private static SimulationState CreatePopulatedState()
    {
        var state = new SimulationState();
        var locationGen = new LocationGenerator();
        locationGen.GenerateCity(state);
        return state;
    }

    [Fact]
    public void GeneratePerson_ReturnsPersonWithValidId()
    {
        var state = CreatePopulatedState();
        var generator = new PersonGenerator();

        var person = generator.GeneratePerson(state);

        Assert.True(person.Id > 0);
    }

    [Fact]
    public void GeneratePerson_AddsPersonToState()
    {
        var state = CreatePopulatedState();
        var generator = new PersonGenerator();

        var person = generator.GeneratePerson(state);

        Assert.Contains(person.Id, state.People.Keys);
        Assert.Same(person, state.People[person.Id]);
    }

    [Fact]
    public void GeneratePerson_NameComesFromNameDataPools()
    {
        var state = CreatePopulatedState();
        var generator = new PersonGenerator();

        var person = generator.GeneratePerson(state);

        Assert.Contains(person.FirstName, NameData.FirstNames);
        Assert.Contains(person.LastName, NameData.LastNames);
    }

    [Fact]
    public void GeneratePerson_HomeAddressIsResidential()
    {
        var state = CreatePopulatedState();
        var generator = new PersonGenerator();

        var person = generator.GeneratePerson(state);

        var home = state.Addresses[person.HomeAddressId];
        Assert.Equal(AddressCategory.Residential, home.Category);
    }

    [Fact]
    public void GeneratePerson_WorkAddressIsCommercial()
    {
        var state = CreatePopulatedState();
        var generator = new PersonGenerator();

        var person = generator.GeneratePerson(state);

        var work = state.Addresses[person.WorkAddressId];
        Assert.Equal(AddressCategory.Commercial, work.Category);
    }

    [Fact]
    public void GeneratePerson_CurrentAddressStartsAtHome()
    {
        var state = CreatePopulatedState();
        var generator = new PersonGenerator();

        var person = generator.GeneratePerson(state);

        Assert.Equal(person.HomeAddressId, person.CurrentAddressId);
    }

    [Fact]
    public void GeneratePerson_SetsCreatedAtToClockTime()
    {
        var state = CreatePopulatedState();
        var generator = new PersonGenerator();

        var person = generator.GeneratePerson(state);

        Assert.Equal(state.Clock.CurrentTime, person.CreatedAt);
    }

    [Fact]
    public void GeneratePerson_MultiplePeople_GetUniqueIds()
    {
        var state = CreatePopulatedState();
        var generator = new PersonGenerator();

        var p1 = generator.GeneratePerson(state);
        var p2 = generator.GeneratePerson(state);
        var p3 = generator.GeneratePerson(state);

        Assert.NotEqual(p1.Id, p2.Id);
        Assert.NotEqual(p2.Id, p3.Id);
        Assert.NotEqual(p1.Id, p3.Id);
    }
}
