using System;
using System.Collections.Generic;
using Godot;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Scheduling;

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
    private readonly PersonBehavior _personBehavior;

    private readonly Dictionary<int, DailySchedule> _schedules = new();

    public SimulationManager(SimulationState state)
    {
        State = state;
        _locationGenerator = new LocationGenerator(_mapConfig);
        _personGenerator = new PersonGenerator(_locationGenerator, _mapConfig);
        _personBehavior = new PersonBehavior(_mapConfig);
    }

    public override void _Ready()
    {
        _locationGenerator.GenerateCityScaffolding(State);

        // Generate 1 person
        var knownAddressIds = new HashSet<int>(State.Addresses.Keys);
        var (person, schedule) = _personGenerator.GeneratePerson(State);
        _schedules[person.Id] = schedule;

        // Emit events for newly created addresses
        foreach (var address in State.Addresses.Values)
        {
            if (!knownAddressIds.Contains(address.Id))
                AddressAdded?.Invoke(address);
        }
        PersonAdded?.Invoke(person);

        // Create player at a generated home address
        var playerHome = _locationGenerator.GenerateAddress(State, AddressType.SuburbanHome);
        AddressAdded?.Invoke(playerHome);

        State.Player = new Player
        {
            HomeAddressId = playerHome.Id,
            CurrentAddressId = playerHome.Id
        };
        PlayerCreated?.Invoke();
    }

    public override void _Process(double delta)
    {
        var scaledDelta = delta * State.Clock.TimeScale;
        if (scaledDelta <= 0) return;

        State.Clock.Tick(scaledDelta);

        foreach (var person in State.People.Values)
        {
            if (_schedules.TryGetValue(person.Id, out var schedule))
            {
                _personBehavior.Update(person, schedule, State);
            }
        }
    }
}
