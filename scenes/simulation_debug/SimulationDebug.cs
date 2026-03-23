using System.Collections.Generic;
using System.Linq;
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

    private Button _pauseButton;
    private Button _playButton;
    private Button _fastButton;
    private Button _superFastButton;

    private const float LocationIconSize = 12f;
    private const float EntityDotSize = 8f;
    private const float HoverDistance = 10f;
    private const int IconBorderWidth = 2;
    private const int DotBorderWidth = 1;

    private readonly Dictionary<int, Panel> _addressNodes = [];
    private readonly Dictionary<int, Panel> _personNodes = [];
    private Panel _playerNode;

    private Button _debugMenuButton;
    private PanelContainer _debugSidebar;
    private VBoxContainer _debugPeopleList;

    private static readonly Color SuburbanHomeColor = new(0.2f, 0.8f, 0.2f);
    private static readonly Color DinerColor = new(0.9f, 0.9f, 0.2f);
    private static readonly Color DiveBarColor = new(0.9f, 0.2f, 0.2f);
    private static readonly Color OfficeColor = new(0.2f, 0.7f, 0.9f);
    private static readonly Color PersonColor = new(1f, 1f, 1f);
    private static readonly Color SleepingPersonColor = new(0.5f, 0.5f, 0.5f);
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

        // Time controls
        _pauseButton = GetNode<Button>("TimeControls/PauseButton");
        _playButton = GetNode<Button>("TimeControls/PlayButton");
        _fastButton = GetNode<Button>("TimeControls/FastButton");
        _superFastButton = GetNode<Button>("TimeControls/SuperFastButton");

        _pauseButton.Pressed += () => SetTimeScale(0f);
        _playButton.Pressed += () => SetTimeScale(1f);
        _fastButton.Pressed += () => SetTimeScale(32f);
        _superFastButton.Pressed += () => SetTimeScale(64f);

        HighlightActiveTimeButton();

        // Debug menu button (upper left)
        _debugMenuButton = new Button
        {
            Text = "Debug",
            Position = new Vector2(10, 10),
            Size = new Vector2(60, 30)
        };
        _debugMenuButton.AddThemeFontOverride("font", GetNode<Label>("ClockLabel").GetThemeFont("font"));
        _debugMenuButton.AddThemeFontSizeOverride("font_size", 12);
        _debugMenuButton.Pressed += OnDebugMenuPressed;
        AddChild(_debugMenuButton);

        // Debug left sidebar (hidden by default)
        _debugSidebar = new PanelContainer
        {
            Position = new Vector2(0, 0),
            Size = new Vector2(200, GetViewportRect().Size.Y),
            Visible = false
        };
        var debugStyle = new StyleBoxFlat { BgColor = new Color(0.1f, 0.1f, 0.1f, 0.95f) };
        _debugSidebar.AddThemeStyleboxOverride("panel", debugStyle);

        var debugScroll = new ScrollContainer
        {
            Position = Vector2.Zero,
            Size = new Vector2(200, GetViewportRect().Size.Y)
        };
        _debugSidebar.AddChild(debugScroll);

        _debugPeopleList = new VBoxContainer();
        _debugPeopleList.SizeFlagsHorizontal = SizeFlags.Fill | SizeFlags.Expand;
        debugScroll.AddChild(_debugPeopleList);

        AddChild(_debugSidebar);
    }

    private void SetTimeScale(float scale)
    {
        _simulationManager.State.Clock.TimeScale = scale;
        HighlightActiveTimeButton();
    }

    private void HighlightActiveTimeButton()
    {
        var scale = _simulationManager.State.Clock.TimeScale;
        var activeColor = new Color(0.3f, 0.6f, 1.0f);
        var normalColor = new Color(1f, 1f, 1f);

        _pauseButton.Modulate = scale == 0f ? activeColor : normalColor;
        _playButton.Modulate = scale == 1f ? activeColor : normalColor;
        _fastButton.Modulate = scale == 32f ? activeColor : normalColor;
        _superFastButton.Modulate = scale == 64f ? activeColor : normalColor;
    }

    private void OnDebugMenuPressed()
    {
        _debugSidebar.Visible = !_debugSidebar.Visible;
        if (_debugSidebar.Visible)
            PopulateDebugPeopleList();
    }

    private void PopulateDebugPeopleList()
    {
        foreach (var child in _debugPeopleList.GetChildren())
            child.QueueFree();

        var header = new Label { Text = "— People —" };
        header.AddThemeFontOverride("font", GetNode<Label>("ClockLabel").GetThemeFont("font"));
        header.AddThemeFontSizeOverride("font_size", 14);
        header.AddThemeColorOverride("font_color", new Color(0.3f, 0.6f, 1.0f));
        header.HorizontalAlignment = HorizontalAlignment.Center;
        _debugPeopleList.AddChild(header);

        var people = _simulationManager.State.People.Values
            .OrderBy(p => p.FullName)
            .ToList();

        var board = _gameManager.EvidenceBoard;
        var font = GetNode<Label>("ClockLabel").GetThemeFont("font");

        foreach (var person in people)
        {
            var btn = new Button { Text = person.FullName };
            btn.AddThemeFontOverride("font", font);
            btn.AddThemeFontSizeOverride("font_size", 12);
            btn.Alignment = HorizontalAlignment.Left;

            var personId = person.Id;
            btn.GuiInput += (@event) =>
            {
                if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Right && mb.Pressed)
                {
                    ShowAddToEvidenceBoardMenu(mb.GlobalPosition, EvidenceEntityType.Person, personId, board);
                    btn.AcceptEvent();
                }
            };

            _debugPeopleList.AddChild(btn);
        }
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
        _clockLabel.Text = time.ToString("ddd MMM dd, yyyy HH:mm:ss");

        // Update person dot positions and colors
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

                lines.AddRange(_simulationManager.State.GetEntityNamesAtAddress(address));
            }
        }

        foreach (var (personId, dot) in _personNodes)
        {
            var center = dot.Position + new Vector2(EntityDotSize / 2, EntityDotSize / 2);
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
