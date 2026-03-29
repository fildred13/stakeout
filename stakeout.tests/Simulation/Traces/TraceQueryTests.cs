using System;
using Stakeout.Simulation;
using Stakeout.Simulation.Traces;
using Xunit;

namespace Stakeout.Tests.Simulation.Traces;

public class TraceQueryTests
{
    private SimulationState StateWithTraces()
    {
        var state = new SimulationState();
        var now = new DateTime(1984, 6, 15);

        state.Traces[1] = new Trace
        {
            Id = 1, Type = TraceType.Mark, LocationId = 10,
            Description = "Blood pool", CreatedAt = now, IsActive = true
        };
        state.Traces[2] = new Trace
        {
            Id = 2, Type = TraceType.Mark, LocationId = 10,
            Description = "Cleaned up blood", CreatedAt = now, IsActive = false
        };
        state.Traces[3] = new Trace
        {
            Id = 3, Type = TraceType.Mark, LocationId = 99,
            Description = "Other", CreatedAt = now, IsActive = true
        };
        state.Traces[4] = new Trace
        {
            Id = 4, Type = TraceType.Fingerprint, LocationId = 10,
            Description = "Old fingerprint", CreatedAt = now.AddDays(-10),
            DecayDays = 7, IsActive = true
        };
        state.Traces[5] = new Trace
        {
            Id = 5, Type = TraceType.Fingerprint, LocationId = 10,
            Description = "Fresh fingerprint", CreatedAt = now.AddDays(-3),
            DecayDays = 7, IsActive = true
        };
        state.Traces[6] = new Trace
        {
            Id = 6, Type = TraceType.Item, FixtureId = 50,
            Description = "Receipt", CreatedAt = now, IsActive = true
        };
        state.Traces[7] = new Trace
        {
            Id = 7, Type = TraceType.Mark, SubLocationId = 20,
            Description = "Scuff marks", CreatedAt = now, IsActive = true
        };
        state.Traces[8] = new Trace
        {
            Id = 8, Type = TraceType.Condition, AttachedToPersonId = 30,
            Description = "Bullet wound", CreatedAt = now, IsActive = true
        };

        return state;
    }

    [Fact]
    public void GetTracesForLocation_FiltersInactiveAndExpired()
    {
        var state = StateWithTraces();
        var now = new DateTime(1984, 6, 15);
        var result = state.GetTracesForLocation(10, now);
        Assert.Equal(2, result.Count);
        Assert.Contains(result, t => t.Id == 1);
        Assert.Contains(result, t => t.Id == 5);
    }

    [Fact]
    public void GetTracesForSubLocation_ReturnsMatching()
    {
        var state = StateWithTraces();
        var now = new DateTime(1984, 6, 15);
        var result = state.GetTracesForSubLocation(20, now);
        Assert.Single(result);
        Assert.Equal(7, result[0].Id);
    }

    [Fact]
    public void GetTracesForFixture_ReturnsMatching()
    {
        var state = StateWithTraces();
        var now = new DateTime(1984, 6, 15);
        var result = state.GetTracesForFixture(50, now);
        Assert.Single(result);
        Assert.Equal(6, result[0].Id);
    }

    [Fact]
    public void GetTracesForPerson_ReturnsMatching()
    {
        var state = StateWithTraces();
        var now = new DateTime(1984, 6, 15);
        var result = state.GetTracesForPerson(30, now);
        Assert.Single(result);
        Assert.Equal(8, result[0].Id);
    }

    [Fact]
    public void GetTracesForLocation_NullDecayDays_NeverExpires()
    {
        var state = new SimulationState();
        var longAgo = new DateTime(1980, 1, 1);
        state.Traces[1] = new Trace
        {
            Id = 1, Type = TraceType.Condition, LocationId = 10,
            Description = "Permanent", CreatedAt = longAgo, DecayDays = null
        };
        var result = state.GetTracesForLocation(10, new DateTime(1984, 6, 15));
        Assert.Single(result);
    }
}
