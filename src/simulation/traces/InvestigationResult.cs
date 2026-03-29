using System.Collections.Generic;
using Stakeout.Simulation.Fixtures;

namespace Stakeout.Simulation.Traces;

public class InvestigationResult
{
    public List<Fixture> Fixtures { get; set; } = new();
    public List<Trace> Traces { get; set; } = new();
}
