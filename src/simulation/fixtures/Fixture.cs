using System;

namespace Stakeout.Simulation.Fixtures;

/// <summary>Something fixed in a room. Fixtures are touch surfaces; their id is the surface id.</summary>
public class Fixture
{
    public int Id { get; set; }
    public int? LocationId { get; set; }
    public int? SubLocationId { get; set; }
    public string Name { get; set; }
    public FixtureType Type { get; set; }
    public string[] Tags { get; set; } = Array.Empty<string>();
    /// <summary>Telephones: the number in the phone book.</summary>
    public string PhoneNumber { get; set; }
    /// <summary>Cameras: the road cell they watch (exterior) or null for an indoor camera.</summary>
    public int? WatchedCell { get; set; }
    /// <summary>Cameras: indoor location they watch.</summary>
    public int? WatchedLocationId { get; set; }

    public bool HasTag(string tag) => Array.IndexOf(Tags, tag) >= 0;
}
