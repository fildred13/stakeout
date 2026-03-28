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
