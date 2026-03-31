using System.Collections.Generic;
using Stakeout.Simulation.Entities;
using Xunit;

namespace Stakeout.Tests.Simulation.Entities;

public class ItemTests
{
    [Fact]
    public void Item_Defaults()
    {
        var item = new Item();
        Assert.Equal(0, item.Id);
        Assert.Equal(ItemType.Key, item.ItemType);
        Assert.Null(item.HeldByEntityId);
        Assert.Null(item.LocationAddressId);
        Assert.Null(item.LocationId);
        Assert.NotNull(item.Data);
        Assert.Empty(item.Data);
    }

    [Fact]
    public void Item_KeyWithTargetConnectionId()
    {
        var item = new Item
        {
            Id = 1,
            ItemType = ItemType.Key,
            HeldByEntityId = 10,
            Data = new Dictionary<string, object> { ["TargetConnectionId"] = 42 }
        };
        Assert.Equal(42, (int)item.Data["TargetConnectionId"]);
        Assert.Equal(10, item.HeldByEntityId);
    }

    [Fact]
    public void Item_LocationPlacement()
    {
        var item = new Item
        {
            Id = 2,
            ItemType = ItemType.Key,
            LocationAddressId = 5,
            LocationId = 15
        };
        Assert.Null(item.HeldByEntityId);
        Assert.Equal(5, item.LocationAddressId);
        Assert.Equal(15, item.LocationId);
    }
}
