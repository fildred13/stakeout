using System;
using Stakeout.Simulation.Data;
using Stakeout.Simulation.Entities;

namespace Stakeout.Simulation;

public class PersonGenerator
{
    private readonly Random _random = new();

    public Person GeneratePerson(SimulationState state)
    {
        var person = new Person
        {
            Id = state.GenerateEntityId(),
            FirstName = NameData.FirstNames[_random.Next(NameData.FirstNames.Length)],
            LastName = NameData.LastNames[_random.Next(NameData.LastNames.Length)],
            CreatedAt = state.Clock.CurrentTime
        };

        state.People[person.Id] = person;
        return person;
    }
}
