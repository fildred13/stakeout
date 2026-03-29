// stakeout.tests/Simulation/Fixtures/FixtureTests.cs
using System;
using System.Collections.Generic;
using Stakeout.Simulation;
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

public class FixtureQueryTests
{
    [Fact]
    public void GetFixturesForLocation_ReturnsMatchingFixtures()
    {
        var state = new SimulationState();
        state.Fixtures[1] = new Fixture { Id = 1, LocationId = 10, Name = "Trash Can", Type = FixtureType.TrashCan };
        state.Fixtures[2] = new Fixture { Id = 2, LocationId = 10, Name = "Trash Can 2", Type = FixtureType.TrashCan };
        state.Fixtures[3] = new Fixture { Id = 3, LocationId = 99, Name = "Other", Type = FixtureType.TrashCan };

        var result = state.GetFixturesForLocation(10);
        Assert.Equal(2, result.Count);
        Assert.All(result, f => Assert.Equal(10, f.LocationId));
    }

    [Fact]
    public void GetFixturesForSubLocation_ReturnsMatchingFixtures()
    {
        var state = new SimulationState();
        state.Fixtures[1] = new Fixture { Id = 1, SubLocationId = 20, Name = "Trash Can", Type = FixtureType.TrashCan };
        state.Fixtures[2] = new Fixture { Id = 2, SubLocationId = 99, Name = "Other", Type = FixtureType.TrashCan };

        var result = state.GetFixturesForSubLocation(20);
        Assert.Single(result);
        Assert.Equal(20, result[0].SubLocationId);
    }

    [Fact]
    public void GetFixturesForLocation_EmptyWhenNone()
    {
        var state = new SimulationState();
        var result = state.GetFixturesForLocation(10);
        Assert.Empty(result);
    }
}
