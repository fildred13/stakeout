using System;
using System.Collections.Generic;
using Stakeout.Simulation.Traces;
using Xunit;

namespace Stakeout.Tests.Simulation.Traces;

public class TraceTests
{
    [Fact]
    public void Trace_ConditionType_AttachedToPerson()
    {
        var trace = new Trace
        {
            Id = 1, TraceType = TraceType.Condition,
            CreatedAt = new DateTime(1980, 1, 2, 1, 15, 0),
            CreatedByPersonId = 5, AttachedToPersonId = 3,
            Description = "Cause of death: stabbing"
        };
        Assert.Equal(TraceType.Condition, trace.TraceType);
        Assert.Equal(3, trace.AttachedToPersonId);
        Assert.Null(trace.LocationId);
    }

    [Fact]
    public void Trace_MarkType_BoundToLocation()
    {
        var trace = new Trace
        {
            Id = 2, TraceType = TraceType.Mark,
            CreatedAt = new DateTime(1980, 1, 2, 1, 15, 0),
            CreatedByPersonId = 5, LocationId = 10,
            Description = "Signs of forced entry"
        };
        Assert.Equal(TraceType.Mark, trace.TraceType);
        Assert.Equal(10, trace.LocationId);
        Assert.Null(trace.AttachedToPersonId);
    }

    [Fact]
    public void Trace_FingerprintType_HasSurfaceData()
    {
        var trace = new Trace
        {
            Id = 3, TraceType = TraceType.Fingerprint,
            CreatedAt = new DateTime(1980, 1, 2, 8, 0, 0),
            CreatedByPersonId = 7,
            Description = "Fingerprint",
            Data = new Dictionary<string, object>
            {
                ["SurfaceType"] = "Connection",
                ["SurfaceId"] = 42,
                ["Side"] = "A"
            }
        };
        Assert.Equal(TraceType.Fingerprint, trace.TraceType);
        Assert.Equal("Connection", trace.Data["SurfaceType"]);
        Assert.Equal(42, trace.Data["SurfaceId"]);
        Assert.Equal("A", trace.Data["Side"]);
    }
}
