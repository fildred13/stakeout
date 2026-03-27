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
