using System;
using System.Linq;
using Stakeout.Simulation;
using Stakeout.Simulation.Addresses;
using Stakeout.Simulation.Businesses;
using Stakeout.Simulation.City;
using Stakeout.Simulation.Entities;
using Xunit;
using CityEntity = Stakeout.Simulation.Entities.City;

namespace Stakeout.Tests.Simulation.Businesses;

public class BusinessGeneratorTests
{
    private static (SimulationState state, CityEntity city) CreateStateWithCity()
    {
        AddressTemplateRegistry.RegisterAll();
        BusinessTemplateRegistry.RegisterAll();
        var state = new SimulationState();
        var city = new CityEntity
        {
            Id = state.GenerateEntityId(),
            Name = "Boston",
            CountryName = "United States"
        };
        state.Cities[city.Id] = city;
        return (state, city);
    }

    [Fact]
    public void CreateBusiness_ReturnsBusiness_WithCorrectAddressId()
    {
        var (state, city) = CreateStateWithCity();
        var address = new Address
        {
            Id = state.GenerateEntityId(),
            CityId = city.Id,
            Type = AddressType.Diner,
            GridX = 5, GridY = 5
        };
        state.Addresses[address.Id] = address;
        var biz = BusinessGenerator.CreateBusiness(state, address, new Random(42));
        Assert.Equal(address.Id, biz.AddressId);
        Assert.Equal(BusinessType.Diner, biz.Type);
        Assert.False(biz.IsResolved);
    }

    [Fact]
    public void CreateBusiness_AddedToState()
    {
        var (state, city) = CreateStateWithCity();
        var address = new Address
        {
            Id = state.GenerateEntityId(),
            CityId = city.Id,
            Type = AddressType.Diner,
            GridX = 5, GridY = 5
        };
        state.Addresses[address.Id] = address;
        var biz = BusinessGenerator.CreateBusiness(state, address, new Random(42));
        Assert.True(state.Businesses.ContainsKey(biz.Id));
    }

    [Fact]
    public void CreateBusiness_HasName()
    {
        var (state, city) = CreateStateWithCity();
        var address = new Address
        {
            Id = state.GenerateEntityId(),
            CityId = city.Id,
            Type = AddressType.Diner,
            GridX = 5, GridY = 5
        };
        state.Addresses[address.Id] = address;
        var biz = BusinessGenerator.CreateBusiness(state, address, new Random(42));
        Assert.False(string.IsNullOrWhiteSpace(biz.Name));
    }

    [Fact]
    public void CreateBusiness_HasPositionsWithIds()
    {
        var (state, city) = CreateStateWithCity();
        var address = new Address
        {
            Id = state.GenerateEntityId(),
            CityId = city.Id,
            Type = AddressType.Office,
            GridX = 5, GridY = 5
        };
        state.Addresses[address.Id] = address;
        var biz = BusinessGenerator.CreateBusiness(state, address, new Random(42));
        Assert.NotEmpty(biz.Positions);
        Assert.All(biz.Positions, p => Assert.True(p.Id > 0));
        Assert.All(biz.Positions, p => Assert.Equal(biz.Id, p.BusinessId));
    }

    [Fact]
    public void CreateBusiness_AllPositionsUnassigned()
    {
        var (state, city) = CreateStateWithCity();
        var address = new Address
        {
            Id = state.GenerateEntityId(),
            CityId = city.Id,
            Type = AddressType.DiveBar,
            GridX = 5, GridY = 5
        };
        state.Addresses[address.Id] = address;
        var biz = BusinessGenerator.CreateBusiness(state, address, new Random(42));
        Assert.All(biz.Positions, p => Assert.Null(p.AssignedPersonId));
    }

    [Fact]
    public void CreateBusiness_ReturnsNull_ForNonCommercialAddress()
    {
        var (state, city) = CreateStateWithCity();
        var address = new Address
        {
            Id = state.GenerateEntityId(),
            CityId = city.Id,
            Type = AddressType.SuburbanHome,
            GridX = 5, GridY = 5
        };
        state.Addresses[address.Id] = address;
        var biz = BusinessGenerator.CreateBusiness(state, address, new Random(42));
        Assert.Null(biz);
    }

    [Fact]
    public void CityGeneration_CreatesBusinesses_ForCommercialAddresses()
    {
        var (state, city) = CreateStateWithCity();
        var cityGen = new CityGenerator(seed: 42);
        state.CityGrids[city.Id] = cityGen.Generate(state, city);
        Assert.NotEmpty(state.Businesses);
        foreach (var biz in state.Businesses.Values)
        {
            Assert.True(state.Addresses.ContainsKey(biz.AddressId));
            var addr = state.Addresses[biz.AddressId];
            Assert.Equal(AddressCategory.Commercial, addr.Category);
        }
    }
}
