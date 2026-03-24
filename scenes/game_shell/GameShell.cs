using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Stakeout;
using Stakeout.Evidence;
using Stakeout.Simulation;
using Stakeout.Simulation.Entities;

/// <summary>
/// Interface implemented by content views that live inside GameShell's content area.
/// </summary>
public interface IContentView
{
    void SetGameShell(GameShell shell);
}

public partial class GameShell : Control
{
    private GameManager _gameManager;
    private SimulationManager _simulationManager;

    // Sidebar elements
    private Label _clockLabel;
    private HBoxContainer _timeControls;
    private Button _pauseButton;
    private Button _playButton;
    private Button _fastButton;
    private Button _superFastButton;
    private VBoxContainer _menuContainer;

    // Content area
    private Control _contentArea;
    private Control _currentContentView;

    // Debug
    private Button _debugMenuButton;
    private PanelContainer _debugSidebar;
    private VBoxContainer _debugPeopleList;

    public override void _Ready()
    {
        _gameManager = GetNode<GameManager>("/root/GameManager");
        _simulationManager = _gameManager.SimulationManager;

        SetupSidebar();
        SetupContentArea();
        SetupDebugPanel();

        // Load the active content view
        LoadContentView(_gameManager.ActiveContentView);
    }

    private void SetupSidebar()
    {
        var sidebar = GetNode<PanelContainer>("LeftSidebar");
        var sidebarStyle = new StyleBoxFlat { BgColor = new Color(0, 0, 0, 1) };
        sidebar.AddThemeStyleboxOverride("panel", sidebarStyle);

        _clockLabel = GetNode<Label>("LeftSidebar/VBox/ClockLabel");
        _timeControls = GetNode<HBoxContainer>("LeftSidebar/VBox/TimeControls");

        _pauseButton = GetNode<Button>("LeftSidebar/VBox/TimeControls/PauseButton");
        _playButton = GetNode<Button>("LeftSidebar/VBox/TimeControls/PlayButton");
        _fastButton = GetNode<Button>("LeftSidebar/VBox/TimeControls/FastButton");
        _superFastButton = GetNode<Button>("LeftSidebar/VBox/TimeControls/SuperFastButton");

        _pauseButton.Pressed += () => SetTimeScale(0f);
        _playButton.Pressed += () => SetTimeScale(1f);
        _fastButton.Pressed += () => SetTimeScale(32f);
        _superFastButton.Pressed += () => SetTimeScale(64f);

        HighlightActiveTimeButton();

        _menuContainer = GetNode<VBoxContainer>("LeftSidebar/VBox/MenuContainer");
    }

    private void SetupContentArea()
    {
        _contentArea = GetNode<Control>("ContentArea");
    }

    private void SetupDebugPanel()
    {
        _debugMenuButton = GetNode<Button>("DebugMenuButton");
        _debugMenuButton.Pressed += OnDebugMenuPressed;

        _debugSidebar = GetNode<PanelContainer>("DebugSidebar");
        var debugStyle = new StyleBoxFlat { BgColor = new Color(0.1f, 0.1f, 0.1f, 0.95f) };
        _debugSidebar.AddThemeStyleboxOverride("panel", debugStyle);
        _debugSidebar.Visible = false;

        var scroll = _debugSidebar.GetNode<ScrollContainer>("ScrollContainer");
        _debugPeopleList = scroll.GetNode<VBoxContainer>("PeopleList");
    }

    public void LoadContentView(string scenePath)
    {
        if (_currentContentView != null)
        {
            _currentContentView.QueueFree();
            _currentContentView = null;
        }

        var scene = GD.Load<PackedScene>(scenePath);
        _currentContentView = scene.Instantiate<Control>();
        _contentArea.AddChild(_currentContentView);

        _gameManager.ActiveContentView = scenePath;

        if (_currentContentView is IContentView contentView)
        {
            contentView.SetGameShell(this);
        }
    }

    public void SetMenuItems(Godot.Collections.Array<Godot.Collections.Dictionary> items)
    {
        foreach (var child in _menuContainer.GetChildren())
            child.QueueFree();

        var font = _clockLabel.GetThemeFont("font");

        foreach (var item in items)
        {
            var label = (string)item["label"];
            var btn = new Button
            {
                Text = label,
                Flat = true,
                Alignment = HorizontalAlignment.Left
            };
            btn.AddThemeFontOverride("font", font);
            btn.AddThemeFontSizeOverride("font_size", 16);
            btn.AddThemeColorOverride("font_color", new Color(1, 1, 1));
            btn.AddThemeColorOverride("font_hover_color", new Color(0.3f, 0.6f, 1.0f));

            if (item.ContainsKey("callback"))
            {
                var callback = (Callable)item["callback"];
                btn.Pressed += () => callback.Call();
            }

            if (item.ContainsKey("personId"))
            {
                var personId = (int)item["personId"];
                btn.GuiInput += (@event) =>
                {
                    if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Right && mb.Pressed)
                    {
                        ShowAddToEvidenceBoardMenu(mb.GlobalPosition, EvidenceEntityType.Person, personId);
                        btn.AcceptEvent();
                    }
                };
            }

            _menuContainer.AddChild(btn);
        }
    }

    public void OpenEvidenceBoard()
    {
        GetTree().ChangeSceneToFile("res://scenes/evidence_board/EvidenceBoard.tscn");
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

    public override void _Process(double delta)
    {
        var time = _simulationManager.State.Clock.CurrentTime;
        _clockLabel.Text = time.ToString("ddd MMM dd, yyyy HH:mm:ss");
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
        header.AddThemeFontOverride("font", _clockLabel.GetThemeFont("font"));
        header.AddThemeFontSizeOverride("font_size", 14);
        header.AddThemeColorOverride("font_color", new Color(0.3f, 0.6f, 1.0f));
        header.HorizontalAlignment = HorizontalAlignment.Center;
        _debugPeopleList.AddChild(header);

        var people = _simulationManager.State.People.Values
            .OrderBy(p => p.FullName)
            .ToList();

        var font = _clockLabel.GetThemeFont("font");

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
                    ShowAddToEvidenceBoardMenu(mb.GlobalPosition, EvidenceEntityType.Person, personId);
                    btn.AcceptEvent();
                }
            };

            _debugPeopleList.AddChild(btn);
        }
    }

    private void ShowAddToEvidenceBoardMenu(Vector2 pos, EvidenceEntityType entityType, int entityId)
    {
        var board = _gameManager.EvidenceBoard;
        var menu = new PopupMenu();
        var alreadyOnBoard = board.HasItem(entityType, entityId);

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
                board.AddItem(entityType, entityId, new Vector2(centerX, centerY));
            }
            menu.QueueFree();
        };
        menu.PopupHide += () => menu.QueueFree();

        AddChild(menu);
        menu.Position = new Vector2I((int)pos.X, (int)pos.Y);
        menu.Popup();
    }
}
