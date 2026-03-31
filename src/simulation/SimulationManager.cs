using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Stakeout.Simulation.Actions;
using Stakeout.Simulation.Addresses;
using Stakeout.Simulation.Brain;
using Stakeout.Simulation.City;
using Stakeout.Simulation.Crimes;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Events;
using Stakeout.Simulation.Scheduling;
using CityEntity = Stakeout.Simulation.Entities.City;

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
    private readonly ActionRunner _actionRunner;

    private readonly Random _random = new(42);
    private readonly CrimeGenerator _crimeGenerator = new();
    public CrimeGenerator CrimeGenerator => _crimeGenerator;

    public SimulationManager(SimulationState state)
    {
        State = state;
        _locationGenerator = new LocationGenerator(_mapConfig);
        _personGenerator = new PersonGenerator(_mapConfig);
        _actionRunner = new ActionRunner(_mapConfig);
    }

    public override void _Ready()
    {
        AddressTemplateRegistry.RegisterAll();

        var country = new Country { Name = "United States" };
        State.Countries.Add(country);

        // Generate Boston
        var boston = new CityEntity
        {
            Id = State.GenerateEntityId(),
            Name = "Boston",
            CountryName = country.Name
        };
        State.Cities[boston.Id] = boston;

        var bostonCityGen = new CityGenerator(seed: 42);
        State.CityGrids[boston.Id] = bostonCityGen.Generate(State, boston);

        // Generate New York City
        var nyc = new CityEntity
        {
            Id = State.GenerateEntityId(),
            Name = "New York City",
            CountryName = country.Name
        };
        State.Cities[nyc.Id] = nyc;

        var nycCityGen = new CityGenerator(seed: 84);
        State.CityGrids[nyc.Id] = nycCityGen.Generate(State, nyc);

        // Notify listeners of all addresses created by the city generators
        foreach (var address in State.Addresses.Values)
            AddressAdded?.Invoke(address);

        // Generate 5 people (they pick addresses from the first city grid)
        for (var i = 0; i < 5; i++)
        {
            var person = _personGenerator.GeneratePerson(State);
            PersonAdded?.Invoke(person);
        }

        // Generate a dating couple for manual testing of the group coordination system
        var guy = _personGenerator.GeneratePerson(State);
        guy.FirstName = "Jack";
        guy.LastName = "Malone";
        PersonAdded?.Invoke(guy);

        var girl = _personGenerator.GeneratePerson(State);
        girl.FirstName = "Susan";
        girl.LastName = "Hayes";
        PersonAdded?.Invoke(girl);

        var relationship = new Relationship
        {
            Id = State.GenerateEntityId(),
            PersonAId = guy.Id,
            PersonBId = girl.Id,
            Type = RelationshipType.Dating,
            StartedAt = State.Clock.CurrentTime.AddDays(-90)
        };
        State.AddRelationship(relationship);


        // Create player at an unresolved suburban home from the first city grid
        var playerHome = LocationGenerator.PickAndResolveAddress(State, AddressType.SuburbanHome, _random, boston.Id);

        State.Player = new Player
        {
            Id = State.GenerateEntityId(),
            CurrentCityId = boston.Id,
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

            // Plan day on wake-up (first tick, plan exhausted, or plan is null)
            if (person.DayPlan == null || person.DayPlan.IsExhausted)
            {
                person.DayPlan = NpcBrain.PlanDay(person, State, State.Clock.CurrentTime);
                State.Journal.Append(new SimulationEvent
                {
                    Timestamp = State.Clock.CurrentTime,
                    PersonId = person.Id,
                    EventType = SimulationEventType.DayPlanned,
                    Description = $"Planned {person.DayPlan.Entries.Count} activities"
                });
            }

            _actionRunner.Tick(person, State, TimeSpan.FromSeconds(scaledDelta));
        }

        DoorLockingService.UpdateDoorStates(State, State.Clock.CurrentTime);

        UpdatePlayerTravel(State);
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

        // Find the main entrance AccessPoint on the first Location with "entrance" tag
        AccessPoint entranceAP = null;
        foreach (var locId in homeAddress.LocationIds)
        {
            var loc = state.Locations[locId];
            if (!loc.HasTag("entrance")) continue;
            entranceAP = loc.AccessPoints.FirstOrDefault(ap => ap.HasTag("main_entrance"));
            if (entranceAP != null) break;
        }

        if (entranceAP == null || entranceAP.LockMechanism == null) return;

        var key = new Item
        {
            Id = state.GenerateEntityId(),
            ItemType = ItemType.Key,
            HeldByEntityId = player.Id,
            Data = new Dictionary<string, object>
            {
                ["TargetAccessPointId"] = entranceAP.Id
            }
        };
        state.Items[key.Id] = key;
        player.InventoryItemIds.Add(key.Id);
        entranceAP.KeyItemId = key.Id;
    }
}
