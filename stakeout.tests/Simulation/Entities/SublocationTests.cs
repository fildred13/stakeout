using Stakeout.Simulation.Entities;
using Xunit;

namespace Stakeout.Tests.Simulation.Entities;

public class SublocationTests
{
    [Fact]
    public void Constructor_SetsDefaultValues()
    {
        var sub = new Sublocation();
        Assert.Equal(0, sub.Id);
        Assert.Equal(0, sub.AddressId);
        Assert.Null(sub.ParentId);
        Assert.Null(sub.Name);
        Assert.Empty(sub.Tags);
        Assert.Null(sub.Floor);
        Assert.True(sub.IsGenerated);
    }

    [Fact]
    public void HasTag_TagPresent_ReturnsTrue()
    {
        var sub = new Sublocation { Tags = new[] { "entrance", "public" } };
        Assert.True(sub.HasTag("entrance"));
        Assert.True(sub.HasTag("public"));
    }

    [Fact]
    public void HasTag_TagAbsent_ReturnsFalse()
    {
        var sub = new Sublocation { Tags = new[] { "entrance" } };
        Assert.False(sub.HasTag("work_area"));
    }

    [Fact]
    public void SublocationConnection_Defaults()
    {
        var conn = new SublocationConnection();
        Assert.Equal(0, conn.FromSublocationId);
        Assert.Equal(0, conn.ToSublocationId);
        Assert.Equal(ConnectionType.OpenPassage, conn.Type);
    }
}

// New SubLocation tests (Task 1) — in this file because Windows filesystem is
// case-insensitive and SubLocationTests.cs == SublocationTests.cs on disk.
public class SubLocationTests
{
    [Fact]
    public void HasTag_ReturnsTrueForPresentTag()
    {
        var sub = new SubLocation { Tags = new[] { "bedroom" } };
        Assert.True(sub.HasTag("bedroom"));
    }

    [Fact]
    public void HasTag_ReturnsFalseForMissingTag()
    {
        var sub = new SubLocation { Tags = new[] { "bedroom" } };
        Assert.False(sub.HasTag("kitchen"));
    }

    [Fact]
    public void AccessPoints_EmptyByDefault()
    {
        var sub = new SubLocation();
        Assert.Empty(sub.AccessPoints);
    }
}
