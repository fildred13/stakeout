using System;
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
}
