using System.Collections.Generic;
using Godot;
using Stakeout;
using Stakeout.Evidence;
using Stakeout.Simulation;
using Stakeout.Simulation.Entities;

public partial class SimulationDebug : Control
{
    private GameManager _gameManager;
    private SimulationManager _simulationManager;
    private Label _clockLabel;
    private Label _hoverLabel;
    private Control _locationIcons;
    private Control _entityDots;

    private const float LocationIconSize = 12f;
    private const float EntityDotSize = 8f;
    private const float HoverDistance = 10f;
    private const int IconBorderWidth = 2;
    private const int DotBorderWidth = 1;

    private readonly Dictionary<int, Panel> _addressNodes = [];
    private readonly Dictionary<int, Panel> _personNodes = [];
    private Panel _playerNode;

    private static readonly Color SuburbanHomeColor = new(0.2f, 0.8f, 0.2f);
    private static readonly Color DinerColor = new(0.9f, 0.9f, 0.2f);
    private static readonly Color DiveBarColor = new(0.9f, 0.2f, 0.2f);
    private static readonly Color OfficeColor = new(0.2f, 0.7f, 0.9f);
    private static readonly Color PersonColor = new(1f, 1f, 1f);
    private static readonly Color PlayerColor = new(0.3f, 0.5f, 1f);
    private static readonly Color BorderColor = new(0f, 0f, 0f);

    public override void _Ready()
    {
        _clockLabel = GetNode<Label>("ClockLabel");
        _hoverLabel = GetNode<Label>("HoverLabel");
        _locationIcons = GetNode<Control>("CityMap/LocationIcons");
        _entityDots = GetNode<Control>("CityMap/EntityDots");

        _gameManager = GetNode<GameManager>("/root/GameManager");
        _simulationManager = _gameManager.SimulationManager;

        _simulationManager.AddressAdded += OnAddressAdded;
        _simulationManager.PersonAdded += OnPersonAdded;
        _simulationManager.PlayerCreated += OnPlayerCreated;

        // Re-render any addresses/people that were created before this scene loaded
        foreach (var address in _simulationManager.State.Addresses.Values)
        {
            OnAddressAdded(address);
        }
        foreach (var person in _simulationManager.State.People.Values)
        {
            OnPersonAdded(person);
        }
        if (_simulationManager.State.Player != null)
        {
            OnPlayerCreated();
        }

        var evidenceBoardButton = GetNode<Button>("Sidebar/VBox/EvidenceBoardButton");
        evidenceBoardButton.Pressed += OnEvidenceBoardPressed;

        // Style sidebar black
        var sidebar = GetNode<PanelContainer>("Sidebar");
        var sidebarStyle = new StyleBoxFlat { BgColor = new Color(0, 0, 0, 1) };
        sidebar.AddThemeStyleboxOverride("panel", sidebarStyle);
    }

