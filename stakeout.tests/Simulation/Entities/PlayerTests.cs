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

        // Simulate a front door connection with entrance tag and LockableProperty
        var frontDoor = new SublocationConnection
        {
            Id = state.GenerateEntityId(),
            FromSublocationId = 100,
            ToSublocationId = 200,
            Type = ConnectionType.Door,
            Name = "Front Door",
            Tags = new[] { "entrance" },
            Lockable = new LockableProperty { Mechanism = LockMechanism.Key }
        };
        address.Connections.Add(frontDoor);

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
        Assert.Equal(frontDoor.Id, (int)key.Data["TargetConnectionId"]);
        Assert.Equal(key.Id, frontDoor.Lockable.KeyItemId);
    }
}
