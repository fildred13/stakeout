// src/simulation/scheduling/TaskResolver.cs
using System;
using System.Collections.Generic;
using System.Linq;
using Stakeout.Simulation.Actions;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Objectives;
using Stakeout.Simulation.Scheduling.Decomposition;

namespace Stakeout.Simulation.Scheduling;

public static class TaskResolver
{
    private static readonly Dictionary<ActionType, IDecompositionStrategy> _strategies = new()
    {
        { ActionType.Work, new WorkDayDecomposition() },
        { ActionType.Sleep, new InhabitDecomposition() },
        { ActionType.Idle, new InhabitDecomposition() },
        { ActionType.KillPerson, new IntrudeDecomposition() },
    };

    public static List<ScheduleEntry> Resolve(SimTask task, SimulationState state, Random rng)
    {
        if (!task.TargetAddressId.HasValue)
            return FallbackEntry(task);

        var graph = BuildGraphForAddress(task.TargetAddressId.Value, state);
        if (graph == null)
            return FallbackEntry(task);

        var strategy = GetStrategy(task);
        return strategy.Decompose(task, graph, task.WindowStart, task.WindowEnd, rng);
    }

    private static SublocationGraph BuildGraphForAddress(int addressId, SimulationState state)
    {
        var subs = state.Sublocations.Values
            .Where(s => s.AddressId == addressId)
            .ToDictionary(s => s.Id);

        if (subs.Count == 0) return null;

        var conns = state.SublocationConnections
            .Where(c => subs.ContainsKey(c.FromSublocationId) || subs.ContainsKey(c.ToSublocationId))
            .ToList();

        return new SublocationGraph(subs, conns);
    }

    private static IDecompositionStrategy GetStrategy(SimTask task)
    {
        if (task.ActionData != null &&
            task.ActionData.TryGetValue("DecompositionStrategy", out var strategyName) &&
            strategyName is string name)
        {
            if (name == "StaffShift") return new StaffShiftDecomposition();
            if (name == "Patronize") return new PatronizeDecomposition();
        }

        return _strategies.TryGetValue(task.ActionType, out var strategy)
            ? strategy
            : new VisitDecomposition();
    }

    private static List<ScheduleEntry> FallbackEntry(SimTask task)
    {
        return new List<ScheduleEntry>
        {
            new ScheduleEntry
            {
                Action = task.ActionType,
                StartTime = task.WindowStart,
                EndTime = task.WindowEnd,
                TargetAddressId = task.TargetAddressId,
                TargetSublocationId = null
            }
        };
    }
}
