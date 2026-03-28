using System;
using Godot;
using Stakeout.Simulation.Data;
using Stakeout.Simulation.Entities;
using CityEntity = Stakeout.Simulation.Entities.City;
using Stakeout.Simulation.Sublocations;

namespace Stakeout.Simulation;

public class LocationGenerator
{
    private static bool _registryInitialized = false;

    private readonly Random _random = new();
    private readonly MapConfig _mapConfig;

    public LocationGenerator(MapConfig mapConfig)
    {
        _mapConfig = mapConfig;
        if (!_registryInitialized)
        {
            SublocationGeneratorRegistry.RegisterAll();
            _registryInitialized = true;
        }
    }

    public void GenerateCityScaffolding(SimulationState state)
    {
        var country = new Country { Name = "United States" };
        state.Countries.Add(country);

        var city = new CityEntity
        {
            Id = state.GenerateEntityId(),
            Name = "Boston",
            CountryName = country.Name
        };
        state.Cities[city.Id] = city;
    }

    public Address GenerateAddress(SimulationState state, AddressType type)
    {
        // Find the first city
        int cityId = 0;
        foreach (var key in state.Cities.Keys) { cityId = key; break; }

        var street = FindOrCreateStreet(state, cityId);

        int gridX = _random.Next(1, _mapConfig.GridWidth - 1);
        int gridY = _random.Next(1, _mapConfig.GridHeight - 1);
        var address = new Address
        {
            Id = state.GenerateEntityId(),
            Number = GenerateAddressNumber(),
            StreetId = street.Id,
            Type = type,
            GridX = gridX,
            GridY = gridY
        };
        state.Addresses[address.Id] = address;

        var sublocationGenerator = SublocationGeneratorRegistry.Get(address.Type);
        if (sublocationGenerator != null)
        {
            sublocationGenerator.Generate(address, state, _random);
        }

        return address;
    }

    private Street FindOrCreateStreet(SimulationState state, int cityId)
    {
        if (state.Streets.Count > 0 && _random.NextDouble() < 0.5)
        {
            var streets = new System.Collections.Generic.List<Street>(state.Streets.Values);
            return streets[_random.Next(streets.Count)];
        }

        var usedNames = new System.Collections.Generic.HashSet<string>();
        foreach (var s in state.Streets.Values) usedNames.Add(s.Name);

        var maxCombinations = StreetData.StreetNames.Length * StreetData.StreetSuffixes.Length;
        string streetName;

        if (usedNames.Count >= maxCombinations)
        {
            // All base combinations exhausted — append a numeric suffix
            var counter = usedNames.Count - maxCombinations + 1;
            do
            {
                var baseName = StreetData.StreetNames[_random.Next(StreetData.StreetNames.Length)];
                var suffix = StreetData.StreetSuffixes[_random.Next(StreetData.StreetSuffixes.Length)];
                streetName = $"{baseName} {suffix} {counter}";
                counter++;
            } while (!usedNames.Add(streetName));
        }
        else
        {
            do
            {
                var baseName = StreetData.StreetNames[_random.Next(StreetData.StreetNames.Length)];
                var suffix = StreetData.StreetSuffixes[_random.Next(StreetData.StreetSuffixes.Length)];
                streetName = $"{baseName} {suffix}";
            } while (!usedNames.Add(streetName));
        }

        var street = new Street
        {
            Id = state.GenerateEntityId(),
            Name = streetName,
            CityId = cityId
        };
        state.Streets[street.Id] = street;
        return street;
    }

    private int GenerateAddressNumber()
    {
        double u1 = 1.0 - _random.NextDouble();
        double u2 = _random.NextDouble();
        double normal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
        double logMean = Math.Log(200);
        double logStd = 1.0;
        return (int)Math.Clamp(Math.Exp(logMean + logStd * normal), 1, 10000);
    }
}
