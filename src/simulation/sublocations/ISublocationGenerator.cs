using System;
using Stakeout.Simulation.Entities;

namespace Stakeout.Simulation.Sublocations;

public interface ISublocationGenerator
{
    SublocationGraph Generate(int addressId, SimulationState state, Random rng);
}
