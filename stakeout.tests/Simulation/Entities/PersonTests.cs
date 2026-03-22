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
}
