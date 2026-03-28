using Stakeout.Simulation;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Sublocations;
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
        SublocationGeneratorRegistry.RegisterAll();
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
        SublocationGeneratorRegistry.RegisterAll();
        var state = new SimulationState();
        var config = new MapConfig();
        var generator = new LocationGenerator(config);
        generator.GenerateCityScaffolding(state);

        var address = generator.GenerateAddress(state, AddressType.SuburbanHome);

        Assert.InRange(address.Position.X, 0f, config.MapWidth);
        Assert.InRange(address.Position.Y, 0f, config.MapHeight);
    }

    [Fact]
    public void GenerateAddress_CreatesSublocationsinState()
    {
        SublocationGeneratorRegistry.RegisterAll();
        var state = new SimulationState();
        var generator = new LocationGenerator(new MapConfig());
        generator.GenerateCityScaffolding(state);

        var address = generator.GenerateAddress(state, AddressType.SuburbanHome);

        Assert.NotEmpty(address.Sublocations);
        Assert.Contains(address.Sublocations.Values, s => s.HasTag("road"));
        Assert.Contains(address.Connections, c => c.HasTag("entrance"));
    }
}
