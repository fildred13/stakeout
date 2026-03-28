# Inventory System Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a general-purpose inventory system with keys as the first item type, spawned on people and the player at world generation.

**Architecture:** New `Item` entity with `ItemType` enum stored in `SimulationState.Items`. Person and Player get `InventoryItemIds` lists. Keys are created during person/player generation and linked to home entrance doors via `LockableProperty.KeyItemId`. Apartment unit doors are upgraded to be lockable with unit tags on the connection.

**Tech Stack:** C# / .NET, Godot 4.6, xUnit

**Spec:** `docs/superpowers/specs/2026-03-26-inventory-system-design.md`

**Test command:** `dotnet test stakeout.tests/ -v minimal`

---

### File Map

| Action | File | Responsibility |
|--------|------|----------------|
| Create | `src/simulation/entities/Item.cs` | `ItemType` enum and `Item` class |
| Create | `stakeout.tests/Simulation/Entities/ItemTests.cs` | Unit tests for Item entity |
| Create | `stakeout.tests/Simulation/Entities/PlayerTests.cs` | Unit tests for Player entity (inventory + key) |
| Modify | `src/simulation/entities/Person.cs:29` | Add `InventoryItemIds` field |
| Modify | `src/simulation/entities/Player.cs:11` | Add `InventoryItemIds` field |
| Modify | `src/simulation/SimulationState.cs:22-24` | Add `Items` dictionary |
| Modify | `src/simulation/sublocations/ApartmentBuildingGenerator.cs:85` | Add LockableProperty + unit tag to unit doors |
| Modify | `stakeout.tests/Simulation/Sublocations/ApartmentBuildingGeneratorTests.cs` | Test unit doors are lockable with tags |
| Modify | `src/simulation/PersonGenerator.cs:101-102` | Spawn home key after person creation |
| Modify | `stakeout.tests/Simulation/PersonGeneratorTests.cs` | Test key spawning for NPCs |
| Modify | `src/simulation/SimulationManager.cs:65-71` | Spawn home key for player |
| Modify | `scenes/game_shell/GameShell.cs:466-477` | Add inventory section to person inspector |

---

### Task 1: Item Entity

**Files:**
- Create: `src/simulation/entities/Item.cs`
- Create: `stakeout.tests/Simulation/Entities/ItemTests.cs`

- [ ] **Step 1: Write Item entity tests**

Create `stakeout.tests/Simulation/Entities/ItemTests.cs`:

```csharp
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
        Assert.Null(item.LocationSublocationId);
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
            LocationSublocationId = 15
        };
        Assert.Null(item.HeldByEntityId);
        Assert.Equal(5, item.LocationAddressId);
        Assert.Equal(15, item.LocationSublocationId);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test stakeout.tests/ -v minimal --filter "FullyQualifiedName~ItemTests"`
Expected: Build failure — `Item` and `ItemType` don't exist yet.

- [ ] **Step 3: Create Item entity**

Create `src/simulation/entities/Item.cs`:

```csharp
using System.Collections.Generic;

namespace Stakeout.Simulation.Entities;

public enum ItemType
{
    Key
}

public class Item
{
    public int Id { get; set; }
    public ItemType ItemType { get; set; }
    public int? HeldByEntityId { get; set; }
    public int? LocationAddressId { get; set; }
    public int? LocationSublocationId { get; set; }
    public Dictionary<string, object> Data { get; set; } = new();
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test stakeout.tests/ -v minimal --filter "FullyQualifiedName~ItemTests"`
Expected: All 3 tests PASS.

- [ ] **Step 5: Commit**

```
git add src/simulation/entities/Item.cs stakeout.tests/Simulation/Entities/ItemTests.cs
git commit -m "feat: add Item entity with ItemType enum"
```

---

### Task 2: Inventory Fields on Person, Player, and SimulationState

**Files:**
- Modify: `src/simulation/entities/Person.cs:29`
- Modify: `src/simulation/entities/Player.cs:11`
- Modify: `src/simulation/SimulationState.cs:22-24`
- Modify: `stakeout.tests/Simulation/SimulationStateTests.cs`

- [ ] **Step 1: Write tests for new fields**

Add to `stakeout.tests/Simulation/SimulationStateTests.cs`:

```csharp
[Fact]
public void SimulationState_ItemsDictionary_Exists()
{
    var state = new SimulationState();
    Assert.NotNull(state.Items);
    Assert.Empty(state.Items);
}
```

Add to `stakeout.tests/Simulation/Entities/PersonTests.cs`:

