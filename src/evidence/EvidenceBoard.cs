using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Stakeout.Evidence;

public class EvidenceBoard
{
    public Dictionary<int, EvidenceItem> Items { get; } = new();
    public List<EvidenceConnection> Connections { get; } = new();

    private int _nextItemId = 1;

    public EvidenceItem AddItem(EvidenceEntityType entityType, int entityId, Vector2 boardPosition)
    {
        var item = new EvidenceItem
        {
            Id = _nextItemId++,
            EntityType = entityType,
            EntityId = entityId,
            BoardPosition = boardPosition
        };
        Items[item.Id] = item;
        return item;
    }

    public void RemoveItem(int itemId)
    {
        Items.Remove(itemId);
        Connections.RemoveAll(c => c.FromItemId == itemId || c.ToItemId == itemId);
    }

    public bool HasItem(EvidenceEntityType entityType, int entityId)
    {
        return Items.Values.Any(i => i.EntityType == entityType && i.EntityId == entityId);
    }

    public void AddConnection(int fromItemId, int toItemId)
    {
        var conn = new EvidenceConnection(fromItemId, toItemId);
        if (!Connections.Contains(conn))
        {
            Connections.Add(conn);
        }
    }

    public void RemoveConnection(int fromItemId, int toItemId)
    {
        var conn = new EvidenceConnection(fromItemId, toItemId);
        Connections.Remove(conn);
    }

    public void RemoveAllConnections(int itemId)
    {
        Connections.RemoveAll(c => c.FromItemId == itemId || c.ToItemId == itemId);
    }
}
