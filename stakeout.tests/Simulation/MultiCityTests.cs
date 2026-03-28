using System;
using System.Linq;
using Stakeout.Simulation;
using Stakeout.Simulation.Addresses;
using Stakeout.Simulation.Entities;
using Xunit;

namespace Stakeout.Tests.Simulation;

public class MultiCityTests
{
    [Fact]
    public void TwoCitiesExist()
    {
        var state = CreateTwoCityState();
        Assert.Equal(2, state.Cities.Count);
    }

    [Fact]
    public void EachCityHasAirport()
    {
        var state = CreateTwoCityState();
        foreach (var city in state.Cities.Values)
        {
            Assert.NotNull(city.AirportAddressId);
            var airport = state.Addresses[city.AirportAddressId.Value];
            Assert.Equal(AddressType.Airport, airport.Type);
        }
    }

    [Fact]
    public void EachCityHasOwnGrid()
    {
        var state = CreateTwoCityState();
        Assert.Equal(2, state.CityGrids.Count);
    }

    [Fact]
    public void AddressesBelongToCorrectCity()
    {
        var state = CreateTwoCityState();
        foreach (var city in state.Cities.Values)
        {
            foreach (var addrId in city.AddressIds)
            {
                Assert.Equal(city.Id, state.Addresses[addrId].CityId);
            }
        }
    }

    [Fact]
    public void PlayerCanFlyBetweenCities()
    {
        var state = CreateTwoCityState();
        var boston = state.Cities.Values.First(c => c.Name == "Boston");
        var nyc = state.Cities.Values.First(c => c.Name == "New York City");

        state.Player = new Player
        {
            Id = state.GenerateEntityId(),
            CurrentCityId = boston.Id,
            CurrentAddressId = boston.AirportAddressId.Value,
            CurrentPosition = state.Addresses[boston.AirportAddressId.Value].Position
        };

        var nycAirport = state.Addresses[nyc.AirportAddressId.Value];
        state.Player.CurrentCityId = nyc.Id;
        state.Player.CurrentAddressId = nycAirport.Id;
        state.Player.CurrentPosition = nycAirport.Position;

        Assert.Equal(nyc.Id, state.Player.CurrentCityId);
        Assert.Equal(nycAirport.Id, state.Player.CurrentAddressId);
    }

    private SimulationState CreateTwoCityState()
    {
        var state = new SimulationState();

        var boston = new Stakeout.Simulation.Entities.City { Id = state.GenerateEntityId(), Name = "Boston", CountryName = "USA" };
        state.Cities[boston.Id] = boston;
        state.CityGrids[boston.Id] = new Stakeout.Simulation.City.CityGrid(10, 10);

        var nyc = new Stakeout.Simulation.Entities.City { Id = state.GenerateEntityId(), Name = "New York City", CountryName = "USA" };
        state.Cities[nyc.Id] = nyc;
        state.CityGrids[nyc.Id] = new Stakeout.Simulation.City.CityGrid(10, 10);

        var bostonAirport = new Address
        {
            Id = state.GenerateEntityId(),
            CityId = boston.Id,
            Type = AddressType.Airport,
            GridX = 5, GridY = 5
        };
        state.Addresses[bostonAirport.Id] = bostonAirport;
        boston.AddressIds.Add(bostonAirport.Id);
        boston.AirportAddressId = bostonAirport.Id;
        new AirportTemplate().Generate(bostonAirport, state, new Random(42));

        var nycAirport = new Address
        {
            Id = state.GenerateEntityId(),
            CityId = nyc.Id,
            Type = AddressType.Airport,
            GridX = 5, GridY = 5
        };
        state.Addresses[nycAirport.Id] = nycAirport;
        nyc.AddressIds.Add(nycAirport.Id);
        nyc.AirportAddressId = nycAirport.Id;
        new AirportTemplate().Generate(nycAirport, state, new Random(42));

        return state;
    }
}
