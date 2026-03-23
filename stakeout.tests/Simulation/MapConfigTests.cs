using Godot;
using Stakeout.Simulation;
using Xunit;

namespace Stakeout.Tests.Simulation;

public class MapConfigTests
{
    [Fact]
    public void ComputeTravelTimeHours_FullDiagonal_ReturnsMaxTravelTime()
    {
        var config = new MapConfig();
        var from = new Vector2(config.MinX, config.MinY);
        var to = new Vector2(config.MaxX, config.MaxY);

        var hours = config.ComputeTravelTimeHours(from, to);

        Assert.Equal(config.MaxTravelTimeHours, hours, precision: 2);
    }

    [Fact]
    public void ComputeTravelTimeHours_ZeroDistance_ReturnsZero()
    {
        var config = new MapConfig();
        var pos = new Vector2(100, 100);

        var hours = config.ComputeTravelTimeHours(pos, pos);

        Assert.Equal(0.0f, hours);
    }

    [Fact]
    public void ComputeTravelTimeHours_HalfDiagonal_ReturnsHalfMaxTime()
    {
        var config = new MapConfig();
        var center = new Vector2(
            (config.MinX + config.MaxX) / 2,
            (config.MinY + config.MaxY) / 2);
        var corner = new Vector2(config.MaxX, config.MaxY);

        var hours = config.ComputeTravelTimeHours(center, corner);

        Assert.Equal(config.MaxTravelTimeHours / 2, hours, precision: 2);
    }
}
