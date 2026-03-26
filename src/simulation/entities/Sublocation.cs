using System;

namespace Stakeout.Simulation.Entities;

public enum ConnectionType
{
    Door,
    Window,
    Elevator,
    Stairs,
    OpenPassage,
    Gate,
    HiddenPath,
    Trail
}

public class Sublocation
{
    public int Id { get; set; }
    public int AddressId { get; set; }
    public int? ParentId { get; set; }
    public string Name { get; set; }
    public string[] Tags { get; set; } = Array.Empty<string>();
    public int? Floor { get; set; }
    public bool IsGenerated { get; set; } = true;

    public bool HasTag(string tag) => Array.IndexOf(Tags, tag) >= 0;
}

public class SublocationConnection
{
    public int FromSublocationId { get; set; }
    public int ToSublocationId { get; set; }
    public ConnectionType Type { get; set; } = ConnectionType.Door;
    public bool IsBidirectional { get; set; } = true;
}
