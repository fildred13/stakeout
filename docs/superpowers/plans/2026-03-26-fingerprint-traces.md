# Fingerprint Traces Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add fingerprint traces to doors and keys, with smudging decay and automatic door locking behavior.

**Architecture:** Fingerprints are `Trace` entities indexed on surfaces via a new `FingerprintSurface` composable property. `FingerprintService` handles deposit + smudge. `DoorLockingService` handles lock/unlock on leave/sleep/arrive. Both hook into `PersonBehavior`.

**Tech Stack:** Godot 4.6, C# (.NET), Xunit for tests

**Spec:** `docs/superpowers/specs/2026-03-26-fingerprint-traces-design.md`

---

## File Map

| File | Action | Responsibility |
|------|--------|---------------|
| `src/simulation/traces/Trace.cs` | Modify | Add `Fingerprint` to `TraceType` enum |
| `src/simulation/entities/ConnectionProperties.cs` | Modify | Add `FingerprintSurface` class |
| `src/simulation/entities/Sublocation.cs` | Modify | Add `FingerprintSurface` property to `SublocationConnection` |
| `src/simulation/entities/Item.cs` | Modify | Add `FingerprintSurface` property to `Item` |
| `src/simulation/traces/FingerprintService.cs` | Create | Deposit + smudge logic for connections and items |
| `src/simulation/scheduling/DoorLockingService.cs` | Create | Lock/unlock entrances, key fingerprints, forget chance |
| `src/simulation/scheduling/PersonBehavior.cs` | Modify | Hook fingerprint deposits on traversal + locking on transitions/arrival |
| `src/simulation/PersonGenerator.cs` | Modify | Assign key to all lockable residence connections |
| `src/simulation/sublocations/SuburbanHomeGenerator.cs` | Modify | Add `FingerprintSurface` to all connections |
| `src/simulation/sublocations/ApartmentBuildingGenerator.cs` | Modify | Add `FingerprintSurface` to all connections |
| `src/simulation/sublocations/DinerGenerator.cs` | Modify | Add `FingerprintSurface` to all connections |
| `src/simulation/sublocations/OfficeGenerator.cs` | Modify | Add `FingerprintSurface` to all connections |
| `src/simulation/sublocations/DiveBarGenerator.cs` | Modify | Add `FingerprintSurface` to all connections |
| `src/simulation/sublocations/ParkGenerator.cs` | Modify | Add `FingerprintSurface` to all connections |
| `stakeout.tests/Simulation/Traces/FingerprintServiceTests.cs` | Create | Tests for deposit + smudge logic |
| `stakeout.tests/Simulation/Scheduling/DoorLockingServiceTests.cs` | Create | Tests for lock/unlock behavior |
| `stakeout.tests/Simulation/Scheduling/PersonBehaviorFingerprintTests.cs` | Create | Integration tests for fingerprint hooks in PersonBehavior |
| `stakeout.tests/Simulation/PersonGeneratorTests.cs` | Modify | Tests for multi-door key assignment |

---

### Task 1: Add `Fingerprint` to `TraceType` and `FingerprintSurface` class

**Files:**
- Modify: `src/simulation/traces/Trace.cs:6-9`
- Modify: `src/simulation/entities/ConnectionProperties.cs:41-45` (add after BreakableProperty)
- Modify: `src/simulation/entities/Sublocation.cs:37-40` (add new property)
- Modify: `src/simulation/entities/Item.cs:10-18` (add new property)

- [ ] **Step 1: Write test for new TraceType value**

In `stakeout.tests/Simulation/Traces/TraceTests.cs`, add:

```csharp
[Fact]
public void Trace_FingerprintType_HasSurfaceData()
{
    var trace = new Trace
    {
        Id = 3, TraceType = TraceType.Fingerprint,
        CreatedAt = new DateTime(1980, 1, 2, 8, 0, 0),
        CreatedByPersonId = 7,
        Description = "Fingerprint",
        Data = new Dictionary<string, object>
        {
            ["SurfaceType"] = "Connection",
            ["SurfaceId"] = 42,
            ["Side"] = "A"
        }
    };
    Assert.Equal(TraceType.Fingerprint, trace.TraceType);
    Assert.Equal("Connection", trace.Data["SurfaceType"]);
    Assert.Equal(42, trace.Data["SurfaceId"]);
    Assert.Equal("A", trace.Data["Side"]);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test stakeout.tests/ --filter "Trace_FingerprintType_HasSurfaceData" -v minimal`
Expected: FAIL — `TraceType` does not contain `Fingerprint`

- [ ] **Step 3: Add `Fingerprint` to `TraceType` enum**

In `src/simulation/traces/Trace.cs`, change line 8:

```csharp
// Before:
Item, Sighting, Mark, Condition, Record

// After:
Item, Sighting, Mark, Condition, Record, Fingerprint
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test stakeout.tests/ --filter "Trace_FingerprintType_HasSurfaceData" -v minimal`
Expected: PASS

- [ ] **Step 5: Write test for FingerprintSurface on connection**

In `stakeout.tests/Simulation/Entities/ConnectionPropertiesTests.cs`, add:

