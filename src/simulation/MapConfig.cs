using System;
using Godot;

namespace Stakeout.Simulation;

public class MapConfig
{
    public float MinX { get; } = 40f;
    public float MaxX { get; } = 1240f;
    public float MinY { get; } = 40f;
    public float MaxY { get; } = 680f;
    public float MaxTravelTimeHours { get; } = 1.0f;

    public float MapDiagonal =>
        new Vector2(MaxX - MinX, MaxY - MinY).Length();

    public float ComputeTravelTimeHours(Vector2 from, Vector2 to)
    {
        var distance = from.DistanceTo(to);
        return distance / MapDiagonal * MaxTravelTimeHours;
    }
}
