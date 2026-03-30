using System;
using System.Collections.Generic;
using System.Linq;
using Stakeout.Simulation.Data;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Events;
using Stakeout.Simulation.Objectives;
using Stakeout.Simulation.Traits;

namespace Stakeout.Simulation;

public class PersonGenerator
{
    private readonly Random _random = new();
    private readonly MapConfig _mapConfig;

    public PersonGenerator(MapConfig mapConfig)
    {
        _mapConfig = mapConfig;
    }

    public Person GeneratePerson(SimulationState state)
    {
        int cityId = 0;
        foreach (var key in state.Cities.Keys) { cityId = key; break; }

        // Home address (keep existing logic)
        var homeType = _random.NextDouble() < 0.5 ? AddressType.SuburbanHome : AddressType.ApartmentBuilding;
        var homeAddress = homeType == AddressType.ApartmentBuilding
            ? FindApartmentBuilding(state)
            : PickAndResolveAddress(state, homeType);

        int? homeLocationId = null;
        if (homeType == AddressType.ApartmentBuilding)
        {
            homeLocationId = AssignVacantUnit(state, homeAddress);
        }
        else
        {
            foreach (var locId in homeAddress.LocationIds)
            {
                var loc = state.Locations[locId];
                if (loc.HasTag("residential"))
                {
                    homeLocationId = loc.Id;
                    break;
                }
            }
        }

        // TODO: Task 6 will add business/position assignment and sleep schedule
        var person = new Person
        {
            Id = state.GenerateEntityId(),
            FirstName = NameData.FirstNames[_random.Next(NameData.FirstNames.Length)],
            LastName = NameData.LastNames[_random.Next(NameData.LastNames.Length)],
            CreatedAt = state.Clock.CurrentTime,
            CurrentCityId = cityId,
            HomeAddressId = homeAddress.Id,
            HomeLocationId = homeLocationId,
            CurrentAddressId = homeAddress.Id,
            CurrentPosition = homeAddress.Position,
            PreferredSleepTime = new TimeSpan(22, 0, 0),
            PreferredWakeTime = new TimeSpan(6, 0, 0),
        };

        // Assign traits
        var allTraits = TraitDefinitions.GetAllTraitNames();
        var traitCount = _random.Next(0, 3);
        var shuffled = allTraits.OrderBy(_ => _random.Next()).Take(traitCount);
        foreach (var trait in shuffled)
            person.Traits.Add(trait);

        // Create objectives: universal + trait-based
        person.Objectives.Add(new SleepObjective { Id = state.GenerateEntityId() });
        foreach (var trait in person.Traits)
        {
            foreach (var obj in TraitDefinitions.CreateObjectivesForTrait(trait))
            {
                obj.Id = state.GenerateEntityId();
                person.Objectives.Add(obj);
            }
        }

        state.People[person.Id] = person;
        CreateHomeKey(state, person, homeAddress);

        state.Journal.Append(new SimulationEvent
        {
            Timestamp = state.Clock.CurrentTime,
            PersonId = person.Id,
            EventType = SimulationEventType.ActivityStarted,
            Description = "Spawned"
        });

        return person;
    }

    private Address PickAndResolveAddress(SimulationState state, AddressType type)
    {
        return LocationGenerator.PickAndResolveAddress(state, type, _random);
    }

    private Address FindApartmentBuilding(SimulationState state)
    {
        // First, try to find a resolved apartment building with vacancy
        if (_random.NextDouble() < 0.5)
        {
            var apartments = new List<Address>();
            foreach (var addr in state.Addresses.Values)
            {
                if (addr.Type == AddressType.ApartmentBuilding && addr.LocationIds.Count > 0)
                    apartments.Add(addr);
            }
            if (apartments.Count > 0)
            {
                var candidate = apartments[_random.Next(apartments.Count)];
                if (HasVacancy(state, candidate))
                    return candidate;
            }
        }
        // Otherwise, pick an unresolved apartment building from the grid and resolve it
        return PickAndResolveAddress(state, AddressType.ApartmentBuilding);
    }

    private static bool HasVacancy(SimulationState state, Address building)
    {
        var occupiedLocationIds = new HashSet<int>();
        foreach (var person in state.People.Values)
        {
            if (person.HomeAddressId == building.Id && person.HomeLocationId.HasValue)
                occupiedLocationIds.Add(person.HomeLocationId.Value);
        }

        foreach (var locId in building.LocationIds)
        {
            var loc = state.Locations[locId];
            if (loc.HasTag("residential") && !occupiedLocationIds.Contains(loc.Id))
                return true;
        }
        return false;
    }

    private int? AssignVacantUnit(SimulationState state, Address building)
    {
        var occupiedLocationIds = new HashSet<int>();
        foreach (var person in state.People.Values)
        {
            if (person.HomeAddressId == building.Id && person.HomeLocationId.HasValue)
                occupiedLocationIds.Add(person.HomeLocationId.Value);
        }

        var vacantLocations = new List<int>();
        foreach (var locId in building.LocationIds)
        {
            var loc = state.Locations[locId];
            if (loc.HasTag("residential") && !occupiedLocationIds.Contains(loc.Id))
                vacantLocations.Add(loc.Id);
        }

        if (vacantLocations.Count == 0)
            throw new InvalidOperationException($"No vacant units in building {building.Id}");

        return vacantLocations[_random.Next(vacantLocations.Count)];
    }

    private static void CreateHomeKey(SimulationState state, Person person, Address homeAddress)
    {
        // Find the main entrance AccessPoint for this person's home
        AccessPoint entranceAP = null;
        if (person.HomeLocationId.HasValue)
        {
            var homeLoc = state.Locations[person.HomeLocationId.Value];
            entranceAP = homeLoc.AccessPoints.FirstOrDefault(ap => ap.HasTag("main_entrance"));
        }

        if (entranceAP == null)
        {
            // Fall back: find any entrance location's main entrance
            foreach (var locId in homeAddress.LocationIds)
            {
                var loc = state.Locations[locId];
                if (!loc.HasTag("entrance")) continue;
                entranceAP = loc.AccessPoints.FirstOrDefault(ap => ap.HasTag("main_entrance"));
                if (entranceAP != null) break;
            }
        }

        if (entranceAP == null || entranceAP.LockMechanism == null) return;

        var key = new Item
        {
            Id = state.GenerateEntityId(),
            ItemType = ItemType.Key,
            HeldByEntityId = person.Id,
            Data = new Dictionary<string, object>
            {
                ["TargetAccessPointId"] = entranceAP.Id
            }
        };
        state.Items[key.Id] = key;
        person.InventoryItemIds.Add(key.Id);

        // Assign key to lockable access points
        if (homeAddress.Type == AddressType.ApartmentBuilding && person.HomeLocationId.HasValue)
        {
            // Apartment: only assign to the unit's access points
            var homeLoc = state.Locations[person.HomeLocationId.Value];
            foreach (var ap in homeLoc.AccessPoints)
            {
                if (ap.LockMechanism != null)
                    ap.KeyItemId = key.Id;
            }
        }
        else
        {
            // Suburban home / other: assign key to all lockable access points across all locations
            foreach (var locId in homeAddress.LocationIds)
            {
                var loc = state.Locations[locId];
                foreach (var ap in loc.AccessPoints)
                {
                    if (ap.LockMechanism != null)
                        ap.KeyItemId = key.Id;
                }
            }
        }
    }
}
