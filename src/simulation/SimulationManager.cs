using System;
using System.Linq;
using Godot;
using Stakeout.Simulation.Entities;

namespace Stakeout.Simulation;

public partial class SimulationManager : Node
{
    public SimulationState State { get; private set; }

    public event Action<Person> PersonAdded;
    public event Action<Address> AddressAdded;
    public event Action PlayerCreated;

    private readonly PersonGenerator _personGenerator = new();
    private readonly LocationGenerator _locationGenerator = new();
    private bool _initialPeopleGenerated;

    private const int InitialPersonCount = 5;

    public SimulationManager(SimulationState state)
    {
        State = state;
    }

    public override void _Ready()
    {
        _locationGenerator.GenerateCity(State);

        foreach (var address in State.Addresses.Values)
        {
            AddressAdded?.Invoke(address);
        }

        var residentialAddresses = State.Addresses.Values
            .Where(a => a.Category == AddressCategory.Residential).ToList();
        var random = new Random();
        var homeAddress = residentialAddresses[random.Next(residentialAddresses.Count)];

        State.Player = new Player
        {
            HomeAddressId = homeAddress.Id,
            CurrentAddressId = homeAddress.Id
        };
        PlayerCreated?.Invoke();
    }

    public override void _Process(double delta)
    {
        State.Clock.Tick(delta);

        if (!_initialPeopleGenerated && State.Clock.ElapsedSeconds >= 1.0)
        {
            for (int i = 0; i < InitialPersonCount; i++)
            {
                var person = _personGenerator.GeneratePerson(State);
                PersonAdded?.Invoke(person);
            }
            _initialPeopleGenerated = true;
        }
    }
}
