namespace Stakeout.Simulation.Traces;

public static class TraceEmitter
{
    public static int EmitFingerprint(SimulationState state, int personId,
        int locationId, string description)
    {
        return AddTrace(state, new Trace
        {
            Type = TraceType.Fingerprint,
            CreatedByPersonId = personId,
            LocationId = locationId,
            Description = description,
            DecayDays = 7
        });
    }

    public static int EmitFingerprintOnFixture(SimulationState state, int personId,
        int fixtureId, string description)
    {
        return AddTrace(state, new Trace
        {
            Type = TraceType.Fingerprint,
            CreatedByPersonId = personId,
            FixtureId = fixtureId,
            Description = description,
            DecayDays = 7
        });
    }

    public static int EmitMark(SimulationState state, int locationId,
        int? subLocationId, string description, int? decayDays = null)
    {
        return AddTrace(state, new Trace
        {
            Type = TraceType.Mark,
            LocationId = locationId,
            SubLocationId = subLocationId,
            Description = description,
            DecayDays = decayDays
        });
    }

    public static int EmitItem(SimulationState state, int? personId,
        int locationId, int? fixtureId, string description, int? decayDays = null)
    {
        return AddTrace(state, new Trace
        {
            Type = TraceType.Item,
            CreatedByPersonId = personId,
            LocationId = locationId,
            FixtureId = fixtureId,
            Description = description,
            DecayDays = decayDays
        });
    }

    public static int EmitCondition(SimulationState state, int personId,
        string description)
    {
        return AddTrace(state, new Trace
        {
            Type = TraceType.Condition,
            AttachedToPersonId = personId,
            Description = description,
            DecayDays = null
        });
    }

    public static int EmitRecord(SimulationState state, int fixtureId,
        int? personId, string description)
    {
        return AddTrace(state, new Trace
        {
            Type = TraceType.Record,
            FixtureId = fixtureId,
            CreatedByPersonId = personId,
            Description = description
        });
    }

    public static int EmitSighting(SimulationState state, int personId,
        int locationId, string description, int? decayDays = null)
    {
        return AddTrace(state, new Trace
        {
            Type = TraceType.Sighting,
            CreatedByPersonId = personId,
            LocationId = locationId,
            Description = description,
            DecayDays = decayDays
        });
    }

    private static int AddTrace(SimulationState state, Trace trace)
    {
        trace.Id = state.GenerateEntityId();
        trace.CreatedAt = state.Clock.CurrentTime;
        state.Traces[trace.Id] = trace;
        return trace.Id;
    }
}
