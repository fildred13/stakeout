using System;
using Stakeout.Simulation.Data;
using Stakeout.Simulation.Entities;
using Xunit;

namespace Stakeout.Tests.Simulation.Data;

public class BusinessNameDataTests
{
    [Theory]
    [InlineData(BusinessType.Diner)]
    [InlineData(BusinessType.DiveBar)]
    [InlineData(BusinessType.Office)]
    public void GenerateName_ReturnsNonEmpty(BusinessType type)
    {
        var name = BusinessNameData.GenerateName(type, new Random(42));
        Assert.False(string.IsNullOrWhiteSpace(name));
    }

    [Fact]
    public void GenerateName_DifferentSeeds_ProduceDifferentNames()
    {
        var name1 = BusinessNameData.GenerateName(BusinessType.Diner, new Random(1));
        var name2 = BusinessNameData.GenerateName(BusinessType.Diner, new Random(99));
        Assert.NotEqual(name1, name2);
    }
}
