using System.Collections.Generic;
using Godot;
using Stakeout;
using Stakeout.Evidence;
using Stakeout.Simulation.Entities;

public partial class EvidenceBoardScene : Control
{
    private Control _corkboardCanvas;
    private Control _polaroidContainer;
    private Button _closeButton;

    private Vector2 _panOffset = Vector2.Zero;
    private float _zoom = 1.0f;
    private const float MinZoom = 0.25f;
    private const float MaxZoom = 2.0f;
    private const float ZoomStep = 0.1f;

    private bool _isPanning;
    private Vector2 _panStartMouse;
    private Vector2 _panStartOffset;

    private PackedScene _polaroidScene;
    private PackedScene _dossierScene;
    private GameManager _gameManager;
    private EvidenceBoard _boardData;
    private readonly Dictionary<int, EvidencePolaroid> _polaroidNodes = new();

    private StringLayer _stringLayer;
    private bool _isCreatingString;
    private int _stringSourceItemId;

    private DossierWindow _currentDossier;

    public override void _Ready()
    {
        _corkboardCanvas = GetNode<Control>("CorkboardCanvas");
        _polaroidContainer = GetNode<Control>("CorkboardCanvas/PolaroidContainer");

        _gameManager = GetNode<GameManager>("/root/GameManager");
        _boardData = _gameManager.EvidenceBoard;
        _polaroidScene = GD.Load<PackedScene>("res://scenes/evidence_board/EvidencePolaroid.tscn");
        _dossierScene = GD.Load<PackedScene>("res://scenes/evidence_board/DossierWindow.tscn");

        _stringLayer = GetNode<StringLayer>("CorkboardCanvas/StringLayer");
        _stringLayer.Board = _boardData;
        _stringLayer.PolaroidNodes = _polaroidNodes;

        PopulateBoard();

        _closeButton = GetNode<Button>("CloseButton");
        _closeButton.Pressed += OnClosePressed;

        // Center the view on the canvas initially
        var viewportSize = GetViewportRect().Size;
        var canvasSize = _corkboardCanvas.GetNode<Control>("CorkboardBackground").Size;
        _panOffset = (viewportSize - canvasSize) / 2;
        ApplyTransform();
    }

    public override void _Process(double delta)
    {
        // Sync polaroid positions back to data model
        foreach (var (itemId, polaroid) in _polaroidNodes)
        {
            if (_boardData.Items.TryGetValue(itemId, out var item))
            {
                item.BoardPosition = polaroid.Position;
            }
        }
    }

    public override void _Input(InputEvent @event)
    {
        if (!_isCreatingString) return;

        if (@event is InputEventMouseMotion motionEvent)
        {
            _stringLayer.DrawingEndPoint = motionEvent.GlobalPosition;
            var hoverId = FindThumbTackAt(motionEvent.GlobalPosition);
            _stringLayer.HoveredThumbTackItemId = hoverId;
            foreach (var (id, polaroid) in _polaroidNodes)
            {
                polaroid.SetThumbTackGlow(id == hoverId && id != _stringSourceItemId);
            }
            GetViewport().SetInputAsHandled();
        }
        else if (@event is InputEventMouseButton mb && !mb.Pressed && mb.ButtonIndex == MouseButton.Left)
        {
            var targetId = FindThumbTackAt(mb.GlobalPosition);
            if (targetId >= 0 && targetId != _stringSourceItemId)
            {
                _boardData.AddConnection(_stringSourceItemId, targetId);
            }
            _isCreatingString = false;
            _stringLayer.IsDrawingString = false;
            _stringLayer.HoveredThumbTackItemId = -1;
            foreach (var (_, p) in _polaroidNodes)
                p.SetThumbTackGlow(false);
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseButton)
        {
            HandleMouseButton(mouseButton);
        }
        else if (@event is InputEventMouseMotion mouseMotion && _isPanning)
        {
            HandlePanMotion(mouseMotion);
        }
    }

    private void HandleMouseButton(InputEventMouseButton mouseButton)
    {
        // Zoom with scroll wheel
        if (mouseButton.ButtonIndex == MouseButton.WheelUp && mouseButton.Pressed)
        {
            Zoom(ZoomStep, mouseButton.Position);
        }
        else if (mouseButton.ButtonIndex == MouseButton.WheelDown && mouseButton.Pressed)
        {
            Zoom(-ZoomStep, mouseButton.Position);
        }
        // Pan with middle mouse button
        else if (mouseButton.ButtonIndex == MouseButton.Middle)
        {
            if (mouseButton.Pressed)
            {
                _isPanning = true;
                _panStartMouse = mouseButton.Position;
                _panStartOffset = _panOffset;
            }
            else
            {
                _isPanning = false;
            }
        }
        // Right-click: check for string hit-test
        else if (mouseButton.ButtonIndex == MouseButton.Right && mouseButton.Pressed)
        {
            var (fromId, toId) = _stringLayer.HitTestString(mouseButton.GlobalPosition);
            if (fromId >= 0)
            {
                var menu = new PopupMenu();
                menu.AddItem("Remove string", 0);
                menu.IdPressed += (id) =>
                {
                    if (id == 0) _boardData.RemoveConnection(fromId, toId);
                    menu.QueueFree();
                };
                menu.PopupHide += () => menu.QueueFree();
                AddChild(menu);
                menu.Position = new Vector2I((int)mouseButton.GlobalPosition.X, (int)mouseButton.GlobalPosition.Y);
                menu.Popup();
            }
        }
        // Left-click on empty space pans
        else if (mouseButton.ButtonIndex == MouseButton.Left)
        {
            if (mouseButton.Pressed)
            {
                _isPanning = true;
                _panStartMouse = mouseButton.Position;
                _panStartOffset = _panOffset;
            }
            else
            {
                _isPanning = false;
            }
        }
    }

