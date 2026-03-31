using Godot;
using Stakeout.Simulation;

// TODO: Project 8 (Player UI) — will be rebuilt
public partial class GraphView : Control
{
    private SimulationState _state;
    private int _addressId;

    public void Initialize(SimulationState state, int addressId)
    {
        _state = state;
        _addressId = addressId;
    }
}
