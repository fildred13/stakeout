using System;
using System.Collections.Generic;

namespace Stakeout.Simulation.Entities;

public enum RelationshipType { Partner, Dating, Ex, Friend, Affair }

public class Relationship
{
    public int Id { get; set; }
    public int PersonAId { get; set; }
    public int PersonBId { get; set; }
    public RelationshipType Type { get; set; }
    public DateTime StartedAt { get; set; }
    public int Other(int personId) => personId == PersonAId ? PersonBId : PersonAId;
    public bool Involves(int personId) => PersonAId == personId || PersonBId == personId;
}

public enum TensionKind { Debt, Jealousy, Affair, Embezzlement, Grudge }

/// <summary>
/// A conflict between two people that could become a motive. <see cref="AggressorId"/> is the one with
/// the reason to act. Some people know about it; documents may record it.
/// </summary>
public class Tension
{
    public int Id { get; set; }
    public TensionKind Kind { get; set; }
    public int AggressorId { get; set; }
    public int TargetId { get; set; }
    public double Intensity { get; set; }
    public int AmountDollars { get; set; }
    public int? ThirdPartyId { get; set; }
    public HashSet<int> KnownBy { get; } = new();
    public bool Involves(int personId) => AggressorId == personId || TargetId == personId;
}
