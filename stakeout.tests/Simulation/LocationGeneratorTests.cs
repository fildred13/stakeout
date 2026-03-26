using Stakeout.Simulation;
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
        var state = new SimulationState();
        var config = new MapConfig();
        var generator = new LocationGenerator(config);
        generator.GenerateCityScaffolding(state);

        var address = generator.GenerateAddress(state, AddressType.SuburbanHome);

        Assert.InRange(address.Position.X, config.MinX, config.MaxX);
        Assert.InRange(address.Position.Y, config.MinY, config.MaxY);
    }

    [Fact]
    public void GenerateAddress_CreatesSublocationsinState()
    {
        var state = new SimulationState();
        var generator = new LocationGenerator(new MapConfig());
        generator.GenerateCityScaffolding(state);

        var address = generator.GenerateAddress(state, AddressType.SuburbanHome);

        var addressSublocations = new System.Collections.Generic.List<Stakeout.Simulation.Entities.Sublocation>();
        foreach (var sub in state.Sublocations.Values)
            if (sub.AddressId == address.Id) addressSublocations.Add(sub);

        Assert.NotEmpty(addressSublocations);
        Assert.Contains(addressSublocations, s => s.HasTag("road"));
        Assert.Contains(addressSublocations, s => s.HasTag("entrance"));
    }
}