```csharp
[Fact]
public void FingerprintSurface_DefaultsToEmptyLists()
{
    var surface = new FingerprintSurface();
    Assert.Empty(surface.SideATraceIds);
    Assert.Empty(surface.SideBTraceIds);
}

[Fact]
public void SublocationConnection_FingerprintSurface_NullByDefault()
{
    var conn = new SublocationConnection();
    Assert.Null(conn.Fingerprints);
}

[Fact]
public void Item_FingerprintSurface_NullByDefault()
{
    var item = new Item();
    Assert.Null(item.Fingerprints);
}
```

- [ ] **Step 6: Run tests to verify they fail**

Run: `dotnet test stakeout.tests/ --filter "FingerprintSurface_DefaultsToEmptyLists|SublocationConnection_FingerprintSurface_NullByDefault|Item_FingerprintSurface_NullByDefault" -v minimal`
Expected: FAIL — `FingerprintSurface` class doesn't exist

- [ ] **Step 7: Implement FingerprintSurface and add properties**

In `src/simulation/entities/ConnectionProperties.cs`, add after `BreakableProperty` (after line 45):

```csharp
public class FingerprintSurface
{
    public List<int> SideATraceIds { get; set; } = new();
    public List<int> SideBTraceIds { get; set; } = new();
    public List<int> TraceIds { get; set; } = new();
}
```

Add `using System.Collections.Generic;` at the top of the file.

In `src/simulation/entities/Sublocation.cs`, add after line 40 (after `Breakable`):

```csharp
public FingerprintSurface Fingerprints { get; set; }
```

In `src/simulation/entities/Item.cs`, add after line 17 (after `Data`):

```csharp
public FingerprintSurface Fingerprints { get; set; }
```

- [ ] **Step 8: Run tests to verify they pass**

Run: `dotnet test stakeout.tests/ --filter "FingerprintSurface_DefaultsToEmptyLists|SublocationConnection_FingerprintSurface_NullByDefault|Item_FingerprintSurface_NullByDefault" -v minimal`
Expected: PASS

- [ ] **Step 9: Run full test suite**

Run: `dotnet test stakeout.tests/ -v minimal`
Expected: All tests pass (no regressions)

- [ ] **Step 10: Commit**

```bash
git add src/simulation/traces/Trace.cs src/simulation/entities/ConnectionProperties.cs src/simulation/entities/Sublocation.cs src/simulation/entities/Item.cs stakeout.tests/Simulation/Traces/TraceTests.cs stakeout.tests/Simulation/Entities/ConnectionPropertiesTests.cs
git commit -m "Add Fingerprint trace type and FingerprintSurface composable property"
```

---

### Task 2: FingerprintService — deposit and smudge logic

**Files:**
- Create: `src/simulation/traces/FingerprintService.cs`
- Create: `stakeout.tests/Simulation/Traces/FingerprintServiceTests.cs`

- [ ] **Step 1: Write test for depositing fingerprint on a connection**

Create `stakeout.tests/Simulation/Traces/FingerprintServiceTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using Stakeout.Simulation;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Traces;
using Xunit;

namespace Stakeout.Tests.Simulation.Traces;

public class FingerprintServiceTests
{
    private static SimulationState CreateState()
    {
        return new SimulationState(new GameClock(new DateTime(1980, 1, 1, 8, 0, 0)));
    }

    private static SublocationConnection CreateConnection(SimulationState state, int fromSubId, int toSubId)
    {
        return new SublocationConnection
        {
            Id = state.GenerateEntityId(),
            FromSublocationId = fromSubId,
            ToSublocationId = toSubId,
            Type = ConnectionType.Door,
            Fingerprints = new FingerprintSurface()
        };
    }

    [Fact]
    public void DepositFingerprint_Connection_CreatesTraceOnCorrectSide()
    {
        var state = CreateState();
        var conn = CreateConnection(state, fromSubId: 10, toSubId: 20);

        FingerprintService.DepositFingerprint(state, personId: 1, conn, fromSublocationId: 10);

        Assert.Single(conn.Fingerprints.SideATraceIds);
        Assert.Empty(conn.Fingerprints.SideBTraceIds);
        var traceId = conn.Fingerprints.SideATraceIds[0];
        var trace = state.Traces[traceId];
        Assert.Equal(TraceType.Fingerprint, trace.TraceType);
        Assert.Equal(1, trace.CreatedByPersonId);
        Assert.Equal("Connection", trace.Data["SurfaceType"]);
        Assert.Equal(conn.Id, trace.Data["SurfaceId"]);
        Assert.Equal("A", trace.Data["Side"]);
    }

    [Fact]
    public void DepositFingerprint_Connection_SideB_WhenComingFromToSublocation()
    {
        var state = CreateState();
        var conn = CreateConnection(state, fromSubId: 10, toSubId: 20);

        FingerprintService.DepositFingerprint(state, personId: 1, conn, fromSublocationId: 20);

        Assert.Empty(conn.Fingerprints.SideATraceIds);
        Assert.Single(conn.Fingerprints.SideBTraceIds);
        var trace = state.Traces[conn.Fingerprints.SideBTraceIds[0]];
        Assert.Equal("B", trace.Data["Side"]);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test stakeout.tests/ --filter "DepositFingerprint_Connection" -v minimal`
Expected: FAIL — `FingerprintService` class doesn't exist

- [ ] **Step 3: Implement FingerprintService deposit for connections**

