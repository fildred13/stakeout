using Stakeout.Simulation.Entities;
using Xunit;

namespace Stakeout.Tests.Simulation.Entities;

public class AddressTypeTests
{
    [Fact]
    public void GetCategory_SuburbanHome_ReturnsResidential()
    {
        Assert.Equal(AddressCategory.Residential, AddressType.SuburbanHome.GetCategory());
    }

    [Theory]
    [InlineData(AddressType.Diner)]
    [InlineData(AddressType.DiveBar)]
    [InlineData(AddressType.Office)]
    public void GetCategory_CommercialTypes_ReturnsCommercial(AddressType type)
    {
        Assert.Equal(AddressCategory.Commercial, type.GetCategory());
    }
}
