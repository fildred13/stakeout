using System.Collections.Generic;
using Stakeout.Simulation.Entities;

namespace Stakeout.Simulation;

public class SimulationState
{
    public GameClock Clock { get; }
    public Dictionary<int, Person> People { get; } = new();

    private int _nextEntityId = 1;

    public SimulationState()
    {
        Clock = new GameClock();
    }

    public int GenerateEntityId() => _nextEntityId++;
}
