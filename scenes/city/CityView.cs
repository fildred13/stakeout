using System;
using System.Collections.Generic;
using Godot;
using Stakeout;
using Stakeout.Evidence;
using Stakeout.Simulation;
using Stakeout.Simulation.Actions;
using Stakeout.Simulation.City;
using Stakeout.Simulation.Entities;

public partial class CityView : Control, IContentView
{
    private GameManager _gameManager;
    private SimulationManager _simulationManager;
    private GameShell _gameShell;
    private Control _cityMapNode;
    private Label _hoverLabel;

    // Pan/zoom state (modeled after EvidenceBoardScene)
    private Vector2 _panOffset = Vector2.Zero;
    private float _zoom = 1.0f;
    private bool _isPanning;
    private Vector2 _panStartMouse;
    private Vector2 _panStartOffset;
    private const float MinZoom = 0.25f;
    private const float MaxZoom = 2.0f;
    private const float ZoomStep = 0.1f;

    // Grid constants
    private const int CellSize = 48;
    private const int GridWidth = 100;
    private const int GridHeight = 100;
    private const float BuildingInset = 4f; // (48-40)/2
    private const float BuildingSize = 40f;
    private const float EntityDotSize = 8f;
    private const float HoverDistance = 15f;

    // Selection state
    private int? _selectedAddressId;
    private bool _wasPlayerTraveling;
    private bool _didDrag;
    private const float DragThreshold = 4f;

    // Colors
    private static readonly Color RoadColor = new(0.6f, 0.6f, 0.6f);
    private static readonly Color BuildingColor = new(0.33f, 0.33f, 0.33f);
    private static readonly Color ParkColor = new(0.29f, 0.54f, 0.2f);
    private static readonly Color ParkTreeColor = new(0.15f, 0.35f, 0.1f);
    private static readonly Color EmptyColor = new(0.23f, 0.42f, 0.14f);
    private static readonly Color DrivewayColor = new(0.5f, 0.5f, 0.5f);
    private static readonly Color PlayerLocationColor = new(0.23f, 0.48f, 0.8f);
    private static readonly Color EvidenceBoardColor = new(0.54f, 0.2f, 0.2f);
    private static readonly Color SelectionOutlineColor = new(1f, 1f, 1f);
    private static readonly Color PersonColor = new(1f, 1f, 1f);
    private static readonly Color SleepingPersonColor = new(0.5f, 0.5f, 0.5f);
    private static readonly Color DeadPersonColor = new(1f, 0f, 0f);
    private static readonly Color PlayerDotColor = new(0.3f, 0.5f, 1f);
    private static readonly Color StreetLabelColor = new(0.85f, 0.85f, 0.85f);

    // Font for street labels
    private Font _font;
    private const int FontSize = 10;

    public void SetGameShell(GameShell shell)
    {
        _gameShell = shell;
        UpdateMenuItems();
    }

    public override void _Ready()
    {
        _cityMapNode = GetNode<Control>("CityMap");
        _hoverLabel = GetNode<Label>("HoverLabel");

        _gameManager = GetNode<GameManager>("/root/GameManager");
        _simulationManager = _gameManager.SimulationManager;

        _font = GD.Load<Font>("res://fonts/exepixelperfect/EXEPixelPerfect.ttf");

        // Center viewport on the middle of the grid
        CenterViewport();
    }

    private void CenterViewport()
    {
        var viewportSize = GetViewportRect().Size;
        var gridPixelSize = new Vector2(GridWidth * CellSize, GridHeight * CellSize);
        _panOffset = (viewportSize - gridPixelSize * _zoom) / 2;
    }

    public override void _Process(double delta)
    {
        // Refresh menu on travel state change
        var player = _simulationManager.State.Player;
        var isTraveling = player?.TravelInfo != null;
        if (_wasPlayerTraveling && !isTraveling)
            UpdateMenuItems();
        _wasPlayerTraveling = isTraveling;

        UpdateHoverLabel();
        QueueRedraw();
    }

