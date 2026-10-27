using System.Collections.Generic;
using System.Linq;
using Stakeout.Simulation.City;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Fixtures;
using CityEntity = Stakeout.Simulation.Entities.City;

namespace Stakeout.Simulation;

/// <summary>The static world: places, people, objects and how they relate. Runtime state lives in World.</summary>
public class SimulationState
{
    public Dictionary<int, Person> People { get; } = new();
    public Dictionary<int, Household> Households { get; } = new();
    public Dictionary<int, Business> Businesses { get; } = new();
    public List<Country> Countries { get; } = new();
    public Dictionary<int, CityEntity> Cities { get; } = new();
    public Dictionary<int, Street> Streets { get; } = new();
    public Dictionary<int, Address> Addresses { get; } = new();
    public Dictionary<int, Location> Locations { get; } = new();
    public Dictionary<int, SubLocation> SubLocations { get; } = new();
    public Dictionary<int, Item> Items { get; } = new();
    public Dictionary<int, Fixture> Fixtures { get; } = new();
    public Dictionary<int, CityGrid> CityGrids { get; } = new();
    public Dictionary<int, Relationship> Relationships { get; } = new();
    public Dictionary<int, List<Relationship>> RelationshipsByPersonId { get; } = new();
    public Dictionary<int, Tension> Tensions { get; } = new();
    public Dictionary<int, Vehicle> Vehicles { get; } = new();

    private readonly Dictionary<int, List<Fixture>> _fixturesByLocation = new();
    private readonly Dictionary<int, List<Fixture>> _fixturesBySubLocation = new();
    private readonly Dictionary<int, int> _businessByAddress = new();
    private readonly Dictionary<int, AccessPoint> _accessPoints = new();
    private readonly Dictionary<int, int> _accessPointLocation = new();

    private int _nextEntityId = 1;

    public int GenerateEntityId() => _nextEntityId++;

    // ---- places ----

    public List<Location> GetLocationsForAddress(int addressId) =>
        Addresses[addressId].LocationIds.Select(id => Locations[id]).ToList();

    public List<SubLocation> GetSubLocationsForLocation(int locationId) =>
        Locations[locationId].SubLocationIds.Select(id => SubLocations[id]).ToList();

    public Location FindLocationByTag(int addressId, string tag) =>
        GetLocationsForAddress(addressId).FirstOrDefault(l => l.HasTag(tag));

    public SubLocation FindSubLocationByTag(int locationId, string tag) =>
        GetSubLocationsForLocation(locationId).FirstOrDefault(s => s.HasTag(tag));

    public List<Address> GetAddressesForCity(int cityId) =>
        Cities[cityId].AddressIds.Select(id => Addresses[id]).ToList();

    public CityEntity GetCityForAddress(int addressId) => Cities[Addresses[addressId].CityId];

    public string AddressLine(int addressId)
    {
        var a = Addresses[addressId];
        return Streets.TryGetValue(a.StreetId, out var s) ? $"{a.Number} {s.Name}" : $"Address {a.Id}";
    }

    /// <summary>Street address of someone's home, with the unit for apartments.</summary>
    public string HomeLine(Person person)
    {
        var line = AddressLine(person.HomeAddressId);
        return Addresses[person.HomeAddressId].Type == AddressType.ApartmentBuilding
            ? $"{line}, {Locations[person.HomeLocationId].Name}" : line;
    }

    public string PlaceName(int addressId)
    {
        var biz = BusinessAt(addressId);
        return biz != null ? $"{biz.Name}, {AddressLine(addressId)}" : AddressLine(addressId);
    }

    public Business BusinessAt(int addressId) =>
        _businessByAddress.TryGetValue(addressId, out var id) ? Businesses[id] : null;

    public void IndexBusinesses()
    {
        _businessByAddress.Clear();
        foreach (var b in Businesses.Values) _businessByAddress[b.AddressId] = b.Id;
    }

    // ---- fixtures and doors ----

