using System;
using Stakeout.Simulation;
using Stakeout.Simulation.Fixtures;
using Stakeout.Simulation.Traces;
using Xunit;

namespace Stakeout.Tests.Simulation.Traces;

public class TraceEmitterTests
{
    private SimulationState Setup()
    {
        var state = new SimulationState();
        state.Fixtures[10] = new Fixture { Id = 10, LocationId = 1, Name = "Trash Can", Type = FixtureType.TrashCan };
        return state;
    }

    [Fact]
    public void EmitFingerprint_CreatesTraceWithDefaults()
    {
        var state = Setup();
        var id = TraceEmitter.EmitFingerprint(state, personId: 5, locationId: 1, "Fingerprint on door");
        var trace = state.Traces[id];
        Assert.Equal(TraceType.Fingerprint, trace.Type);
        Assert.Equal(5, trace.CreatedByPersonId);
        Assert.Equal(1, trace.LocationId);
        Assert.Equal(7, trace.DecayDays);
        Assert.True(trace.IsActive);
    }

    [Fact]
    public void EmitFingerprintOnFixture_SetsFixtureId()
    {
        var state = Setup();
        var id = TraceEmitter.EmitFingerprintOnFixture(state, personId: 5, fixtureId: 10, "Fingerprint on trash can");
        var trace = state.Traces[id];
        Assert.Equal(TraceType.Fingerprint, trace.Type);
        Assert.Equal(10, trace.FixtureId);
        Assert.Equal(7, trace.DecayDays);
    }

    [Fact]
    public void EmitMark_CreatesMarkTrace()
    {
        var state = Setup();
        var id = TraceEmitter.EmitMark(state, locationId: 1, subLocationId: null, "Blood pool");
        var trace = state.Traces[id];
        Assert.Equal(TraceType.Mark, trace.Type);
        Assert.Equal(1, trace.LocationId);
        Assert.Null(trace.SubLocationId);
        Assert.Null(trace.DecayDays);
    }

    [Fact]
    public void EmitMark_WithDecay_SetsDecayDays()
    {
        var state = Setup();
        var id = TraceEmitter.EmitMark(state, locationId: 1, subLocationId: null, "Muddy footprints", decayDays: 3);
        var trace = state.Traces[id];
        Assert.Equal(3, trace.DecayDays);
    }

    [Fact]
    public void EmitMark_WithSubLocation_SetsSubLocationId()
    {
        var state = Setup();
        var id = TraceEmitter.EmitMark(state, locationId: 1, subLocationId: 20, "Scuff marks");
        var trace = state.Traces[id];
        Assert.Equal(1, trace.LocationId);
        Assert.Equal(20, trace.SubLocationId);
    }

    [Fact]
    public void EmitItem_CreatesItemTrace()
    {
        var state = Setup();
        var id = TraceEmitter.EmitItem(state, personId: 5, locationId: 1, fixtureId: 10, "Crumpled receipt");
        var trace = state.Traces[id];
        Assert.Equal(TraceType.Item, trace.Type);
        Assert.Equal(5, trace.CreatedByPersonId);
        Assert.Equal(1, trace.LocationId);
        Assert.Equal(10, trace.FixtureId);
    }

    [Fact]
    public void EmitCondition_CreatesPermanentTrace()
    {
        var state = Setup();
        var id = TraceEmitter.EmitCondition(state, personId: 3, "Bullet wound to the chest");
        var trace = state.Traces[id];
        Assert.Equal(TraceType.Condition, trace.Type);
        Assert.Equal(3, trace.AttachedToPersonId);
        Assert.Null(trace.DecayDays);
        Assert.Null(trace.LocationId);
    }

    [Fact]
    public void EmitRecord_CreatesRecordOnFixture()
    {
        var state = Setup();
        var id = TraceEmitter.EmitRecord(state, fixtureId: 10, personId: 5, "Threatening letter");
        var trace = state.Traces[id];
        Assert.Equal(TraceType.Record, trace.Type);
        Assert.Equal(10, trace.FixtureId);
        Assert.Equal(5, trace.CreatedByPersonId);
    }

    [Fact]
    public void EmitSighting_CreatesSightingTrace()
    {
        var state = Setup();
        var id = TraceEmitter.EmitSighting(state, personId: 5, locationId: 1, "Seen entering at 2am");
        var trace = state.Traces[id];
        Assert.Equal(TraceType.Sighting, trace.Type);
        Assert.Equal(5, trace.CreatedByPersonId);
        Assert.Equal(1, trace.LocationId);
    }

    [Fact]
    public void EmitSighting_WithDecay()
    {
        var state = Setup();
        var id = TraceEmitter.EmitSighting(state, personId: 5, locationId: 1, "Seen lurking", decayDays: 14);
        var trace = state.Traces[id];
        Assert.Equal(14, trace.DecayDays);
    }

    [Fact]
    public void AllEmitters_SetCreatedAtFromClock()
    {
        var clock = new GameClock(new DateTime(1984, 6, 15, 14, 30, 0));
        var state = new SimulationState(clock);
        state.Fixtures[10] = new Fixture { Id = 10, LocationId = 1, Name = "Trash Can", Type = FixtureType.TrashCan };
        var id = TraceEmitter.EmitMark(state, locationId: 1, subLocationId: null, "Test");
        Assert.Equal(new DateTime(1984, 6, 15, 14, 30, 0), state.Traces[id].CreatedAt);
    }
}