    public override void _Draw()
    {
        var state = _simulationManager.State;
        var grid = state.CityGrid;
        if (grid == null) return;

        // Compute visible grid range for culling
        var viewportSize = GetViewportRect().Size;
        int minGX = Math.Max(0, (int)((-_panOffset.X) / (_zoom * CellSize)));
        int maxGX = Math.Min(GridWidth - 1, (int)((-_panOffset.X + viewportSize.X) / (_zoom * CellSize)));
        int minGY = Math.Max(0, (int)((-_panOffset.Y) / (_zoom * CellSize)));
        int maxGY = Math.Min(GridHeight - 1, (int)((-_panOffset.Y + viewportSize.Y) / (_zoom * CellSize)));

        // Collect highlight sets
        var playerCells = new HashSet<(int, int)>();
        var evidenceCells = new HashSet<(int, int)>();
        var selectedCells = new HashSet<(int, int)>();

        // Player location cells
        var player = state.Player;
        if (player != null && player.TravelInfo == null && player.CurrentAddressId > 0)
        {
            foreach (var cell in grid.GetCellsForAddress(player.CurrentAddressId))
                playerCells.Add(cell);
        }

        // Evidence board address cells
        if (_gameManager.EvidenceBoard != null)
        {
            foreach (var item in _gameManager.EvidenceBoard.Items.Values)
            {
                if (item.EntityType == EvidenceEntityType.Address)
                {
                    foreach (var cell in grid.GetCellsForAddress(item.EntityId))
                        evidenceCells.Add(cell);
                }
            }
        }

        // Selected address cells
        if (_selectedAddressId.HasValue)
        {
            foreach (var cell in grid.GetCellsForAddress(_selectedAddressId.Value))
                selectedCells.Add(cell);
        }

        // Track drawn multi-cell buildings to avoid redrawing
        var drawnAddresses = new HashSet<int>();

        // Track street label positions to avoid overlap
        var labelPositions = new List<Vector2>();

        // Draw grid cells
        for (int gx = minGX; gx <= maxGX; gx++)
        {
            for (int gy = minGY; gy <= maxGY; gy++)
            {
                var cell = grid.GetCell(gx, gy);
                var screenPos = GridToScreen(gx, gy);
                var scaledCell = CellSize * _zoom;

                // Draw base cell
                switch (cell.PlotType)
                {
                    case PlotType.Road:
                        DrawRect(new Rect2(screenPos, new Vector2(scaledCell, scaledCell)), RoadColor);
                        break;

                    case PlotType.Empty:
                        DrawRect(new Rect2(screenPos, new Vector2(scaledCell, scaledCell)), EmptyColor);
                        break;

                    case PlotType.Park:
                        if (cell.AddressId.HasValue && !drawnAddresses.Contains(cell.AddressId.Value))
                        {
                            drawnAddresses.Add(cell.AddressId.Value);
                            DrawPark(screenPos, scaledCell, cell);
                        }
                        else if (!cell.AddressId.HasValue)
                        {
                            // Standalone park cell
                            DrawRect(new Rect2(screenPos, new Vector2(scaledCell, scaledCell)), ParkColor);
                        }
                        break;

                    default:
                        if (cell.PlotType.IsBuilding())
                        {
                            if (cell.AddressId.HasValue && !drawnAddresses.Contains(cell.AddressId.Value))
                            {
                                drawnAddresses.Add(cell.AddressId.Value);
                                DrawBuilding(screenPos, scaledCell, cell);
                            }
                            else if (!cell.AddressId.HasValue)
                            {
                                // Fallback: draw single building cell
                                var inset = BuildingInset * _zoom;
                                var bSize = BuildingSize * _zoom;
                                DrawRect(new Rect2(screenPos + new Vector2(inset, inset), new Vector2(bSize, bSize)), BuildingColor);
                            }
                        }
                        break;
                }

                // Draw highlight overlays
                var key = (gx, gy);
                if (playerCells.Contains(key))
                    DrawRect(new Rect2(screenPos, new Vector2(scaledCell, scaledCell)), new Color(PlayerLocationColor, 0.5f));
                else if (evidenceCells.Contains(key))
                    DrawRect(new Rect2(screenPos, new Vector2(scaledCell, scaledCell)), new Color(EvidenceBoardColor, 0.5f));

                // Draw selection outline
                if (selectedCells.Contains(key))
                {
                    var outlineWidth = 2f * _zoom;
                    DrawRect(new Rect2(screenPos, new Vector2(scaledCell, scaledCell)), SelectionOutlineColor, false, outlineWidth);
                }

                // Street labels (every ~8 cells on road)
                if (cell.PlotType == PlotType.Road && cell.StreetId.HasValue && (gx % 8 == 0 || gy % 8 == 0))
                {
                    DrawStreetLabel(gx, gy, cell, screenPos, scaledCell, labelPositions);
                }
            }
        }

        // Draw entity dots on top
        DrawEntityDots(state, minGX, maxGX, minGY, maxGY);
    }

