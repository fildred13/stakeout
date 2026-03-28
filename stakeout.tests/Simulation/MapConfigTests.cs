using Godot;
using Stakeout.Simulation;
using Xunit;

namespace Stakeout.Tests.Simulation;

public class MapConfigTests
{
    [Fact]
    public void GridDimensions_AreCorrect()
    {
        var config = new MapConfig();
        Assert.Equal(100, config.GridWidth);
        Assert.Equal(100, config.GridHeight);
        Assert.Equal(48, config.CellSize);
        Assert.Equal(4800f, config.MapWidth);
        Assert.Equal(4800f, config.MapHeight);
    }

    [Fact]
    public void ComputeTravelTimeHours_UsesGridPositions()
    {
        var config = new MapConfig();
        var from = new Vector2(0, 0);
        var to = new Vector2(config.MapWidth, config.MapHeight);
        var hours = config.ComputeTravelTimeHours(from, to);
        Assert.InRange(hours, 0.9f, 1.1f);
    }

    [Fact]
    public void ComputeTravelTimeHours_ZeroDistance_ReturnsZero()
    {
        var config = new MapConfig();
        var pos = new Vector2(100, 100);

        var hours = config.ComputeTravelTimeHours(pos, pos);

        Assert.Equal(0.0f, hours);
    }
}