    public void AddFixture(Fixture fixture)
    {
        Fixtures[fixture.Id] = fixture;
        if (fixture.LocationId.HasValue) Add(_fixturesByLocation, fixture.LocationId.Value, fixture);
        if (fixture.SubLocationId.HasValue) Add(_fixturesBySubLocation, fixture.SubLocationId.Value, fixture);
    }

    public IReadOnlyList<Fixture> GetFixturesForLocation(int locationId) =>
        _fixturesByLocation.TryGetValue(locationId, out var l) ? l : (IReadOnlyList<Fixture>)System.Array.Empty<Fixture>();

    public IReadOnlyList<Fixture> GetFixturesForSubLocation(int subLocationId) =>
        _fixturesBySubLocation.TryGetValue(subLocationId, out var l) ? l : (IReadOnlyList<Fixture>)System.Array.Empty<Fixture>();

    /// <summary>All fixtures in a location including those in its sublocations.</summary>
    public IEnumerable<Fixture> AllFixturesIn(int locationId)
    {
        foreach (var f in GetFixturesForLocation(locationId)) yield return f;
        foreach (var subId in Locations[locationId].SubLocationIds)
            foreach (var f in GetFixturesForSubLocation(subId)) yield return f;
    }

    public void IndexAccessPoints()
    {
        _accessPoints.Clear();
        foreach (var loc in Locations.Values)
            foreach (var ap in loc.AccessPoints)
            {
                _accessPoints[ap.Id] = ap;
                _accessPointLocation[ap.Id] = loc.Id;
            }
    }

    public AccessPoint GetAccessPoint(int id) => _accessPoints.TryGetValue(id, out var ap) ? ap : null;
    public int LocationOfAccessPoint(int id) => _accessPointLocation[id];

    /// <summary>The location a fixture is in, whether it sits in the location or in one of its sublocations.</summary>
    public int LocationOfFixture(Fixture f) => f.LocationId ?? SubLocations[f.SubLocationId.Value].LocationId;

    public Fixture Phone(int locationId) =>
        AllFixturesIn(locationId).FirstOrDefault(f => f.Type == FixtureType.Telephone);

    // ---- people ----

    public void AddRelationship(Relationship rel)
    {
        Relationships[rel.Id] = rel;
        Add(RelationshipsByPersonId, rel.PersonAId, rel);
        Add(RelationshipsByPersonId, rel.PersonBId, rel);
    }

    public IReadOnlyList<Relationship> RelationshipsOf(int personId) =>
        RelationshipsByPersonId.TryGetValue(personId, out var list) ? list : (IReadOnlyList<Relationship>)System.Array.Empty<Relationship>();

    public Relationship GetRelationship(int personAId, int personBId) =>
        RelationshipsOf(personAId).FirstOrDefault(r => r.Involves(personBId));

    public IEnumerable<int> RelatedPeople(int personId, RelationshipType type) =>
        RelationshipsOf(personId).Where(r => r.Type == type).Select(r => r.Other(personId));

    public IEnumerable<Person> Coworkers(int personId)
    {
        var p = People[personId];
        if (!p.BusinessId.HasValue) yield break;
        foreach (var pos in Businesses[p.BusinessId.Value].Positions)
            if (pos.AssignedPersonId.HasValue && pos.AssignedPersonId != personId)
                yield return People[pos.AssignedPersonId.Value];
    }

    public Position PositionOf(Person person) =>
        person.BusinessId.HasValue && person.PositionId.HasValue
            ? Businesses[person.BusinessId.Value].Positions.First(p => p.Id == person.PositionId.Value)
            : null;

    public Fixture PhoneOf(Person person) =>
        person.HomePhoneId.HasValue ? Fixtures[person.HomePhoneId.Value] : null;

    private static void Add<T>(Dictionary<int, List<T>> index, int key, T value)
    {
        if (!index.TryGetValue(key, out var list)) index[key] = list = new List<T>();
        list.Add(value);
    }
}
