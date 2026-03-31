using System;
using System.Linq;
using Stakeout.Simulation;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Fixtures;
using Stakeout.Simulation.Traces;
using Xunit;

namespace Stakeout.Tests.Simulation.Traces;

public class InvestigationQueryTests
{
    private static readonly DateTime Now = new(1984, 6, 15);

    private SimulationState SetupScene()
    {
        var state = new SimulationState();
        state.Fixtures[1] = new Fixture { Id = 1, LocationId = 10, Name = "Trash Can", Type = FixtureType.TrashCan };
        state.Traces[1] = new Trace
        {
            Id = 1, Type = TraceType.Mark, LocationId = 10,
            Description = "Blood pool", CreatedAt = Now, IsActive = true
        };
        state.Traces[2] = new Trace
        {
            Id = 2, Type = TraceType.Item, FixtureId = 1,
            Description = "Receipt", CreatedAt = Now, IsActive = true
        };
        state.Traces[3] = new Trace
        {
            Id = 3, Type = TraceType.Mark, LocationId = 10,
            Description = "Cleaned", CreatedAt = Now, IsActive = false
        };
        state.Traces[4] = new Trace
        {
            Id = 4, Type = TraceType.Mark, SubLocationId = 20,
            Description = "Scuff marks", CreatedAt = Now, IsActive = true
        };
        state.Fixtures[2] = new Fixture { Id = 2, SubLocationId = 20, Name = "Trash Can", Type = FixtureType.TrashCan };
        state.Traces[5] = new Trace
        {
            Id = 5, Type = TraceType.Condition, AttachedToPersonId = 30,
            Description = "Bullet wound", CreatedAt = Now, IsActive = true
        };
        return state;
    }

    [Fact]
    public void GetDiscoveriesForLocation_ReturnsFixturesAndTraces()
    {
        var state = SetupScene();
        var result = InvestigationQuery.GetDiscoveriesForLocation(state, 10, Now);
        Assert.Single(result.Fixtures);
        Assert.Equal(1, result.Fixtures[0].Id);
        Assert.Single(result.Traces);
        Assert.Equal(1, result.Traces[0].Id);
    }

    [Fact]
    public void GetDiscoveriesForSubLocation_ReturnsFixturesAndTraces()
    {
        var state = SetupScene();
        var result = InvestigationQuery.GetDiscoveriesForSubLocation(state, 20, Now);
        Assert.Single(result.Fixtures);
        Assert.Equal(2, result.Fixtures[0].Id);
        Assert.Single(result.Traces);
        Assert.Equal(4, result.Traces[0].Id);
    }

    [Fact]
    public void GetFixtureTraces_ReturnsTracesInsideFixture()
    {
        var state = SetupScene();
        var result = InvestigationQuery.GetFixtureTraces(state, 1, Now);
        Assert.Single(result);
        Assert.Equal("Receipt", result[0].Description);
    }

    [Fact]
    public void GetPersonTraces_ReturnsConditions()
    {
        var state = SetupScene();
        var result = InvestigationQuery.GetPersonTraces(state, 30, Now);
        Assert.Single(result);
        Assert.Equal("Bullet wound", result[0].Description);
    }

    [Fact]
    public void GetDiscoveriesForLocation_ExcludesExpiredTraces()
    {
        var state = new SimulationState();
        state.Traces[1] = new Trace
        {
            Id = 1, Type = TraceType.Fingerprint, LocationId = 10,
            Description = "Old print", CreatedAt = Now.AddDays(-10),
            DecayDays = 7, IsActive = true
        };
        var result = InvestigationQuery.GetDiscoveriesForLocation(state, 10, Now);
        Assert.Empty(result.Traces);
    }
}
