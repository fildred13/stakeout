using System.Linq;
using Stakeout.Simulation;
using Stakeout.Simulation.Entities;
using Xunit;

namespace Stakeout.Tests.Simulation;

public class LocationGeneratorTests
{
    private static SimulationState GenerateCityState()
    {
        var state = new SimulationState();
        var generator = new LocationGenerator();
        generator.GenerateCity(state);
        return state;
    }

    [Fact]
    public void GenerateCity_CreatesOneCountry()
    {
        var state = GenerateCityState();

        Assert.Single(state.Countries);
        Assert.Equal("United States", state.Countries[0].Name);
    }

    [Fact]
    public void GenerateCity_CreatesOneCity()
    {
        var state = GenerateCityState();

        Assert.Single(state.Cities);
        Assert.Equal("Boston", state.Cities.Values.First().Name);
    }

    [Fact]
    public void GenerateCity_Creates15Streets()
    {
        var state = GenerateCityState();

        Assert.Equal(15, state.Streets.Count);
    }

    [Fact]
    public void GenerateCity_AllStreetNamesAreUnique()
    {
        var state = GenerateCityState();

        var names = state.Streets.Values.Select(s => s.Name).ToList();
        Assert.Equal(names.Count, names.Distinct().Count());
    }

    [Fact]
    public void GenerateCity_EachStreetHas3To8Addresses()
    {
        var state = GenerateCityState();

        foreach (var street in state.Streets.Values)
        {
            var addressCount = state.Addresses.Values.Count(a => a.StreetId == street.Id);
            Assert.InRange(addressCount, 3, 8);
        }
    }

    [Fact]
    public void GenerateCity_TotalAddressCountInExpectedRange()
    {
        var state = GenerateCityState();

        // 15 streets * 3-8 addresses = 45-120 addresses
        Assert.InRange(state.Addresses.Count, 45, 120);
    }

    [Fact]
    public void GenerateCity_AddressPositionsWithinMapBounds()
    {
        var state = GenerateCityState();

        foreach (var address in state.Addresses.Values)
        {
            Assert.InRange(address.Position.X, 40f, 1240f);
            Assert.InRange(address.Position.Y, 40f, 680f);
        }
    }

    [Fact]
    public void GenerateCity_ContainsBothResidentialAndCommercialAddresses()
    {
        var state = GenerateCityState();

        Assert.Contains(state.Addresses.Values, a => a.Category == AddressCategory.Residential);
        Assert.Contains(state.Addresses.Values, a => a.Category == AddressCategory.Commercial);
    }

    [Fact]
    public void GenerateCity_AddressNumbersArePositive()
    {
        var state = GenerateCityState();

        foreach (var address in state.Addresses.Values)
        {
            Assert.InRange(address.Number, 1, 10000);
        }
    }
}
