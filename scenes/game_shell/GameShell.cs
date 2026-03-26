using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Stakeout;
using Stakeout.Evidence;
using Stakeout.Simulation;
using Stakeout.Simulation.Actions;
using Stakeout.Simulation.Crimes;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Objectives;

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
    private Button _ultraFastButton;
    private VBoxContainer _menuContainer;

    // Content area
    private Control _contentArea;
    private Control _currentContentView;

    // Debug
    private Button _debugMenuButton;
    private PanelContainer _debugSidebar;
    private VBoxContainer _debugPeopleList;

    // Crime Generator
    private Label _crimeResultLabel;
    private CrimeGenerator _crimeGenerator;

    // Person Inspectors (multiple allowed, auto-updating)
    private readonly List<(int personId, Window window, VBoxContainer content)> _inspectorWindows = new();
    private double _inspectorRefreshTimer;

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
        _ultraFastButton = GetNode<Button>("LeftSidebar/VBox/TimeControls/UltraFastButton");

        _pauseButton.Pressed += () => SetTimeScale(0f);
        _playButton.Pressed += () => SetTimeScale(1f);
        _fastButton.Pressed += () => SetTimeScale(64f);
        _superFastButton.Pressed += () => SetTimeScale(256f);
        _ultraFastButton.Pressed += () => SetTimeScale(512f);

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

        _crimeGenerator = new CrimeGenerator();
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

        // Ensure the content view fills the content area
        _currentContentView.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

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
            btn.AddThemeFontSizeOverride("font_size", 18);
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
        _fastButton.Modulate = scale == 64f ? activeColor : normalColor;
        _superFastButton.Modulate = scale == 256f ? activeColor : normalColor;
        _ultraFastButton.Modulate = scale == 512f ? activeColor : normalColor;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("ui_cancel"))
        {
            // Save current time scale so it can be restored on Resume
            _gameManager.PreviousTimeScale = _simulationManager.State.Clock.TimeScale;
            // Pause the simulation
            _simulationManager.State.Clock.TimeScale = 0f;
            // Open the main menu as a pause menu
            GetTree().ChangeSceneToFile("res://scenes/main_menu/MainMenu.tscn");
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _Process(double delta)
    {
        var time = _simulationManager.State.Clock.CurrentTime;
        _clockLabel.Text = time.ToString("ddd MMM dd, yyyy HH:mm:ss");

        RefreshInspectorWindows(delta);
    }

    private void OnGenerateCrimePressed()
    {
        var crime = _crimeGenerator.Generate(CrimeTemplateType.SerialKiller, _simulationManager.State);
        if (crime == null)
        {
            _crimeResultLabel.Text = "Failed: not enough people";
            return;
        }

        var killerId = crime.Roles["Killer"].Value;
        var killer = _simulationManager.State.People[killerId];

        // Rebuild killer's schedule to include crime tasks
        _simulationManager.RebuildSchedule(killer);

        // Resolve objectives to pick victim
        ObjectiveResolver.ResolveTasks(killer.Objectives, _simulationManager.State);
        var victimId = crime.Roles["Victim"];
        var victimName = victimId.HasValue
            ? _simulationManager.State.People[victimId.Value].FullName
            : "unknown";

        _crimeResultLabel.Text = $"{killer.FullName} → murder → {victimName}";
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

        var font = _clockLabel.GetThemeFont("font");

        // Crime Generator section
        var crimeHeader = new Label { Text = "— Crime Generator —" };
        crimeHeader.AddThemeFontOverride("font", font);
        crimeHeader.AddThemeFontSizeOverride("font_size", 14);
        crimeHeader.AddThemeColorOverride("font_color", new Color(0.3f, 0.6f, 1.0f));
        crimeHeader.HorizontalAlignment = HorizontalAlignment.Center;
        _debugPeopleList.AddChild(crimeHeader);

        var templateLabel = new Label { Text = "Template: Serial Killer" };
        templateLabel.AddThemeFontOverride("font", font);
        templateLabel.AddThemeFontSizeOverride("font_size", 12);
        _debugPeopleList.AddChild(templateLabel);

        var generateBtn = new Button { Text = "Generate Now" };
        generateBtn.AddThemeFontOverride("font", font);
        generateBtn.AddThemeFontSizeOverride("font_size", 12);
        generateBtn.Pressed += OnGenerateCrimePressed;
        _debugPeopleList.AddChild(generateBtn);

        _crimeResultLabel = new Label { Text = _crimeResultLabel?.Text ?? "No crime active", AutowrapMode = TextServer.AutowrapMode.Word };
        _crimeResultLabel.AddThemeFontOverride("font", font);
        _crimeResultLabel.AddThemeFontSizeOverride("font_size", 12);
        _debugPeopleList.AddChild(_crimeResultLabel);

        // People section
        var header = new Label { Text = "— People —" };
        header.AddThemeFontOverride("font", font);
        header.AddThemeFontSizeOverride("font_size", 14);
        header.AddThemeColorOverride("font_color", new Color(0.3f, 0.6f, 1.0f));
        header.HorizontalAlignment = HorizontalAlignment.Center;
        _debugPeopleList.AddChild(header);

        var people = _simulationManager.State.People.Values
            .OrderBy(p => p.FullName)
            .ToList();

        foreach (var person in people)
        {
            var btn = new Button { Text = person.FullName };
            btn.AddThemeFontOverride("font", font);
            btn.AddThemeFontSizeOverride("font_size", 12);
            btn.Alignment = HorizontalAlignment.Left;

            var personId = person.Id;
            btn.Pressed += () => ShowPersonInspector(personId);
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

    private void ShowPersonInspector(int personId)
    {
        // If already open for this person, bring it to front
        var existing = _inspectorWindows.FindIndex(w => w.personId == personId);
        if (existing >= 0 && IsInstanceValid(_inspectorWindows[existing].window))
        {
            _inspectorWindows[existing].window.GrabFocus();
            return;
        }

        var person = _simulationManager.State.People[personId];

        // Offset each new window so they don't stack exactly
        var offset = _inspectorWindows.Count(w => IsInstanceValid(w.window)) * 30;
        var screenHeight = (int)GetViewportRect().Size.Y;
        var windowHeight = (int)(screenHeight * 0.75f);

        var window = new Window
        {
            Title = $"Inspector: {person.FullName}",
            Size = new Vector2I(400, windowHeight),
            Position = new Vector2I(200 + offset, 50 + offset),
            Exclusive = false
        };

        var scroll = new ScrollContainer();
        scroll.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        var vbox = new VBoxContainer();
        vbox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

        PopulateInspectorContent(vbox, personId);

        scroll.AddChild(vbox);
        window.AddChild(scroll);
        window.CloseRequested += () =>
        {
            _inspectorWindows.RemoveAll(w => w.window == window);
            window.QueueFree();
        };
        AddChild(window);
        _inspectorWindows.Add((personId, window, vbox));
        window.Show();
    }

    private const double InspectorRefreshInterval = 0.5;

    private void RefreshInspectorWindows(double delta)
    {
        // Clean up freed windows
        _inspectorWindows.RemoveAll(w => !IsInstanceValid(w.window));

        if (_inspectorWindows.Count == 0) return;

        _inspectorRefreshTimer += delta;
        if (_inspectorRefreshTimer < InspectorRefreshInterval) return;
        _inspectorRefreshTimer = 0;

        foreach (var (personId, _, vbox) in _inspectorWindows)
        {
            foreach (var child in vbox.GetChildren())
                child.QueueFree();
            PopulateInspectorContent(vbox, personId);
        }
    }

    private void PopulateInspectorContent(VBoxContainer vbox, int personId)
    {
        var person = _simulationManager.State.People[personId];
        var state = _simulationManager.State;
        var font = _clockLabel.GetThemeFont("font");

        // Identity
        AddInspectorSection(vbox, font, "— Identity —", new[]
        {
            $"Name: {person.FullName}",
            $"ID: {person.Id}",
            $"Alive: {person.IsAlive}"
        });

        // Location
        var locationLines = new List<string>();
        if (person.TravelInfo != null)
        {
            var toAddr = state.Addresses.GetValueOrDefault(person.TravelInfo.ToAddressId);
            var street = toAddr != null ? state.Streets.GetValueOrDefault(toAddr.StreetId) : null;
            locationLines.Add($"In transit to: {toAddr?.Number} {street?.Name ?? "Unknown"}");
        }
        else if (person.CurrentAddressId.HasValue)
        {
            var addr = state.Addresses.GetValueOrDefault(person.CurrentAddressId.Value);
            var street = addr != null ? state.Streets.GetValueOrDefault(addr.StreetId) : null;
            var locationText = $"At: {addr?.Number} {street?.Name ?? "Unknown"} ({addr?.Type})";
            if (person.CurrentSublocationId.HasValue &&
                addr != null && addr.Sublocations.TryGetValue(person.CurrentSublocationId.Value, out var subloc))
            {
                locationText += $" → {subloc.Name}";
            }
            locationLines.Add(locationText);
        }
        locationLines.Add($"Position: ({person.CurrentPosition.X:F0}, {person.CurrentPosition.Y:F0})");
        AddInspectorSection(vbox, font, "— Location —", locationLines.ToArray());

        // Current State
        AddInspectorSection(vbox, font, "— Current State —", new[]
        {
            $"Action: {person.CurrentAction}"
        });

        // Job
        if (state.Jobs.TryGetValue(person.JobId, out var job))
        {
            var workAddr = state.Addresses.GetValueOrDefault(job.WorkAddressId);
            var workStreet = workAddr != null ? state.Streets.GetValueOrDefault(workAddr.StreetId) : null;
            AddInspectorSection(vbox, font, "— Job —", new[]
            {
                $"Title: {job.Title}",
                $"Work: {workAddr?.Number} {workStreet?.Name ?? "Unknown"}",
                $"Shift: {job.ShiftStart:hh\\:mm} - {job.ShiftEnd:hh\\:mm}"
            });
        }

        // Objectives
        var objLines = new List<string>();
        foreach (var obj in person.Objectives)
        {
            objLines.Add($"[{obj.Status}] {obj.Type} (pri:{obj.Priority}, src:{obj.Source})");
            for (int i = 0; i < obj.Steps.Count; i++)
            {
                var step = obj.Steps[i];
                var marker = step.Status == StepStatus.Completed ? "✓"
                    : i == obj.CurrentStepIndex ? "→"
                    : " ";
                objLines.Add($"  {marker} {step.Description} [{step.Status}]");
            }
        }
        if (objLines.Count > 0)
            AddInspectorSection(vbox, font, "— Objectives —", objLines.ToArray());

        // Schedule
        if (person.Schedule != null)
        {
            var schedLines = person.Schedule.Entries.Select(e =>
            {
                var targetStr = "";
                if (e.TargetAddressId.HasValue)
                {
                    var addr = state.Addresses.GetValueOrDefault(e.TargetAddressId.Value);
                    if (addr != null)
                    {
                        var street = state.Streets.GetValueOrDefault(addr.StreetId);
                        targetStr = $" @ {addr.Number} {street?.Name ?? "Unknown"} ({addr.Type})";
                    }
                    else
                    {
                        targetStr = $" @ addr {e.TargetAddressId.Value}";
                    }
                }
                return $"[{e.StartTime:hh\\:mm}-{e.EndTime:hh\\:mm}] {e.Action}{targetStr}";
            }).ToArray();
            AddInspectorSection(vbox, font, "— Schedule —", schedLines);
        }

        // Recent Events
        var events = state.Journal.GetEventsForPerson(person.Id);
        var recentEvents = events.TakeLast(10).Reverse().Select(e =>
            $"{e.Timestamp:HH:mm:ss} {e.EventType}"
        ).ToArray();
        if (recentEvents.Length > 0)
            AddInspectorSection(vbox, font, "— Recent Events —", recentEvents);
    }

    private static void AddInspectorSection(VBoxContainer vbox, Font font, string header, string[] lines)
    {
        var headerLabel = new Label { Text = header };
        headerLabel.AddThemeFontOverride("font", font);
        headerLabel.AddThemeFontSizeOverride("font_size", 14);
        headerLabel.AddThemeColorOverride("font_color", new Color(0.3f, 0.6f, 1.0f));
        vbox.AddChild(headerLabel);

        foreach (var line in lines)
        {
            var label = new Label { Text = line };
            label.AddThemeFontOverride("font", font);
            label.AddThemeFontSizeOverride("font_size", 12);
            label.AddThemeColorOverride("font_color", new Color(0.9f, 0.9f, 0.9f));
            vbox.AddChild(label);
        }

        // Spacer
        vbox.AddChild(new Control { CustomMinimumSize = new Vector2(0, 10) });
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
