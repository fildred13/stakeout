using System.Collections.Generic;
using Godot;
using Stakeout.Evidence;

public partial class StringLayer : Control
{
    public Stakeout.Evidence.EvidenceBoard Board { get; set; }
    public Dictionary<int, EvidencePolaroid> PolaroidNodes { get; set; }

    public bool IsDrawingString { get; set; }
    public int DrawingFromItemId { get; set; }
    public Vector2 DrawingEndPoint { get; set; }

    public int HoveredThumbTackItemId { get; set; } = -1;

    private static readonly Color StringColor = new(0.8f, 0.1f, 0.1f);
    private const float StringWidth = 2.5f;

    public override void _Draw()
    {
        if (Board == null || PolaroidNodes == null) return;

        foreach (var conn in Board.Connections)
        {
            if (PolaroidNodes.TryGetValue(conn.FromItemId, out var fromNode) &&
                PolaroidNodes.TryGetValue(conn.ToItemId, out var toNode))
            {
                var from = GetGlobalTransform().AffineInverse() * fromNode.GetThumbTackGlobalCenter();
                var to = GetGlobalTransform().AffineInverse() * toNode.GetThumbTackGlobalCenter();
                DrawLine(from, to, StringColor, StringWidth, true);
            }
        }

        if (IsDrawingString && PolaroidNodes.TryGetValue(DrawingFromItemId, out var sourceNode))
        {
            var from = GetGlobalTransform().AffineInverse() * sourceNode.GetThumbTackGlobalCenter();
            var to = GetGlobalTransform().AffineInverse() * DrawingEndPoint;
            DrawLine(from, to, StringColor, StringWidth, true);
        }
    }

    public override void _Process(double delta)
    {
        QueueRedraw();
    }

    public (int fromId, int toId) HitTestString(Vector2 globalPos, float tolerance = 8f)
    {
        if (Board == null || PolaroidNodes == null) return (-1, -1);

        foreach (var conn in Board.Connections)
        {
            if (PolaroidNodes.TryGetValue(conn.FromItemId, out var fromNode) &&
                PolaroidNodes.TryGetValue(conn.ToItemId, out var toNode))
            {
                var from = fromNode.GetThumbTackGlobalCenter();
                var to = toNode.GetThumbTackGlobalCenter();
                var dist = DistanceToLineSegment(globalPos, from, to);
                if (dist <= tolerance)
                {
                    return (conn.FromItemId, conn.ToItemId);
                }
            }
        }

        return (-1, -1);
    }

    private static float DistanceToLineSegment(Vector2 point, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        var ap = point - a;
        var t = Mathf.Clamp(ap.Dot(ab) / ab.LengthSquared(), 0f, 1f);
        var closest = a + ab * t;
        return point.DistanceTo(closest);
    }
}
