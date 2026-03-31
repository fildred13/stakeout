using System;
using System.Collections.Generic;
using Stakeout.Simulation.Entities;

namespace Stakeout.Simulation.Businesses;

public interface IBusinessTemplate
{
    BusinessType Type { get; }
    List<BusinessHours> GenerateHours();
    List<Position> GeneratePositions(SimulationState state, Random random);
    string GenerateName(Random random);
}
