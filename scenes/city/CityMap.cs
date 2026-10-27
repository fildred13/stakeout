using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Stakeout.Simulation;
using Stakeout.Simulation.City;
using Stakeout.Simulation.Entities;
using Stakeout.UI;

/// <summary>
/// The city from above: streets, buildings, the traffic actually on the roads (every moving car is a real trip),
/// your car, and the places you know about. Click a building to drive there.
/// </summary>
public partial class CityMap : Control
{
    private readonly GameShell _shell;
    private Vector2 _pan;
    private float _zoom = 0.5f;
    private bool _dragging;
    private bool _moved;
    private Vector2 _dragStart;
    private Vector2 _panStart;
    private int? _hoverAddress;
    private Label _tooltip;
    private bool _centered;
    public bool FollowPlayer { get; set; }

    private const int Cell = Address.CellSize;
    private static readonly Color Road = new(0.38f, 0.36f, 0.34f);
    private static readonly Color Ground = new(0.12f, 0.14f, 0.1f);
    private static readonly Color Building = new(0.22f, 0.21f, 0.2f);
    private static readonly Color Park = new(0.16f, 0.27f, 0.13f);
    private static readonly Color KnownColor = Ui.Amber;
    private static readonly Color SceneColor = Ui.Red;

    public CityMap(GameShell shell)
    {
        _shell = shell;
        MouseFilter = MouseFilterEnum.Stop;
        ClipContents = true;
        _tooltip = Ui.Label("", 16, Ui.Text);
        _tooltip.Visible = false;
        var bg = Ui.PanelBox(new Color(0.03f, 0.03f, 0.03f, 0.92f), Ui.Amber, 6);
        bg.AddChild(_tooltip);
        bg.Visible = false;
        bg.MouseFilter = MouseFilterEnum.Ignore;
        _tooltipPanel = bg;
        AddChild(bg);
    }

    private readonly PanelContainer _tooltipPanel;

    private World World => _shell.Session.World;
    private CityGrid Grid => World.State.CityGrids[World.CityId];

    public override void _Process(double delta)
    {
        if (!Visible || _shell.Game.Session == null) return;
        if (!_centered && Size.X > 10)
        {
            _centered = true;
            CenterOn(World.PositionOf(_shell.D.Agent));
        }
        if (FollowPlayer)
        {
            CenterOn(World.PositionOf(_shell.D.Agent));
            if (!_shell.D.Travelling) FollowPlayer = false;
        }
        QueueRedraw();
    }

    private void CenterOn(Vector2 world) => _pan = Size / 2 - world * _zoom;

    private Vector2 ToScreen(Vector2 world) => world * _zoom + _pan;
    private Vector2 ToWorld(Vector2 screen) => (screen - _pan) / _zoom;

