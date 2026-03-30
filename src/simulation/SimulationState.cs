using System;
using System.Collections.Generic;
using System.Linq;
using Stakeout.Simulation.City;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Events;
using Stakeout.Simulation.Crimes;
using Stakeout.Simulation.Fixtures;
using Stakeout.Simulation.Traces;
using CityEntity = Stakeout.Simulation.Entities.City;

namespace Stakeout.Simulation;

public class SimulationState
{
    public GameClock Clock { get; }
    public Dictionary<int, Person> People { get; } = new();
public Dictionary<int, Business> Businesses { get; } = new();
    public Player Player { get; set; }
    public List<Country> Countries { get; } = new();
    public Dictionary<int, CityEntity> Cities { get; } = new();
    public Dictionary<int, Street> Streets { get; } = new();
    public Dictionary<int, Address> Addresses { get; } = new();
    public Dictionary<int, Location> Locations { get; } = new();
    public Dictionary<int, SubLocation> SubLocations { get; } = new();
    public EventJournal Journal { get; } = new();
    public Dictionary<int, Crime> Crimes { get; } = new();
    public Dictionary<int, Trace> Traces { get; } = new();
    public Dictionary<int, Item> Items { get; } = new();
    public Dictionary<int, Fixture> Fixtures { get; } = new();
    public Dictionary<int, CityGrid> CityGrids { get; } = new();

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

    // Query helpers

    public List<Location> GetLocationsForAddress(int addressId)
    {
        var addr = Addresses[addressId];
        return addr.LocationIds.Select(id => Locations[id]).ToList();
    }

    public List<SubLocation> GetSubLocationsForLocation(int locationId)
    {
        var loc = Locations[locationId];
        return loc.SubLocationIds.Select(id => SubLocations[id]).ToList();
    }

    public Location FindLocationByTag(int addressId, string tag)
    {
        return GetLocationsForAddress(addressId).FirstOrDefault(l => l.HasTag(tag));
    }

    public SubLocation FindSubLocationByTag(int locationId, string tag)
    {
        return GetSubLocationsForLocation(locationId).FirstOrDefault(s => s.HasTag(tag));
    }

    public List<Address> GetAddressesForCity(int cityId)
    {
        var city = Cities[cityId];
        return city.AddressIds.Select(id => Addresses[id]).ToList();
    }

    public CityEntity GetCityForAddress(int addressId)
    {
        var addr = Addresses[addressId];
        return Cities[addr.CityId];
    }

    public List<Fixture> GetFixturesForLocation(int locationId)
    {
        return Fixtures.Values.Where(f => f.LocationId == locationId).ToList();
    }

    public List<Fixture> GetFixturesForSubLocation(int subLocationId)
    {
        return Fixtures.Values.Where(f => f.SubLocationId == subLocationId).ToList();
    }

    public List<Trace> GetTracesForLocation(int locationId, DateTime currentTime)
    {
        return Traces.Values.Where(t => t.LocationId == locationId && IsTraceVisible(t, currentTime)).ToList();
    }

    public List<Trace> GetTracesForSubLocation(int subLocationId, DateTime currentTime)
    {
        return Traces.Values.Where(t => t.SubLocationId == subLocationId && IsTraceVisible(t, currentTime)).ToList();
    }

    public List<Trace> GetTracesForFixture(int fixtureId, DateTime currentTime)
    {
        return Traces.Values.Where(t => t.FixtureId == fixtureId && IsTraceVisible(t, currentTime)).ToList();
    }

    public List<Trace> GetTracesForPerson(int personId, DateTime currentTime)
    {
        return Traces.Values.Where(t => t.AttachedToPersonId == personId && IsTraceVisible(t, currentTime)).ToList();
    }

    private static bool IsTraceVisible(Trace trace, DateTime currentTime)
    {
        if (!trace.IsActive) return false;
        if (trace.DecayDays.HasValue && trace.CreatedAt.AddDays(trace.DecayDays.Value) < currentTime) return false;
        return true;
    }
}
