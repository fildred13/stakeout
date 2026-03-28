# Inventory System Design

## Purpose

Add a general-purpose inventory system to the simulation, with keys as the first item type. People and the player are spawned with a key to their home entrance. No usage mechanics yet — this is the data foundation for future features (door locking, fingerprints on items, key placement in locations).

## Design Decisions

- **General item system, not key-specific**: `Item` entity with `ItemType` enum. Keys are the first type; future types (weapons, tools) add enum values.
- **Type-specific data via dictionary**: `Item.Data` holds type-specific properties (e.g., `TargetConnectionId` for keys). Matches existing patterns (`Objective.Data`, `ActionData`).
- **Lists on entities, not a container class**: `Person.InventoryItemIds` and `Player.InventoryItemIds` are `List<int>`. Items at locations are queried by filtering `SimulationState.Items` on `LocationAddressId`/`LocationSublocationId`.
- **Residential keys only**: Only home entrance doors get keys. Commercial door keys deferred.
- **No spare keys or location storage**: Each person carries one key. No keys left in locations.

## Item Entity

New file: `src/simulation/entities/Item.cs`

```csharp
public enum ItemType { Key }

public class Item
{
    public int Id { get; set; }
    public ItemType ItemType { get; set; }
    public int? HeldByEntityId { get; set; }         // Person.Id or Player.Id; null = in a location
    public int? LocationAddressId { get; set; }       // Address where item sits; null = held
    public int? LocationSublocationId { get; set; }   // Specific room; null = address-level
    public Dictionary<string, object> Data { get; set; } = new();
}
```

For keys, `Data` contains:
- `"TargetConnectionId"` (int): The `SublocationConnection.Id` this key unlocks.

## Inventory on Entities

**Person** — new field:
```csharp
public List<int> InventoryItemIds { get; set; } = new();
```

**Player** — new field:
```csharp
public List<int> InventoryItemIds { get; set; } = new();
```

## State Storage

**SimulationState** — new dictionary:
```csharp
public Dictionary<int, Item> Items { get; } = new();
```

Items use `SimulationState.GenerateEntityId()` for IDs.

## Key Spawning

### Apartment Generator Change (prerequisite)

The `ApartmentBuildingGenerator` currently creates unit doors without `LockableProperty` or unit tags on the connection (line 85):
```csharp
Connect(floorHallway, living, new SublocationConnection { Type = ConnectionType.Door });
```

This must be updated to:
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

The unit tag on the connection enables finding the correct door for a given person's `HomeUnitTag`.

### NPC Keys (in PersonGenerator.GeneratePerson)

After generating a person's home address:

1. Find the home entrance connection via `Address.Connections`:
   - **Suburban homes**: The connection with an `"entrance"` tag (the front door). Suburban homes have three lockable connections but only the front door is tagged `"entrance"`.
   - **Apartments**: The connection whose `Tags` contain the person's `HomeUnitTag` (e.g., `"unit_f3_2"`). This is the unit door, not the building front entrance (which is tagged `"entrance"`).
2. Create an `Item` with `ItemType.Key` and `Data["TargetConnectionId"]` = connection ID.
3. Set `LockableProperty.KeyItemId` on the connection to the new item's ID.
4. Set `Item.HeldByEntityId` to the person's ID.
5. Add item ID to `Person.InventoryItemIds`.
6. Register in `SimulationState.Items`.

### Player Key (in SimulationManager._Ready)

After creating the Player object and its home address, follow the same process as suburban home NPC keys — find the `"entrance"`-tagged connection on the player's home `Address.Connections`. The player is currently always assigned a `SuburbanHome`, so no apartment logic is needed. Set `Item.HeldByEntityId` to `Player.Id`. If the player is ever assigned an apartment in the future, `Player` would need a `HomeUnitTag` field added.

## Two-Way Link: Key <-> Door

- `Item.Data["TargetConnectionId"]` points key -> door connection.
- `LockableProperty.KeyItemId` points door connection -> key item.

This enables both "which door does this key open?" and "which key opens this door?" queries.

## Debug UI: Person Inspector Inventory Section

In `GameShell.PopulateInspectorContent()`, add an "Inventory" section after the existing sections (before "Recent Events"). For each item ID in `Person.InventoryItemIds`:

- Look up the `Item` from `SimulationState.Items`
- For keys: get `(int)item.Data["TargetConnectionId"]`, then find the matching connection in `Address.Connections` for the person's home address (small list, linear scan is fine). Use the connection's `Name` and the address's street/number for display.
- Example line: `"Key: Front Door at 42 Beacon St"`

If the person has no items, show `"(empty)"`.

Note: `Item.Data` stores values as `object`, so `int` values require casting on retrieval (e.g., `(int)item.Data["TargetConnectionId"]`). This is consistent with existing `Objective.Data` and `ActionData` patterns.

## Scope Boundaries (Explicitly Deferred)

- No key usage mechanics (doors don't check for keys)
- No fingerprints on keys (traces system, separate feature)
- No spare keys or location inventories
- No commercial/work keys
- No player inventory UI (debug inspector only shows NPC items)
- No item pickup/drop actions
