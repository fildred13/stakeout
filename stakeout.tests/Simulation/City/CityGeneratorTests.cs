using System.Collections.Generic;
using System.Linq;
using Stakeout.Simulation.City;
using Stakeout.Simulation;
using Stakeout.Simulation.Entities;
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
    public void Generate_BuildingAnchorCellsFaceARoad()
    {
        var state = new SimulationState();
        var grid = new CityGenerator(seed: 42).Generate(state);

        var checkedAddresses = new HashSet<int>();

        foreach (var address in state.Addresses.Values)
        {
            if (!checkedAddresses.Add(address.Id)) continue;

            int x = address.GridX, y = address.GridY;

            // Verify that at least one cell of the address has a facing direction
            // that eventually reaches a road (not necessarily immediately adjacent)
            var cells = grid.GetCellsForAddress(address.Id);
            bool anyFacesRoad = cells.Any(pos =>
            {
                var c = grid.GetCell(pos.X, pos.Y);
                var (dx, dy) = c.FacingDirection switch
                {
                    FacingDirection.North => (0, -1),
                    FacingDirection.South => (0, 1),
                    FacingDirection.East => (1, 0),
                    FacingDirection.West => (-1, 0),
                    _ => (0, 0)
                };
                // Walk in the facing direction until we hit a road or go out of bounds
                int nx = pos.X + dx, ny = pos.Y + dy;
                while (grid.IsInBounds(nx, ny))
                {
                    if (grid.GetCell(nx, ny).PlotType == PlotType.Road)
                        return true;
                    nx += dx;
                    ny += dy;
                }
                return false;
            });

            Assert.True(anyFacesRoad,
                $"Address {address.Id} at ({x},{y}) has no cell facing toward a road");
        }
    }

    [Fact]
    public void Generate_CreatesAddressesInSimulationState()
    {
        var state = new SimulationState();
        var grid = new CityGenerator(seed: 42).Generate(state);

        Assert.NotEmpty(state.Addresses);
        foreach (var address in state.Addresses.Values)
        {
            Assert.True(address.GridX >= 0 && address.GridX < 100);
            Assert.True(address.GridY >= 0 && address.GridY < 100);
            Assert.True(address.Number > 0);
            Assert.True(address.StreetId > 0);
        }
    }

    [Fact]
    public void Generate_AddressStreetNumbersIncreaseAlongStreet()
    {
        var state = new SimulationState();
        var grid = new CityGenerator(seed: 42).Generate(state);

        var byStreet = state.Addresses.Values.GroupBy(a => a.StreetId);
        foreach (var group in byStreet)
        {
            var sorted = group.OrderBy(a => a.GridX).ThenBy(a => a.GridY).ToList();
            for (int i = 1; i < sorted.Count; i++)
            {
                Assert.True(sorted[i].Number >= sorted[i - 1].Number,
                    $"Street {group.Key}: address numbers should increase along street");
            }
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
