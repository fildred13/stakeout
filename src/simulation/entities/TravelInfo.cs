using System;
using Godot;

namespace Stakeout.Simulation.Entities;

public class TravelInfo
{
    public Vector2 FromPosition { get; set; }
    public Vector2 ToPosition { get; set; }
    public DateTime DepartureTime { get; set; }
    public DateTime ArrivalTime { get; set; }
    public int FromAddressId { get; set; }
    public int ToAddressId { get; set; }
}