    private void OnEvidenceBoardPressed()
    {
        GetTree().ChangeSceneToFile("res://scenes/evidence_board/EvidenceBoard.tscn");
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Right && mb.Pressed)
        {
            TryShowAddToEvidenceBoardMenu(mb.GlobalPosition);
        }
    }

    private void TryShowAddToEvidenceBoardMenu(Vector2 mousePos)
    {
        var state = _simulationManager.State;
        var board = _gameManager.EvidenceBoard;

        foreach (var (addressId, icon) in _addressNodes)
        {
            var center = icon.Position + new Vector2(LocationIconSize / 2, LocationIconSize / 2);
            if (mousePos.DistanceTo(center) <= HoverDistance)
            {
                ShowAddToEvidenceBoardMenu(mousePos, EvidenceEntityType.Address, addressId, board);
                return;
            }
        }

        foreach (var (personId, dot) in _personNodes)
        {
            var center = dot.Position + new Vector2(EntityDotSize / 2, EntityDotSize / 2);
            if (mousePos.DistanceTo(center) <= HoverDistance)
            {
                ShowAddToEvidenceBoardMenu(mousePos, EvidenceEntityType.Person, personId, board);
                return;
            }
        }
    }

    private void ShowAddToEvidenceBoardMenu(Vector2 pos, EvidenceEntityType entityType, int entityId, EvidenceBoard board)
    {
        var menu = new PopupMenu();
        var alreadyOnBoard = board.HasItem(entityType, entityId);

        menu.AddItem(alreadyOnBoard ? "Already on Board" : "Add to Evidence Board", 0);
        if (alreadyOnBoard)
        {
            menu.SetItemDisabled(0, true);
        }

        menu.IdPressed += (id) =>
        {
            if (id == 0 && !alreadyOnBoard)
            {
                var random = new System.Random();
                var centerX = 3840f / 2 + (float)(random.NextDouble() * 100 - 50);
                var centerY = 2160f / 2 + (float)(random.NextDouble() * 100 - 50);
                board.AddItem(entityType, entityId, new Vector2(centerX, centerY));
            }
            menu.QueueFree();
        };
        menu.PopupHide += () => menu.QueueFree();

        AddChild(menu);
        menu.Position = new Vector2I((int)pos.X, (int)pos.Y);
        menu.Popup();
    }

    public override void _Process(double delta)
    {
        var time = _simulationManager.State.Clock.CurrentTime;
        _clockLabel.Text = time.ToString("HH:mm:ss");

        UpdateHoverLabel();
    }

    private void OnAddressAdded(Address address)
    {
        var size = new Vector2(LocationIconSize, LocationIconSize);
        var icon = CreateIconPanel(size, GetAddressColor(address.Type), BorderColor, IconBorderWidth);
        icon.Position = address.Position - size / 2;
        _locationIcons.AddChild(icon);
        _addressNodes[address.Id] = icon;
    }

    private void OnPersonAdded(Person person)
    {
        var address = _simulationManager.State.Addresses[person.CurrentAddressId];
        var size = new Vector2(EntityDotSize, EntityDotSize);
        var dot = CreateIconPanel(size, PersonColor, BorderColor, DotBorderWidth);
        dot.Position = address.Position - size / 2;
        _entityDots.AddChild(dot);
        _personNodes[person.Id] = dot;
    }

    private void OnPlayerCreated()
    {
        var player = _simulationManager.State.Player;
        var address = _simulationManager.State.Addresses[player.CurrentAddressId];
        var size = new Vector2(EntityDotSize, EntityDotSize);
        _playerNode = CreateIconPanel(size, PlayerColor, BorderColor, DotBorderWidth);
        _playerNode.Position = address.Position - size / 2;
        _entityDots.AddChild(_playerNode);
    }

    private void UpdateHoverLabel()
    {
        var mousePos = GetGlobalMousePosition();
        var lines = new List<string>();

        if (_playerNode != null)
        {
            var center = _playerNode.Position + new Vector2(EntityDotSize / 2, EntityDotSize / 2);
            if (mousePos.DistanceTo(center) <= HoverDistance)
                lines.Add("You");
        }

        foreach (var (addressId, icon) in _addressNodes)
        {
            var center = icon.Position + new Vector2(LocationIconSize / 2, LocationIconSize / 2);
            if (mousePos.DistanceTo(center) <= HoverDistance)
            {
                var address = _simulationManager.State.Addresses[addressId];
                var street = _simulationManager.State.Streets[address.StreetId];
                lines.Add($"{address.Number} {street.Name} ({address.Type})");

                // Use testable query for people at this address
                lines.AddRange(_simulationManager.State.GetEntityNamesAtAddress(address));
            }
        }

        // Check people at positions not co-located with an address icon
        foreach (var (personId, dot) in _personNodes)
        {
            var center = dot.Position + new Vector2(EntityDotSize / 2, EntityDotSize / 2);
            if (mousePos.DistanceTo(center) <= HoverDistance)
            {
                var person = _simulationManager.State.People[personId];
                // Only add if not already found via address hover above
                if (!lines.Contains(person.FullName))
                    lines.Add(person.FullName);
            }
        }

        if (lines.Count > 0)
        {
            _hoverLabel.Text = string.Join("\n", lines);
            _hoverLabel.Position = mousePos + new Vector2(15, -10);
            _hoverLabel.Visible = true;
        }
        else
        {
            _hoverLabel.Visible = false;
        }
    }

    private static Panel CreateIconPanel(Vector2 size, Color fillColor, Color borderColor, int borderWidth)
    {
        var style = new StyleBoxFlat
        {
            BgColor = fillColor,
            BorderColor = borderColor,
            BorderWidthLeft = borderWidth,
            BorderWidthRight = borderWidth,
            BorderWidthTop = borderWidth,
            BorderWidthBottom = borderWidth
        };
        var panel = new Panel { Size = size };
        panel.AddThemeStyleboxOverride("panel", style);
        return panel;
    }

    private static Color GetAddressColor(AddressType type)
    {
        return type switch
        {
            AddressType.SuburbanHome => SuburbanHomeColor,
            AddressType.Diner => DinerColor,
            AddressType.DiveBar => DiveBarColor,
            AddressType.Office => OfficeColor,
            _ => PersonColor
        };
    }
}