    private void DrawBuilding(Vector2 screenPos, float scaledCell, Cell cell)
    {
        var (sizeW, sizeH) = cell.PlotType.GetSize();
        var inset = BuildingInset * _zoom;
        // For multi-cell buildings, span across cells with gap accounting
        var totalW = (sizeW * CellSize - BuildingInset * 2) * _zoom;
        var totalH = (sizeH * CellSize - BuildingInset * 2) * _zoom;

        // Draw empty/green background behind building
        DrawRect(new Rect2(screenPos, new Vector2(sizeW * scaledCell, sizeH * scaledCell)), EmptyColor);

        // Draw building rect
        DrawRect(new Rect2(screenPos + new Vector2(inset, inset), new Vector2(totalW, totalH)), BuildingColor);

        // Draw driveway
        if (cell.AddressId.HasValue)
        {
            DrawDriveway(screenPos, scaledCell, sizeW, sizeH, cell.FacingDirection);
        }
    }

    private void DrawPark(Vector2 screenPos, float scaledCell, Cell cell)
    {
        var (sizeW, sizeH) = cell.PlotType.GetSize();

        // Draw park background
        DrawRect(new Rect2(screenPos, new Vector2(sizeW * scaledCell, sizeH * scaledCell)), ParkColor);

        // Draw trees (small dark green circles)
        var treeRadius = 4f * _zoom;
        var centerX = screenPos.X + sizeW * scaledCell / 2;
        var centerY = screenPos.Y + sizeH * scaledCell / 2;
        DrawCircle(new Vector2(centerX - 12 * _zoom, centerY - 8 * _zoom), treeRadius, ParkTreeColor);
        DrawCircle(new Vector2(centerX + 10 * _zoom, centerY + 6 * _zoom), treeRadius, ParkTreeColor);
        DrawCircle(new Vector2(centerX + 2 * _zoom, centerY + 14 * _zoom), treeRadius, ParkTreeColor);
    }

    private void DrawDriveway(Vector2 buildingScreenPos, float scaledCell, int buildingW, int buildingH, FacingDirection facing)
    {
        var drivewayW = 8f * _zoom;
        var drivewayH = 4f * _zoom;
        Vector2 pos;

        var bCenterX = buildingScreenPos.X + buildingW * scaledCell / 2;
        var bCenterY = buildingScreenPos.Y + buildingH * scaledCell / 2;

        switch (facing)
        {
            case FacingDirection.South:
                pos = new Vector2(bCenterX - drivewayW / 2, buildingScreenPos.Y + buildingH * scaledCell - drivewayH);
                DrawRect(new Rect2(pos, new Vector2(drivewayW, drivewayH)), DrivewayColor);
                break;
            case FacingDirection.North:
                pos = new Vector2(bCenterX - drivewayW / 2, buildingScreenPos.Y);
                DrawRect(new Rect2(pos, new Vector2(drivewayW, drivewayH)), DrivewayColor);
                break;
            case FacingDirection.East:
                pos = new Vector2(buildingScreenPos.X + buildingW * scaledCell - drivewayH, bCenterY - drivewayW / 2);
                DrawRect(new Rect2(pos, new Vector2(drivewayH, drivewayW)), DrivewayColor);
                break;
            case FacingDirection.West:
                pos = new Vector2(buildingScreenPos.X, bCenterY - drivewayW / 2);
                DrawRect(new Rect2(pos, new Vector2(drivewayH, drivewayW)), DrivewayColor);
                break;
        }
    }

