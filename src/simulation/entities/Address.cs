using System.Collections.Generic;
using Godot;

namespace Stakeout.Simulation.Entities;

public class Address
{
    public int Id { get; set; }
    public int CityId { get; set; }
    public int Number { get; set; }
    public int StreetId { get; set; }
    public AddressType Type { get; set; }
    public AddressCategory Category => Type.GetCategory();
    public int GridX { get; set; }
    public int GridY { get; set; }
    public const int CellSize = 48;
    public Vector2 Position => new Vector2(GridX * CellSize, GridY * CellSize);
    public List<int> LocationIds { get; } = new();
}
