// stakeout.tests/Simulation/Entities/SublocationGraphTests.cs
using System.Collections.Generic;
using System.Linq;
using Stakeout.Simulation.Entities;
using Xunit;

namespace Stakeout.Tests.Simulation.Entities;

public class SublocationGraphTests
{
    /// <summary>
    /// Road --[Front Door, id=100, tag="entrance"]--> Lobby --[id=101]--> Cubicle Area --[id=102]--> Break Room
    /// Lobby tags: "work_area", "public"  (no "entrance" tag on the sublocation)
    /// </summary>
    private static SublocationGraph CreateSimpleOffice()
    {
        var subs = new Dictionary<int, Sublocation>
        {
            { 1, new Sublocation { Id = 1, AddressId = 10, Name = "Road",         Tags = new[] { "road" } } },
            { 2, new Sublocation { Id = 2, AddressId = 10, Name = "Lobby",        Tags = new[] { "work_area", "public" } } },
            { 3, new Sublocation { Id = 3, AddressId = 10, Name = "Cubicle Area", Tags = new[] { "work_area" } } },
            { 4, new Sublocation { Id = 4, AddressId = 10, Name = "Break Room",   Tags = new[] { "food" } } },
        };
        var conns = new List<SublocationConnection>
        {
            new() { Id = 100, Name = "Front Door", FromSublocationId = 1, ToSublocationId = 2,
                    Type = ConnectionType.Door, Tags = new[] { "entrance" } },
            new() { Id = 101, FromSublocationId = 2, ToSublocationId = 3,
                    Type = ConnectionType.Door },
            new() { Id = 102, FromSublocationId = 3, ToSublocationId = 4,
                    Type = ConnectionType.OpenPassage },
        };
        return new SublocationGraph(subs, conns);
    }

    // ── FindByTag ────────────────────────────────────────────────────────────

    [Fact]
    public void FindByTag_ExistingTag_ReturnsSublocation()
    {
        var graph = CreateSimpleOffice();
        var result = graph.FindByTag("work_area");
        Assert.NotNull(result);
        // Returns first match — either Lobby or Cubicle Area
        Assert.True(result.Name == "Lobby" || result.Name == "Cubicle Area");
    }

    [Fact]
    public void FindByTag_NoMatch_ReturnsNull()
    {
        var graph = CreateSimpleOffice();
        var result = graph.FindByTag("restroom");
        Assert.Null(result);
    }

    // ── FindAllByTag ─────────────────────────────────────────────────────────

    [Fact]
    public void FindAllByTag_ReturnsAllMatching()
    {
        var graph = CreateSimpleOffice();
        var results = graph.FindAllByTag("work_area");
        Assert.Equal(2, results.Count);
        Assert.Contains(results, r => r.Name == "Lobby");
        Assert.Contains(results, r => r.Name == "Cubicle Area");
    }

    // ── GetRoad ───────────────────────────────────────────────────────────────

    [Fact]
    public void GetRoad_ReturnsRoadNode()
    {
        var graph = CreateSimpleOffice();
        var road = graph.GetRoad();
        Assert.NotNull(road);
        Assert.Equal("Road", road.Name);
    }

    // ── FindPath ─────────────────────────────────────────────────────────────

    [Fact]
    public void FindPath_AdjacentRooms_ReturnsPathStepWithVia()
    {
        var graph = CreateSimpleOffice();
        var path = graph.FindPath(2, 3);
        Assert.Equal(2, path.Count);
        Assert.Equal(2, path[0].Location.Id);
        Assert.Null(path[0].Via);               // first step has no "via"
        Assert.Equal(3, path[1].Location.Id);
        Assert.NotNull(path[1].Via);            // arrived via connection id=101
        Assert.Equal(101, path[1].Via.Id);
    }

    [Fact]
    public void FindPath_TwoHops_ReturnsFullPath()
    {
        var graph = CreateSimpleOffice();
        var path = graph.FindPath(1, 3);
        Assert.Equal(3, path.Count);
        Assert.Equal(1, path[0].Location.Id);
        Assert.Equal(2, path[1].Location.Id);
        Assert.Equal(3, path[2].Location.Id);
    }

    [Fact]
    public void FindPath_SameRoom_ReturnsSingleElementWithNullVia()
    {
        var graph = CreateSimpleOffice();
        var path = graph.FindPath(2, 2);
        Assert.Single(path);
        Assert.Equal(2, path[0].Location.Id);
        Assert.Null(path[0].Via);
    }

