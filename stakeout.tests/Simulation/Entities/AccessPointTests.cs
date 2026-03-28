using Stakeout.Simulation.Entities;
using Xunit;

namespace Stakeout.Tests.Simulation.Entities;

public class AccessPointTests
{
    [Fact]
    public void HasTag_ReturnsTrueForPresentTag()
    {
        var ap = new AccessPoint { Tags = new[] { "main_entrance", "covert_entry" } };
        Assert.True(ap.HasTag("main_entrance"));
    }

    [Fact]
    public void HasTag_ReturnsFalseForMissingTag()
    {
        var ap = new AccessPoint { Tags = new[] { "main_entrance" } };
        Assert.False(ap.HasTag("covert_entry"));
    }

    [Fact]
    public void DefaultState_UnlockedAndNotBroken()
    {
        var ap = new AccessPoint();
        Assert.False(ap.IsLocked);
        Assert.False(ap.IsBroken);
    }

    [Fact]
    public void LockMechanism_NullByDefault()
    {
        var ap = new AccessPoint();
        Assert.Null(ap.LockMechanism);
    }
}
