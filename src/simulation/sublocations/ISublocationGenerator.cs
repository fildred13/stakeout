using System;
using Stakeout.Simulation.Entities;

namespace Stakeout.Simulation.Sublocations;

public interface ISublocationGenerator
{
    SublocationGraph Generate(Address address, SimulationState state, Random rng);
}