```csharp
[Fact]
public void Person_InventoryItemIds_DefaultsToEmptyList()
{
    var person = new Person();
    Assert.NotNull(person.InventoryItemIds);
    Assert.Empty(person.InventoryItemIds);
}
```

Create `stakeout.tests/Simulation/Entities/PlayerTests.cs`:

```csharp
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
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test stakeout.tests/ -v minimal --filter "FullyQualifiedName~ItemsDictionary|FullyQualifiedName~InventoryItemIds"`
Expected: Build failure — fields don't exist yet.

- [ ] **Step 3: Add fields**

In `src/simulation/entities/Person.cs`, after line 29 (`public bool NeedsScheduleRebuild { get; set; }`), add:

```csharp
    public List<int> InventoryItemIds { get; set; } = new();
```

In `src/simulation/entities/Player.cs`, after line 11 (`public TravelInfo TravelInfo { get; set; }`), add:

```csharp
    public List<int> InventoryItemIds { get; set; } = new();
```

Note: `Player.cs` needs `using System.Collections.Generic;` added to its imports.

In `src/simulation/SimulationState.cs`, after line 22 (`public Dictionary<int, Trace> Traces { get; } = new();`), add:

```csharp
    public Dictionary<int, Item> Items { get; } = new();
```

Note: `SimulationState.cs` already imports `Stakeout.Simulation.Entities`, so no new using needed.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test stakeout.tests/ -v minimal`
Expected: All tests PASS (including previous tests, to catch regressions).

- [ ] **Step 5: Commit**

```
git add src/simulation/entities/Person.cs src/simulation/entities/Player.cs src/simulation/SimulationState.cs stakeout.tests/Simulation/SimulationStateTests.cs stakeout.tests/Simulation/Entities/PersonTests.cs stakeout.tests/Simulation/Entities/PlayerTests.cs
git commit -m "feat: add InventoryItemIds to Person/Player and Items dict to SimulationState"
```

---

### Task 3: Upgrade Apartment Unit Doors

**Files:**
- Modify: `src/simulation/sublocations/ApartmentBuildingGenerator.cs:85`
- Modify: `stakeout.tests/Simulation/Sublocations/ApartmentBuildingGeneratorTests.cs`

- [ ] **Step 1: Write tests for lockable unit doors with tags**

Add to `stakeout.tests/Simulation/Sublocations/ApartmentBuildingGeneratorTests.cs`:

```csharp
[Fact]
public void Generate_UnitDoorsAreLockableWithUnitTag()
{
    var state = new SimulationState();
    var address = new Address { Id = 1, Type = AddressType.ApartmentBuilding };
    state.Addresses[1] = address;
    var gen = new ApartmentBuildingGenerator();
    gen.Generate(address, state, new Random(42));

    // Find all connections that have a unit tag
    var unitDoors = address.Connections
        .Where(c => c.Tags.Any(t => t.StartsWith("unit_f")))
        .ToList();

    Assert.NotEmpty(unitDoors);
    foreach (var door in unitDoors)
    {
        Assert.Equal(ConnectionType.Door, door.Type);
        Assert.NotNull(door.Lockable);
        Assert.Equal(LockMechanism.Key, door.Lockable.Mechanism);
        Assert.NotNull(door.Breakable);
        Assert.NotNull(door.Name);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test stakeout.tests/ -v minimal --filter "FullyQualifiedName~UnitDoorsAreLockable"`
Expected: FAIL — unit doors currently have no tags and no LockableProperty.

- [ ] **Step 3: Update ApartmentBuildingGenerator**

In `src/simulation/sublocations/ApartmentBuildingGenerator.cs`, replace line 85:

```csharp
                Connect(floorHallway, living, new SublocationConnection { Type = ConnectionType.Door });
```

with:

```csharp
                Connect(floorHallway, living, new SublocationConnection
                {
                    Type = ConnectionType.Door,
                    Name = $"Apt {i} Door",
                    Tags = new[] { unitTag },
                    Lockable = new LockableProperty { Mechanism = LockMechanism.Key },
                    Breakable = new BreakableProperty()
                });
```

- [ ] **Step 4: Run all tests to verify they pass**

Run: `dotnet test stakeout.tests/ -v minimal`
Expected: All tests PASS.

- [ ] **Step 5: Commit**

```
git add src/simulation/sublocations/ApartmentBuildingGenerator.cs stakeout.tests/Simulation/Sublocations/ApartmentBuildingGeneratorTests.cs
git commit -m "feat: add LockableProperty and unit tags to apartment unit doors"
```

---

### Task 4: Key Spawning for NPCs

**Files:**
- Modify: `src/simulation/PersonGenerator.cs:101-102`
- Modify: `stakeout.tests/Simulation/PersonGeneratorTests.cs`

- [ ] **Step 1: Write tests for NPC key spawning**

Add to `stakeout.tests/Simulation/PersonGeneratorTests.cs`:

```csharp
[Fact]
public void GeneratePerson_HasHomeKeyInInventory()
{
    var state = CreateState();
    var person = CreateGenerator().GeneratePerson(state);
    Assert.Single(person.InventoryItemIds);
    var itemId = person.InventoryItemIds[0];
    Assert.True(state.Items.ContainsKey(itemId));
    var item = state.Items[itemId];
    Assert.Equal(ItemType.Key, item.ItemType);
    Assert.Equal(person.Id, item.HeldByEntityId);
    Assert.True(item.Data.ContainsKey("TargetConnectionId"));
}

[Fact]
public void GeneratePerson_HomeKey_LinksToEntranceConnection()
{
    var state = CreateState();
    var gen = CreateGenerator();
    for (int i = 0; i < 50; i++)
    {
        gen.GeneratePerson(state);
    }
    foreach (var person in state.People.Values)
    {
        var itemId = person.InventoryItemIds[0];
        var item = state.Items[itemId];
        var targetConnId = (int)item.Data["TargetConnectionId"];
        var homeAddress = state.Addresses[person.HomeAddressId];
        var conn = homeAddress.Connections.First(c => c.Id == targetConnId);
        Assert.NotNull(conn.Lockable);
        Assert.Equal(itemId, conn.Lockable.KeyItemId);
    }
}

[Fact]
public void GeneratePerson_SuburbanHome_KeyTargetsEntranceDoor()
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
    var item = state.Items[itemId];
    var targetConnId = (int)item.Data["TargetConnectionId"];
    var homeAddress = state.Addresses[suburbanResident.HomeAddressId];
    var conn = homeAddress.Connections.First(c => c.Id == targetConnId);
    Assert.True(conn.HasTag("entrance"));
}

[Fact]
public void GeneratePerson_ApartmentResident_KeyTargetsUnitDoor()
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
    var item = state.Items[itemId];
    var targetConnId = (int)item.Data["TargetConnectionId"];
    var homeAddress = state.Addresses[aptResident.HomeAddressId];
    var conn = homeAddress.Connections.First(c => c.Id == targetConnId);
    Assert.True(conn.HasTag(aptResident.HomeUnitTag));
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test stakeout.tests/ -v minimal --filter "FullyQualifiedName~HomeKey|FullyQualifiedName~KeyTargets"`
Expected: FAIL — no key spawning logic yet.

- [ ] **Step 3: Add key spawning to PersonGenerator**

In `src/simulation/PersonGenerator.cs`, add a new method after `AssignVacantUnit`:

```csharp
private static void CreateHomeKey(SimulationState state, Person person, Address homeAddress)
{
    // Find the entrance connection for this person's home
    SublocationConnection entranceConn = null;
    if (person.HomeUnitTag != null)
    {
        // Apartment: find the unit door tagged with their unit tag
        entranceConn = homeAddress.Connections
            .FirstOrDefault(c => c.Tags != null && c.Tags.Contains(person.HomeUnitTag));
    }
    else
    {
        // Suburban home: find the front door tagged "entrance"
        entranceConn = homeAddress.Connections
            .FirstOrDefault(c => c.Tags != null && c.Tags.Contains("entrance"));
    }

    if (entranceConn?.Lockable == null) return;

    var key = new Item
    {
        Id = state.GenerateEntityId(),
        ItemType = ItemType.Key,
        HeldByEntityId = person.Id,
        Data = new Dictionary<string, object>
        {
            ["TargetConnectionId"] = entranceConn.Id
        }
    };
    state.Items[key.Id] = key;
    person.InventoryItemIds.Add(key.Id);
    entranceConn.Lockable.KeyItemId = key.Id;
}
```

Add `using System.Linq;` to the imports at the top if not already present.

Then in `GeneratePerson`, after `state.People[person.Id] = person;` (line 102), add:

```csharp
        // 8. Create home key
        CreateHomeKey(state, person, homeAddress);
```

Update the existing comment on line 104 from `// 8.` to `// 9.`.

- [ ] **Step 4: Run all tests to verify they pass**

Run: `dotnet test stakeout.tests/ -v minimal`
Expected: All tests PASS.

- [ ] **Step 5: Commit**

```
git add src/simulation/PersonGenerator.cs stakeout.tests/Simulation/PersonGeneratorTests.cs
git commit -m "feat: spawn home key for NPCs during person generation"
```

---

### Task 5: Key Spawning for Player

**Files:**
- Modify: `src/simulation/SimulationManager.cs:65-72`
- Modify: `stakeout.tests/Simulation/Entities/PlayerTests.cs`

- [ ] **Step 1: Write test for player key spawning**

Add to `stakeout.tests/Simulation/Entities/PlayerTests.cs` (created in Task 2). Add the needed imports and test:

```csharp
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
```

Note: This replaces the full `PlayerTests.cs` file content from Task 2, adding the `using Stakeout.Simulation;` import and the new test method.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test stakeout.tests/ -v minimal --filter "FullyQualifiedName~CreatePlayerKey"`
Expected: Build failure — `SimulationManager.CreatePlayerKey` doesn't exist.

- [ ] **Step 3: Add player key spawning**

In `src/simulation/SimulationManager.cs`, add a new public static method (after `StartPlayerTravel`):

```csharp
public static void CreatePlayerKey(SimulationState state)
{
    var player = state.Player;
    var homeAddress = state.Addresses[player.HomeAddressId];

    var entranceConn = homeAddress.Connections
        .FirstOrDefault(c => c.Tags != null && c.Tags.Contains("entrance"));

    if (entranceConn?.Lockable == null) return;

    var key = new Item
    {
        Id = state.GenerateEntityId(),
        ItemType = ItemType.Key,
        HeldByEntityId = player.Id,
        Data = new Dictionary<string, object>
        {
            ["TargetConnectionId"] = entranceConn.Id
        }
    };
    state.Items[key.Id] = key;
    player.InventoryItemIds.Add(key.Id);
    entranceConn.Lockable.KeyItemId = key.Id;
}
```

Add `using System.Linq;` to the imports if not already present.

Then in `_Ready()`, after `PlayerCreated?.Invoke();` (line 72), add:

```csharp
        CreatePlayerKey(State);
```

- [ ] **Step 4: Run all tests to verify they pass**

Run: `dotnet test stakeout.tests/ -v minimal`
Expected: All tests PASS.

- [ ] **Step 5: Commit**

```
git add src/simulation/SimulationManager.cs stakeout.tests/Simulation/Entities/PlayerTests.cs
git commit -m "feat: spawn home key for player at game start"
```

---

### Task 6: Person Inspector Inventory Section

**Files:**
- Modify: `scenes/game_shell/GameShell.cs:466-477`

This is a debug UI change — no unit tests (UI is built at runtime in Godot).

- [ ] **Step 1: Add inventory section to PopulateInspectorContent**

In `scenes/game_shell/GameShell.cs`, in the `PopulateInspectorContent` method, find the "Recent Events" section (around line 471). Insert the following **before** the Recent Events block (before the line `// Recent Events`):

```csharp
        // Inventory
        var inventoryLines = new List<string>();
        if (person.InventoryItemIds.Count == 0)
        {
            inventoryLines.Add("(empty)");
        }
        else
        {
            foreach (var itemId in person.InventoryItemIds)
            {
                if (state.Items.TryGetValue(itemId, out var item))
                {
                    var desc = item.ItemType.ToString();
                    if (item.ItemType == ItemType.Key && item.Data.TryGetValue("TargetConnectionId", out var connIdObj))
                    {
                        var connId = (int)connIdObj;
                        var homeAddr = state.Addresses.GetValueOrDefault(person.HomeAddressId);
                        var conn = homeAddr?.Connections.FirstOrDefault(c => c.Id == connId);
                        if (conn != null && homeAddr != null)
                        {
                            var street = state.Streets.GetValueOrDefault(homeAddr.StreetId);
                            desc = $"Key: {conn.Name} at {homeAddr.Number} {street?.Name ?? "Unknown"}";
                        }
                    }
                    inventoryLines.Add(desc);
                }
            }
        }
        AddInspectorSection(vbox, font, "— Inventory —", inventoryLines.ToArray());
```

- [ ] **Step 2: Verify the project builds**

Run: `dotnet build stakeout.tests/ -v minimal`
Expected: Build succeeds.

- [ ] **Step 3: Run all tests to verify no regressions**

Run: `dotnet test stakeout.tests/ -v minimal`
Expected: All tests PASS.

- [ ] **Step 4: Commit**

```
git add scenes/game_shell/GameShell.cs
git commit -m "feat: show inventory items in person inspector debug panel"
```

---

### Task 7: Final Verification

- [ ] **Step 1: Run full test suite**

Run: `dotnet test stakeout.tests/ -v minimal`
Expected: All tests PASS with zero failures.

- [ ] **Step 2: Verify build**

Run: `dotnet build -v minimal`
Expected: Build succeeds with zero errors.
