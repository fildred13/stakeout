using System;
using System.Linq;
using Stakeout.Simulation;
using Stakeout.Simulation.Data;
using Stakeout.Simulation.Entities;
using Xunit;

namespace Stakeout.Tests.Simulation;

public class PersonGeneratorTests
{
    private static SimulationState CreateState()
    {
        var state = new SimulationState();
        var mapConfig = new MapConfig();
        var locationGen = new LocationGenerator(mapConfig);
        locationGen.GenerateCityScaffolding(state);
        return state;
    }

    private static PersonGenerator CreateGenerator()
    {
        var mapConfig = new MapConfig();
        return new PersonGenerator(new LocationGenerator(mapConfig), mapConfig);
    }

    [Fact]
    public void GeneratePerson_ReturnsPersonWithValidId()
    {
        var state = CreateState();
        var (person, _) = CreateGenerator().GeneratePerson(state);
        Assert.True(person.Id > 0);
    }

    [Fact]
    public void GeneratePerson_AddsPersonToState()
    {
        var state = CreateState();
        var (person, _) = CreateGenerator().GeneratePerson(state);
        Assert.Contains(person.Id, state.People.Keys);
    }

    [Fact]
    public void GeneratePerson_NameComesFromNameDataPools()
    {
        var state = CreateState();
        var (person, _) = CreateGenerator().GeneratePerson(state);
        Assert.Contains(person.FirstName, NameData.FirstNames);
        Assert.Contains(person.LastName, NameData.LastNames);
    }

    [Fact]
    public void GeneratePerson_CreatesHomeAddress()
    {
        var state = CreateState();
        var (person, _) = CreateGenerator().GeneratePerson(state);
        Assert.True(state.Addresses.ContainsKey(person.HomeAddressId));
        Assert.Equal(AddressCategory.Residential, state.Addresses[person.HomeAddressId].Category);
    }

    [Fact]
    public void GeneratePerson_CreatesJobWithMatchingAddress()
    {
        var state = CreateState();
        var (person, _) = CreateGenerator().GeneratePerson(state);
        Assert.True(state.Jobs.ContainsKey(person.JobId));
        var job = state.Jobs[person.JobId];
        Assert.True(state.Addresses.ContainsKey(job.WorkAddressId));
        Assert.Equal(AddressCategory.Commercial, state.Addresses[job.WorkAddressId].Category);
    }

    [Fact]
    public void GeneratePerson_ReturnsDailySchedule()
    {
        var state = CreateState();
        var (_, schedule) = CreateGenerator().GeneratePerson(state);
        Assert.NotNull(schedule);
        Assert.True(schedule.Entries.Count > 0);
    }

    [Fact]
    public void GeneratePerson_SetsInitialActivity()
    {
        var state = CreateState();
        var (person, _) = CreateGenerator().GeneratePerson(state);
        Assert.True(Enum.IsDefined(person.CurrentActivity));
    }

    [Fact]
    public void GeneratePerson_AppendsJournalEvent()
    {
        var state = CreateState();
        var (person, _) = CreateGenerator().GeneratePerson(state);
        Assert.True(state.Journal.GetEventsForPerson(person.Id).Count > 0);
    }

    [Fact]
    public void GeneratePerson_HasSleepSchedule()
    {
        var state = CreateState();
        var (person, _) = CreateGenerator().GeneratePerson(state);
        var duration = (person.PreferredWakeTime - person.PreferredSleepTime).TotalHours;
        if (duration < 0) duration += 24;
        Assert.Equal(8.0, duration, precision: 1);
    }
}
