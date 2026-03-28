using System.Collections.Generic;
using System.Linq;
using Stakeout.Simulation.City;
using Stakeout.Simulation;
using Xunit;

namespace Stakeout.Tests.Simulation.City;

public class CityGeneratorTests
{
    [Fact]
    public void Generate_EdgesAreRoads()
    {
        var state = new SimulationState();
        var generator = new CityGenerator(seed: 42);
        var grid = generator.Generate(state);

        // All edge cells should be roads
        for (int x = 0; x < grid.Width; x++)
        {
            Assert.Equal(PlotType.Road, grid.GetCell(x, 0).PlotType);
            Assert.Equal(PlotType.Road, grid.GetCell(x, grid.Height - 1).PlotType);
        }
        for (int y = 0; y < grid.Height; y++)
        {
            Assert.Equal(PlotType.Road, grid.GetCell(0, y).PlotType);
            Assert.Equal(PlotType.Road, grid.GetCell(grid.Width - 1, y).PlotType);
        }
    }

    [Fact]
    public void Generate_HasArterialRoads()
    {
        var state = new SimulationState();
        var generator = new CityGenerator(seed: 42);
        var grid = generator.Generate(state);

        int interiorHorizontalRoadRows = 0;
        for (int y = 1; y < grid.Height - 1; y++)
        {
            bool fullRow = true;
            for (int x = 0; x < grid.Width; x++)
            {
                if (grid.GetCell(x, y).PlotType != PlotType.Road)
                {
                    fullRow = false;
                    break;
                }
            }
            if (fullRow) interiorHorizontalRoadRows++;
        }
        Assert.True(interiorHorizontalRoadRows >= 5, $"Expected >=5 arterial rows, got {interiorHorizontalRoadRows}");
    }

    [Fact]
    public void Generate_AllRoadsConnected()
    {
        var state = new SimulationState();
        var generator = new CityGenerator(seed: 42);
        var grid = generator.Generate(state);

        var roadCells = grid.GetPlotsByType(PlotType.Road);
        Assert.NotEmpty(roadCells);

        var visited = new HashSet<(int, int)>();
        var queue = new Queue<(int, int)>();
        queue.Enqueue(roadCells[0]);
        visited.Add(roadCells[0]);

        while (queue.Count > 0)
        {
            var (x, y) = queue.Dequeue();
            foreach (var (dx, dy) in new[] { (0, 1), (0, -1), (1, 0), (-1, 0) })
            {
                int nx = x + dx, ny = y + dy;
                if (grid.IsInBounds(nx, ny) &&
                    grid.GetCell(nx, ny).PlotType == PlotType.Road &&
                    visited.Add((nx, ny)))
                {
                    queue.Enqueue((nx, ny));
                }
            }
        }

        Assert.Equal(roadCells.Count, visited.Count);
    }

    [Fact]
    public void Generate_SameSeedProducesSameGrid()
    {
        var grid1 = new CityGenerator(seed: 123).Generate(new SimulationState());
        var grid2 = new CityGenerator(seed: 123).Generate(new SimulationState());

        for (int x = 0; x < grid1.Width; x++)
            for (int y = 0; y < grid1.Height; y++)
                Assert.Equal(grid1.GetCell(x, y).PlotType, grid2.GetCell(x, y).PlotType);
    }

    [Fact]
    public void Generate_AllRoadCellsHaveStreetIds()
    {
        var state = new SimulationState();
        var generator = new CityGenerator(seed: 42);
        var grid = generator.Generate(state);

        var roadCells = grid.GetPlotsByType(PlotType.Road);
        foreach (var (x, y) in roadCells)
        {
            var cell = grid.GetCell(x, y);
            Assert.NotNull(cell.StreetId);
        }
    }

    [Fact]
    public void Generate_StreetsHaveNames()
    {
        var state = new SimulationState();
        var generator = new CityGenerator(seed: 42);
        generator.Generate(state);

        Assert.NotEmpty(state.Streets);
        foreach (var street in state.Streets.Values)
        {
            Assert.False(string.IsNullOrEmpty(street.Name));
        }
    }

    [Fact]
    public void Generate_ArterialStreetsHaveGrandNames()
    {
        var state = new SimulationState();
        var generator = new CityGenerator(seed: 42);
        generator.Generate(state);

        var names = state.Streets.Values.Select(s => s.Name).ToList();
        Assert.Contains(names, n => n.Contains("Boulevard") || n.Contains("Avenue"));
    }

