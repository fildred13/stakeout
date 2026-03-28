using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Stakeout.Simulation;
using Stakeout.Simulation.Actions;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Scheduling;
using Stakeout.Simulation.Traces;
using Xunit;

namespace Stakeout.Tests.Simulation.Scheduling;

public class PersonBehaviorFingerprintTests
{
    [Fact]
    public void Update_SublocationChange_DepositsFingerprint_WhenViaConnectionId()
    {
        var state = new SimulationState(new GameClock(new DateTime(1980, 1, 1, 10, 0, 0)));
        var address = new Address
        {
            Id = state.GenerateEntityId(),
            Position = new Vector2(100, 100),
            Type = AddressType.SuburbanHome,
            Number = 1,
            StreetId = 1
        };
        state.Addresses[address.Id] = address;

        var sub1 = new Sublocation { Id = state.GenerateEntityId(), AddressId = address.Id, Name = "Room A" };
        var sub2 = new Sublocation { Id = state.GenerateEntityId(), AddressId = address.Id, Name = "Room B" };
        address.Sublocations[sub1.Id] = sub1;
        address.Sublocations[sub2.Id] = sub2;

        var conn = new SublocationConnection
        {
            Id = state.GenerateEntityId(),
            FromSublocationId = sub1.Id,
            ToSublocationId = sub2.Id,
            Type = ConnectionType.Door,
            Fingerprints = new FingerprintSurface()
        };
        address.Connections.Add(conn);

        var person = new Person
        {
            Id = state.GenerateEntityId(),
            FirstName = "Test",
            LastName = "Person",
            HomeAddressId = address.Id,
            CurrentAddressId = address.Id,
            CurrentPosition = address.Position,
            CurrentAction = ActionType.Idle,
            CurrentSublocationId = sub1.Id,
            Schedule = new DailySchedule()
        };
        person.Schedule.Entries.Add(new ScheduleEntry
        {
            Action = ActionType.Idle,
            StartTime = new TimeSpan(0, 0, 0),
            EndTime = new TimeSpan(23, 59, 59),
            TargetSublocationId = sub2.Id,
            ViaConnectionId = conn.Id
        });
        state.People[person.Id] = person;

        var behavior = new PersonBehavior(new MapConfig());
        behavior.Update(person, state);

        Assert.Equal(sub2.Id, person.CurrentSublocationId);
        Assert.Single(conn.Fingerprints.SideATraceIds);
        var trace = state.Traces[conn.Fingerprints.SideATraceIds[0]];
        Assert.Equal(TraceType.Fingerprint, trace.TraceType);
        Assert.Equal(person.Id, trace.CreatedByPersonId);
    }

    [Fact]
    public void Transition_LeavingHome_LocksDoors()
    {
        var state = new SimulationState(new GameClock(new DateTime(1980, 1, 1, 8, 0, 0)));
        var home = new Address
        {
            Id = state.GenerateEntityId(),
            Position = new Vector2(100, 100),
            Type = AddressType.SuburbanHome,
            Number = 1,
            StreetId = 1
        };
        var work = new Address
        {
            Id = state.GenerateEntityId(),
            Position = new Vector2(600, 100),
            Type = AddressType.Office,
            Number = 2,
            StreetId = 1
        };
        state.Addresses[home.Id] = home;
        state.Addresses[work.Id] = work;

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
        home.Connections.Add(frontDoor);

        var key = new Item
        {
            Id = state.GenerateEntityId(),
            ItemType = ItemType.Key,
            Fingerprints = new FingerprintSurface(),
            Data = new Dictionary<string, object> { ["TargetConnectionId"] = frontDoor.Id }
        };
        state.Items[key.Id] = key;
        frontDoor.Lockable.KeyItemId = key.Id;

        var person = new Person
        {
            Id = state.GenerateEntityId(),
            FirstName = "Test",
            LastName = "Person",
            HomeAddressId = home.Id,
            CurrentAddressId = home.Id,
            CurrentPosition = home.Position,
            CurrentAction = ActionType.Idle,
            InventoryItemIds = new List<int> { key.Id },
            Schedule = new DailySchedule()
        };
        key.HeldByEntityId = person.Id;
        state.People[person.Id] = person;

        person.Schedule.Entries.Add(new ScheduleEntry
        {
            Action = ActionType.TravelByCar,
            StartTime = new TimeSpan(8, 0, 0),
            EndTime = new TimeSpan(9, 0, 0),
            TargetAddressId = work.Id,
            FromAddressId = home.Id
        });

        var behavior = new PersonBehavior(new MapConfig());

        bool lockedOnce = false;
        for (int i = 0; i < 50; i++)
        {
            frontDoor.Lockable.IsLocked = false;
            person.CurrentAction = ActionType.Idle;
            person.CurrentAddressId = home.Id;
            person.TravelInfo = null;

            behavior.Update(person, state);

            if (frontDoor.Lockable.IsLocked)
            {
                lockedOnce = true;
                break;
            }
        }
        Assert.True(lockedOnce);
    }

