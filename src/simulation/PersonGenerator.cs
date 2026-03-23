using System;
using System.Collections.Generic;
using System.Linq;
using Stakeout.Simulation.Data;
using Stakeout.Simulation.Entities;

namespace Stakeout.Simulation;

public class PersonGenerator
{
    private readonly Random _random = new();

    public Person GeneratePerson(SimulationState state)
    {
        var residentialAddresses = state.Addresses.Values
            .Where(a => a.Category == AddressCategory.Residential).ToList();

        var homeAddress = residentialAddresses[_random.Next(residentialAddresses.Count)];

        var person = new Person
        {
            Id = state.GenerateEntityId(),
            FirstName = NameData.FirstNames[_random.Next(NameData.FirstNames.Length)],
            LastName = NameData.LastNames[_random.Next(NameData.LastNames.Length)],
            CreatedAt = state.Clock.CurrentTime,
            HomeAddressId = homeAddress.Id,
            JobId = 0,
            CurrentActivity = ActivityType.AtHome
        };
        person.CurrentAddressId = person.HomeAddressId;
        person.CurrentPosition = homeAddress.Position;

        state.People[person.Id] = person;
        return person;
    }
}
