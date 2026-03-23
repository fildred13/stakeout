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

    private readonly MapConfig _mapConfig = new();
    private readonly PersonGenerator _personGenerator;
    private readonly LocationGenerator _locationGenerator;
    private bool _initialPeopleGenerated;

    private const int InitialPersonCount = 5;

    public SimulationManager(SimulationState state)
    {
        State = state;
        _locationGenerator = new LocationGenerator(_mapConfig);
        _personGenerator = new PersonGenerator(_locationGenerator, _mapConfig);
    }

    public override void _Ready()
    {
        _locationGenerator.GenerateCityScaffolding(State);

        // Generate a home address for the player
        var homeAddress = _locationGenerator.GenerateAddress(State, AddressType.SuburbanHome);
        AddressAdded?.Invoke(homeAddress);

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
                var (person, _) = _personGenerator.GeneratePerson(State);
                PersonAdded?.Invoke(person);

                // Notify about newly created addresses
                foreach (var address in State.Addresses.Values)
                {
                    AddressAdded?.Invoke(address);
                }
            }
            _initialPeopleGenerated = true;
        }
    }
}
