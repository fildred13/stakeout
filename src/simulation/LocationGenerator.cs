using System;
using Godot;
using Stakeout.Simulation.Data;
using Stakeout.Simulation.Entities;

namespace Stakeout.Simulation;

public class LocationGenerator
{
    private readonly Random _random = new();

    private const int StreetCount = 15;
    private const int MinAddressesPerStreet = 3;
    private const int MaxAddressesPerStreet = 8;
    private const float MapMinX = 40f;
    private const float MapMaxX = 1240f;
    private const float MapMinY = 40f;
    private const float MapMaxY = 680f;

    public void GenerateCity(SimulationState state)
    {
        var country = new Country { Name = "United States" };
        state.Countries.Add(country);

        var city = new City
        {
            Id = state.GenerateEntityId(),
            Name = "Boston",
            CountryName = country.Name
        };
        state.Cities[city.Id] = city;

        GenerateStreets(state, city);
    }

    private void GenerateStreets(SimulationState state, City city)
    {
        var usedNames = new System.Collections.Generic.HashSet<string>();

        for (int i = 0; i < StreetCount; i++)
        {
            string streetName;
            do
            {
                var baseName = StreetData.StreetNames[_random.Next(StreetData.StreetNames.Length)];
                var suffix = StreetData.StreetSuffixes[_random.Next(StreetData.StreetSuffixes.Length)];
                streetName = $"{baseName} {suffix}";
            } while (!usedNames.Add(streetName));

            var street = new Street
            {
                Id = state.GenerateEntityId(),
                Name = streetName,
                CityId = city.Id
            };
            state.Streets[street.Id] = street;

            GenerateAddresses(state, street);
        }
    }

    private void GenerateAddresses(SimulationState state, Street street)
    {
        int count = _random.Next(MinAddressesPerStreet, MaxAddressesPerStreet + 1);

        for (int i = 0; i < count; i++)
        {
            var type = PickAddressType();
            var address = new Address
            {
                Id = state.GenerateEntityId(),
                Number = GenerateAddressNumber(),
                StreetId = street.Id,
                Type = type,
                Position = new Vector2(
                    (float)(_random.NextDouble() * (MapMaxX - MapMinX) + MapMinX),
                    (float)(_random.NextDouble() * (MapMaxY - MapMinY) + MapMinY)
                )
            };
            state.Addresses[address.Id] = address;
        }
    }

    private AddressType PickAddressType()
    {
        double roll = _random.NextDouble();
        if (roll < 0.50) return AddressType.SuburbanHome;
        if (roll < 0.70) return AddressType.Office;
        if (roll < 0.85) return AddressType.Diner;
        return AddressType.DiveBar;
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
