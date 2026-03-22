# Plan Part 3: Fix Event Subscription Race Condition

## Issues

1. **Multi-entity tooltips not working** — Hovering over a space with both a Person and an Address only shows the Person name, not the Address.
2. **Only 5 locations on the map** — Should see ~60-90 address icons (15 streets × 3-8 addresses each), plus the player dot. Currently only person dots appear.

## Root Cause

Both issues share the same root cause: **event subscription race condition** in `SimulationDebug._Ready()`.

The current order is:
1. `_simulationManager = new SimulationManager()`
2. `AddChild(_simulationManager)` — this triggers `SimulationManager._Ready()`, which fires `AddressAdded` (for all addresses) and `PlayerCreated` events
3. Subscribe to `AddressAdded`, `PersonAdded`, `PlayerCreated` — **too late**, events already fired

Because subscriptions happen after `AddChild`, the `AddressAdded` and `PlayerCreated` events fire into the void. No address icons or player dot are created. Person dots work because `PersonAdded` fires after a 1-second delay in `_Process`, by which time subscriptions are active.

This also explains the tooltip issue: since no address nodes exist in `_addressNodes`, the tooltip loop over addresses finds nothing to match.

## Fix

**SimulationDebug.cs**: Move event subscriptions before the `AddChild` call:

```csharp
_simulationManager = new SimulationManager();

// Subscribe BEFORE AddChild so we catch events fired during _Ready()
_simulationManager.AddressAdded += OnAddressAdded;
_simulationManager.PersonAdded += OnPersonAdded;
_simulationManager.PlayerCreated += OnPlayerCreated;

AddChild(_simulationManager);
```

This is the only change needed. Both issues will be resolved.
