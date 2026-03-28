# Fingerprint Traces Design

## Overview

The first implementation of the traces framework, focused on fingerprints. When people interact with doors and keys, they leave fingerprints that accumulate and naturally decay through a smudging mechanic. This creates a forensic evidence layer that the player will eventually use during investigations.

## Architecture: Hybrid (Trace + Surface Index)

Fingerprints are stored as `Trace` entities in `SimulationState.Traces` (the simulation's single source of truth for evidence), but surfaces that can hold fingerprints maintain a lightweight index of trace IDs for fast access and smudging.

This follows the codebase's existing composable-property pattern (`LockableProperty`, `BreakableProperty`, etc.) and keeps the smudging mechanic efficient — iterate the surface's list directly rather than scanning all traces.

## Data Model

### New TraceType Value

Add `Fingerprint` to the `TraceType` enum.

### FingerprintSurface (New Composable Property)

**On SublocationConnection** (new nullable property, like `LockableProperty`):
- `SideATraceIds: List<int>` — fingerprint trace IDs on side A (the `FromSublocationId` side)
- `SideBTraceIds: List<int>` — fingerprint trace IDs on side B (the `ToSublocationId` side)

**On Item** (new nullable property `public FingerprintSurface? Fingerprints`):
- `TraceIds: List<int>` — fingerprint trace IDs on the item

This extends `Item` with its first composable property, establishing the pattern for future item properties. `Item.Data` remains for schemaless metadata; typed properties are used when the data has structure and behavior (like smudging).

All connection types get a `FingerprintSurface` at generation time. `FingerprintSurface` is added in the sublocation generators (`SuburbanHomeGenerator`, `ApartmentGenerator`, etc.) where connections are created — not in `PersonGenerator`.

### Trace.Data for Fingerprints

- `SurfaceType`: `"Connection"` or `"Item"`
- `SurfaceId`: the connection ID or item ID
- `Side`: `"A"` or `"B"` (null for items)

### Trace.CreatedAt

Set to `state.Clock.CurrentTime` at the moment of deposit. Fingerprint age may matter for future forensic mechanics.

## Fingerprint Deposit & Smudging

### Deposit Trigger: Door Traversal

When a person traverses a connection:

1. **Create** a `Trace` with `TraceType.Fingerprint`, `CreatedByPersonId` = the person, `CreatedAt` = current game time, `Data` referencing the connection and the side they're coming from (they touch the handle on their side before walking through).
2. **Index** the trace ID on the connection's `FingerprintSurface` for the appropriate side.
3. **Smudge** existing fingerprints on that same side (see formula below).
4. **Erased fingerprints** are removed from the surface's trace ID list and deleted from `SimulationState.Traces`.

### Smudging Formula

After depositing a new fingerprint, evaluate each **other** fingerprint on the same surface (same side for connections, same list for items). The erase chance per fingerprint is:

```
chance = min(100, N × 25)
```

Where **N** = total fingerprints on that surface **minus the one being evaluated** (i.e., including the newly deposited one). Examples after depositing a new print:

- Surface now has 2 prints total (1 old + 1 new): old print has N=1 → 25% erase chance
- Surface now has 3 prints total (2 old + 1 new): each old print has N=2 → 50% each
- Surface now has 4 prints total: each old print has N=3 → 75% each
- Surface now has 5+ prints total: each old print has N≥4 → 100% (guaranteed erasure)

### Deposit Trigger: Key Usage

When a person uses a key (to lock or unlock a door):

1. **Create** a fingerprint `Trace` on the key's `FingerprintSurface`.
2. **Smudge** other fingerprints on the key using the same formula.

### FingerprintService

A static service class with two methods:

- `DepositFingerprint(SimulationState, int personId, SublocationConnection conn, int fromSublocationId)` — for connection traversal
- `DepositFingerprint(SimulationState, int personId, Item item)` — for item interaction (keys)

Both handle trace creation, surface indexing, and smudging in one place.

## Door Locking Behavior (New)

### Which Doors Get Locked

"Exterior lockable connections" = all `SublocationConnection`s **belonging to the person's residence** that have a non-null `LockableProperty`. For suburban homes, this is all lockable connections on the address (front door tagged `"entrance"`, back doors tagged `"covert_entry"`, `"staff_entry"`, etc.). For apartments, this is only the person's unit door (the connection tagged with their `HomeUnitTag`).

### Rules

- **Leaving home:** When a person's action transition involves travel away from their `HomeAddressId` (i.e., `person.CurrentAddressId == person.HomeAddressId` and the new scheduled action is at a different address), lock all exterior lockable connections. 10% chance to forget per door (rolled independently).
- **Going to sleep:** When a person transitions to `ActionType.Sleep`, lock all exterior lockable connections. 10% chance to forget per door.
- **Arriving home:** In `UpdateTravel`, when `travel.ToAddressId == person.HomeAddressId` and the person arrives (arrival time reached), unlock all exterior lockable connections.

Each lock/unlock operation uses the person's home key, triggering a fingerprint deposit on that key.

### One Key, All Doors

A person's home key works on all exterior doors of their residence. Changes to `PersonGenerator.CreateHomeKey`:

- The key's `Data["TargetConnectionId"]` is kept pointing to the primary entrance connection (the `"entrance"`-tagged or unit-tagged door) for backward compatibility and display purposes.
- All lockable connections **belonging to the person's residence** get their `LockableProperty.KeyItemId` set to the same key's ID. For suburban homes, this means all lockable connections on the address. For apartments, this means only the person's unit door (tagged with their `HomeUnitTag`), not other tenants' doors.
- `DoorLockingService` finds the person's home key by iterating `person.InventoryItemIds`, looking up each in `state.Items`, and finding the one with `ItemType.Key`. (For now there's only one key per person; future multi-key support would add address-matching logic.)

