using Stakeout.Simulation.Entities;
using Xunit;

namespace Stakeout.Tests.Simulation.Entities;

public class LocationTests
{
    [Fact]
    public void HasTag_ReturnsTrueForPresentTag()
    {
        var loc = new Location { Tags = new[] { "residential", "private" } };
        Assert.True(loc.HasTag("residential"));
    }

    [Fact]
    public void HasTag_ReturnsFalseForMissingTag()
    {
        var loc = new Location { Tags = new[] { "residential" } };
        Assert.False(loc.HasTag("exterior"));
    }

    [Fact]
    public void SubLocationIds_EmptyByDefault()
    {
        var loc = new Location();
        Assert.Empty(loc.SubLocationIds);
    }

    [Fact]
    public void AccessPoints_EmptyByDefault()
    {
        var loc = new Location();
        Assert.Empty(loc.AccessPoints);
    }

    [Fact]
    public void UnitLabel_NullByDefault()
    {
        var loc = new Location();
        Assert.Null(loc.UnitLabel);
    }
}
