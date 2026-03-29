// stakeout.tests/Simulation/Fixtures/FixtureTests.cs
using System;
using Stakeout.Simulation.Fixtures;
using Xunit;

namespace Stakeout.Tests.Simulation.Fixtures;

public class FixtureTests
{
    [Fact]
    public void Fixture_DefaultTags_Empty()
    {
        var fixture = new Fixture { Id = 1, Name = "Trash Can", Type = FixtureType.TrashCan };
        Assert.Empty(fixture.Tags);
    }

    [Fact]
    public void Fixture_HasTag_ReturnsTrueWhenPresent()
    {
        var fixture = new Fixture
        {
            Id = 1, Name = "Trash Can", Type = FixtureType.TrashCan,
            Tags = new[] { "kitchen", "searchable" }
        };
        Assert.True(fixture.HasTag("kitchen"));
        Assert.False(fixture.HasTag("bedroom"));
    }

    [Fact]
    public void Fixture_LocationId_Set()
    {
        var fixture = new Fixture { Id = 1, LocationId = 10, Name = "Trash Can", Type = FixtureType.TrashCan };
        Assert.Equal(10, fixture.LocationId);
        Assert.Null(fixture.SubLocationId);
    }

    [Fact]
    public void Fixture_SubLocationId_Set()
    {
        var fixture = new Fixture { Id = 1, SubLocationId = 20, Name = "Trash Can", Type = FixtureType.TrashCan };
        Assert.Null(fixture.LocationId);
        Assert.Equal(20, fixture.SubLocationId);
    }
}
