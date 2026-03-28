using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Stakeout.Simulation;
using Stakeout.Simulation.Entities;

public partial class GraphView : Control
{
    private SimulationState _state;
    private int _addressId;
    private Dictionary<int, Vector2> _nodePositions = new();
    private List<Sublocation> _sublocations = new();
    private List<SublocationConnection> _connections = new();

    public void Initialize(SimulationState state, int addressId)
    {
        _state = state;
        _addressId = addressId;
        var address = state.Addresses[addressId];
        _sublocations = new List<Sublocation>(address.Sublocations.Values);
        _connections = address.Connections;
        LayoutNodes();
        QueueRedraw();
    }

    private void LayoutNodes()
    {
        // Simple tree layout: BFS from road node, spread by depth
        var road = _sublocations.FirstOrDefault(s => s.HasTag("road"));
        if (road == null && _sublocations.Count > 0) road = _sublocations[0];
        if (road == null) return;

        var adjacency = new Dictionary<int, List<int>>();
        foreach (var s in _sublocations) adjacency[s.Id] = new List<int>();
        foreach (var c in _connections)
        {
            if (adjacency.ContainsKey(c.FromSublocationId))
                adjacency[c.FromSublocationId].Add(c.ToSublocationId);
            if (adjacency.ContainsKey(c.ToSublocationId))
                adjacency[c.ToSublocationId].Add(c.FromSublocationId);
        }

        var visited = new HashSet<int>();
        var queue = new Queue<(int id, int depth)>();
        var depthNodes = new Dictionary<int, List<int>>();
        queue.Enqueue((road.Id, 0));
        visited.Add(road.Id);

        while (queue.Count > 0)
        {
            var (id, depth) = queue.Dequeue();
            if (!depthNodes.ContainsKey(depth)) depthNodes[depth] = new List<int>();
            depthNodes[depth].Add(id);

            foreach (var neighbor in adjacency[id])
            {
                if (visited.Add(neighbor))
                    queue.Enqueue((neighbor, depth + 1));
            }
        }

        // Position nodes: each depth level is a row, nodes spread horizontally
        float startY = 40;
        float rowHeight = 80;
        float viewWidth = Size.X > 0 ? Size.X : 600;

        foreach (var (depth, nodes) in depthNodes)
        {
            float spacing = viewWidth / (nodes.Count + 1);
            for (int i = 0; i < nodes.Count; i++)
            {
                _nodePositions[nodes[i]] = new Vector2(spacing * (i + 1), startY + depth * rowHeight);
            }
        }

        // Any unvisited nodes (disconnected) go at the bottom
        float extraY = startY + (depthNodes.Count + 1) * rowHeight;
        int extraIndex = 0;
        foreach (var s in _sublocations)
        {
            if (!_nodePositions.ContainsKey(s.Id))
            {
                _nodePositions[s.Id] = new Vector2(100 + extraIndex * 120, extraY);
                extraIndex++;
            }
        }
    }

    public override void _Draw()
    {
        var font = ThemeDB.FallbackFont;

        // Draw connections
        foreach (var conn in _connections)
        {
            if (_nodePositions.TryGetValue(conn.FromSublocationId, out var from) &&
                _nodePositions.TryGetValue(conn.ToSublocationId, out var to))
            {
                DrawLine(from, to, Colors.Gray, 2);

                if (conn.Type != ConnectionType.OpenPassage)
                {
                    var mid = (from + to) / 2;
                    var label = conn.Name ?? conn.Type.ToString();
                    var labelSize = font.GetStringSize(label, HorizontalAlignment.Center, -1, 9);
                    DrawString(font, new Vector2(mid.X - labelSize.X / 2, mid.Y - 2), label,
                        HorizontalAlignment.Left, -1, 9, new Color(0.7f, 0.7f, 0.5f));
                }
            }
        }

        // Draw nodes
        int fontSize = 12;

        foreach (var sub in _sublocations)
        {
            if (!_nodePositions.TryGetValue(sub.Id, out var pos)) continue;

            var color = GetTagColor(sub);
            var rect = new Rect2(pos.X - 50, pos.Y - 15, 100, 30);
            DrawRect(rect, color);
            DrawRect(rect, Colors.White, false, 1);

            // Label
            var textSize = font.GetStringSize(sub.Name, HorizontalAlignment.Center, -1, fontSize);
            DrawString(font, new Vector2(pos.X - textSize.X / 2, pos.Y + fontSize / 2), sub.Name, HorizontalAlignment.Left, -1, fontSize, Colors.White);

            // NPC dots
            if (_state != null)
            {
                int npcCount = _state.People.Values.Count(p => p.CurrentSublocationId == sub.Id);
                if (npcCount > 0)
                {
                    DrawCircle(new Vector2(pos.X + 45, pos.Y - 10), 5, Colors.Yellow);
                    DrawString(font, new Vector2(pos.X + 38, pos.Y - 2), npcCount.ToString(), HorizontalAlignment.Left, -1, 10, Colors.White);
                }
            }
        }
    }

    public override void _Process(double delta)
    {
        QueueRedraw(); // Redraw each frame to update NPC positions
    }

    private Color GetTagColor(Sublocation sub)
    {
        if (sub.HasTag("entrance")) return new Color(0.2f, 0.4f, 0.8f);
        if (sub.HasTag("work_area")) return new Color(0.8f, 0.7f, 0.2f);
        if (sub.HasTag("food")) return new Color(0.2f, 0.7f, 0.3f);
        if (sub.HasTag("bedroom")) return new Color(0.6f, 0.3f, 0.7f);
        if (sub.HasTag("restroom")) return new Color(0.3f, 0.6f, 0.6f);
        if (sub.HasTag("road")) return new Color(0.4f, 0.4f, 0.4f);
        if (sub.HasTag("covert_entry")) return new Color(0.7f, 0.2f, 0.2f);
        return new Color(0.3f, 0.3f, 0.3f);
    }
}
