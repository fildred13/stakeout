using System.Collections.Generic;
using Stakeout.Simulation.City;
using Stakeout.Simulation.Entities;
using Xunit;

namespace Stakeout.Tests.Simulation.City;

public class CityGridTests
{
    [Fact]
    public void Constructor_CreatesGridOfCorrectSize()
    {
        var grid = new CityGrid(100, 100);
        Assert.Equal(100, grid.Width);
        Assert.Equal(100, grid.Height);
    }

    [Fact]
    public void GetCell_ReturnsDefaultCellInitially()
    {
        var grid = new CityGrid(10, 10);
        var cell = grid.GetCell(5, 5);
        Assert.Equal(PlotType.Empty, cell.PlotType);
    }

    [Fact]
    public void SetCell_UpdatesGrid()
    {
        var grid = new CityGrid(10, 10);
        grid.SetCell(3, 4, new Cell { PlotType = PlotType.Road, StreetId = 1 });
        var cell = grid.GetCell(3, 4);
        Assert.Equal(PlotType.Road, cell.PlotType);
        Assert.Equal(1, cell.StreetId);
    }

    [Fact]
    public void GetCellsForAddress_ReturnsAllMatchingCells()
    {
        var grid = new CityGrid(10, 10);
        grid.SetCell(2, 2, new Cell { PlotType = PlotType.ApartmentBuilding, AddressId = 5 });
        grid.SetCell(2, 3, new Cell { PlotType = PlotType.ApartmentBuilding, AddressId = 5 });
        grid.SetCell(3, 2, new Cell { PlotType = PlotType.ApartmentBuilding, AddressId = 5 });
        grid.SetCell(3, 3, new Cell { PlotType = PlotType.ApartmentBuilding, AddressId = 5 });

        var cells = grid.GetCellsForAddress(5);
        Assert.Equal(4, cells.Count);
    }

    [Fact]
    public void GetPlotsByType_ReturnsMatchingPositions()
    {
        var grid = new CityGrid(10, 10);
        grid.SetCell(1, 1, new Cell { PlotType = PlotType.SuburbanHome });
        grid.SetCell(3, 5, new Cell { PlotType = PlotType.SuburbanHome });
        grid.SetCell(2, 2, new Cell { PlotType = PlotType.Office });

        var homes = grid.GetPlotsByType(PlotType.SuburbanHome);
        Assert.Equal(2, homes.Count);
    }

    [Fact]
    public void GetUnresolvedAddressIdsByType_ReturnsOnlyThoseWithEmptyInteriors()
    {
        var grid = new CityGrid(10, 10);
        var addresses = new Dictionary<int, Address>
        {
            [10] = new Address { Id = 10, Type = AddressType.SuburbanHome },
            [20] = new Address { Id = 20, Type = AddressType.SuburbanHome }
        };
        // Address 20 has a sublocation (resolved interior)
        addresses[20].Sublocations[1] = new Sublocation { Id = 1 };

        grid.SetCell(1, 1, new Cell { PlotType = PlotType.SuburbanHome, AddressId = 10 });
        grid.SetCell(2, 2, new Cell { PlotType = PlotType.SuburbanHome, AddressId = 20 });

        var unresolved = grid.GetUnresolvedAddressIdsByType(PlotType.SuburbanHome, addresses);
        Assert.Single(unresolved);
        Assert.Equal(10, unresolved[0]);
    }

    [Fact]
    public void IsInBounds_ReturnsTrueForValidCoords()
    {
        var grid = new CityGrid(10, 10);
        Assert.True(grid.IsInBounds(0, 0));
        Assert.True(grid.IsInBounds(9, 9));
        Assert.False(grid.IsInBounds(-1, 0));
        Assert.False(grid.IsInBounds(10, 0));
    }

    [Fact]
    public void FindAdjacentRoad_ReturnsCorrectDirection()
    {
        var grid = new CityGrid(10, 10);
        grid.SetCell(3, 5, new Cell { PlotType = PlotType.Road, StreetId = 1 });
        grid.SetCell(3, 4, new Cell { PlotType = PlotType.SuburbanHome });

        var (direction, streetId) = grid.FindAdjacentRoad(3, 4);
        Assert.Equal(FacingDirection.South, direction);
        Assert.Equal(1, streetId);
    }
}
