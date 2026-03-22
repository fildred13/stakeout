using Godot;

namespace Stakeout.Evidence;

public class EvidenceItem
{
    public int Id { get; set; }
    public EvidenceEntityType EntityType { get; set; }
    public int EntityId { get; set; }
    public Vector2 BoardPosition { get; set; }
}
