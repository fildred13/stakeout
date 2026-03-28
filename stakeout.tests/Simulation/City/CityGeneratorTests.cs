using System.Collections.Generic;
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
