using System;
using Stakeout.Simulation.Entities;

namespace Stakeout.Simulation.Addresses;

public interface IAddressTemplate
{
    void Generate(Address address, SimulationState state, Random random);
}
