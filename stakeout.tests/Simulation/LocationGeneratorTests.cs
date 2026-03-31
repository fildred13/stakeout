using System.Linq;
using Stakeout.Simulation;
using Stakeout.Simulation.Addresses;
using Stakeout.Simulation.Entities;
using Xunit;

namespace Stakeout.Tests.Simulation;

public class LocationGeneratorTests
{
    [Fact]
    public void GenerateCityScaffolding_CreatesCountryAndCity()
    {
        var state = new SimulationState();
        var generator = new LocationGenerator(new MapConfig());
        generator.GenerateCityScaffolding(state);

        Assert.Single(state.Countries);
        Assert.Single(state.Cities);
    }

    [Fact]
    public void GenerateAddress_CreatesAddressInState()
    {
        AddressTemplateRegistry.RegisterAll();
        var state = new SimulationState();
        var generator = new LocationGenerator(new MapConfig());
        generator.GenerateCityScaffolding(state);

        var address = generator.GenerateAddress(state, AddressType.Office);

        Assert.Contains(address.Id, state.Addresses.Keys);
        Assert.Equal(AddressType.Office, address.Type);
    }

    [Fact]
    public void GenerateAddress_PositionWithinMapBounds()
    {
        AddressTemplateRegistry.RegisterAll();
        var state = new SimulationState();
        var config = new MapConfig();
        var generator = new LocationGenerator(config);
        generator.GenerateCityScaffolding(state);

        var address = generator.GenerateAddress(state, AddressType.SuburbanHome);

        Assert.InRange(address.Position.X, 0f, config.MapWidth);
        Assert.InRange(address.Position.Y, 0f, config.MapHeight);
    }

    [Fact]
    public void GenerateAddress_CreatesLocationsInState()
    {
        AddressTemplateRegistry.RegisterAll();
        var state = new SimulationState();
        var generator = new LocationGenerator(new MapConfig());
        generator.GenerateCityScaffolding(state);

        var address = generator.GenerateAddress(state, AddressType.SuburbanHome);

        Assert.NotEmpty(address.LocationIds);
        var locations = address.LocationIds.Select(id => state.Locations[id]).ToList();
        Assert.Contains(locations, l => l.HasTag("entrance"));
    }
}
