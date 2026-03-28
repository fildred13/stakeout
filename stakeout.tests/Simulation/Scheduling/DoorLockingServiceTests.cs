using System;
using System.Collections.Generic;
using System.Linq;
using Stakeout.Simulation;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Scheduling;
using Stakeout.Simulation.Traces;
using Xunit;

namespace Stakeout.Tests.Simulation.Scheduling;

public class DoorLockingServiceTests
{
    private static SimulationState CreateState()
    {
        return new SimulationState(new GameClock(new DateTime(1980, 1, 1, 8, 0, 0)));
    }

    private static (SimulationState state, Person person, Address home, Item key, List<SublocationConnection> lockableConns) CreateSuburbanScenario()
    {
        var state = CreateState();
        var home = new Address
        {
            Id = state.GenerateEntityId(),
            Type = AddressType.SuburbanHome,
            GridX = 2,
            GridY = 2,
            Number = 1,
            StreetId = 1
        };
        state.Addresses[home.Id] = home;

        var frontDoor = new SublocationConnection
        {
            Id = state.GenerateEntityId(),
            FromSublocationId = 100,
            ToSublocationId = 200,
            Type = ConnectionType.Door,
            Tags = new[] { "entrance" },
            Lockable = new LockableProperty(),
            Fingerprints = new FingerprintSurface()
        };
        var backDoor = new SublocationConnection
        {
            Id = state.GenerateEntityId(),
            FromSublocationId = 200,
            ToSublocationId = 300,
            Type = ConnectionType.Door,
            Tags = new[] { "covert_entry" },
            Lockable = new LockableProperty(),
            Fingerprints = new FingerprintSurface()
        };
        home.Connections.Add(frontDoor);
        home.Connections.Add(backDoor);

        var key = new Item
        {
            Id = state.GenerateEntityId(),
            ItemType = ItemType.Key,
            Fingerprints = new FingerprintSurface(),
            Data = new Dictionary<string, object> { ["TargetConnectionId"] = frontDoor.Id }
        };
        state.Items[key.Id] = key;
        frontDoor.Lockable.KeyItemId = key.Id;
        backDoor.Lockable.KeyItemId = key.Id;

        var person = new Person
        {
            Id = state.GenerateEntityId(),
            HomeAddressId = home.Id,
            CurrentAddressId = home.Id,
            InventoryItemIds = new List<int> { key.Id }
        };
        key.HeldByEntityId = person.Id;
        state.People[person.Id] = person;

        return (state, person, home, key, new List<SublocationConnection> { frontDoor, backDoor });
    }

    [Fact]
    public void LockEntrances_LocksAllDoors_WhenNoForget()
    {
        var (state, person, home, key, conns) = CreateSuburbanScenario();

        bool allLockedOnce = false;
        for (int i = 0; i < 100; i++)
        {
            foreach (var c in conns) c.Lockable.IsLocked = false;
            DoorLockingService.LockEntrances(state, person);
            if (conns.All(c => c.Lockable.IsLocked))
            {
                allLockedOnce = true;
                break;
            }
        }
        Assert.True(allLockedOnce);
    }

    [Fact]
    public void LockEntrances_DepositsKeyFingerprint()
    {
        var (state, person, home, key, conns) = CreateSuburbanScenario();

        DoorLockingService.LockEntrances(state, person);

        Assert.NotEmpty(key.Fingerprints.TraceIds);
    }

    [Fact]
    public void LockEntrances_ForgetChance_SomeDoorsRemainUnlocked()
    {
        int forgottenCount = 0;
        for (int t = 0; t < 200; t++)
        {
            var (state, person, home, key, conns) = CreateSuburbanScenario();
            DoorLockingService.LockEntrances(state, person);
            if (conns.Any(c => !c.Lockable.IsLocked))
                forgottenCount++;
        }
        Assert.InRange(forgottenCount, 10, 80);
    }

    [Fact]
    public void UnlockEntrances_UnlocksAllDoors()
    {
        var (state, person, home, key, conns) = CreateSuburbanScenario();
        foreach (var c in conns) c.Lockable.IsLocked = true;

        DoorLockingService.UnlockEntrances(state, person);

        Assert.All(conns, c => Assert.False(c.Lockable.IsLocked));
    }

    [Fact]
    public void UnlockEntrances_DepositsKeyFingerprint()
    {
        var (state, person, home, key, conns) = CreateSuburbanScenario();
        foreach (var c in conns) c.Lockable.IsLocked = true;

        DoorLockingService.UnlockEntrances(state, person);

        Assert.NotEmpty(key.Fingerprints.TraceIds);
    }
}
