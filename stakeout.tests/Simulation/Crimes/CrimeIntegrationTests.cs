// stakeout.tests/Simulation/Crimes/CrimeIntegrationTests.cs
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Stakeout.Simulation;
using Stakeout.Simulation.Crimes;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Traces;
using Xunit;

namespace Stakeout.Tests.Simulation.Crimes;

public class CrimeIntegrationTests
{
    [Fact]
    public void CrimeGeneration_SerialKiller_AssignsKillerRole()
    {
        // Setup: state with 2 people
        var state = new SimulationState(new GameClock(new DateTime(1980, 1, 1, 0, 0, 0)));
        var street = new Street { Id = state.GenerateEntityId(), Name = "Oak St", CityId = 1 };
        state.Streets[street.Id] = street;

        var homeA = new Address { Id = state.GenerateEntityId(), GridX = 2, GridY = 2,
            Type = AddressType.SuburbanHome, Number = 10, StreetId = street.Id };
        var homeB = new Address { Id = state.GenerateEntityId(), GridX = 10, GridY = 2,
            Type = AddressType.SuburbanHome, Number = 20, StreetId = street.Id };
        state.Addresses[homeA.Id] = homeA;
        state.Addresses[homeB.Id] = homeB;

        var personA = new Person
        {
            Id = state.GenerateEntityId(), FirstName = "Alice", LastName = "A",
            IsAlive = true, HomeAddressId = homeA.Id, JobId = 0,
            CurrentAddressId = homeA.Id, CurrentPosition = homeA.Position
        };
        var personB = new Person
        {
            Id = state.GenerateEntityId(), FirstName = "Bob", LastName = "B",
            IsAlive = true, HomeAddressId = homeB.Id, JobId = 0,
            CurrentAddressId = homeB.Id, CurrentPosition = homeB.Position
        };
        state.People[personA.Id] = personA;
        state.People[personB.Id] = personB;

        // Generate crime
        var generator = new CrimeGenerator();
        var crime = generator.Generate(CrimeTemplateType.SerialKiller, state);
        Assert.NotNull(crime);
        Assert.Equal(CrimeStatus.InProgress, crime.Status);
        Assert.True(crime.Roles.ContainsKey("Killer"));
        Assert.NotNull(crime.Roles["Killer"]);

        // TODO: Project 3 — full pipeline test will be restored when crime objectives are implemented
    }
}
