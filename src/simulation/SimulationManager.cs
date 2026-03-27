using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Stakeout.Simulation.Crimes;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Events;
using Stakeout.Simulation.Objectives;
using Stakeout.Simulation.Scheduling;
using Stakeout.Simulation.Sublocations;

namespace Stakeout.Simulation;

public partial class SimulationManager : Node
{
    public SimulationState State { get; private set; }

    public event Action<Person> PersonAdded;
    public event Action<Address> AddressAdded;
    public event Action PlayerCreated;

    private readonly MapConfig _mapConfig = new();
    public MapConfig MapConfig => _mapConfig;
    private readonly PersonGenerator _personGenerator;
    private readonly LocationGenerator _locationGenerator;
    private readonly PersonBehavior _personBehavior;

    private readonly CrimeGenerator _crimeGenerator = new();
    public CrimeGenerator CrimeGenerator => _crimeGenerator;

    public SimulationManager(SimulationState state)
    {
        State = state;
        _locationGenerator = new LocationGenerator(_mapConfig);
        _personGenerator = new PersonGenerator(_locationGenerator, _mapConfig);
        _personBehavior = new PersonBehavior(_mapConfig);
    }

    public override void _Ready()
    {
        SublocationGeneratorRegistry.RegisterAll();
        _locationGenerator.GenerateCityScaffolding(State);

        // Generate a park
        var park = _locationGenerator.GenerateAddress(State, AddressType.Park);
        AddressAdded?.Invoke(park);

        // Generate 5 people
        for (var i = 0; i < 5; i++)
        {
            var knownAddressIds = new HashSet<int>(State.Addresses.Keys);
            var person = _personGenerator.GeneratePerson(State);

            foreach (var address in State.Addresses.Values)
            {
                if (!knownAddressIds.Contains(address.Id))
                    AddressAdded?.Invoke(address);
            }
            PersonAdded?.Invoke(person);
        }

        // Create player at a generated home address
        var playerHome = _locationGenerator.GenerateAddress(State, AddressType.SuburbanHome);
        AddressAdded?.Invoke(playerHome);

        State.Player = new Player
        {
            Id = State.GenerateEntityId(),
            HomeAddressId = playerHome.Id,
            CurrentAddressId = playerHome.Id,
            CurrentPosition = playerHome.Position
        };
        PlayerCreated?.Invoke();
        CreatePlayerKey(State);
    }

    public override void _Process(double delta)
    {
        var scaledDelta = delta * State.Clock.TimeScale;
        if (scaledDelta <= 0) return;

        State.Clock.Tick(scaledDelta);

        foreach (var person in State.People.Values)
        {
            if (!person.IsAlive) continue;
            _personBehavior.Update(person, State);
        }

        // Check for schedule rebuilds
        foreach (var person in State.People.Values)
        {
            if (person.NeedsScheduleRebuild)
            {
                RebuildSchedule(person);
                person.NeedsScheduleRebuild = false;
            }
        }

        UpdatePlayerTravel(State);
    }

    public void RebuildSchedule(Person person)
    {
        var tasks = ObjectiveResolver.ResolveTasks(person.Objectives, State);
        person.Schedule = ScheduleBuilder.BuildFromTasks(tasks, State, _mapConfig);
    }

    public static void UpdatePlayerTravel(SimulationState state)
    {
        var player = state.Player;
        if (player?.TravelInfo == null) return;

        var travel = player.TravelInfo;
        var currentTime = state.Clock.CurrentTime;

        if (currentTime >= travel.ArrivalTime)
        {
            player.CurrentPosition = travel.ToPosition;
            player.CurrentAddressId = travel.ToAddressId;
            player.TravelInfo = null;

            state.Journal.Append(new SimulationEvent
            {
                Timestamp = currentTime,
                PersonId = player.Id,
                EventType = SimulationEventType.ArrivedAtAddress,
                AddressId = travel.ToAddressId
            });
        }
        else
        {
            var totalSeconds = (travel.ArrivalTime - travel.DepartureTime).TotalSeconds;
            var elapsedSeconds = (currentTime - travel.DepartureTime).TotalSeconds;
            var progress = Math.Clamp(elapsedSeconds / totalSeconds, 0.0, 1.0);
            player.CurrentPosition = travel.FromPosition.Lerp(travel.ToPosition, (float)progress);
        }
    }

    public static void StartPlayerTravel(SimulationState state, int destinationAddressId, MapConfig mapConfig)
    {
        var player = state.Player;
        var destAddress = state.Addresses[destinationAddressId];
        var currentTime = state.Clock.CurrentTime;

        // Log departure if currently at an address (not already traveling)
        if (player.TravelInfo == null && player.CurrentAddressId != 0)
        {
            state.Journal.Append(new SimulationEvent
            {
                Timestamp = currentTime,
                PersonId = player.Id,
                EventType = SimulationEventType.DepartedAddress,
                FromAddressId = player.CurrentAddressId,
                ToAddressId = destinationAddressId
            });
        }

        var fromPosition = player.CurrentPosition;
        var travelHours = mapConfig.ComputeTravelTimeHours(fromPosition, destAddress.Position);
        var arrivalTime = currentTime.AddHours(travelHours);

        player.TravelInfo = new TravelInfo
        {
            FromPosition = fromPosition,
            ToPosition = destAddress.Position,
            DepartureTime = currentTime,
            ArrivalTime = arrivalTime,
            FromAddressId = player.CurrentAddressId,
            ToAddressId = destinationAddressId
        };

        player.CurrentAddressId = 0; // In transit, no current address
    }

    public static void CreatePlayerKey(SimulationState state)
    {
        var player = state.Player;
        var homeAddress = state.Addresses[player.HomeAddressId];

        var entranceConn = homeAddress.Connections
            .FirstOrDefault(c => c.Tags != null && c.Tags.Contains("entrance"));

        if (entranceConn?.Lockable == null) return;

        var key = new Item
        {
            Id = state.GenerateEntityId(),
            ItemType = ItemType.Key,
            HeldByEntityId = player.Id,
            Data = new Dictionary<string, object>
            {
                ["TargetConnectionId"] = entranceConn.Id
            }
        };
        state.Items[key.Id] = key;
        player.InventoryItemIds.Add(key.Id);
        entranceConn.Lockable.KeyItemId = key.Id;
    }
}
