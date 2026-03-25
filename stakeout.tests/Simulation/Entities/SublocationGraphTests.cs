// stakeout.tests/Simulation/Entities/SublocationGraphTests.cs
using System.Collections.Generic;
using System.Linq;
using Stakeout.Simulation.Entities;
using Xunit;

namespace Stakeout.Tests.Simulation.Entities;

public class SublocationGraphTests
{
    private static SublocationGraph CreateSimpleOffice()
    {
        var subs = new Dictionary<int, Sublocation>
        {
            { 1, new Sublocation { Id = 1, AddressId = 10, Name = "Road", Tags = new[] { "road" } } },
            { 2, new Sublocation { Id = 2, AddressId = 10, Name = "Lobby", Tags = new[] { "entrance", "public" } } },
            { 3, new Sublocation { Id = 3, AddressId = 10, Name = "Cubicle Area", Tags = new[] { "work_area" } } },
            { 4, new Sublocation { Id = 4, AddressId = 10, Name = "Break Room", Tags = new[] { "food" } } },
        };
        var conns = new List<SublocationConnection>
        {
            new() { FromSublocationId = 1, ToSublocationId = 2, Type = ConnectionType.Door },
            new() { FromSublocationId = 2, ToSublocationId = 3, Type = ConnectionType.Door },
            new() { FromSublocationId = 3, ToSublocationId = 4, Type = ConnectionType.OpenPassage },
        };
        return new SublocationGraph(subs, conns);
    }

    [Fact]
    public void FindByTag_ExistingTag_ReturnsSublocation()
    {
        var graph = CreateSimpleOffice();
        var result = graph.FindByTag("entrance");
        Assert.NotNull(result);
        Assert.Equal("Lobby", result.Name);
    }

    [Fact]
    public void FindByTag_NoMatch_ReturnsNull()
    {
        var graph = CreateSimpleOffice();
        var result = graph.FindByTag("restroom");
        Assert.Null(result);
    }

    [Fact]
    public void FindAllByTag_ReturnsAllMatching()
    {
        var graph = CreateSimpleOffice();
        var results = graph.FindAllByTag("road");
        Assert.Single(results);
        Assert.Equal("Road", results[0].Name);
    }

    [Fact]
    public void GetRoad_ReturnsRoadNode()
    {
        var graph = CreateSimpleOffice();
        var road = graph.GetRoad();
        Assert.NotNull(road);
        Assert.Equal("Road", road.Name);
    }

    [Fact]
    public void FindPath_AdjacentRooms_ReturnsDirectPath()
    {
        var graph = CreateSimpleOffice();
        var path = graph.FindPath(2, 3);
        Assert.Equal(2, path.Count);
        Assert.Equal(2, path[0].Id);
        Assert.Equal(3, path[1].Id);
    }

    [Fact]
    public void FindPath_TwoHops_ReturnsFullPath()
    {
        var graph = CreateSimpleOffice();
        var path = graph.FindPath(1, 3);
        Assert.Equal(3, path.Count);
        Assert.Equal(1, path[0].Id);
        Assert.Equal(2, path[1].Id);
        Assert.Equal(3, path[2].Id);
    }

    [Fact]
    public void FindPath_SameRoom_ReturnsSingleElement()
    {
        var graph = CreateSimpleOffice();
        var path = graph.FindPath(2, 2);
        Assert.Single(path);
        Assert.Equal(2, path[0].Id);
    }

    [Fact]
    public void FindPath_Unreachable_ReturnsEmptyList()
    {
        var subs = new Dictionary<int, Sublocation>
        {
            { 1, new Sublocation { Id = 1, AddressId = 10, Name = "Road", Tags = new[] { "road" } } },
            { 2, new Sublocation { Id = 2, AddressId = 10, Name = "Island", Tags = new[] { "isolated" } } },
        };
        var graph = new SublocationGraph(subs, new List<SublocationConnection>());
        var path = graph.FindPath(1, 2);
        Assert.Empty(path);
    }

    [Fact]
    public void GetNeighbors_ReturnsConnectedRooms()
    {
        var graph = CreateSimpleOffice();
        var neighbors = graph.GetNeighbors(2);
        Assert.Equal(2, neighbors.Count);
        Assert.Contains(neighbors, n => n.Id == 1);
        Assert.Contains(neighbors, n => n.Id == 3);
    }
}
