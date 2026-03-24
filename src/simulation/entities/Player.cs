using Godot;

namespace Stakeout.Simulation.Entities;

public class Player
{
    public int Id { get; set; }
    public int HomeAddressId { get; set; }
    public int CurrentAddressId { get; set; }
    public Vector2 CurrentPosition { get; set; }
    public TravelInfo TravelInfo { get; set; }
}
