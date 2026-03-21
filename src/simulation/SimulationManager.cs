using System;
using Godot;
using Stakeout.Simulation.Entities;

namespace Stakeout.Simulation;

public partial class SimulationManager : Node
{
    public SimulationState State { get; private set; }

    public event Action<Person> PersonAdded;

    private readonly PersonGenerator _personGenerator = new();
    private bool _initialPersonGenerated;

    public override void _Ready()
    {
        State = new SimulationState();
    }

    public override void _Process(double delta)
    {
        State.Clock.Tick(delta);

        if (!_initialPersonGenerated && State.Clock.ElapsedSeconds >= 1.0)
        {
            var person = _personGenerator.GeneratePerson(State);
            PersonAdded?.Invoke(person);
            _initialPersonGenerated = true;
        }
    }
}
