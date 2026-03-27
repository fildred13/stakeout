using System.Collections.Generic;
using System.Linq;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Events;
using Stakeout.Simulation.Crimes;
using Stakeout.Simulation.Traces;

namespace Stakeout.Simulation;

public class SimulationState
{
    public GameClock Clock { get; }
    public Dictionary<int, Person> People { get; } = new();
    public Dictionary<int, Job> Jobs { get; } = new();
    public Player Player { get; set; }
    public List<Country> Countries { get; } = new();
    public Dictionary<int, City> Cities { get; } = new();
    public Dictionary<int, Street> Streets { get; } = new();
    public Dictionary<int, Address> Addresses { get; } = new();
    public EventJournal Journal { get; } = new();
    public Dictionary<int, Crime> Crimes { get; } = new();
    public Dictionary<int, Trace> Traces { get; } = new();
    public Dictionary<int, Item> Items { get; } = new();
    public Dictionary<int, Sublocation> Sublocations { get; } = new();
    public List<SublocationConnection> SublocationConnections { get; } = new();

    private int _nextEntityId = 1;

    public SimulationState(GameClock clock = null)
    {
        Clock = clock ?? new GameClock();
    }

    public int GenerateEntityId() => _nextEntityId++;

    public List<string> GetEntityNamesAtAddress(Address address)
    {
        return People.Values
            .Where(p => p.CurrentAddressId.HasValue && p.CurrentAddressId.Value == address.Id)
            .Select(p => p.FullName)
            .ToList();
    }
}
