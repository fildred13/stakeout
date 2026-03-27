using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Stakeout.Simulation;
using Stakeout.Simulation.Entities;

public partial class BlueprintView : Control
{
    private SimulationState _state;
    private int _addressId;
    private int? _currentFloor;
    private List<int?> _floors = new();
    private Dictionary<int, Rect2> _roomRects = new();
    private List<Sublocation> _sublocations = new();
    private List<SublocationConnection> _connections = new();

    public void Initialize(SimulationState state, int addressId)
    {
        _state = state;
        _addressId = addressId;
        var address = state.Addresses[addressId];
        _sublocations = new List<Sublocation>(address.Sublocations.Values);
        _connections = address.Connections;

        _floors = _sublocations.Select(s => s.Floor).Distinct().OrderBy(f => f).ToList();
        _currentFloor = _floors.FirstOrDefault();
        LayoutRooms();
        QueueRedraw();
    }

    public void SetFloor(int? floor)
    {
        _currentFloor = floor;
        LayoutRooms();
        QueueRedraw();
    }

    private void LayoutRooms()
    {
        _roomRects.Clear();
        var floorSubs = _sublocations.Where(s => s.Floor == _currentFloor).ToList();
        if (floorSubs.Count == 0) return;

        // Simple grid layout: entrance at bottom, others fill grid
        float startX = 50;
        float startY = 50;
        float roomW = 120;
        float roomH = 60;
        float gap = 10;
        int cols = Math.Max(3, (int)Math.Ceiling(Math.Sqrt(floorSubs.Count)));

        // Sort: entrance-tagged rooms first (bottom), then others
        var entranceRooms = floorSubs.Where(s => s.HasTag("entrance") || s.HasTag("road")).ToList();
        var otherRooms = floorSubs.Where(s => !s.HasTag("entrance") && !s.HasTag("road")).ToList();
        var ordered = otherRooms.Concat(entranceRooms).ToList();

        for (int i = 0; i < ordered.Count; i++)
        {
            int row = i / cols;
            int col = i % cols;
            var rect = new Rect2(startX + col * (roomW + gap), startY + row * (roomH + gap), roomW, roomH);
            _roomRects[ordered[i].Id] = rect;
        }
    }

    public override void _Draw()
    {
        var font = ThemeDB.FallbackFont;
        int fontSize = 12;

        // Draw rooms
        foreach (var sub in _sublocations.Where(s => s.Floor == _currentFloor))
        {
            if (!_roomRects.TryGetValue(sub.Id, out var rect)) continue;

            var color = GetTagColor(sub);
            DrawRect(rect, color);
            DrawRect(rect, Colors.White, false, 1);

            // Label
            DrawString(font, new Vector2(rect.Position.X + 5, rect.Position.Y + 20),
                sub.Name, HorizontalAlignment.Left, (int)rect.Size.X - 10, fontSize, Colors.White);

            // NPC count
            if (_state != null)
            {
                int npcCount = _state.People.Values.Count(p => p.CurrentSublocationId == sub.Id);
                if (npcCount > 0)
                {
                    DrawCircle(new Vector2(rect.End.X - 10, rect.Position.Y + 10), 5, Colors.Yellow);
                    DrawString(font, new Vector2(rect.End.X - 16, rect.Position.Y + 18),
                        npcCount.ToString(), HorizontalAlignment.Left, -1, 10, Colors.White);
                }
            }
        }

        // Draw connections between visible rooms
        foreach (var conn in _connections)
        {
            if (_roomRects.TryGetValue(conn.FromSublocationId, out var fromRect) &&
                _roomRects.TryGetValue(conn.ToSublocationId, out var toRect))
            {
                var from = fromRect.GetCenter();
                var to = toRect.GetCenter();
                DrawLine(from, to, new Color(0.5f, 0.5f, 0.5f, 0.5f), 1);

                if (conn.Type != ConnectionType.OpenPassage && conn.Name != null)
                {
                    var mid = (from + to) / 2;
                    DrawString(font, new Vector2(mid.X, mid.Y - 2), conn.Name,
                        HorizontalAlignment.Left, -1, 8, new Color(0.6f, 0.6f, 0.4f, 0.7f));
                }
            }
        }

        // Floor label
        string floorText = _currentFloor.HasValue ? $"Floor {_currentFloor.Value}" : "Ground";
        DrawString(font, new Vector2(10, 30), floorText, HorizontalAlignment.Left, -1, 16, Colors.White);
    }

    public override void _Process(double delta)
    {
        QueueRedraw();
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
