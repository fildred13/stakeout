using System.Collections.Generic;
using System.Linq;
using Stakeout.Simulation;
using Stakeout.Simulation.Entities;
using Xunit;

namespace Stakeout.Tests.Simulation;

public class SimulationStateTests
{
    [Fact]
    public void Constructor_InitializesClockAndEmptyCollections()
    {
        var state = new SimulationState();

        Assert.NotNull(state.Clock);
        Assert.Empty(state.People);
        Assert.Empty(state.Jobs);
        Assert.Empty(state.Countries);
        Assert.Empty(state.Cities);
        Assert.Empty(state.Streets);
        Assert.Empty(state.Addresses);
        Assert.Empty(state.Journal.AllEvents);
        Assert.Null(state.Player);
    }

    [Fact]
    public void GenerateEntityId_FirstCall_Returns1()
    {
        var state = new SimulationState();

        Assert.Equal(1, state.GenerateEntityId());
    }

    [Fact]
    public void GenerateEntityId_MultipleCalls_ReturnsIncrementing()
    {
        var state = new SimulationState();

        var id1 = state.GenerateEntityId();
        var id2 = state.GenerateEntityId();
        var id3 = state.GenerateEntityId();

        Assert.Equal(1, id1);
        Assert.Equal(2, id2);
        Assert.Equal(3, id3);
    }

    [Fact]
    public void GetEntityNamesAtAddress_NoPeople_ReturnsEmptyList()
    {
        var state = new SimulationState();
        var address = new Address { Id = 1, Number = 100, StreetId = 1, Type = AddressType.SuburbanHome };
        state.Addresses[address.Id] = address;

        var names = state.GetEntityNamesAtAddress(address);

        Assert.Empty(names);
    }

    [Fact]
    public void GetEntityNamesAtAddress_OnePersonAtAddress_ReturnsTheirName()
    {
        var state = new SimulationState();
        var address = new Address { Id = 1, Number = 100, StreetId = 1, Type = AddressType.SuburbanHome };
        state.Addresses[address.Id] = address;
        state.People[10] = new Person
        {
            Id = 10, FirstName = "James", LastName = "Smith",
            HomeAddressId = 1, JobId = 0, CurrentAddressId = 1
        };

        var names = state.GetEntityNamesAtAddress(address);

        Assert.Single(names);
        Assert.Equal("James Smith", names[0]);
    }

    [Fact]
    public void GetEntityNamesAtAddress_MultiplePeopleAtAddress_ReturnsAllNames()
    {
        var state = new SimulationState();
        var address = new Address { Id = 1, Number = 100, StreetId = 1, Type = AddressType.SuburbanHome };
        state.Addresses[address.Id] = address;
        state.People[10] = new Person
        {
            Id = 10, FirstName = "James", LastName = "Smith",
            HomeAddressId = 1, JobId = 0, CurrentAddressId = 1
        };
        state.People[11] = new Person
        {
            Id = 11, FirstName = "Mary", LastName = "Johnson",
            HomeAddressId = 1, JobId = 0, CurrentAddressId = 1
        };

        var names = state.GetEntityNamesAtAddress(address);

        Assert.Equal(2, names.Count);
        Assert.Contains("James Smith", names);
        Assert.Contains("Mary Johnson", names);
    }

    [Fact]
    public void GetEntityNamesAtAddress_PeopleAtDifferentAddresses_OnlyReturnsMatching()
    {
        var state = new SimulationState();
        var addr1 = new Address { Id = 1, Number = 100, StreetId = 1, Type = AddressType.SuburbanHome };
        var addr2 = new Address { Id = 2, Number = 200, StreetId = 1, Type = AddressType.Office };
        state.Addresses[addr1.Id] = addr1;
        state.Addresses[addr2.Id] = addr2;
        state.People[10] = new Person
        {
            Id = 10, FirstName = "James", LastName = "Smith",
            HomeAddressId = 1, JobId = 0, CurrentAddressId = 1
        };
        state.People[11] = new Person
        {
            Id = 11, FirstName = "Mary", LastName = "Johnson",
            HomeAddressId = 2, JobId = 0, CurrentAddressId = 2
        };

        var names = state.GetEntityNamesAtAddress(addr1);

        Assert.Single(names);
        Assert.Equal("James Smith", names[0]);
    }

    [Fact]
    public void GetEntityNamesAtAddress_MatchesOnCurrentAddressId()
    {
        var state = new SimulationState();
        var home = new Address { Id = 1, Number = 100, StreetId = 1, Type = AddressType.SuburbanHome };
        var work = new Address { Id = 2, Number = 200, StreetId = 1, Type = AddressType.Office };
        state.Addresses[home.Id] = home;
        state.Addresses[work.Id] = work;
        // Person's home is addr1 but they're currently at addr2
        state.People[10] = new Person
        {
            Id = 10, FirstName = "James", LastName = "Smith",
            HomeAddressId = 1, JobId = 0, CurrentAddressId = 2
        };

        var namesAtHome = state.GetEntityNamesAtAddress(home);
        var namesAtWork = state.GetEntityNamesAtAddress(work);

        Assert.Empty(namesAtHome);
        Assert.Single(namesAtWork);
        Assert.Equal("James Smith", namesAtWork[0]);
    }
}
