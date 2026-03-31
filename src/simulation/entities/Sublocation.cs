using System;
using System.Collections.Generic;

namespace Stakeout.Simulation.Entities;

public class SubLocation
{
    public int Id { get; set; }
    public int LocationId { get; set; }
    public string Name { get; set; }
    public string[] Tags { get; set; } = Array.Empty<string>();
    public List<AccessPoint> AccessPoints { get; } = new();

    public bool HasTag(string tag) => Array.IndexOf(Tags, tag) >= 0;
}