### DoorLockingService

A static service that:
- `LockEntrances(SimulationState, Person)` — finds all lockable connections on the person's home address, locks each (setting `IsLocked = true`) with 10% independent forget chance, deposits fingerprint on key for each successful lock.
- `UnlockEntrances(SimulationState, Person)` — finds all lockable connections on the person's home address, unlocks each (setting `IsLocked = false`), deposits fingerprint on key for each unlock.

## Integration Points

### PersonBehavior (Traversal Hook)

Currently, sublocation changes in `PersonBehavior` assign `person.CurrentSublocationId` directly without referencing which connection was crossed. However, schedule entries already store `ViaConnectionId` (populated during decomposition). The traversal hook uses this:

- When `PersonBehavior` updates `CurrentSublocationId` from a schedule entry that has a non-null `ViaConnectionId`, look up the connection and call `FingerprintService.DepositFingerprint`.
- The "from" side is determined by: if `conn.FromSublocationId == previousSublocationId`, the person is on side A; otherwise side B.

### PersonBehavior (Locking Hook)

Two hook points:

1. **In `Transition()`**: When the person is at their `HomeAddressId` and the new action requires travel to a different address → `DoorLockingService.LockEntrances`. When the new action is `ActionType.Sleep` → `DoorLockingService.LockEntrances`.
2. **In `UpdateTravel()`**: When `travel.ToAddressId == person.HomeAddressId` and arrival time is reached → `DoorLockingService.UnlockEntrances`.

### Sublocation Generators (Setup)

All sublocation generators (`SuburbanHomeGenerator`, `ApartmentGenerator`, etc.) add `FingerprintSurface` to every connection they create — initialized with empty lists.

### PersonGenerator (Key Setup)

`CreateHomeKey` updated to set `LockableProperty.KeyItemId` on **all** lockable connections for the home address, not just the primary entrance.

## Scope

### In Scope

- `FingerprintSurface` composable property on connections and items
- `TraceType.Fingerprint` enum value
- `FingerprintService` — deposit + smudge logic
- `DoorLockingService` — lock/unlock on leave/sleep/arrive, 10% forget, key fingerprints
- All connections get `FingerprintSurface` at generation time (in sublocation generators)
- Hook into `PersonBehavior` for traversal fingerprints (using `ViaConnectionId`)
- Update `PersonGenerator` to assign same key to all exterior doors of a residence

### Out of Scope (Future Work)

- Player-facing fingerprint discovery/analysis UI
- Lockpicking / forced entry actions
- Fingerprints from non-door interactions (picking up objects, crime scenes)
- Window entry by intruders
- Stair railing fingerprints
