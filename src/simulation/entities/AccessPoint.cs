// src/simulation/entities/AccessPoint.cs
using System;

namespace Stakeout.Simulation.Entities;

public enum AccessPointType { Door, Window, Gate, Hatch, SecurityGate }
public enum LockMechanism { Key, Combination, Keypad, Electronic }

public class AccessPoint
{
    public int Id { get; set; }
    public string Name { get; set; }
    public AccessPointType Type { get; set; }
    public string[] Tags { get; set; } = Array.Empty<string>();
    public bool IsLocked { get; set; }
    public bool IsBroken { get; set; }
    public LockMechanism? LockMechanism { get; set; }
    public int? KeyItemId { get; set; }

    public bool HasTag(string tag) => Array.IndexOf(Tags, tag) >= 0;
}