    [Fact]
    public void Generate_HasBuildingPlots()
    {
        var state = new SimulationState();
        var grid = new CityGenerator(seed: 42).Generate(state);

        int buildings = 0;
        for (int x = 0; x < grid.Width; x++)
            for (int y = 0; y < grid.Height; y++)
                if (grid.GetCell(x, y).PlotType.IsBuilding())
                    buildings++;

        Assert.True(buildings > 100, $"Expected >100 building plots, got {buildings}");
    }

    [Fact]
    public void Generate_UrbanCenterHasMoreApartmentsAndOffices()
    {
        var state = new SimulationState();
        var grid = new CityGenerator(seed: 42).Generate(state);

        int centerUrban = 0, edgeUrban = 0;
        int centerTotal = 0, edgeTotal = 0;

        for (int x = 0; x < grid.Width; x++)
        {
            for (int y = 0; y < grid.Height; y++)
            {
                var type = grid.GetCell(x, y).PlotType;
                if (type == PlotType.Road) continue;

                bool isCenter = x >= 30 && x < 70 && y >= 30 && y < 70;
                bool isUrbanType = type == PlotType.ApartmentBuilding || type == PlotType.Office;

                if (isCenter) { centerTotal++; if (isUrbanType) centerUrban++; }
                else { edgeTotal++; if (isUrbanType) edgeUrban++; }
            }
        }

        float centerRatio = centerTotal > 0 ? (float)centerUrban / centerTotal : 0;
        float edgeRatio = edgeTotal > 0 ? (float)edgeUrban / edgeTotal : 0;
        Assert.True(centerRatio > edgeRatio,
            $"Center urban ratio ({centerRatio:F3}) should exceed edge ({edgeRatio:F3})");
    }

    [Fact]
    public void Generate_MultiPlotBuildingsOccupy2x2()
    {
        var state = new SimulationState();
        var grid = new CityGenerator(seed: 42).Generate(state);

        // Find an apartment building cell — all 4 cells in the 2x2 should be ApartmentBuilding
        for (int x = 0; x < grid.Width - 1; x++)
        {
            for (int y = 0; y < grid.Height - 1; y++)
            {
                var cell = grid.GetCell(x, y);
                if (cell.PlotType == PlotType.ApartmentBuilding)
                {
                    Assert.Equal(PlotType.ApartmentBuilding, grid.GetCell(x + 1, y).PlotType);
                    Assert.Equal(PlotType.ApartmentBuilding, grid.GetCell(x, y + 1).PlotType);
                    Assert.Equal(PlotType.ApartmentBuilding, grid.GetCell(x + 1, y + 1).PlotType);
                    return;
                }
            }
        }
        Assert.Fail("No 2x2 apartment building found in generated city");
    }

    [Fact]
    public void Generate_NoBuildingsOnRoads()
    {
        var state = new SimulationState();
        var grid = new CityGenerator(seed: 42).Generate(state);

        var roads = grid.GetPlotsByType(PlotType.Road);
        foreach (var (x, y) in roads)
        {
            Assert.Equal(PlotType.Road, grid.GetCell(x, y).PlotType);
            Assert.Null(grid.GetCell(x, y).AddressId);
        }
    }

    [Fact]
    public void Generate_UrbanCenterHasSmallerBlocks()
    {
        var state = new SimulationState();
        var generator = new CityGenerator(seed: 42);
        var grid = generator.Generate(state);

        int centerRoads = 0, edgeRoads = 0;
        int centerTotal = 0, edgeTotal = 0;

        for (int x = 0; x < grid.Width; x++)
        {
            for (int y = 0; y < grid.Height; y++)
            {
                bool isCenter = x >= 30 && x < 70 && y >= 30 && y < 70;
                if (isCenter)
                {
                    centerTotal++;
                    if (grid.GetCell(x, y).PlotType == PlotType.Road) centerRoads++;
                }
                else
                {
                    edgeTotal++;
                    if (grid.GetCell(x, y).PlotType == PlotType.Road) edgeRoads++;
                }
            }
        }

        float centerDensity = (float)centerRoads / centerTotal;
        float edgeDensity = (float)edgeRoads / edgeTotal;
        Assert.True(centerDensity > edgeDensity,
            $"Center road density ({centerDensity:F3}) should exceed edge ({edgeDensity:F3})");
    }
}
