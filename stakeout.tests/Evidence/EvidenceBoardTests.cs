using System.Linq;
using Godot;
using Stakeout.Evidence;
using Xunit;

namespace Stakeout.Tests.Evidence;

public class EvidenceBoardTests
{
    [Fact]
    public void AddItem_ReturnsItemWithCorrectFields()
    {
        var board = new EvidenceBoard();
        var item = board.AddItem(EvidenceEntityType.Person, 42, new Vector2(100, 200));

        Assert.Equal(EvidenceEntityType.Person, item.EntityType);
        Assert.Equal(42, item.EntityId);
        Assert.Equal(new Vector2(100, 200), item.BoardPosition);
    }

    [Fact]
    public void AddItem_AssignsUniqueIds()
    {
        var board = new EvidenceBoard();
        var item1 = board.AddItem(EvidenceEntityType.Person, 1, Vector2.Zero);
        var item2 = board.AddItem(EvidenceEntityType.Address, 2, Vector2.Zero);

        Assert.NotEqual(item1.Id, item2.Id);
    }

    [Fact]
    public void HasItem_ExistingItem_ReturnsTrue()
    {
        var board = new EvidenceBoard();
        board.AddItem(EvidenceEntityType.Person, 42, Vector2.Zero);

        Assert.True(board.HasItem(EvidenceEntityType.Person, 42));
    }

    [Fact]
    public void HasItem_WrongType_ReturnsFalse()
    {
        var board = new EvidenceBoard();
        board.AddItem(EvidenceEntityType.Person, 42, Vector2.Zero);

        Assert.False(board.HasItem(EvidenceEntityType.Address, 42));
    }

    [Fact]
    public void HasItem_NoItems_ReturnsFalse()
    {
        var board = new EvidenceBoard();

        Assert.False(board.HasItem(EvidenceEntityType.Person, 1));
    }

    [Fact]
    public void RemoveItem_RemovesFromDictionary()
    {
        var board = new EvidenceBoard();
        var item = board.AddItem(EvidenceEntityType.Person, 42, Vector2.Zero);

        board.RemoveItem(item.Id);

        Assert.False(board.HasItem(EvidenceEntityType.Person, 42));
        Assert.Empty(board.Items);
    }

    [Fact]
    public void RemoveItem_AlsoRemovesAttachedConnections()
    {
        var board = new EvidenceBoard();
        var a = board.AddItem(EvidenceEntityType.Person, 1, Vector2.Zero);
        var b = board.AddItem(EvidenceEntityType.Person, 2, Vector2.Zero);
        var c = board.AddItem(EvidenceEntityType.Person, 3, Vector2.Zero);
        board.AddConnection(a.Id, b.Id);
        board.AddConnection(a.Id, c.Id);
        board.AddConnection(b.Id, c.Id);

        board.RemoveItem(a.Id);

        // Only b<->c connection should remain
        Assert.Single(board.Connections);
        Assert.Contains(board.Connections, conn =>
            conn.FromItemId == b.Id && conn.ToItemId == c.Id ||
            conn.FromItemId == c.Id && conn.ToItemId == b.Id);
    }

    [Fact]
    public void AddConnection_StoresConnection()
    {
        var board = new EvidenceBoard();
        var a = board.AddItem(EvidenceEntityType.Person, 1, Vector2.Zero);
        var b = board.AddItem(EvidenceEntityType.Person, 2, Vector2.Zero);

        board.AddConnection(a.Id, b.Id);

        Assert.Single(board.Connections);
    }

    [Fact]
    public void AddConnection_DuplicateIgnored()
    {
        var board = new EvidenceBoard();
        var a = board.AddItem(EvidenceEntityType.Person, 1, Vector2.Zero);
        var b = board.AddItem(EvidenceEntityType.Person, 2, Vector2.Zero);

        board.AddConnection(a.Id, b.Id);
        board.AddConnection(a.Id, b.Id);
        board.AddConnection(b.Id, a.Id); // reversed duplicate

        Assert.Single(board.Connections);
    }

    [Fact]
    public void RemoveConnection_RemovesSpecificConnection()
    {
        var board = new EvidenceBoard();
        var a = board.AddItem(EvidenceEntityType.Person, 1, Vector2.Zero);
        var b = board.AddItem(EvidenceEntityType.Person, 2, Vector2.Zero);
        board.AddConnection(a.Id, b.Id);

        board.RemoveConnection(a.Id, b.Id);

        Assert.Empty(board.Connections);
    }

    [Fact]
    public void RemoveConnection_ReversedOrder_StillRemoves()
    {
        var board = new EvidenceBoard();
        var a = board.AddItem(EvidenceEntityType.Person, 1, Vector2.Zero);
        var b = board.AddItem(EvidenceEntityType.Person, 2, Vector2.Zero);
        board.AddConnection(a.Id, b.Id);

        board.RemoveConnection(b.Id, a.Id);

        Assert.Empty(board.Connections);
    }

    [Fact]
    public void RemoveAllConnections_RemovesOnlyTargetItems()
    {
        var board = new EvidenceBoard();
        var a = board.AddItem(EvidenceEntityType.Person, 1, Vector2.Zero);
        var b = board.AddItem(EvidenceEntityType.Person, 2, Vector2.Zero);
        var c = board.AddItem(EvidenceEntityType.Person, 3, Vector2.Zero);
        board.AddConnection(a.Id, b.Id);
        board.AddConnection(a.Id, c.Id);
        board.AddConnection(b.Id, c.Id);

        board.RemoveAllConnections(a.Id);

        Assert.Single(board.Connections);
    }
}
