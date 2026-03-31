using System.Linq;
using Stakeout.Simulation;
using Stakeout.Simulation.Addresses;
using Stakeout.Simulation.City;
using Stakeout.Simulation.Entities;
using Xunit;

namespace Stakeout.Tests.Simulation.City;

public class CityIntegrationTests
{
    [Fact]
    public void FullCityGeneration_ProducesValidState()
    {
        AddressTemplateRegistry.RegisterAll();
        var state = new SimulationState();
        var mapConfig = new MapConfig();

        // Generate city scaffolding
        var city = new Stakeout.Simulation.Entities.City
        {
            Id = state.GenerateEntityId(),
            Name = "Boston",
            CountryName = "United States"
        };
        state.Cities[city.Id] = city;

        // Generate city grid
        var cityGen = new CityGenerator(seed: 42);
        var grid = cityGen.Generate(state, city);
        state.CityGrids[city.Id] = grid;

        // Verify grid exists and has content
        Assert.NotNull(grid);
        Assert.Equal(100, grid.Width);

        // Verify addresses were created
        Assert.NotEmpty(state.Addresses);

        // Verify streets were created
        Assert.NotEmpty(state.Streets);

        // Verify every address has valid grid position
        foreach (var addr in state.Addresses.Values)
        {
            Assert.True(addr.GridX >= 0 && addr.GridX < 100);
            Assert.True(addr.GridY >= 0 && addr.GridY < 100);
            var cell = grid.GetCell(addr.GridX, addr.GridY);
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