Create `src/simulation/traces/FingerprintService.cs`:

```csharp
using System;
using System.Collections.Generic;
using Stakeout.Simulation.Entities;

namespace Stakeout.Simulation.Traces;

public static class FingerprintService
{
    public static void DepositFingerprint(SimulationState state, int personId, SublocationConnection conn, int fromSublocationId)
    {
        var side = conn.FromSublocationId == fromSublocationId ? "A" : "B";
        var sideList = side == "A" ? conn.Fingerprints.SideATraceIds : conn.Fingerprints.SideBTraceIds;

        var trace = CreateFingerprintTrace(state, personId, "Connection", conn.Id, side);
        sideList.Add(trace.Id);

        Smudge(state, sideList, trace.Id);
    }

    public static void DepositFingerprint(SimulationState state, int personId, Item item)
    {
        var trace = CreateFingerprintTrace(state, personId, "Item", item.Id, null);
        item.Fingerprints.TraceIds.Add(trace.Id);

        Smudge(state, item.Fingerprints.TraceIds, trace.Id);
    }

    private static Trace CreateFingerprintTrace(SimulationState state, int personId, string surfaceType, int surfaceId, string side)
    {
        var trace = new Trace
        {
            Id = state.GenerateEntityId(),
            TraceType = TraceType.Fingerprint,
            CreatedAt = state.Clock.CurrentTime,
            CreatedByPersonId = personId,
            Description = "Fingerprint",
            Data = new Dictionary<string, object>
            {
                ["SurfaceType"] = surfaceType,
                ["SurfaceId"] = surfaceId,
                ["Side"] = side
            }
        };
        state.Traces[trace.Id] = trace;
        return trace;
    }

    private static void Smudge(SimulationState state, List<int> traceIds, int newTraceId)
    {
        var random = new Random();
        var toRemove = new List<int>();

        foreach (var id in traceIds)
        {
            if (id == newTraceId) continue;

            // N = total fingerprints on surface minus the one being evaluated
            int n = traceIds.Count - 1;
            int chance = Math.Min(100, n * 25);

            if (random.Next(100) < chance)
            {
                toRemove.Add(id);
            }
        }

        foreach (var id in toRemove)
        {
            traceIds.Remove(id);
            state.Traces.Remove(id);
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test stakeout.tests/ --filter "DepositFingerprint_Connection" -v minimal`
Expected: PASS

- [ ] **Step 5: Write test for depositing fingerprint on an item**

Add to `FingerprintServiceTests.cs`:

```csharp
[Fact]
public void DepositFingerprint_Item_CreatesTraceOnItem()
{
    var state = CreateState();
    var item = new Item
    {
        Id = state.GenerateEntityId(),
        ItemType = ItemType.Key,
        Fingerprints = new FingerprintSurface()
    };

    FingerprintService.DepositFingerprint(state, personId: 1, item);

    Assert.Single(item.Fingerprints.TraceIds);
    var trace = state.Traces[item.Fingerprints.TraceIds[0]];
    Assert.Equal(TraceType.Fingerprint, trace.TraceType);
    Assert.Equal(1, trace.CreatedByPersonId);
    Assert.Equal("Item", trace.Data["SurfaceType"]);
    Assert.Equal(item.Id, trace.Data["SurfaceId"]);
    Assert.Null(trace.Data["Side"]);
}
```

- [ ] **Step 6: Run test to verify it passes**

Run: `dotnet test stakeout.tests/ --filter "DepositFingerprint_Item_CreatesTraceOnItem" -v minimal`
Expected: PASS (already implemented)

- [ ] **Step 7: Write test for smudging — guaranteed erasure at 5+ prints**

Add to `FingerprintServiceTests.cs`:

```csharp
[Fact]
public void DepositFingerprint_FivePrintsOnSurface_OldPrintsGuaranteedErased()
{
    var state = CreateState();
    var item = new Item
    {
        Id = state.GenerateEntityId(),
        ItemType = ItemType.Key,
        Fingerprints = new FingerprintSurface()
    };

    // Deposit 4 prints first (they survive because chance is low with few prints)
    // We directly add trace IDs to simulate pre-existing prints
    for (int i = 0; i < 4; i++)
    {
        var oldTrace = new Trace
        {
            Id = state.GenerateEntityId(),
            TraceType = TraceType.Fingerprint,
            CreatedAt = state.Clock.CurrentTime,
            CreatedByPersonId = i + 10,
            Data = new Dictionary<string, object>
            {
                ["SurfaceType"] = "Item",
                ["SurfaceId"] = item.Id,
                ["Side"] = (object)null
            }
        };
        state.Traces[oldTrace.Id] = oldTrace;
        item.Fingerprints.TraceIds.Add(oldTrace.Id);
    }

    // Deposit a 5th — now there are 5 total. Each old print sees N=4, chance=100%
    FingerprintService.DepositFingerprint(state, personId: 99, item);

    // Only the new print should remain
    Assert.Single(item.Fingerprints.TraceIds);
    var remainingTrace = state.Traces[item.Fingerprints.TraceIds[0]];
    Assert.Equal(99, remainingTrace.CreatedByPersonId);
}
```

- [ ] **Step 8: Run test to verify it passes**

