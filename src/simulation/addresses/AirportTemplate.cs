using System;
using Stakeout.Simulation.Entities;

namespace Stakeout.Simulation.Addresses;

public class AirportTemplate : IAddressTemplate
{
    public void Generate(Address address, SimulationState state, Random random)
    {
        LocationBuilders.CreateLocation(state, address, "Terminal",
            new[] { "publicly_accessible", "entrance" });
    }
}
