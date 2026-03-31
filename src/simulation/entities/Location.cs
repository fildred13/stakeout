// src/simulation/entities/Location.cs
using System;
using System.Collections.Generic;

namespace Stakeout.Simulation.Entities;

public class Location
{
    public int Id { get; set; }
    public int AddressId { get; set; }
    public string Name { get; set; }
    public string[] Tags { get; set; } = Array.Empty<string>();
    public int? Floor { get; set; }
    public string UnitLabel { get; set; }
    public List<int> SubLocationIds { get; } = new();
    public List<AccessPoint> AccessPoints { get; } = new();

    public bool HasTag(string tag) => Array.IndexOf(Tags, tag) >= 0;
}