Run: `dotnet test stakeout.tests/ --filter "FivePrintsOnSurface_OldPrintsGuaranteedErased" -v minimal`
Expected: PASS

- [ ] **Step 9: Write test for smudging — no erasure with only 1 old print (deterministic)**

To test probabilistic smudging deterministically, we need to inject a `Random` into `FingerprintService`. Refactor `Smudge` to accept a `Random` parameter.

Add to `FingerprintServiceTests.cs`:

```csharp
[Fact]
public void DepositFingerprint_TwoPrintsOnSurface_SmudgeChanceIs25Percent()
{
    // With 2 total prints, the old one has N=1 → 25% chance.
    // Use a seeded random to get deterministic behavior.
    // We'll run 1000 trials and check the ratio is approximately 25%.
    int erased = 0;
    int trials = 1000;

    for (int t = 0; t < trials; t++)
    {
        var state = CreateState();
        var item = new Item
        {
            Id = state.GenerateEntityId(),
            ItemType = ItemType.Key,
            Fingerprints = new FingerprintSurface()
        };

        // Add one existing print
        var oldTrace = new Trace
        {
            Id = state.GenerateEntityId(),
            TraceType = TraceType.Fingerprint,
            CreatedAt = state.Clock.CurrentTime,
            CreatedByPersonId = 10,
            Data = new Dictionary<string, object>
            {
                ["SurfaceType"] = "Item",
                ["SurfaceId"] = item.Id,
                ["Side"] = (object)null
            }
        };
        state.Traces[oldTrace.Id] = oldTrace;
        item.Fingerprints.TraceIds.Add(oldTrace.Id);

        FingerprintService.DepositFingerprint(state, personId: 99, item);

        if (item.Fingerprints.TraceIds.Count == 1)
            erased++;
    }

    // 25% ± tolerance. With 1000 trials, expect ~250 ± 50.
    Assert.InRange(erased, 150, 350);
}
```

- [ ] **Step 10: Run test to verify it passes**

Run: `dotnet test stakeout.tests/ --filter "SmudgeChanceIs25Percent" -v minimal`
Expected: PASS

- [ ] **Step 11: Run full test suite**

Run: `dotnet test stakeout.tests/ -v minimal`
Expected: All tests pass

- [ ] **Step 12: Commit**

```bash
git add src/simulation/traces/FingerprintService.cs stakeout.tests/Simulation/Traces/FingerprintServiceTests.cs
git commit -m "Add FingerprintService with deposit and smudge logic"
```

---

### Task 3: Add FingerprintSurface to all sublocation generators

**Files:**
- Modify: `src/simulation/sublocations/SuburbanHomeGenerator.cs`
- Modify: `src/simulation/sublocations/ApartmentBuildingGenerator.cs`
- Modify: `src/simulation/sublocations/DinerGenerator.cs`
- Modify: `src/simulation/sublocations/OfficeGenerator.cs`
- Modify: `src/simulation/sublocations/DiveBarGenerator.cs`
- Modify: `src/simulation/sublocations/ParkGenerator.cs`

Every generator uses a local `Connect()` helper that creates a `SublocationConnection`. The simplest approach: in each generator's `Connect` method, ensure `conn.Fingerprints` is initialized.

- [ ] **Step 1: Write test that generated connections have FingerprintSurface**

Add to `stakeout.tests/Simulation/Entities/ConnectionPropertiesTests.cs`:

```csharp
[Fact]
public void GeneratedConnections_AllHaveFingerprintSurface()
{
    var state = new SimulationState(new GameClock(new DateTime(1980, 1, 1)));
    var address = new Address
    {
        Id = state.GenerateEntityId(),
        Type = AddressType.SuburbanHome,
        Position = new Godot.Vector2(100, 100),
        Number = 1,
        StreetId = 1
    };
    state.Addresses[address.Id] = address;
    var generator = new SuburbanHomeGenerator();
    generator.Generate(address, state, new Random(42));

    foreach (var conn in address.Connections)
    {
        Assert.NotNull(conn.Fingerprints);
    }
}
```

Add the necessary using statements: `using Stakeout.Simulation;`, `using Stakeout.Simulation.Sublocations;`, `using Stakeout.Simulation.Entities;`.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test stakeout.tests/ --filter "GeneratedConnections_AllHaveFingerprintSurface" -v minimal`
Expected: FAIL — `Fingerprints` is null on some connections

- [ ] **Step 3: Update all generators to add FingerprintSurface**

In each generator's `Connect` helper, add `FingerprintSurface` initialization. The pattern is the same in every generator. In the `Connect` method, after `conn.Id = state.GenerateEntityId();` and before `conns.Add(conn);`, add:

```csharp
conn.Fingerprints ??= new FingerprintSurface();
```

This uses `??=` so that if a template already set `Fingerprints`, it's preserved; otherwise a new empty one is created. Apply to all 6 generators:

1. `SuburbanHomeGenerator.cs` — in `Connect()` at ~line 33
2. `ApartmentBuildingGenerator.cs` — in its `Connect()` helper
3. `DinerGenerator.cs` — in its `Connect()` helper
4. `OfficeGenerator.cs` — in its `Connect()` helper
5. `DiveBarGenerator.cs` — in its `Connect()` helper
6. `ParkGenerator.cs` — in its `Connect()` helper

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test stakeout.tests/ --filter "GeneratedConnections_AllHaveFingerprintSurface" -v minimal`
Expected: PASS