    [Fact]
    public void FindPath_Unreachable_ReturnsEmptyList()
    {
        var subs = new Dictionary<int, Sublocation>
        {
            { 1, new Sublocation { Id = 1, AddressId = 10, Name = "Road",   Tags = new[] { "road" } } },
            { 2, new Sublocation { Id = 2, AddressId = 10, Name = "Island", Tags = new[] { "isolated" } } },
        };
        var graph = new SublocationGraph(subs, new List<SublocationConnection>());
        var path = graph.FindPath(1, 2);
        Assert.Empty(path);
    }

    [Fact]
    public void FindPath_LockedDoor_BlocksTraversal()
    {
        var subs = new Dictionary<int, Sublocation>
        {
            { 1, new Sublocation { Id = 1, AddressId = 10, Name = "Outside", Tags = new[] { "road" } } },
            { 2, new Sublocation { Id = 2, AddressId = 10, Name = "Inside",  Tags = new[] { "private" } } },
        };
        var conns = new List<SublocationConnection>
        {
            new() { Id = 200, FromSublocationId = 1, ToSublocationId = 2,
                    Type = ConnectionType.Door,
                    Lockable = new LockableProperty { IsLocked = true } },
        };
        var graph = new SublocationGraph(subs, conns);
        var ctx = new TraversalContext();
        var path = graph.FindPath(1, 2, ctx);
        Assert.Empty(path);
    }

    [Fact]
    public void FindPath_UnlockedDoor_AllowsTraversal()
    {
        var subs = new Dictionary<int, Sublocation>
        {
            { 1, new Sublocation { Id = 1, AddressId = 10, Name = "Outside", Tags = new[] { "road" } } },
            { 2, new Sublocation { Id = 2, AddressId = 10, Name = "Inside",  Tags = new[] { "private" } } },
        };
        var conns = new List<SublocationConnection>
        {
            new() { Id = 201, FromSublocationId = 1, ToSublocationId = 2,
                    Type = ConnectionType.Door,
                    Lockable = new LockableProperty { IsLocked = false } },
        };
        var graph = new SublocationGraph(subs, conns);
        var ctx = new TraversalContext();
        var path = graph.FindPath(1, 2, ctx);
        Assert.Equal(2, path.Count);
        Assert.Equal(2, path[1].Location.Id);
    }

    // ── GetNeighbors ─────────────────────────────────────────────────────────

    [Fact]
    public void GetNeighbors_ReturnsConnectedRooms()
    {
        var graph = CreateSimpleOffice();
        var neighbors = graph.GetNeighbors(2);
        Assert.Equal(2, neighbors.Count);
        Assert.Contains(neighbors, n => n.Id == 1);
        Assert.Contains(neighbors, n => n.Id == 3);
    }

    // ── FindConnectionByTag ───────────────────────────────────────────────────

    [Fact]
    public void FindConnectionByTag_ReturnsConnectionAndTarget()
    {
        var graph = CreateSimpleOffice();
        var result = graph.FindConnectionByTag("entrance");
        Assert.NotNull(result);
        Assert.Equal(100, result.Value.conn.Id);
        Assert.Equal("Front Door", result.Value.conn.Name);
        Assert.Equal(2, result.Value.target.Id); // ToSublocationId = Lobby
    }

    [Fact]
    public void FindConnectionByTag_NoMatch_ReturnsNull()
    {
        var graph = CreateSimpleOffice();
        var result = graph.FindConnectionByTag("hidden_hatch");
        Assert.Null(result);
    }

    // ── FindEntryPoint ────────────────────────────────────────────────────────

    [Fact]
    public void FindEntryPoint_WithConnectionTag_ReturnsConnAndTarget()
    {
        var graph = CreateSimpleOffice();
        var result = graph.FindEntryPoint("entrance");
        Assert.NotNull(result);
        Assert.NotNull(result.Value.conn);
        Assert.Equal(100, result.Value.conn.Id);
        Assert.Equal(2, result.Value.target.Id);
    }

    [Fact]
    public void FindEntryPoint_WithSublocationTag_ReturnsNullConnAndTarget()
    {
        var graph = CreateSimpleOffice();
        // "food" is a sublocation tag on Break Room, not a connection tag
        var result = graph.FindEntryPoint("food");
        Assert.NotNull(result);
        Assert.Null(result.Value.conn);
        Assert.Equal("Break Room", result.Value.target.Name);
    }

    // ── GetConnectionBetween ─────────────────────────────────────────────────

    [Fact]
    public void GetConnectionBetween_BothDirections()
    {
        var graph = CreateSimpleOffice();

        // Forward direction
        var fwd = graph.GetConnectionBetween(1, 2);
        Assert.NotNull(fwd);
        Assert.Equal(100, fwd.Id);

        // Reverse direction — same logical connection, reflected
        var rev = graph.GetConnectionBetween(2, 1);
        Assert.NotNull(rev);
        Assert.Equal(100, rev.Id);
    }
}
