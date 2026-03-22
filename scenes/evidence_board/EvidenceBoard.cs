using System.Collections.Generic;
using Godot;
using Stakeout;
using Stakeout.Evidence;
using Stakeout.Simulation.Entities;
using EvidenceBoardData = Stakeout.Evidence.EvidenceBoard;

public partial class EvidenceBoard : Control
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
    private GameManager _gameManager;
    private EvidenceBoardData _boardData;
    private readonly Dictionary<int, EvidencePolaroid> _polaroidNodes = new();

    public override void _Ready()
    {
        _corkboardCanvas = GetNode<Control>("CorkboardCanvas");
        _polaroidContainer = GetNode<Control>("CorkboardCanvas/PolaroidContainer");

        _gameManager = GetNode<GameManager>("/root/GameManager");
        _boardData = (EvidenceBoardData)(object)_gameManager.EvidenceBoard;
        _polaroidScene = GD.Load<PackedScene>("res://scenes/evidence_board/EvidencePolaroid.tscn");
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

        // Zoom towards mouse position
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

        var (initials, caption) = ResolveEntityDisplay(item);
        polaroid.SetContent(initials, caption);

        polaroid.PolaroidClicked += OnPolaroidClicked;
        polaroid.PolaroidRemoved += OnPolaroidRemoved;
        polaroid.ThumbtackDragStarted += OnThumbtackDragStarted;
        polaroid.ThumbtackRightClicked += OnThumbtackRightClicked;

        _polaroidContainer.AddChild(polaroid);
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
                Stakeout.Simulation.Entities.AddressType.SuburbanHome => "H",
                Stakeout.Simulation.Entities.AddressType.Diner => "D",
                Stakeout.Simulation.Entities.AddressType.DiveBar => "B",
                Stakeout.Simulation.Entities.AddressType.Office => "O",
                _ => "?"
            };
            var caption = $"{address.Number} {street.Name}";
            return (icon, caption);
        }

        return ("?", "Unknown");
    }

    private void OnPolaroidClicked(int itemId) { /* Task 10: dossier */ }

    private void OnPolaroidRemoved(int itemId)
    {
        _boardData.RemoveItem(itemId);
        if (_polaroidNodes.TryGetValue(itemId, out var node))
        {
            node.QueueFree();
            _polaroidNodes.Remove(itemId);
        }
    }

    private void OnThumbtackDragStarted(int itemId, Vector2 globalPos) { /* Task 9: strings */ }

    private void OnThumbtackRightClicked(int itemId, Vector2 globalPos) { /* Task 9: strings */ }

    private void OnClosePressed()
    {
        GetTree().ChangeSceneToFile("res://scenes/simulation_debug/SimulationDebug.tscn");
    }
}
