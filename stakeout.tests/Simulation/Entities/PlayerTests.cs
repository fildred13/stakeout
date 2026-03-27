using Stakeout.Simulation.Entities;
using Xunit;

namespace Stakeout.Tests.Simulation.Entities;

public class PlayerTests
{
    [Fact]
    public void Player_InventoryItemIds_DefaultsToEmptyList()
    {
        var player = new Player();
        Assert.NotNull(player.InventoryItemIds);
        Assert.Empty(player.InventoryItemIds);
    }
}
