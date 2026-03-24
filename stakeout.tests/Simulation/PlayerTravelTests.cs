// stakeout.tests/Simulation/PlayerTravelTests.cs
using System;
using Godot;
using Stakeout.Simulation;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Events;
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

    [Fact]
    public void PlayerTravel_InterpolatesPosition()
    {
        var state = new SimulationState(new GameClock(new DateTime(1980, 1, 1, 8, 0, 0)));
        var from = new Address { Id = state.GenerateEntityId(), Position = new Vector2(100, 100), Type = AddressType.SuburbanHome, Number = 1, StreetId = 1 };
        var to = new Address { Id = state.GenerateEntityId(), Position = new Vector2(500, 100), Type = AddressType.Office, Number = 2, StreetId = 1 };
        state.Addresses[from.Id] = from;
        state.Addresses[to.Id] = to;

        var mapConfig = new MapConfig();
        var travelHours = mapConfig.ComputeTravelTimeHours(from.Position, to.Position);
        var departureTime = state.Clock.CurrentTime;
        var arrivalTime = departureTime.AddHours(travelHours);

        state.Player = new Player
        {
            Id = state.GenerateEntityId(),
            HomeAddressId = from.Id,
            CurrentAddressId = from.Id,
            CurrentPosition = from.Position,
            TravelInfo = new TravelInfo
            {
                FromPosition = from.Position,
                ToPosition = to.Position,
                DepartureTime = departureTime,
                ArrivalTime = arrivalTime,
                FromAddressId = from.Id,
                ToAddressId = to.Id
            }
        };

        var halfTravelSeconds = travelHours * 3600 / 2;
        state.Clock.Tick(halfTravelSeconds);
        SimulationManager.UpdatePlayerTravel(state);

        Assert.InRange(state.Player.CurrentPosition.X, 250, 350);
        Assert.NotNull(state.Player.TravelInfo);
    }

    [Fact]
    public void PlayerTravel_ArrivesAtDestination()
    {
        var state = new SimulationState(new GameClock(new DateTime(1980, 1, 1, 8, 0, 0)));
        var from = new Address { Id = state.GenerateEntityId(), Position = new Vector2(100, 100), Type = AddressType.SuburbanHome, Number = 1, StreetId = 1 };
        var to = new Address { Id = state.GenerateEntityId(), Position = new Vector2(500, 100), Type = AddressType.Office, Number = 2, StreetId = 1 };
        state.Addresses[from.Id] = from;
        state.Addresses[to.Id] = to;

        var mapConfig = new MapConfig();
        var travelHours = mapConfig.ComputeTravelTimeHours(from.Position, to.Position);
        var departureTime = state.Clock.CurrentTime;
        var arrivalTime = departureTime.AddHours(travelHours);

        state.Player = new Player
        {
            Id = state.GenerateEntityId(),
            HomeAddressId = from.Id,
            CurrentAddressId = from.Id,
            CurrentPosition = from.Position,
            TravelInfo = new TravelInfo
            {
                FromPosition = from.Position,
                ToPosition = to.Position,
                DepartureTime = departureTime,
                ArrivalTime = arrivalTime,
                FromAddressId = from.Id,
                ToAddressId = to.Id
            }
        };

        state.Clock.Tick(travelHours * 3600 + 1);
        SimulationManager.UpdatePlayerTravel(state);

        Assert.Equal(to.Position, state.Player.CurrentPosition);
        Assert.Equal(to.Id, state.Player.CurrentAddressId);
        Assert.Null(state.Player.TravelInfo);

        var events = state.Journal.GetEventsForPerson(state.Player.Id);
        Assert.Single(events);
        Assert.Equal(SimulationEventType.ArrivedAtAddress, events[0].EventType);
        Assert.Equal(to.Id, events[0].AddressId);
    }

    [Fact]
    public void StartPlayerTravel_LogsDepartedAddressEvent()
    {
        var state = new SimulationState(new GameClock(new DateTime(1980, 1, 1, 8, 0, 0)));
        var from = new Address { Id = state.GenerateEntityId(), Position = new Vector2(100, 100), Type = AddressType.SuburbanHome, Number = 1, StreetId = 1 };
        var to = new Address { Id = state.GenerateEntityId(), Position = new Vector2(500, 100), Type = AddressType.Office, Number = 2, StreetId = 1 };
        state.Addresses[from.Id] = from;
        state.Addresses[to.Id] = to;

        var mapConfig = new MapConfig();

        state.Player = new Player
        {
            Id = state.GenerateEntityId(),
            HomeAddressId = from.Id,
            CurrentAddressId = from.Id,
            CurrentPosition = from.Position
        };

        SimulationManager.StartPlayerTravel(state, to.Id, mapConfig);

        var events = state.Journal.GetEventsForPerson(state.Player.Id);
        Assert.Single(events);
        Assert.Equal(SimulationEventType.DepartedAddress, events[0].EventType);
        Assert.Equal(from.Id, events[0].FromAddressId);
        Assert.Equal(to.Id, events[0].ToAddressId);
    }

    [Fact]
    public void PlayerTravel_CanBeInterrupted()
    {
        var state = new SimulationState(new GameClock(new DateTime(1980, 1, 1, 8, 0, 0)));
        var from = new Address { Id = state.GenerateEntityId(), Position = new Vector2(100, 100), Type = AddressType.SuburbanHome, Number = 1, StreetId = 1 };
        var to = new Address { Id = state.GenerateEntityId(), Position = new Vector2(500, 100), Type = AddressType.Office, Number = 2, StreetId = 1 };
        var newDest = new Address { Id = state.GenerateEntityId(), Position = new Vector2(100, 500), Type = AddressType.Diner, Number = 3, StreetId = 1 };
        state.Addresses[from.Id] = from;
        state.Addresses[to.Id] = to;
        state.Addresses[newDest.Id] = newDest;

        var mapConfig = new MapConfig();
        var travelHours = mapConfig.ComputeTravelTimeHours(from.Position, to.Position);

        state.Player = new Player
        {
            Id = state.GenerateEntityId(),
            HomeAddressId = from.Id,
            CurrentAddressId = from.Id,
            CurrentPosition = from.Position,
            TravelInfo = new TravelInfo
            {
                FromPosition = from.Position,
                ToPosition = to.Position,
                DepartureTime = state.Clock.CurrentTime,
                ArrivalTime = state.Clock.CurrentTime.AddHours(travelHours),
                FromAddressId = from.Id,
                ToAddressId = to.Id
            }
        };

        state.Clock.Tick(travelHours * 3600 / 2);
        SimulationManager.UpdatePlayerTravel(state);
        var midpoint = state.Player.CurrentPosition;

        SimulationManager.StartPlayerTravel(state, newDest.Id, mapConfig);

        Assert.Equal(newDest.Id, state.Player.TravelInfo.ToAddressId);
        Assert.Equal(midpoint, state.Player.TravelInfo.FromPosition);
        Assert.NotNull(state.Player.TravelInfo);
    }
}
