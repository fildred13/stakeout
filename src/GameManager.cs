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

    public override void _Ready()
    {
        State = new SimulationState();
        EvidenceBoard = new EvidenceBoard();
        SimulationManager = new SimulationManager(State);
        AddChild(SimulationManager);
    }
}