    private void HandlePanMotion(InputEventMouseMotion mouseMotion)
    {
        _panOffset = _panStartOffset + (mouseMotion.Position - _panStartMouse);
        ApplyTransform();
    }

    private void Zoom(float delta, Vector2 mousePos)
    {
        var oldZoom = _zoom;
        _zoom = Mathf.Clamp(_zoom + delta, MinZoom, MaxZoom);

        var zoomRatio = _zoom / oldZoom;
        _panOffset = mousePos - (mousePos - _panOffset) * zoomRatio;
        ApplyTransform();
    }

    private void ApplyTransform()
    {
        _corkboardCanvas.Position = _panOffset;
        _corkboardCanvas.Scale = new Vector2(_zoom, _zoom);
    }

    private void PopulateBoard()
    {
        foreach (var item in _boardData.Items.Values)
        {
            CreatePolaroidNode(item);
        }
    }

    private void CreatePolaroidNode(EvidenceItem item)
    {
        var polaroid = _polaroidScene.Instantiate<EvidencePolaroid>();
        polaroid.ItemId = item.Id;
        polaroid.Position = item.BoardPosition;

        polaroid.PolaroidClicked += OnPolaroidClicked;
        polaroid.PolaroidRemoved += OnPolaroidRemoved;
        polaroid.ThumbtackDragStarted += OnThumbtackDragStarted;
        polaroid.ThumbtackRightClicked += OnThumbtackRightClicked;

        _polaroidContainer.AddChild(polaroid);

        var (initials, caption) = ResolveEntityDisplay(item);
        polaroid.SetContent(initials, caption);
        _polaroidNodes[item.Id] = polaroid;
    }

    private (string initials, string caption) ResolveEntityDisplay(EvidenceItem item)
    {
        var state = _gameManager.SimulationManager.State;

        if (item.EntityType == EvidenceEntityType.Person && state.People.TryGetValue(item.EntityId, out var person))
        {
            var initials = $"{person.FirstName[0]}{person.LastName[0]}";
            return (initials, person.FullName);
        }
        else if (item.EntityType == EvidenceEntityType.Address && state.Addresses.TryGetValue(item.EntityId, out var address))
        {
            var street = state.Streets[address.StreetId];
            var icon = address.Type switch
            {
                AddressType.SuburbanHome => "H",
                AddressType.Diner => "D",
                AddressType.DiveBar => "B",
                AddressType.Office => "O",
                _ => "?"
            };
            var caption = $"{address.Number} {street.Name}";
            return (icon, caption);
        }

        return ("?", "Unknown");
    }

    private void OnPolaroidClicked(int itemId)
    {
        _currentDossier?.QueueFree();

        if (!_boardData.Items.TryGetValue(itemId, out var item)) return;

        _currentDossier = _dossierScene.Instantiate<DossierWindow>();
        _currentDossier.Position = GetViewportRect().Size / 2 - new Vector2(125, 100);
        _currentDossier.TreeExiting += () => _currentDossier = null;
        AddChild(_currentDossier);
        _currentDossier.Populate(item, _gameManager.SimulationManager.State);
    }

    private void OnPolaroidRemoved(int itemId)
    {
        _boardData.RemoveItem(itemId);
        if (_polaroidNodes.TryGetValue(itemId, out var node))
        {
            node.QueueFree();
            _polaroidNodes.Remove(itemId);
        }
    }

    private void OnThumbtackDragStarted(int itemId, Vector2 globalPos)
    {
        _isCreatingString = true;
        _stringSourceItemId = itemId;
        _stringLayer.IsDrawingString = true;
        _stringLayer.DrawingFromItemId = itemId;
        _stringLayer.DrawingEndPoint = globalPos;
    }

    private void OnThumbtackRightClicked(int itemId, Vector2 globalPos)
    {
        var menu = new PopupMenu();
        menu.AddItem("Remove all strings", 0);
        menu.IdPressed += (id) =>
        {
            if (id == 0) _boardData.RemoveAllConnections(itemId);
            menu.QueueFree();
        };
        menu.PopupHide += () => menu.QueueFree();
        AddChild(menu);
        menu.Position = new Vector2I((int)globalPos.X, (int)globalPos.Y);
        menu.Popup();
    }

    private int FindThumbTackAt(Vector2 globalPos)
    {
        foreach (var (itemId, polaroid) in _polaroidNodes)
        {
            var thumbCenter = polaroid.GetThumbTackGlobalCenter();
            if (globalPos.DistanceTo(thumbCenter) <= 15f)
            {
                return itemId;
            }
        }
        return -1;
    }

    private void OnClosePressed()
    {
        GetTree().ChangeSceneToFile("res://scenes/city/CityView.tscn");
    }
}
