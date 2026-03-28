using System;
using Stakeout.Simulation.City;
using Stakeout.Simulation.Entities;
using Xunit;

namespace Stakeout.Tests.Simulation.City;

public class PlotTypeTests
{
    [Theory]
    [InlineData(PlotType.SuburbanHome, 1, 1)]
    [InlineData(PlotType.Diner, 1, 1)]
    [InlineData(PlotType.DiveBar, 1, 1)]
    [InlineData(PlotType.ApartmentBuilding, 2, 2)]
    [InlineData(PlotType.Office, 2, 2)]
    [InlineData(PlotType.Park, 2, 2)]
    [InlineData(PlotType.Road, 1, 1)]
    [InlineData(PlotType.Empty, 1, 1)]
    public void GetSize_ReturnsCorrectSize(PlotType type, int expectedWidth, int expectedHeight)
    {
        var (w, h) = type.GetSize();
        Assert.Equal(expectedWidth, w);
        Assert.Equal(expectedHeight, h);
    }

    [Theory]
    [InlineData(PlotType.SuburbanHome, AddressType.SuburbanHome)]
    [InlineData(PlotType.ApartmentBuilding, AddressType.ApartmentBuilding)]
    [InlineData(PlotType.Office, AddressType.Office)]
    [InlineData(PlotType.Diner, AddressType.Diner)]
    [InlineData(PlotType.DiveBar, AddressType.DiveBar)]
    [InlineData(PlotType.Park, AddressType.Park)]
    public void ToAddressType_MapsCorrectly(PlotType plotType, AddressType expected)
    {
        Assert.Equal(expected, plotType.ToAddressType());
    }

    [Theory]
    [InlineData(PlotType.Road)]
    [InlineData(PlotType.Empty)]
    public void ToAddressType_ThrowsForNonBuildingTypes(PlotType plotType)
    {
        Assert.Throws<InvalidOperationException>(() => plotType.ToAddressType());
    }

    [Theory]
    [InlineData(PlotType.SuburbanHome, true)]
    [InlineData(PlotType.ApartmentBuilding, true)]
    [InlineData(PlotType.Office, true)]
    [InlineData(PlotType.Diner, true)]
    [InlineData(PlotType.DiveBar, true)]
    [InlineData(PlotType.Park, true)]
    [InlineData(PlotType.Road, false)]
    [InlineData(PlotType.Empty, false)]
    public void IsBuilding_ReturnsCorrectly(PlotType type, bool expected)
    {
        Assert.Equal(expected, type.IsBuilding());
    }
}

public class CellTests
{
    [Fact]
    public void DefaultCell_HasEmptyType()
    {
        var cell = new Cell();
        Assert.Equal(PlotType.Empty, cell.PlotType);
        Assert.Null(cell.AddressId);
        Assert.Null(cell.StreetId);
    }

    [Fact]
    public void RoadCell_HasStreetId()
    {
        var cell = new Cell { PlotType = PlotType.Road, StreetId = 5 };
        Assert.Equal(PlotType.Road, cell.PlotType);
        Assert.Equal(5, cell.StreetId);
        Assert.Null(cell.AddressId);
    }

    [Fact]
    public void BuildingCell_HasAddressIdAndFacing()
    {
        var cell = new Cell
        {
            PlotType = PlotType.SuburbanHome,
            AddressId = 10,
            FacingDirection = FacingDirection.South
        };
        Assert.Equal(10, cell.AddressId);
        Assert.Equal(FacingDirection.South, cell.FacingDirection);
    }
}
