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

        // Deposit 4 prints first — directly add trace IDs to simulate pre-existing prints
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

    [Fact]
    public void DepositFingerprint_TwoPrintsOnSurface_SmudgeChanceIs25Percent()
    {
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
}