    private void DrawStreetLabel(int gx, int gy, Cell cell, Vector2 screenPos, float scaledCell, List<Vector2> labelPositions)
    {
        if (!cell.StreetId.HasValue || _font == null) return;

        var state = _simulationManager.State;
        if (!state.Streets.TryGetValue(cell.StreetId.Value, out var street)) return;

        var labelCenter = screenPos + new Vector2(scaledCell / 2, scaledCell / 2);

        // Check distance from existing labels to avoid overlap
        foreach (var existing in labelPositions)
        {
            if (labelCenter.DistanceTo(existing) < 80 * _zoom)
                return;
        }

        labelPositions.Add(labelCenter);

        var fontSize = (int)(FontSize * _zoom);
        if (fontSize < 4) return; // Too small to read

        // Determine if this is a horizontal or vertical road segment
        var grid = state.CityGrid;
        bool isHorizontal = (grid.IsInBounds(gx - 1, gy) && grid.GetCell(gx - 1, gy).PlotType == PlotType.Road) ||
                            (grid.IsInBounds(gx + 1, gy) && grid.GetCell(gx + 1, gy).PlotType == PlotType.Road);

        if (isHorizontal)
        {
            DrawString(_font, screenPos + new Vector2(2 * _zoom, scaledCell / 2 + fontSize / 2f),
                street.Name, HorizontalAlignment.Left, -1, fontSize, StreetLabelColor);
        }
        else
        {
            // For vertical streets, draw with 90-degree rotation
            var transform = Transform2D.Identity;
            var textPos = screenPos + new Vector2(scaledCell / 2 - fontSize / 2f, scaledCell - 2 * _zoom);
            transform = transform.Translated(textPos);
            transform = transform.Rotated(-Mathf.Pi / 2);
            DrawSetTransformMatrix(transform);
            DrawString(_font, Vector2.Zero, street.Name, HorizontalAlignment.Left, -1, fontSize, StreetLabelColor);
            DrawSetTransformMatrix(Transform2D.Identity);
        }
    }

    private void DrawEntityDots(SimulationState state, int minGX, int maxGX, int minGY, int maxGY)
    {
        var dotRadius = EntityDotSize / 2 * _zoom;

        // Draw person dots
        foreach (var person in state.People.Values)
        {
            var screenPos = WorldToScreen(person.CurrentPosition);

            Color color;
            if (!person.IsAlive)
                color = DeadPersonColor;
            else if (person.CurrentAction == ActionType.Sleep)
                color = SleepingPersonColor;
            else
                color = PersonColor;

            DrawCircle(screenPos, dotRadius, color);
        }

        // Draw player dot on top
        if (state.Player != null)
        {
            var screenPos = WorldToScreen(state.Player.CurrentPosition);
            DrawCircle(screenPos, dotRadius, PlayerDotColor);
        }
    }

