using System;
using System.Collections.Generic;

namespace Stakeout.Simulation.Entities;

// --- Legacy classes (to be removed in Task 9) ---

public enum ConnectionType
{
    OpenPassage,
    Door,
    Window,
    Stairs,
    Gate,
    HiddenPath
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
    public int Id { get; set; }
    public int FromSublocationId { get; set; }
    public int ToSublocationId { get; set; }
    public ConnectionType Type { get; set; } = ConnectionType.OpenPassage;
    public string Name { get; set; }
    public string[] Tags { get; set; } = Array.Empty<string>();

    public LockableProperty Lockable { get; set; }
    public ConcealableProperty Concealable { get; set; }
    public TransparentProperty Transparent { get; set; }
    public BreakableProperty Breakable { get; set; }
    public FingerprintSurface Fingerprints { get; set; }

    public bool HasTag(string tag) => Array.IndexOf(Tags, tag) >= 0;
}

// SubLocation lives in this file because Windows is case-insensitive (SubLocation.cs == Sublocation.cs).
// When the legacy Sublocation/SublocationConnection classes above are deleted in Task 9,
// rename this file to SubLocation.cs.

public class SubLocation
{
    public int Id { get; set; }
    public int LocationId { get; set; }
    public string Name { get; set; }
    public string[] Tags { get; set; } = Array.Empty<string>();
    public List<AccessPoint> AccessPoints { get; } = new();

    public bool HasTag(string tag) => Array.IndexOf(Tags, tag) >= 0;
}