    public override void _Draw()
    {
        var state = World.State;
        var grid = Grid;
        DrawRect(new Rect2(Vector2.Zero, Size), Ground);
        float cs = Cell * _zoom;
        int minX = Math.Max(0, (int)Math.Floor(-_pan.X / cs) - 3), maxX = Math.Min(grid.Width - 1, (int)Math.Ceiling((Size.X - _pan.X) / cs));
        int minY = Math.Max(0, (int)Math.Floor(-_pan.Y / cs) - 3), maxY = Math.Min(grid.Height - 1, (int)Math.Ceiling((Size.Y - _pan.Y) / cs));
        var drawn = new HashSet<int>();
        var d = _shell.D;
        int? scene = d.Case?.SceneAddressId;

        for (int x = minX; x <= maxX; x++)
            for (int y = minY; y <= maxY; y++)
            {
                var cell = grid.GetCell(x, y);
                var pos = ToScreen(new Vector2(x * Cell, y * Cell));
                if (cell.PlotType == PlotType.Road)
                {
                    DrawRect(new Rect2(pos, new Vector2(cs, cs)), Road);
                    continue;
                }
                if (!cell.AddressId.HasValue || !drawn.Add(cell.AddressId.Value)) continue;
                if (!state.Addresses.TryGetValue(cell.AddressId.Value, out var a)) continue;
                var (w, h) = cell.PlotType.GetSize();
                var origin = ToScreen(new Vector2(a.GridX * Cell, a.GridY * Cell));
                float inset = 4 * _zoom;
                var rect = new Rect2(origin + new Vector2(inset, inset), new Vector2(w * cs - 2 * inset, h * cs - 2 * inset));
                Color fill = cell.PlotType == PlotType.Park ? Park : BuildingTint(a.Type);
                DrawRect(rect, fill);
                bool known = d.KnownAddresses.Contains(a.Id);
                if (a.Id == scene) DrawRect(rect, new Color(SceneColor, 0.55f));
                if (a.Id == d.Me.HomeAddressId) DrawRect(rect, new Color(Ui.Blue, 0.45f));
                if (known) DrawRect(rect, a.Id == scene ? SceneColor : KnownColor, false, Math.Max(1.5f, 3 * _zoom));
                if (a.Id == _hoverAddress) DrawRect(rect, Ui.Text, false, 2);
                if (_zoom >= 0.7f && (known || a.Id == _hoverAddress))
                    DrawString(Ui.Pixel, rect.Position + new Vector2(2, 14), Short(a), HorizontalAlignment.Left, rect.Size.X - 4, 16, Ui.Text);
            }

        // Street names when zoomed in.
        if (_zoom >= 0.9f)
        {
            var labelled = new HashSet<int>();
            for (int x = minX; x <= maxX; x += 6)
                for (int y = minY; y <= maxY; y += 6)
                {
                    var c = grid.GetCell(x, y);
                    if (c.PlotType != PlotType.Road || !c.StreetId.HasValue || !labelled.Add(c.StreetId.Value)) continue;
                    DrawString(Ui.Pixel, ToScreen(new Vector2(x * Cell + 2, y * Cell + 30)), state.Streets[c.StreetId.Value].Name,
                        HorizontalAlignment.Left, -1, 16, new Color(Ui.Text, 0.7f));
                }
        }

        // Traffic: every moving trip.
        float r = Math.Max(2f, 6 * _zoom);
        var viewRect = new Rect2(Vector2.Zero, Size).Grow(20);
        foreach (var trip in World.History.TripsBetween(World.Now, World.Now.AddSeconds(1)))
        {
            if (trip.PersonIds.Contains(d.Agent.Id)) continue;
            var p = ToScreen(World.PositionOnTrip(trip));
            if (!viewRect.HasPoint(p)) continue;
            var color = trip.VehicleId.HasValue ? new Color(0.85f, 0.85f, 0.8f) : trip.Taxi ? new Color(0.95f, 0.85f, 0.2f) : new Color(0.6f, 0.75f, 0.6f);
            if (trip.VehicleId.HasValue || trip.Taxi) DrawRect(new Rect2(p - new Vector2(r, r * 0.7f), new Vector2(r * 2, r * 1.4f)), color);
            else DrawCircle(p, r * 0.6f, color);
        }

        // You.
        var me = ToScreen(World.PositionOf(d.Agent));
        DrawCircle(me, Math.Max(5, 10 * _zoom), Ui.Blue);
        DrawArc(me, Math.Max(7, 14 * _zoom), 0, Mathf.Tau, 24, Ui.Text, 2);

        // Legend.
        var legend = "Amber: places you know   Red: crime scene   Blue: you   Click a building to go there. Drag to pan, wheel to zoom.";
        DrawRect(new Rect2(0, Size.Y - 24, Size.X, 24), new Color(0, 0, 0, 0.6f));
        DrawString(Ui.Pixel, new Vector2(8, Size.Y - 7), legend, HorizontalAlignment.Left, Size.X - 16, 16, Ui.Dim);
    }

    private static Color BuildingTint(AddressType type) => type switch
    {
        AddressType.Diner => new Color(0.3f, 0.24f, 0.18f),
        AddressType.DiveBar => new Color(0.28f, 0.18f, 0.2f),
        AddressType.Office => new Color(0.2f, 0.22f, 0.27f),
        AddressType.ApartmentBuilding => new Color(0.25f, 0.23f, 0.22f),
        _ => Building
    };

    private string Short(Address a)
    {
        var biz = World.State.BusinessAt(a.Id);
        return biz?.Name ?? World.State.AddressLine(a.Id);
    }

    private int? AddressAt(Vector2 screen)
    {
        var w = ToWorld(screen);
        int x = (int)Math.Floor(w.X / Cell), y = (int)Math.Floor(w.Y / Cell);
        if (!Grid.IsInBounds(x, y)) return null;
        return Grid.GetCell(x, y).AddressId;
    }

