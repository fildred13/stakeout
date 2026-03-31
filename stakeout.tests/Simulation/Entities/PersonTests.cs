using Stakeout.Simulation.Entities;
using Xunit;

namespace Stakeout.Tests.Simulation.Entities;

public class PersonTests
{
    [Fact]
    public void FullName_ReturnsCombinedFirstAndLastName()
    {
        var person = new Person { FirstName = "James", LastName = "Smith" };

        Assert.Equal("James Smith", person.FullName);
    }

    [Fact]
    public void HomeLocationId_DefaultsToNull()
    {
        var person = new Person();
        Assert.Null(person.HomeLocationId);
    }

    [Fact]
    public void Person_InventoryItemIds_DefaultsToEmptyList()
    {
        var person = new Person();
        Assert.NotNull(person.InventoryItemIds);
        Assert.Empty(person.InventoryItemIds);
    }
}
