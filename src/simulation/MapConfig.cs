using Godot;

namespace Stakeout.Simulation;

public class MapConfig
{
    public int GridWidth { get; } = 100;
    public int GridHeight { get; } = 100;
    public int CellSize { get; } = 48;
    public float MapWidth => GridWidth * CellSize;
    public float MapHeight => GridHeight * CellSize;
    public float MaxTravelTimeHours { get; } = 1.0f;

    public float MapDiagonal => new Vector2(MapWidth, MapHeight).Length();

    public float ComputeTravelTimeHours(Vector2 from, Vector2 to)
    {
        var distance = from.DistanceTo(to);
        return distance / MapDiagonal * MaxTravelTimeHours;
    }
}