    private string Describe(int addressId)
    {
        var state = World.State;
        var a = state.Addresses[addressId];
        var lines = new List<string> { state.PlaceName(addressId) };
        lines.Add(a.Type switch
        {
            AddressType.ApartmentBuilding => "Apartment building",
            AddressType.SuburbanHome => "House",
            AddressType.DiveBar => "Bar",
            _ => a.Type.ToString()
        });
        var biz = state.BusinessAt(addressId);
        if (biz != null) lines[^1] += Stakeout.Simulation.Agents.Planner.IsOpen(biz, World.Now) ? " (open)" : " (closed)";
        var d = _shell.D;
        foreach (var k in d.People.Values.Where(k => k.AddressKnown && state.People[k.PersonId].HomeAddressId == addressId).Take(4))
            lines.Add($"Home of {state.People[k.PersonId].FullName}");
        foreach (var k in d.People.Values.Where(k => k.EmployerKnown && state.People[k.PersonId].BusinessId.HasValue
                                                     && state.Businesses[state.People[k.PersonId].BusinessId.Value].AddressId == addressId).Take(4))
            lines.Add($"{state.People[k.PersonId].FullName} works here");
        if (d.Case != null && addressId == d.Case.SceneAddressId) lines.Add("The crime scene");
        if (addressId == d.Me.HomeAddressId) lines.Add("Your office");
        if (d.AddressId.HasValue && addressId != d.AddressId && !d.Travelling)
        {
            var mode = World.Movement.ModeFor(d.Agent);
            var t = World.Movement.TravelEstimate(d.AddressId.Value, addressId, mode);
            lines.Add($"About {Math.Max(1, (int)t.TotalMinutes)} min away");
        }
        return string.Join("\n", lines);
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mb)
        {
            if (mb.ButtonIndex == MouseButton.WheelUp && mb.Pressed) Zoom(1.15f, mb.Position);
            else if (mb.ButtonIndex == MouseButton.WheelDown && mb.Pressed) Zoom(1 / 1.15f, mb.Position);
            else if (mb.ButtonIndex == MouseButton.Left)
            {
                if (mb.Pressed)
                {
                    _dragging = true;
                    _moved = false;
                    _dragStart = mb.Position;
                    _panStart = _pan;
                }
                else
                {
                    _dragging = false;
                    if (!_moved) Click(mb.Position);
                }
            }
            AcceptEvent();
        }
        else if (@event is InputEventMouseMotion mm)
        {
            if (_dragging)
            {
                if ((mm.Position - _dragStart).Length() > 4) _moved = true;
                _pan = _panStart + mm.Position - _dragStart;
                FollowPlayer = false;
            }
            _hoverAddress = AddressAt(mm.Position);
            if (_hoverAddress.HasValue)
            {
                _tooltip.Text = Describe(_hoverAddress.Value);
                _tooltip.Visible = true;
                _tooltipPanel.Visible = true;
                _tooltipPanel.Position = mm.Position + new Vector2(16, 16);
                if (_tooltipPanel.Position.X + _tooltipPanel.Size.X > Size.X) _tooltipPanel.Position -= new Vector2(_tooltipPanel.Size.X + 32, 0);
                if (_tooltipPanel.Position.Y + _tooltipPanel.Size.Y > Size.Y) _tooltipPanel.Position -= new Vector2(0, _tooltipPanel.Size.Y + 32);
            }
            else _tooltipPanel.Visible = false;
        }
    }

    private void Zoom(float factor, Vector2 at)
    {
        var before = ToWorld(at);
        _zoom = Math.Clamp(_zoom * factor, 0.12f, 2.0f);
        _pan = at - before * _zoom;
    }

    private void Click(Vector2 at)
    {
        var address = AddressAt(at);
        if (!address.HasValue) return;
        int id = address.Value;
        var d = _shell.D;
        var menu = new PopupMenu();
        menu.AddItem(World.State.PlaceName(id), 99);
        menu.SetItemDisabled(0, true);
        bool canGo = !d.Travelling && d.AddressId != id;
        menu.AddItem(canGo ? "Drive there" : d.AddressId == id ? "You are here" : "You're on the road", 0);
        menu.SetItemDisabled(1, !canGo);
        menu.AddItem("Pin to the board", 1);
        menu.IdPressed += item =>
        {
            if (item == 0) _shell.TravelTo(id);
            else if (item == 1) _shell.PinToBoard(Stakeout.Evidence.EvidenceEntityType.Address, id);
            menu.QueueFree();
        };
        menu.PopupHide += () => menu.QueueFree();
        AddChild(menu);
        menu.Position = (Vector2I)(GetGlobalMousePosition() + new Vector2(8, 8));
        menu.Popup();
    }
}
