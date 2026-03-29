using System;
using System.Collections.Generic;

namespace Stakeout.Simulation.Traces;

public static class InvestigationQuery
{
    public static InvestigationResult GetDiscoveriesForLocation(SimulationState state,
        int locationId, DateTime currentTime)
    {
        return new InvestigationResult
        {
            Fixtures = state.GetFixturesForLocation(locationId),
            Traces = state.GetTracesForLocation(locationId, currentTime)
        };
    }

    public static InvestigationResult GetDiscoveriesForSubLocation(SimulationState state,
        int subLocationId, DateTime currentTime)
    {
        return new InvestigationResult
        {
            Fixtures = state.GetFixturesForSubLocation(subLocationId),
            Traces = state.GetTracesForSubLocation(subLocationId, currentTime)
        };
    }

    public static List<Trace> GetFixtureTraces(SimulationState state,
        int fixtureId, DateTime currentTime)
    {
        return state.GetTracesForFixture(fixtureId, currentTime);
    }

    public static List<Trace> GetPersonTraces(SimulationState state,
        int personId, DateTime currentTime)
    {
        return state.GetTracesForPerson(personId, currentTime);
    }
}
