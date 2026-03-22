using System.Collections.Generic;
using System.Linq;
using Stakeout.Simulation.Entities;

namespace Stakeout.Simulation;

public class SimulationState
{
    public GameClock Clock { get; }
    public Dictionary<int, Person> People { get; } = new();
    public Player Player { get; set; }
    public List<Country> Countries { get; } = new();
    public Dictionary<int, City> Cities { get; } = new();
    public Dictionary<int, Street> Streets { get; } = new();
    public Dictionary<int, Address> Addresses { get; } = new();

    private int _nextEntityId = 1;

    public SimulationState()
    {
        Clock = new GameClock();
    }

    public int GenerateEntityId() => _nextEntityId++;

    public List<string> GetEntityNamesAtAddress(Address address)
    {
        return People.Values
            .Where(p => p.CurrentAddressId == address.Id)
            .Select(p => p.FullName)
            .ToList();
    }
}
