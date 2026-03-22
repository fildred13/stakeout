using Stakeout.Evidence;
using Xunit;

namespace Stakeout.Tests.Evidence;

public class EvidenceConnectionTests
{
    [Fact]
    public void Constructor_NormalizesOrder_SmallerIdFirst()
    {
        var conn = new EvidenceConnection(5, 3);

        Assert.Equal(3, conn.FromItemId);
        Assert.Equal(5, conn.ToItemId);
    }

    [Fact]
    public void Constructor_AlreadyNormalized_PreservesOrder()
    {
        var conn = new EvidenceConnection(2, 7);

        Assert.Equal(2, conn.FromItemId);
        Assert.Equal(7, conn.ToItemId);
    }

    [Fact]
    public void Equals_SamePair_ReturnsTrue()
    {
        var a = new EvidenceConnection(1, 2);
        var b = new EvidenceConnection(1, 2);

        Assert.Equal(a, b);
    }

    [Fact]
    public void Equals_ReversedPair_ReturnsTrue()
    {
        var a = new EvidenceConnection(1, 2);
        var b = new EvidenceConnection(2, 1);

        Assert.Equal(a, b);
    }

    [Fact]
    public void Equals_DifferentPair_ReturnsFalse()
    {
        var a = new EvidenceConnection(1, 2);
        var b = new EvidenceConnection(1, 3);

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void GetHashCode_ReversedPair_SameHash()
    {
        var a = new EvidenceConnection(1, 2);
        var b = new EvidenceConnection(2, 1);

        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }
}