    [Fact]
    public void Transition_GoingToSleep_LocksDoors()
    {
        var state = new SimulationState(new GameClock(new DateTime(1980, 1, 1, 22, 0, 0)));
        var home = new Address
        {
            Id = state.GenerateEntityId(),
            Position = new Vector2(100, 100),
            Type = AddressType.SuburbanHome,
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
        home.Connections.Add(frontDoor);

        var key = new Item
        {
            Id = state.GenerateEntityId(),
            ItemType = ItemType.Key,
            Fingerprints = new FingerprintSurface(),
            Data = new Dictionary<string, object> { ["TargetConnectionId"] = frontDoor.Id }
        };
        state.Items[key.Id] = key;
        frontDoor.Lockable.KeyItemId = key.Id;

        var person = new Person
        {
            Id = state.GenerateEntityId(),
            FirstName = "Test",
            LastName = "Person",
            HomeAddressId = home.Id,
            CurrentAddressId = home.Id,
            CurrentPosition = home.Position,
            CurrentAction = ActionType.Idle,
            InventoryItemIds = new List<int> { key.Id },
            Schedule = new DailySchedule()
        };
        key.HeldByEntityId = person.Id;
        state.People[person.Id] = person;

        person.Schedule.Entries.Add(new ScheduleEntry
        {
            Action = ActionType.Sleep,
            StartTime = new TimeSpan(22, 0, 0),
            EndTime = new TimeSpan(7, 0, 0),
            TargetAddressId = home.Id
        });

        var behavior = new PersonBehavior(new MapConfig());

        bool lockedOnce = false;
        for (int i = 0; i < 50; i++)
        {
            frontDoor.Lockable.IsLocked = false;
            person.CurrentAction = ActionType.Idle;

            behavior.Update(person, state);

            if (frontDoor.Lockable.IsLocked)
            {
                lockedOnce = true;
                break;
            }
        }
        Assert.True(lockedOnce);
    }

    [Fact]
    public void UpdateTravel_ArrivingHome_UnlocksDoors()
    {
        var state = new SimulationState(new GameClock(new DateTime(1980, 1, 1, 17, 0, 0)));
        var home = new Address
        {
            Id = state.GenerateEntityId(),
            Position = new Vector2(100, 100),
            Type = AddressType.SuburbanHome,
            Number = 1,
            StreetId = 1
        };
        state.Addresses[home.Id] = home;
        var work = new Address
        {
            Id = state.GenerateEntityId(),
            Position = new Vector2(600, 100),
            Type = AddressType.Office,
            Number = 2,
            StreetId = 1
        };
        state.Addresses[work.Id] = work;

        var frontDoor = new SublocationConnection
        {
            Id = state.GenerateEntityId(),
            FromSublocationId = 100,
            ToSublocationId = 200,
            Type = ConnectionType.Door,
            Tags = new[] { "entrance" },
            Lockable = new LockableProperty { IsLocked = true },
            Fingerprints = new FingerprintSurface()
        };
        home.Connections.Add(frontDoor);

        var key = new Item
        {
            Id = state.GenerateEntityId(),
            ItemType = ItemType.Key,
            Fingerprints = new FingerprintSurface(),
            Data = new Dictionary<string, object> { ["TargetConnectionId"] = frontDoor.Id }
        };
        state.Items[key.Id] = key;
        frontDoor.Lockable.KeyItemId = key.Id;

        var person = new Person
        {
            Id = state.GenerateEntityId(),
            FirstName = "Test",
            LastName = "Person",
            HomeAddressId = home.Id,
            CurrentAddressId = null,
            CurrentPosition = new Vector2(300, 100),
            CurrentAction = ActionType.TravelByCar,
            InventoryItemIds = new List<int> { key.Id },
            TravelInfo = new TravelInfo
            {
                FromPosition = work.Position,
                ToPosition = home.Position,
                DepartureTime = new DateTime(1980, 1, 1, 16, 0, 0),
                ArrivalTime = new DateTime(1980, 1, 1, 16, 30, 0),
                FromAddressId = work.Id,
                ToAddressId = home.Id
            },
            Schedule = new DailySchedule()
        };
        person.Schedule.Entries.Add(new ScheduleEntry
        {
            Action = ActionType.Idle,
            StartTime = new TimeSpan(17, 0, 0),
            EndTime = new TimeSpan(23, 0, 0),
            TargetAddressId = home.Id
        });
        key.HeldByEntityId = person.Id;
        state.People[person.Id] = person;

        var behavior = new PersonBehavior(new MapConfig());
        behavior.Update(person, state);

        Assert.Equal(home.Id, person.CurrentAddressId);
        Assert.False(frontDoor.Lockable.IsLocked);
    }
}
