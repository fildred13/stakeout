using Godot;
using Stakeout.Evidence;
using Stakeout.Simulation;

namespace Stakeout;

/// <summary>Autoload that owns the running game between scene changes.</summary>
public partial class GameManager : Node
{
    public World World { get; set; }
    public EvidenceBoard EvidenceBoard { get; private set; } = new();
    public bool IsGameActive { get; set; }

    public override void _Ready()
    {
        DisplaySettings.Load();
    }

    public void Reset()
    {
        World = null;
        EvidenceBoard = new EvidenceBoard();
        IsGameActive = false;
    }
}