- [ ] **Step 5: Run full test suite**

Run: `dotnet test stakeout.tests/ -v minimal`
Expected: All tests pass

- [ ] **Step 6: Commit**

```bash
git add src/simulation/sublocations/SuburbanHomeGenerator.cs src/simulation/sublocations/ApartmentBuildingGenerator.cs src/simulation/sublocations/DinerGenerator.cs src/simulation/sublocations/OfficeGenerator.cs src/simulation/sublocations/DiveBarGenerator.cs src/simulation/sublocations/ParkGenerator.cs stakeout.tests/Simulation/Entities/ConnectionPropertiesTests.cs
git commit -m "Add FingerprintSurface to all generated connections"
```

---

### Task 4: Update PersonGenerator to assign key to all lockable residence connections

**Files:**
- Modify: `src/simulation/PersonGenerator.cs:238-270`
- Modify: `stakeout.tests/Simulation/PersonGeneratorTests.cs`

- [ ] **Step 1: Write test for multi-door key assignment (suburban)**

Add to `stakeout.tests/Simulation/PersonGeneratorTests.cs`:

```csharp
[Fact]
public void GeneratePerson_SuburbanHome_KeyAssignedToAllLockableConnections()
{
    var state = CreateState();
    var gen = CreateGenerator();
    for (int i = 0; i < 100; i++)
    {
        gen.GeneratePerson(state);
    }
    var suburbanResident = state.People.Values
        .First(p => state.Addresses[p.HomeAddressId].Type == AddressType.SuburbanHome);
    var itemId = suburbanResident.InventoryItemIds[0];
    var homeAddress = state.Addresses[suburbanResident.HomeAddressId];
    var lockableConns = homeAddress.Connections.Where(c => c.Lockable != null).ToList();

    // Suburban homes have front door, back door, and ground floor window — all lockable
    Assert.True(lockableConns.Count >= 2);
    foreach (var conn in lockableConns)
    {
        Assert.Equal(itemId, conn.Lockable.KeyItemId);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test stakeout.tests/ --filter "SuburbanHome_KeyAssignedToAllLockableConnections" -v minimal`
Expected: FAIL — only the entrance door has KeyItemId set

- [ ] **Step 3: Update CreateHomeKey to assign key to all lockable residence connections**

In `src/simulation/PersonGenerator.cs`, replace the `CreateHomeKey` method (lines 238-270):

```csharp
private static void CreateHomeKey(SimulationState state, Person person, Address homeAddress)
{
    // Find the primary entrance connection for this person's home
    SublocationConnection entranceConn = null;
    if (person.HomeUnitTag != null)
    {
        entranceConn = homeAddress.Connections
            .FirstOrDefault(c => c.Tags != null && c.Tags.Contains(person.HomeUnitTag));
    }
    else
    {
        entranceConn = homeAddress.Connections
            .FirstOrDefault(c => c.Tags != null && c.Tags.Contains("entrance"));
    }

    if (entranceConn?.Lockable == null) return;

    var key = new Item
    {
        Id = state.GenerateEntityId(),
        ItemType = ItemType.Key,
        HeldByEntityId = person.Id,
        Fingerprints = new FingerprintSurface(),
        Data = new Dictionary<string, object>
        {
            ["TargetConnectionId"] = entranceConn.Id
        }
    };
    state.Items[key.Id] = key;
    person.InventoryItemIds.Add(key.Id);

    // Assign key to all lockable connections belonging to this person's residence
    var residenceConnections = DoorLockingService.GetResidenceLockableConnections(homeAddress, person.HomeUnitTag);
    foreach (var conn in residenceConnections)
    {
        conn.Lockable.KeyItemId = key.Id;
    }
}

```

Before this compiles, we need the shared helper. Create `src/simulation/scheduling/DoorLockingService.cs` with just the utility method (the full service is completed in Task 5):

```csharp
using System.Collections.Generic;
using System.Linq;
using Stakeout.Simulation.Entities;

namespace Stakeout.Simulation.Scheduling;

public static class DoorLockingService
{
    internal static List<SublocationConnection> GetResidenceLockableConnections(Address home, string homeUnitTag)
    {
        if (homeUnitTag != null)
        {
            return home.Connections
                .Where(c => c.Lockable != null && c.Tags != null && c.Tags.Contains(homeUnitTag))
                .ToList();
        }
        else
        {
            return home.Connections
                .Where(c => c.Lockable != null)
                .ToList();
        }
    }
}
```

