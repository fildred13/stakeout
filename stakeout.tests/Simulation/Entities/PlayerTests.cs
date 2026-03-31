using Stakeout.Simulation;
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

    [Fact]
    public void CreatePlayerKey_AddsKeyToPlayerInventory()
    {
        var state = new SimulationState();
        var address = new Address { Id = state.GenerateEntityId(), Type = AddressType.SuburbanHome };
        state.Addresses[address.Id] = address;

        // Create a Location with an entrance tag and an AccessPoint with main_entrance tag
        var location = new Location
        {
            Id = state.GenerateEntityId(),
            AddressId = address.Id,
            Name = "Interior",
            Tags = new[] { "residential", "private", "entrance" }
        };
        location.AccessPoints.Add(new AccessPoint
        {
            Id = state.GenerateEntityId(),
            Name = "Front Door",
            Type = AccessPointType.Door,
            Tags = new[] { "main_entrance" },
            IsLocked = true,
            LockMechanism = LockMechanism.Key
        });
        state.Locations[location.Id] = location;
        address.LocationIds.Add(location.Id);

        var player = new Player
        {
            Id = state.GenerateEntityId(),
            HomeAddressId = address.Id,
            CurrentAddressId = address.Id
        };
        state.Player = player;

        // Call the static helper
        SimulationManager.CreatePlayerKey(state);

        Assert.Single(player.InventoryItemIds);
        var key = state.Items[player.InventoryItemIds[0]];
        Assert.Equal(ItemType.Key, key.ItemType);
        Assert.Equal(player.Id, key.HeldByEntityId);
        var targetApId = (int)key.Data["TargetAccessPointId"];
        Assert.Equal(location.AccessPoints[0].Id, targetApId);
        Assert.Equal(key.Id, location.AccessPoints[0].KeyItemId);
    }
}
