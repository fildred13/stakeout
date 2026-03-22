using System;

namespace Stakeout.Evidence;

public class EvidenceConnection : IEquatable<EvidenceConnection>
{
    public int FromItemId { get; }
    public int ToItemId { get; }

    public EvidenceConnection(int itemIdA, int itemIdB)
    {
        FromItemId = Math.Min(itemIdA, itemIdB);
        ToItemId = Math.Max(itemIdA, itemIdB);
    }

    public bool Equals(EvidenceConnection other)
    {
        if (other is null) return false;
        return FromItemId == other.FromItemId && ToItemId == other.ToItemId;
    }

    public override bool Equals(object obj) => Equals(obj as EvidenceConnection);

    public override int GetHashCode() => HashCode.Combine(FromItemId, ToItemId);
}
