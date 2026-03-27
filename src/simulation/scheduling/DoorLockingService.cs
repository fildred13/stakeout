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
