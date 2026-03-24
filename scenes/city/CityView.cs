using System.Collections.Generic;
using System.Linq;
using Godot;
using Stakeout;
using Stakeout.Evidence;
using Stakeout.Simulation;
using Stakeout.Simulation.Entities;

public partial class CityView : Control, IContentView
{
    private GameManager _gameManager;
    private SimulationManager _simulationManager;
    private GameShell _gameShell;
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
    private bool _wasPlayerTraveling;

    private static readonly Color SuburbanHomeColor = new(0.2f, 0.8f, 0.2f);
    private static readonly Color DinerColor = new(0.9f, 0.9f, 0.2f);
    private static readonly Color DiveBarColor = new(0.9f, 0.2f, 0.2f);
    private static readonly Color OfficeColor = new(0.2f, 0.7f, 0.9f);
    private static readonly Color PersonColor = new(1f, 1f, 1f);
    private static readonly Color SleepingPersonColor = new(0.5f, 0.5f, 0.5f);
    private static readonly Color PlayerColor = new(0.3f, 0.5f, 1f);
    private static readonly Color BorderColor = new(0f, 0f, 0f);

    public void SetGameShell(GameShell shell)
    {
        _gameShell = shell;
        UpdateMenuItems();
    }

    public override void _Ready()
    {
        _hoverLabel = GetNode<Label>("HoverLabel");
        _locationIcons = GetNode<Control>("CityMap/LocationIcons");
        _entityDots = GetNode<Control>("CityMap/EntityDots");

        _gameManager = GetNode<GameManager>("/root/GameManager");
        _simulationManager = _gameManager.SimulationManager;

        _simulationManager.AddressAdded += OnAddressAdded;
        _simulationManager.PersonAdded += OnPersonAdded;
        _simulationManager.PlayerCreated += OnPlayerCreated;

        foreach (var address in _simulationManager.State.Addresses.Values)
            OnAddressAdded(address);
        foreach (var person in _simulationManager.State.People.Values)
            OnPersonAdded(person);
        if (_simulationManager.State.Player != null)
            OnPlayerCreated();
    }

    private void UpdateMenuItems()
    {
        if (_gameShell == null) return;

        var items = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        var player = _simulationManager.State.Player;

        if (player?.TravelInfo == null && player?.CurrentAddressId > 0)
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

    private void OnEnterLocation()
    {
        _gameShell.LoadContentView("res://scenes/address/AddressView.tscn");
    }

    public override void _ExitTree()
    {
        _simulationManager.AddressAdded -= OnAddressAdded;
        _simulationManager.PersonAdded -= OnPersonAdded;
        _simulationManager.PlayerCreated -= OnPlayerCreated;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Right && mb.Pressed)
        {
            TryShowContextMenu(mb.GlobalPosition);
        }
    }

    private void TryShowContextMenu(Vector2 mousePos)
    {
        var state = _simulationManager.State;
        var board = _gameManager.EvidenceBoard;

        foreach (var (addressId, icon) in _addressNodes)
        {
            var center = icon.GlobalPosition + new Vector2(LocationIconSize / 2, LocationIconSize / 2);
            if (mousePos.DistanceTo(center) <= HoverDistance)
            {
                ShowAddressContextMenu(mousePos, addressId, board);
                return;
            }
        }

        foreach (var (personId, dot) in _personNodes)
        {
            var center = dot.GlobalPosition + new Vector2(EntityDotSize / 2, EntityDotSize / 2);
            if (mousePos.DistanceTo(center) <= HoverDistance)
            {
                ShowPersonContextMenu(mousePos, personId, board);
                return;
            }
        }
    }

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

    public override void _Process(double delta)
    {
        var size = new Vector2(EntityDotSize, EntityDotSize);
        foreach (var (personId, dot) in _personNodes)
        {
            var person = _simulationManager.State.People[personId];
            dot.Position = person.CurrentPosition - size / 2;

            var color = person.CurrentActivity == ActivityType.Sleeping ? SleepingPersonColor : PersonColor;
            var style = new StyleBoxFlat
            {
                BgColor = color,
                BorderColor = BorderColor,
                BorderWidthLeft = DotBorderWidth,
                BorderWidthRight = DotBorderWidth,
                BorderWidthTop = DotBorderWidth,
                BorderWidthBottom = DotBorderWidth
            };
            dot.AddThemeStyleboxOverride("panel", style);
        }

        if (_playerNode != null && _simulationManager.State.Player != null)
        {
            _playerNode.Position = _simulationManager.State.Player.CurrentPosition - size / 2;
        }

        // Refresh menu only on travel state change
        var player = _simulationManager.State.Player;
        var isTraveling = player?.TravelInfo != null;
        if (_wasPlayerTraveling && !isTraveling)
        {
            UpdateMenuItems();
        }
        _wasPlayerTraveling = isTraveling;

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
        var size = new Vector2(EntityDotSize, EntityDotSize);
        var dot = CreateIconPanel(size, PersonColor, BorderColor, DotBorderWidth);
        dot.Position = person.CurrentPosition - size / 2;
        _entityDots.AddChild(dot);
        _personNodes[person.Id] = dot;
    }

    private void OnPlayerCreated()
    {
        var player = _simulationManager.State.Player;
        var size = new Vector2(EntityDotSize, EntityDotSize);
        _playerNode = CreateIconPanel(size, PlayerColor, BorderColor, DotBorderWidth);
        _playerNode.Position = player.CurrentPosition - size / 2;
        _entityDots.AddChild(_playerNode);
    }

    private void UpdateHoverLabel()
    {
        var mousePos = GetGlobalMousePosition();
        var lines = new List<string>();

        if (_playerNode != null)
        {
            var center = _playerNode.GlobalPosition + new Vector2(EntityDotSize / 2, EntityDotSize / 2);
            if (mousePos.DistanceTo(center) <= HoverDistance)
                lines.Add("You");
        }

        foreach (var (addressId, icon) in _addressNodes)
        {
            var center = icon.GlobalPosition + new Vector2(LocationIconSize / 2, LocationIconSize / 2);
            if (mousePos.DistanceTo(center) <= HoverDistance)
            {
                var address = _simulationManager.State.Addresses[addressId];
                var street = _simulationManager.State.Streets[address.StreetId];
                lines.Add($"{address.Number} {street.Name} ({address.Type})");

                lines.AddRange(_simulationManager.State.GetEntityNamesAtAddress(address));
            }
        }

        foreach (var (personId, dot) in _personNodes)
        {
            var center = dot.GlobalPosition + new Vector2(EntityDotSize / 2, EntityDotSize / 2);
            if (mousePos.DistanceTo(center) <= HoverDistance)
            {
                var person = _simulationManager.State.People[personId];
                var activityLabel = person.CurrentActivity switch
                {
                    ActivityType.Working => "Working",
                    ActivityType.Sleeping => "Sleeping",
                    ActivityType.TravellingByCar => "Travelling",
                    ActivityType.AtHome => "At Home",
                    _ => ""
                };
                var label = $"{person.FullName} — {activityLabel}";
                if (!lines.Contains(label) && !lines.Contains(person.FullName))
                    lines.Add(label);
            }
        }

        if (lines.Count > 0)
        {
            _hoverLabel.Text = string.Join("\n", lines);
            _hoverLabel.GlobalPosition = mousePos + new Vector2(15, -10);
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
        var panel = new Panel { Size = size, MouseFilter = MouseFilterEnum.Ignore };
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