    // --- Input handling ---

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mb)
            HandleMouseButton(mb);
        else if (@event is InputEventMouseMotion mm && _isPanning)
            HandlePanMotion(mm);
    }

    private void HandleMouseButton(InputEventMouseButton mb)
    {
        if (mb.ButtonIndex == MouseButton.WheelUp && mb.Pressed)
        {
            Zoom(ZoomStep, mb.Position);
            GetViewport().SetInputAsHandled();
        }
        else if (mb.ButtonIndex == MouseButton.WheelDown && mb.Pressed)
        {
            Zoom(-ZoomStep, mb.Position);
            GetViewport().SetInputAsHandled();
        }
        else if (mb.ButtonIndex == MouseButton.Middle)
        {
            if (mb.Pressed)
            {
                _isPanning = true;
                _panStartMouse = mb.Position;
                _panStartOffset = _panOffset;
                _didDrag = false;
            }
            else
            {
                _isPanning = false;
            }
            GetViewport().SetInputAsHandled();
        }
        else if (mb.ButtonIndex == MouseButton.Left)
        {
            if (mb.Pressed)
            {
                _isPanning = true;
                _panStartMouse = mb.Position;
                _panStartOffset = _panOffset;
                _didDrag = false;
            }
            else
            {
                _isPanning = false;
                if (!_didDrag)
                {
                    HandleClick(mb.Position);
                }
            }
            GetViewport().SetInputAsHandled();
        }
        else if (mb.ButtonIndex == MouseButton.Right && mb.Pressed)
        {
            HandleRightClick(mb.Position, mb.GlobalPosition);
            GetViewport().SetInputAsHandled();
        }
    }

    private void HandlePanMotion(InputEventMouseMotion mm)
    {
        var delta = mm.Position - _panStartMouse;
        if (delta.Length() > DragThreshold)
            _didDrag = true;

        _panOffset = _panStartOffset + delta;
        GetViewport().SetInputAsHandled();
    }

    private void Zoom(float delta, Vector2 mousePos)
    {
        var oldZoom = _zoom;
        _zoom = Mathf.Clamp(_zoom + delta, MinZoom, MaxZoom);
        var zoomRatio = _zoom / oldZoom;
        _panOffset = mousePos - (mousePos - _panOffset) * zoomRatio;
    }

    // --- Coordinate conversion ---

    private Vector2 GridToScreen(int gridX, int gridY)
    {
        return _panOffset + new Vector2(gridX * CellSize, gridY * CellSize) * _zoom;
    }

    private Vector2 WorldToScreen(Vector2 worldPos)
    {
        return _panOffset + worldPos * _zoom;
    }

    private (int gridX, int gridY) ScreenToGrid(Vector2 screenPos)
    {
        var gx = (int)((screenPos.X - _panOffset.X) / (_zoom * CellSize));
        var gy = (int)((screenPos.Y - _panOffset.Y) / (_zoom * CellSize));
        return (gx, gy);
    }

    // --- Click handling ---

    private void HandleClick(Vector2 screenPos)
    {
        var (gx, gy) = ScreenToGrid(screenPos);
        var grid = _simulationManager.State.CityGrid;
        if (grid == null) return;

        if (grid.IsInBounds(gx, gy))
        {
            var cell = grid.GetCell(gx, gy);
            if (cell.AddressId.HasValue)
            {
                _selectedAddressId = cell.AddressId.Value;
                UpdateMenuItems();
                return;
            }
        }

        // Clicked on road/empty/out-of-bounds: deselect
        _selectedAddressId = null;
        UpdateMenuItems();
    }

    private void HandleRightClick(Vector2 screenPos, Vector2 globalPos)
    {
        var (gx, gy) = ScreenToGrid(screenPos);
        var grid = _simulationManager.State.CityGrid;
        if (grid == null) return;

        if (grid.IsInBounds(gx, gy))
        {
            var cell = grid.GetCell(gx, gy);
            if (cell.AddressId.HasValue)
            {
                ShowAddressContextMenu(globalPos, cell.AddressId.Value, _gameManager.EvidenceBoard);
                return;
            }
        }

        // Check if right-clicking near a person dot
        foreach (var person in _simulationManager.State.People.Values)
        {
            var dotScreen = WorldToScreen(person.CurrentPosition);
            if (screenPos.DistanceTo(dotScreen) <= HoverDistance * _zoom)
            {
                ShowPersonContextMenu(globalPos, person.Id, _gameManager.EvidenceBoard);
                return;
            }
        }
    }

    // --- Menu items ---

    private void UpdateMenuItems()
    {
        if (_gameShell == null) return;

        var items = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        var player = _simulationManager.State.Player;
        var state = _simulationManager.State;

        if (_selectedAddressId.HasValue && state.Addresses.TryGetValue(_selectedAddressId.Value, out var selectedAddr))
        {
            var street = state.Streets.GetValueOrDefault(selectedAddr.StreetId);
            var streetName = street?.Name ?? "Unknown";

            // Show selected address info as a disabled label item
            var infoItem = new Godot.Collections.Dictionary
            {
                { "label", $"{selectedAddr.Number} {streetName} ({selectedAddr.Type})" },
                { "callback", Callable.From(() => { }) }
            };
            items.Add(infoItem);

            if (player != null && player.TravelInfo == null)
            {
                if (player.CurrentAddressId != _selectedAddressId.Value)
                {
                    var goItem = new Godot.Collections.Dictionary
                    {
                        { "label", "Go here" },
                        { "callback", Callable.From(() => OnGoToAddress(_selectedAddressId.Value)) }
                    };
                    items.Add(goItem);
                }
                else
                {
                    var enterItem = new Godot.Collections.Dictionary
                    {
                        { "label", "Enter building" },
                        { "callback", Callable.From(OnEnterLocation) }
                    };
                    items.Add(enterItem);
                }
            }
        }
        else if (player?.TravelInfo == null && player?.CurrentAddressId > 0)
        {
            var enterItem = new Godot.Collections.Dictionary
            {
                { "label", "Enter Location" },
                { "callback", Callable.From(OnEnterLocation) }
            };
            items.Add(enterItem);
        }

        var ebItem = new Godot.Collections.Dictionary
        {
            { "label", "Evidence Board" },
            { "callback", Callable.From(() => _gameShell.OpenEvidenceBoard()) }
        };
        items.Add(ebItem);

        _gameShell.SetMenuItems(items);
    }

    private void OnGoToAddress(int addressId)
    {
        SimulationManager.StartPlayerTravel(_simulationManager.State, addressId, _simulationManager.MapConfig);
        UpdateMenuItems();
    }

    private void OnEnterLocation()
    {
        _gameShell.LoadContentView("res://scenes/address/AddressView.tscn");
    }

    // --- Context menus ---

    private void ShowAddressContextMenu(Vector2 pos, int addressId, EvidenceBoard board)
    {
        var menu = new PopupMenu();
        var alreadyOnBoard = board.HasItem(EvidenceEntityType.Address, addressId);

        menu.AddItem("Go here", 0);
        menu.AddItem(alreadyOnBoard ? "Already on Board" : "Add to Evidence Board", 1);
        if (alreadyOnBoard)
            menu.SetItemDisabled(1, true);

        menu.IdPressed += (id) =>
        {
            if (id == 0)
            {
                SimulationManager.StartPlayerTravel(_simulationManager.State, addressId, _simulationManager.MapConfig);
                UpdateMenuItems();
            }
            else if (id == 1 && !alreadyOnBoard)
            {
                var random = new System.Random();
                var centerX = 3840f / 2 + (float)(random.NextDouble() * 100 - 50);
                var centerY = 2160f / 2 + (float)(random.NextDouble() * 100 - 50);
                board.AddItem(EvidenceEntityType.Address, addressId, new Vector2(centerX, centerY));
            }
            menu.QueueFree();
        };
        menu.PopupHide += () => menu.QueueFree();

        AddChild(menu);
        menu.Position = new Vector2I((int)pos.X, (int)pos.Y);
        menu.Popup();
    }

    private void ShowPersonContextMenu(Vector2 pos, int personId, EvidenceBoard board)
    {
        var menu = new PopupMenu();
        var alreadyOnBoard = board.HasItem(EvidenceEntityType.Person, personId);

        menu.AddItem(alreadyOnBoard ? "Already on Board" : "Add to Evidence Board", 0);
        if (alreadyOnBoard)
            menu.SetItemDisabled(0, true);

        menu.IdPressed += (id) =>
        {
            if (id == 0 && !alreadyOnBoard)
            {
                var random = new System.Random();
                var centerX = 3840f / 2 + (float)(random.NextDouble() * 100 - 50);
                var centerY = 2160f / 2 + (float)(random.NextDouble() * 100 - 50);
                board.AddItem(EvidenceEntityType.Person, personId, new Vector2(centerX, centerY));
            }
            menu.QueueFree();
        };
        menu.PopupHide += () => menu.QueueFree();

        AddChild(menu);
        menu.Position = new Vector2I((int)pos.X, (int)pos.Y);
        menu.Popup();
    }

    // --- Hover label ---

    private void UpdateHoverLabel()
    {
        var mousePos = GetLocalMousePosition();
        var (gx, gy) = ScreenToGrid(mousePos);
        var state = _simulationManager.State;
        var grid = state.CityGrid;
        var lines = new List<string>();

        // Check player dot hover
        if (state.Player != null)
        {
            var playerScreen = WorldToScreen(state.Player.CurrentPosition);
            if (mousePos.DistanceTo(playerScreen) <= HoverDistance)
                lines.Add("You");
        }

        // Check grid cell hover for address info
        if (grid != null && grid.IsInBounds(gx, gy))
        {
            var cell = grid.GetCell(gx, gy);
            if (cell.AddressId.HasValue && state.Addresses.TryGetValue(cell.AddressId.Value, out var address))
            {
                var street = state.Streets.GetValueOrDefault(address.StreetId);
                lines.Add($"{address.Number} {street?.Name ?? "Unknown"} ({address.Type})");
                lines.AddRange(state.GetEntityNamesAtAddress(address));
            }
        }

        // Check person dot hover
        foreach (var person in state.People.Values)
        {
            var dotScreen = WorldToScreen(person.CurrentPosition);
            if (mousePos.DistanceTo(dotScreen) <= HoverDistance)
            {
                string label;
                if (!person.IsAlive)
                {
                    label = $"{person.FullName}: Dead";
                }
                else
                {
                    var actionLabel = person.CurrentAction switch
                    {
                        ActionType.Work => FormatWorkLabel(person),
                        ActionType.Sleep => "Sleep",
                        ActionType.TravelByCar => FormatTravelLabel(person),
                        ActionType.Idle => "Idle",
                        ActionType.KillPerson => "KillPerson",
                        _ => person.CurrentAction.ToString()
                    };
                    label = $"{person.FullName}: {actionLabel}";
                }
                if (!lines.Contains(label) && !lines.Contains(person.FullName))
                    lines.Add(label);
            }
        }

        if (lines.Count > 0)
        {
            _hoverLabel.Text = string.Join("\n", lines);
            _hoverLabel.GlobalPosition = GetGlobalMousePosition() + new Vector2(15, -10);
            _hoverLabel.Visible = true;
        }
        else
        {
            _hoverLabel.Visible = false;
        }
    }

    private string FormatTravelLabel(Person person)
    {
        if (person.TravelInfo == null) return "TravelByCar";
        var toAddr = _simulationManager.State.Addresses.GetValueOrDefault(person.TravelInfo.ToAddressId);
        if (toAddr == null) return "TravelByCar";
        var street = _simulationManager.State.Streets.GetValueOrDefault(toAddr.StreetId);
        return $"TravelByCar -> {toAddr.Number} {street?.Name ?? "Unknown"}";
    }

    private string FormatWorkLabel(Person person)
    {
        var job = _simulationManager.State.Jobs.GetValueOrDefault(person.JobId);
        if (job == null) return "Work";
        var workAddr = _simulationManager.State.Addresses.GetValueOrDefault(job.WorkAddressId);
        if (workAddr == null) return "Work";
        var street = _simulationManager.State.Streets.GetValueOrDefault(workAddr.StreetId);
        return $"Work at {workAddr.Number} {street?.Name ?? "Unknown"}";
    }
}
