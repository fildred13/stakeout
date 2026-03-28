using System.Linq;
using Stakeout.Simulation;
using Stakeout.Simulation.City;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Sublocations;
using Xunit;

namespace Stakeout.Tests.Simulation.City;

public class CityIntegrationTests
{
    [Fact]
    public void FullCityGeneration_ProducesValidState()
    {
        SublocationGeneratorRegistry.RegisterAll();
        var state = new SimulationState();
        var mapConfig = new MapConfig();

        // Generate city scaffolding
        var locationGen = new LocationGenerator(mapConfig);
        locationGen.GenerateCityScaffolding(state);

        // Generate city grid
        var cityGen = new CityGenerator(seed: 42);
        state.CityGrid = cityGen.Generate(state);

        // Verify grid exists and has content
        Assert.NotNull(state.CityGrid);
        Assert.Equal(100, state.CityGrid.Width);

        // Verify addresses were created
        Assert.NotEmpty(state.Addresses);

        // Verify streets were created
        Assert.NotEmpty(state.Streets);

        // Verify every address has valid grid position
        foreach (var addr in state.Addresses.Values)
        {
            Assert.True(addr.GridX >= 0 && addr.GridX < 100);
            Assert.True(addr.GridY >= 0 && addr.GridY < 100);
            var cell = state.CityGrid.GetCell(addr.GridX, addr.GridY);
            Assert.True(cell.PlotType.IsBuilding());
            Assert.Equal(addr.Id, cell.AddressId);
        }

        // Verify travel time computation works with grid positions
        var addr1 = state.Addresses.Values.First();
        var addr2 = state.Addresses.Values.Last();
        var hours = mapConfig.ComputeTravelTimeHours(addr1.Position, addr2.Position);
        Assert.True(hours > 0 && hours <= mapConfig.MaxTravelTimeHours);
    }
}
