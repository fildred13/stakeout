// stakeout.tests/Simulation/PlayerTravelTests.cs
using System;
using Godot;
using Stakeout.Simulation;
using Stakeout.Simulation.Entities;
using Xunit;

namespace Stakeout.Tests.Simulation;

public class PlayerTravelTests
{
    [Fact]
    public void Player_HasIdAndCurrentPosition()
    {
        var player = new Player
        {
            Id = 42,
            CurrentPosition = new Vector2(100, 200),
            HomeAddressId = 1,
            CurrentAddressId = 1
        };

        Assert.Equal(42, player.Id);
        Assert.Equal(new Vector2(100, 200), player.CurrentPosition);
        Assert.Null(player.TravelInfo);
    }
}
