using Godot;
using Stakeout.Evidence;
using Stakeout.Simulation;

namespace Stakeout;

public partial class GameManager : Node
{
    public SimulationState State { get; private set; }
    public SimulationManager SimulationManager { get; private set; }
    public EvidenceBoard EvidenceBoard { get; private set; }
    public string ActiveContentView { get; set; } = "res://scenes/city/CityView.tscn";
    public bool IsGameActive { get; set; } = false;
    public float PreviousTimeScale { get; set; } = 1.0f;

    public override void _Ready()
    {
        DisplaySettings.Load();
        State = new SimulationState();
        EvidenceBoard = new EvidenceBoard();
        SimulationManager = new SimulationManager(State);
        AddChild(SimulationManager);
    }

    public void Reinitialize()
    {
        // Remove old SimulationManager from the tree
        if (SimulationManager != null)
        {
            RemoveChild(SimulationManager);
            SimulationManager.QueueFree();
        }

        State = new SimulationState();
        EvidenceBoard = new EvidenceBoard();
        SimulationManager = new SimulationManager(State);
        AddChild(SimulationManager);
        PreviousTimeScale = 1.0f;
    }
}
