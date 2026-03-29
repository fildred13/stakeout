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
            Id = 1, Type = TraceType.Condition,
            CreatedAt = new DateTime(1980, 1, 2, 1, 15, 0),
            CreatedByPersonId = 5, AttachedToPersonId = 3,
            Description = "Cause of death: stabbing"
        };
        Assert.Equal(TraceType.Condition, trace.Type);
        Assert.Equal(3, trace.AttachedToPersonId);
        Assert.Null(trace.LocationId);
    }

    [Fact]
    public void Trace_MarkType_BoundToLocation()
    {
        var trace = new Trace
        {
            Id = 2, Type = TraceType.Mark,
            CreatedAt = new DateTime(1980, 1, 2, 1, 15, 0),
            CreatedByPersonId = 5, LocationId = 10,
            Description = "Signs of forced entry"
        };
        Assert.Equal(TraceType.Mark, trace.Type);
        Assert.Equal(10, trace.LocationId);
        Assert.Null(trace.AttachedToPersonId);
    }

    [Fact]
    public void Trace_FingerprintType_OnFixture()
    {
        var trace = new Trace
        {
            Id = 3, Type = TraceType.Fingerprint,
            CreatedAt = new DateTime(1980, 1, 2, 8, 0, 0),
            CreatedByPersonId = 7,
            FixtureId = 42,
            Description = "Fingerprint on trash can",
            DecayDays = 7
        };
        Assert.Equal(TraceType.Fingerprint, trace.Type);
        Assert.Equal(42, trace.FixtureId);
        Assert.Equal(7, trace.DecayDays);
    }

    [Fact]
    public void Trace_IsActive_DefaultsToTrue()
    {
        var trace = new Trace { Id = 1, Type = TraceType.Mark, Description = "Blood" };
        Assert.True(trace.IsActive);
    }

    [Fact]
    public void Trace_DecayDays_NullMeansPermanent()
    {
        var trace = new Trace
        {
            Id = 1, Type = TraceType.Condition,
            Description = "Bullet wound",
            DecayDays = null
        };
        Assert.Null(trace.DecayDays);
    }

    [Fact]
    public void Trace_SubLocationId_Set()
    {
        var trace = new Trace
        {
            Id = 1, Type = TraceType.Mark,
            SubLocationId = 15,
            Description = "Scuff marks"
        };
        Assert.Equal(15, trace.SubLocationId);
        Assert.Null(trace.LocationId);
    }
}
