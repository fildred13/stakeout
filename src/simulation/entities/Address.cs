using Godot;

namespace Stakeout.Simulation.Entities;

public class Address
{
    public int Id { get; set; }
    public int Number { get; set; }
    public int StreetId { get; set; }
    public AddressType Type { get; set; }
    public AddressCategory Category => Type.GetCategory();
    public Vector2 Position { get; set; }
}