Add `using Stakeout.Simulation.Scheduling;` to `PersonGenerator.cs`.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test stakeout.tests/ --filter "SuburbanHome_KeyAssignedToAllLockableConnections" -v minimal`
Expected: PASS

- [ ] **Step 5: Write test for apartment key scoping**

Add to `PersonGeneratorTests.cs`:

```csharp
[Fact]
public void GeneratePerson_ApartmentResident_KeyOnlyAssignedToOwnUnitDoor()
{
    var state = CreateState();
    var gen = CreateGenerator();
    for (int i = 0; i < 100; i++)
    {
        gen.GeneratePerson(state);
    }
    var aptResident = state.People.Values
        .First(p => state.Addresses[p.HomeAddressId].Type == AddressType.ApartmentBuilding);
    var itemId = aptResident.InventoryItemIds[0];
    var homeAddress = state.Addresses[aptResident.HomeAddressId];

    // Only the unit door should have this person's key
    var connsWithKey = homeAddress.Connections
        .Where(c => c.Lockable != null && c.Lockable.KeyItemId == itemId)
        .ToList();
    Assert.All(connsWithKey, c => Assert.True(c.HasTag(aptResident.HomeUnitTag)));
}
```

- [ ] **Step 6: Run test to verify it passes**

Run: `dotnet test stakeout.tests/ --filter "ApartmentResident_KeyOnlyAssignedToOwnUnitDoor" -v minimal`
Expected: PASS

- [ ] **Step 7: Verify existing key tests still pass**

Run: `dotnet test stakeout.tests/ --filter "HomeKey|KeyTargets" -v minimal`
Expected: All existing key tests pass (backward compatible since `TargetConnectionId` still points to entrance)

- [ ] **Step 8: Run full test suite**

Run: `dotnet test stakeout.tests/ -v minimal`
Expected: All tests pass

- [ ] **Step 9: Commit**

```bash
git add src/simulation/PersonGenerator.cs src/simulation/scheduling/DoorLockingService.cs stakeout.tests/Simulation/PersonGeneratorTests.cs
git commit -m "Assign home key to all lockable residence connections"
```

---

### Task 5: DoorLockingService — lock/unlock behavior

**Files:**
- Modify: `src/simulation/scheduling/DoorLockingService.cs` (created in Task 4 with just the utility method)
- Create: `stakeout.tests/Simulation/Scheduling/DoorLockingServiceTests.cs`

- [ ] **Step 1: Write test for LockEntrances**

Create `stakeout.tests/Simulation/Scheduling/DoorLockingServiceTests.cs`:

```csharp
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
            Position = new Godot.Vector2(100, 100),
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

        // Run many times to confirm at least once all doors lock
        // (10% forget means 90%^2 = 81% chance both lock per trial)
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

        // Key should have fingerprints (one per door that was successfully locked)
        Assert.NotEmpty(key.Fingerprints.TraceIds);
    }

    [Fact]
    public void LockEntrances_ForgetChance_SomeDoorsRemainUnlocked()
    {
        // Over many trials, at least one trial should have a forgotten door
        int forgottenCount = 0;
        for (int t = 0; t < 200; t++)
        {
            var (state, person, home, key, conns) = CreateSuburbanScenario();
            DoorLockingService.LockEntrances(state, person);
            if (conns.Any(c => !c.Lockable.IsLocked))
                forgottenCount++;
        }
        // With 2 doors and 10% forget each: P(at least one forgotten) ≈ 19%
        // Over 200 trials, expect ~38. Allow wide range.
        Assert.InRange(forgottenCount, 10, 80);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test stakeout.tests/ --filter "LockEntrances" -v minimal`
Expected: FAIL — `LockEntrances` method doesn't exist yet (class was created in Task 4 with only the utility method)

- [ ] **Step 3: Add lock/unlock methods to DoorLockingService**

`DoorLockingService.cs` already exists from Task 4 with `GetResidenceLockableConnections`. Add `LockEntrances`, `UnlockEntrances`, and `FindHomeKey`. The full file should now be:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Traces;

namespace Stakeout.Simulation.Scheduling;

public static class DoorLockingService
{
    private const double ForgetChance = 0.10;

    public static void LockEntrances(SimulationState state, Person person)
    {
        var home = state.Addresses[person.HomeAddressId];
        var lockableConns = GetResidenceLockableConnections(home, person.HomeUnitTag);
        var key = FindHomeKey(state, person);

        var random = new Random();
        foreach (var conn in lockableConns)
        {
            if (random.NextDouble() < ForgetChance)
                continue; // Forgot to lock this door

            conn.Lockable.IsLocked = true;

            if (key?.Fingerprints != null)
                FingerprintService.DepositFingerprint(state, person.Id, key);
        }
    }

    public static void UnlockEntrances(SimulationState state, Person person)
    {
        var home = state.Addresses[person.HomeAddressId];
        var lockableConns = GetResidenceLockableConnections(home, person.HomeUnitTag);
        var key = FindHomeKey(state, person);

        foreach (var conn in lockableConns)
        {
            conn.Lockable.IsLocked = false;

            if (key?.Fingerprints != null)
                FingerprintService.DepositFingerprint(state, person.Id, key);
        }
    }

    internal static List<SublocationConnection> GetResidenceLockableConnections(Address home, string homeUnitTag)
    {
        if (homeUnitTag != null)
        {
            return home.Connections
                .Where(c => c.Lockable != null && c.Tags != null && c.Tags.Contains(homeUnitTag))
                .ToList();
        }
        else
        {
            return home.Connections
                .Where(c => c.Lockable != null)
                .ToList();
        }
    }

    private static Item FindHomeKey(SimulationState state, Person person)
    {
        foreach (var itemId in person.InventoryItemIds)
        {
            if (state.Items.TryGetValue(itemId, out var item) && item.ItemType == ItemType.Key)
                return item;
        }
        return null;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test stakeout.tests/ --filter "LockEntrances" -v minimal`
Expected: PASS

- [ ] **Step 5: Write test for UnlockEntrances**

Add to `DoorLockingServiceTests.cs`:

```csharp
[Fact]
public void UnlockEntrances_UnlocksAllDoors()
{
    var (state, person, home, key, conns) = CreateSuburbanScenario();

    // Lock them first
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
```

- [ ] **Step 6: Run tests to verify they pass**

Run: `dotnet test stakeout.tests/ --filter "UnlockEntrances" -v minimal`
Expected: PASS

- [ ] **Step 7: Run full test suite**

Run: `dotnet test stakeout.tests/ -v minimal`
Expected: All tests pass

- [ ] **Step 8: Commit**

```bash
git add src/simulation/scheduling/DoorLockingService.cs stakeout.tests/Simulation/Scheduling/DoorLockingServiceTests.cs
git commit -m "Add DoorLockingService for lock/unlock with forget chance and key fingerprints"
```

---

### Task 6: Hook fingerprints and locking into PersonBehavior

**Files:**
- Modify: `src/simulation/scheduling/PersonBehavior.cs`
- Create: `stakeout.tests/Simulation/Scheduling/PersonBehaviorFingerprintTests.cs`

This is the integration task. PersonBehavior needs three hooks:

1. **Traversal fingerprint:** When `CurrentSublocationId` changes and the schedule entry has `ViaConnectionId`, deposit a fingerprint on that connection.
2. **Lock on leave/sleep:** In `Transition()`, when leaving home or going to sleep, call `DoorLockingService.LockEntrances`.
3. **Unlock on arrive:** In `UpdateTravel()`, when arriving at home, call `DoorLockingService.UnlockEntrances`.

- [ ] **Step 1: Write test for traversal fingerprint deposit**

Create `stakeout.tests/Simulation/Scheduling/PersonBehaviorFingerprintTests.cs`:

```csharp
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
        // Schedule entry: same action, different sublocation, with ViaConnectionId
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

        // Person moved to sub2
        Assert.Equal(sub2.Id, person.CurrentSublocationId);
        // Fingerprint deposited on side A (coming from sub1 = FromSublocationId)
        Assert.Single(conn.Fingerprints.SideATraceIds);
        var trace = state.Traces[conn.Fingerprints.SideATraceIds[0]];
        Assert.Equal(TraceType.Fingerprint, trace.TraceType);
        Assert.Equal(person.Id, trace.CreatedByPersonId);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test stakeout.tests/ --filter "SublocationChange_DepositsFingerprint_WhenViaConnectionId" -v minimal`
Expected: FAIL — no fingerprint deposited (PersonBehavior doesn't call FingerprintService yet)

- [ ] **Step 3: Add traversal fingerprint hook to PersonBehavior**

In `src/simulation/scheduling/PersonBehavior.cs`, add `using Stakeout.Simulation.Traces;` at the top.

Modify the sublocation-change block in `Update()` (around lines 41-45). Change:

```csharp
else if (entry.TargetSublocationId != person.CurrentSublocationId)
{
    // Same action, different sublocation — just move within the address
    person.CurrentSublocationId = entry.TargetSublocationId;
}
```

To:

```csharp
else if (entry.TargetSublocationId != person.CurrentSublocationId)
{
    // Same action, different sublocation — move within the address
    var previousSublocationId = person.CurrentSublocationId;
    person.CurrentSublocationId = entry.TargetSublocationId;

    // Deposit fingerprint on the connection traversed
    if (entry.ViaConnectionId.HasValue && previousSublocationId.HasValue)
    {
        var conn = FindConnection(state, entry.ViaConnectionId.Value);
        if (conn?.Fingerprints != null)
        {
            FingerprintService.DepositFingerprint(state, person.Id, conn, previousSublocationId.Value);
        }
    }
}
```

Also add the same fingerprint logic in `Transition()` where `CurrentSublocationId` is assigned (line 110). Change:

```csharp
// Same location, switch activity directly
person.CurrentAction = entry.Action;
person.CurrentSublocationId = entry.TargetSublocationId;
LogActivityStart(person, entry.Action, state);
```

To:

```csharp
// Same location, switch activity directly
var previousSublocationId = person.CurrentSublocationId;
person.CurrentAction = entry.Action;
person.CurrentSublocationId = entry.TargetSublocationId;
LogActivityStart(person, entry.Action, state);

// Deposit fingerprint on connection traversed
if (entry.ViaConnectionId.HasValue && previousSublocationId.HasValue
    && entry.TargetSublocationId != previousSublocationId)
{
    var conn = FindConnection(state, entry.ViaConnectionId.Value);
    if (conn?.Fingerprints != null)
    {
        FingerprintService.DepositFingerprint(state, person.Id, conn, previousSublocationId.Value);
    }
}
```

Add helper method at the bottom of the class:

```csharp
private static SublocationConnection FindConnection(SimulationState state, int addressId, int connectionId)
{
    if (!state.Addresses.TryGetValue(addressId, out var address)) return null;
    return address.Connections.FirstOrDefault(c => c.Id == connectionId);
}
```

**Important:** Generators add connections to `address.Connections` but NOT to `state.SublocationConnections`. The lookup must go through the address. Update the two call sites above to pass `person.CurrentAddressId.Value`:

```csharp
var conn = FindConnection(state, person.CurrentAddressId.Value, entry.ViaConnectionId.Value);
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test stakeout.tests/ --filter "SublocationChange_DepositsFingerprint_WhenViaConnectionId" -v minimal`
Expected: PASS

- [ ] **Step 5: Write test for locking on leaving home**

Add to `PersonBehaviorFingerprintTests.cs`:

```csharp
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

    // Schedule: travel to work
    person.Schedule.Entries.Add(new ScheduleEntry
    {
        Action = ActionType.TravelByCar,
        StartTime = new TimeSpan(8, 0, 0),
        EndTime = new TimeSpan(9, 0, 0),
        TargetAddressId = work.Id,
        FromAddressId = home.Id
    });

    var behavior = new PersonBehavior(new MapConfig());

    // Run many times — at least once all doors should be locked (90% chance per door)
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
```

- [ ] **Step 6: Run test to verify it fails**

Run: `dotnet test stakeout.tests/ --filter "Transition_LeavingHome_LocksDoors" -v minimal`
Expected: FAIL — doors not being locked

- [ ] **Step 7: Add locking hooks to PersonBehavior.Transition**

In `PersonBehavior.Transition()`, add locking logic. After `LogActivityEnd` (line 89) and before starting travel, add:

```csharp
// Lock doors when leaving home
if (person.CurrentAddressId == person.HomeAddressId)
{
    var targetAddressId = GetTargetAddressId(person, entry);
    if (targetAddressId.HasValue && targetAddressId.Value != person.HomeAddressId)
    {
        DoorLockingService.LockEntrances(state, person);
    }
}

// Lock doors when going to sleep at home
if (entry.Action == ActionType.Sleep && person.CurrentAddressId == person.HomeAddressId)
{
    DoorLockingService.LockEntrances(state, person);
}
```

Place this right after `LogActivityEnd(person, oldActivity, state);` on line 89, before the `if (entry.Action == ActionType.TravelByCar)` block on line 91.

- [ ] **Step 8: Run test to verify it passes**

Run: `dotnet test stakeout.tests/ --filter "Transition_LeavingHome_LocksDoors" -v minimal`
Expected: PASS

- [ ] **Step 9: Write test for locking on going to sleep**

Add to `PersonBehaviorFingerprintTests.cs`:

```csharp
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

    // Schedule: go to sleep
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
```

- [ ] **Step 10: Run test to verify it passes**

Run: `dotnet test stakeout.tests/ --filter "Transition_GoingToSleep_LocksDoors" -v minimal`
Expected: PASS (already implemented by the locking hook in Step 7)

- [ ] **Step 11: Write test for unlocking on arriving home**

Add to `PersonBehaviorFingerprintTests.cs`:

```csharp
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
        CurrentAddressId = null, // travelling
        CurrentPosition = new Vector2(300, 100),
        CurrentAction = ActionType.TravelByCar,
        InventoryItemIds = new List<int> { key.Id },
        TravelInfo = new TravelInfo
        {
            FromPosition = work.Position,
            ToPosition = home.Position,
            DepartureTime = new DateTime(1980, 1, 1, 16, 0, 0),
            ArrivalTime = new DateTime(1980, 1, 1, 16, 30, 0), // Already past
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

    // Should have arrived and unlocked
    Assert.Equal(home.Id, person.CurrentAddressId);
    Assert.False(frontDoor.Lockable.IsLocked);
}
```

- [ ] **Step 12: Run test to verify it fails**

Run: `dotnet test stakeout.tests/ --filter "UpdateTravel_ArrivingHome_UnlocksDoors" -v minimal`
Expected: FAIL — door remains locked

- [ ] **Step 13: Add unlock hook to PersonBehavior.UpdateTravel**

In `PersonBehavior.UpdateTravel()`, after `person.CurrentAddressId = travel.ToAddressId;` (line 57), add:

```csharp
// Unlock doors when arriving home
if (travel.ToAddressId == person.HomeAddressId)
{
    DoorLockingService.UnlockEntrances(state, person);
}
```

- [ ] **Step 14: Run test to verify it passes**

Run: `dotnet test stakeout.tests/ --filter "UpdateTravel_ArrivingHome_UnlocksDoors" -v minimal`
Expected: PASS

- [ ] **Step 15: Run full test suite**

Run: `dotnet test stakeout.tests/ -v minimal`
Expected: All tests pass

- [ ] **Step 16: Commit**

```bash
git add src/simulation/scheduling/PersonBehavior.cs stakeout.tests/Simulation/Scheduling/PersonBehaviorFingerprintTests.cs
git commit -m "Hook fingerprint deposits and door locking into PersonBehavior"
```

---

### Task 7: Final integration test and cleanup

**Files:**
- Review all modified files for consistency

- [ ] **Step 1: Run full test suite**

Run: `dotnet test stakeout.tests/ -v minimal`
Expected: All tests pass

- [ ] **Step 2: Verify the build succeeds**

Run: `dotnet build stakeout.sln`
Expected: Build succeeded, 0 errors

- [ ] **Step 3: Commit any remaining changes**

If there are any unstaged fixes from the build/test run:

```bash
git add -A
git commit -m "Fix any remaining build issues from fingerprint traces implementation"
```

Only commit if there are actual changes. Skip if working tree is clean.
