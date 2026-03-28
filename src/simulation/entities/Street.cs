using System.Collections.Generic;
using Godot;

namespace Stakeout.Simulation.Entities;

public class Street
{
    public int Id { get; set; }
    public string Name { get; set; }
    public int CityId { get; set; }
    public List<Vector2I> RoadCells { get; set; } = new();
}
