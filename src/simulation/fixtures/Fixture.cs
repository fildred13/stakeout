// src/simulation/fixtures/Fixture.cs
using System;

namespace Stakeout.Simulation.Fixtures;

public class Fixture
{
    public int Id { get; set; }
    public int? LocationId { get; set; }
    public int? SubLocationId { get; set; }
    public string Name { get; set; }
    public FixtureType Type { get; set; }
    public string[] Tags { get; set; } = Array.Empty<string>();

    public bool HasTag(string tag) => Array.IndexOf(Tags, tag) >= 0;
}
