using System;
using System.Linq;
using Stakeout.Simulation.Entities;

namespace Stakeout.Simulation.Scheduling;

public static class DoorLockingService
{
    public static void UpdateDoorStates(SimulationState state, DateTime currentTime)
    {
        foreach (var business in state.Businesses.Values)
        {
            if (!business.IsResolved) continue;

            var hours = business.Hours.FirstOrDefault(h => h.Day == currentTime.DayOfWeek);
            var isOpen = IsWithinOperatingHours(hours, currentTime.TimeOfDay);

            SetEntranceLockState(state, business.AddressId, locked: !isOpen);
        }
    }

    private static bool IsWithinOperatingHours(BusinessHours hours, TimeSpan timeOfDay)
    {
        if (hours == null || !hours.OpenTime.HasValue || !hours.CloseTime.HasValue)
            return false;

        var open = hours.OpenTime.Value;
        var close = hours.CloseTime.Value;

        if (close > open)
        {
            // Same day: e.g., 7:00 - 19:00
            return timeOfDay >= open && timeOfDay < close;
        }
        else if (close < open || (close == open && open == TimeSpan.Zero))
        {
            // Crosses midnight or 24/7 (00:00 - 00:00)
            if (close == open) return true; // 24/7
            return timeOfDay >= open || timeOfDay < close;
        }

        return false;
    }

    private static void SetEntranceLockState(SimulationState state, int addressId, bool locked)
    {
        if (!state.Addresses.TryGetValue(addressId, out var address)) return;

        foreach (var locId in address.LocationIds)
        {
            if (!state.Locations.TryGetValue(locId, out var location)) continue;
            foreach (var ap in location.AccessPoints)
            {
                if (ap.HasTag("main_entrance"))
                    ap.IsLocked = locked;
            }
        }
    }
}
