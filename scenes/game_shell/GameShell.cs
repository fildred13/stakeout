using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Stakeout;
using Stakeout.Investigation;
using Stakeout.UI;

/// <summary>
/// The main play screen: clock and time controls on top, what is here on the left (people, rooms, things),
/// the map / notebook / people / case file on the right, and the message log at the bottom. Time is paused
/// unless the player lets it run, travels, or sits on a stakeout; every action spends its own game time.
/// </summary>
public partial class GameShell : Control
{
    public GameManager Game { get; private set; }
    public GameSession Session => Game.Session;
    public Detective D => Game.Session.Detective;

    private Label _clock;
    private Label _status;
    private readonly Dictionary<double, Button> _speedButtons = new();
    private VBoxContainer _here;
    private Control _content;
    private readonly Dictionary<string, Button> _tabButtons = new();
    private string _tab = "Map";
    private RichTextLabel _log;
    private Control _overlay;
    private Control _modal;

    private CityMap _map;
    private NotebookView _notebook;
    private PeopleView _people;
    private CaseFileView _caseFile;

    /// <summary>Game seconds per real second. Zero is paused.</summary>
    public double Speed { get; private set; }
    private double _travelSpeed;
    private string _hereKey = "";
    private double _hereTimer;
    private int _seenClues;

    public override void _Ready()
    {
        Game = GetNode<GameManager>("/root/GameManager");
        if (Game.Session == null)
        {
            GetTree().CallDeferred(SceneTree.MethodName.ChangeSceneToFile, "res://scenes/main_menu/MainMenu.tscn");
            return;
        }
        Theme = Ui.Theme;
        BuildLayout();
        D.Message += OnMessage;
        foreach (var line in D.Log.TakeLast(30)) AppendLog(line, Ui.Dim);
        if (D.Log.Count == 0)
        {
            AppendLog($"{Session.World.Now:dddd, MMMM d, yyyy}. Your office at {D.State.HomeLine(D.Me)}. The radio's on the police band.", Ui.Text);
            AppendLog("Time only moves when you act or press play (space). Wait for the scanner, or press Help.", Ui.Dim);
        }
        _seenClues = D.Clues.Count;
        ShowTab(_tab);
        RebuildHere();
        Game.Shell = this;
    }

    public override void _ExitTree()
    {
        if (Game?.Session != null) D.Message -= OnMessage;
        if (Game != null && Game.Shell == this) Game.Shell = null;
    }

    // ---- layout ----

    private void BuildLayout()
    {
        var bg = new ColorRect { Color = Ui.Bg };
        bg.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(bg);

        var root = Ui.VBox(0);
        root.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(root);

        // Top bar
        var top = Ui.PanelBox(new Color(0.04f, 0.035f, 0.03f), Ui.Line, 6);
        var bar = Ui.HBox(8);
        top.AddChild(bar);
        _clock = Ui.Label("", 32, Ui.Amber);
        _clock.CustomMinimumSize = new Vector2(330, 0);
        bar.AddChild(_clock);
        foreach (var (label, speed, tip) in new[] { ("||", 0.0, "Pause"), (">", 60.0, "1 minute per second"), (">>", 600.0, "10 minutes per second"), (">>>", 3600.0, "1 hour per second") })
        {
            var b = Ui.Button(label, () => SetSpeed(speed), tooltip: tip);
            b.Alignment = HorizontalAlignment.Center;
            b.CustomMinimumSize = new Vector2(44, 0);
            bar.AddChild(b);
            _speedButtons[speed] = b;
        }
        bar.AddChild(Ui.Button("Wait 1h", () => Wait(TimeSpan.FromHours(1))));
        bar.AddChild(Ui.Button("Till morning", WaitTillMorning, tooltip: "Sleep until 7 am"));
        _status = Ui.Label("", 16, Ui.Dim);
        _status.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _status.HorizontalAlignment = HorizontalAlignment.Right;
        bar.AddChild(_status);
        bar.AddChild(Ui.Button("Help", ShowHelp));
        bar.AddChild(Ui.Button("Board", OpenBoard, tooltip: "Evidence board"));
        bar.AddChild(Ui.Button("Menu", OpenMenu));
        root.AddChild(top);

        // Middle: here panel + tabs
        var middle = Ui.HBox(0);
        middle.SizeFlagsVertical = SizeFlags.ExpandFill;
        root.AddChild(middle);

        var left = Ui.PanelBox(Ui.Panel, Ui.Line, 8);
        left.CustomMinimumSize = new Vector2(400, 0);
        var leftScroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        leftScroll.SizeFlagsVertical = SizeFlags.ExpandFill;
        _here = Ui.VBox(4);
        _here.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        leftScroll.AddChild(_here);
        left.AddChild(leftScroll);
        middle.AddChild(left);

        var right = Ui.VBox(0);
        right.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        middle.AddChild(right);
        var tabs = Ui.HBox(2);
        foreach (var t in new[] { "Map", "Notebook", "People", "Case" })
        {
            string name = t;
            var b = Ui.Button(t, () => ShowTab(name));
            b.Alignment = HorizontalAlignment.Center;
            b.CustomMinimumSize = new Vector2(110, 0);
            tabs.AddChild(b);
            _tabButtons[t] = b;
        }
        right.AddChild(tabs);
        _content = new Control { SizeFlagsVertical = SizeFlags.ExpandFill, SizeFlagsHorizontal = SizeFlags.ExpandFill, ClipContents = true };
        right.AddChild(_content);

        // Log
        var logPanel = Ui.PanelBox(new Color(0.04f, 0.035f, 0.03f), Ui.Line, 6);
        logPanel.CustomMinimumSize = new Vector2(0, 130);
        _log = Ui.Rich("", 16, fit: false);
        _log.ScrollFollowing = true;
        _log.SizeFlagsVertical = SizeFlags.ExpandFill;
        logPanel.AddChild(_log);
        root.AddChild(logPanel);

        _overlay = new Control { MouseFilter = MouseFilterEnum.Ignore };
        _overlay.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(_overlay);

        _map = new CityMap(this);
        _notebook = new NotebookView(this);
        _people = new PeopleView(this);
        _caseFile = new CaseFileView(this);
        foreach (var view in new Control[] { _map, _notebook, _people, _caseFile })
        {
            view.SetAnchorsPreset(LayoutPreset.FullRect);
            view.Visible = false;
            _content.AddChild(view);
        }
    }

    public void ShowTab(string tab)
    {
        _tab = tab;
        _map.Visible = tab == "Map";
        _notebook.Visible = tab == "Notebook";
        _people.Visible = tab == "People";
        _caseFile.Visible = tab == "Case";
        foreach (var (name, b) in _tabButtons)
            b.AddThemeColorOverride("font_color", name == tab ? Ui.Amber : Ui.Text);
        if (tab == "Notebook") _notebook.Refresh();
        if (tab == "People") _people.Refresh();
        if (tab == "Case") _caseFile.Refresh();
        if (tab == "Notebook") _seenClues = D.Clues.Count;
        UpdateTabBadges();
    }

    public void ShowNotebookAt(Clue clue)
    {
        ShowTab("Notebook");
        _notebook.Select(clue);
    }

    public void ShowPerson(int personId)
    {
        ShowTab("People");
        _people.Select(personId);
    }

    private void UpdateTabBadges()
    {
        int unseen = D.Clues.Count - _seenClues;
        _tabButtons["Notebook"].Text = unseen > 0 && _tab != "Notebook" ? $"Notebook ({unseen} new)" : "Notebook";
        _tabButtons["Case"].Disabled = D.Case == null;
    }

    // ---- time ----

    public void SetSpeed(double speed)
    {
        Speed = speed;
        foreach (var (s, b) in _speedButtons)
            b.AddThemeColorOverride("font_color", Math.Abs(s - speed) < 0.01 ? Ui.Amber : Ui.Text);
    }

    private void Wait(TimeSpan span)
    {
        if (D.Travelling) return;
        Session.AdvanceTo(Session.World.Now + span);
        AfterAction();
    }

    private void WaitTillMorning()
    {
        if (D.Travelling) return;
        var now = Session.World.Now;
        var morning = now.Date.AddHours(7);
        if (morning <= now.AddMinutes(30)) morning = morning.AddDays(1);
        if (D.AddressId != D.Me.HomeAddressId)
            AppendLog("You doze off in the car for a few hours.", Ui.Dim);
        Session.AdvanceTo(morning);
        AfterAction();
    }

    public override void _Process(double delta)
    {
        if (Game?.Session == null) return;
        var world = Session.World;
        double speed = _modal != null ? 0 : D.Travelling ? _travelSpeed : Speed;
        if (speed > 0)
        {
            var step = TimeSpan.FromSeconds(Math.Min(delta * speed, 7200));
            var target = world.Now + step;
            if (D.Travelling && D.Agent.Trip != null && target > D.Agent.Trip.Arrive) target = D.Agent.Trip.Arrive.AddSeconds(1);
            Session.AdvanceTo(target);
        }
        _clock.Text = world.Now.ToString("ddd MMM d  h:mm tt");
        _status.Text = $"${D.Cash}   Wiretaps: {D.Wiretaps}   " + (D.Travelling ? "On the road" : D.OnStakeout ? "On a stakeout" : PlaceText());

        _hereTimer += delta;
        var key = HereKey();
        if (key != _hereKey || (_hereTimer > 1.0 && speed > 0))
        {
            if (key != _hereKey && !D.Travelling && _hereKey.StartsWith("T")) SetSpeed(0);
            _hereKey = key;
            _hereTimer = 0;
            RebuildHere();
        }
        UpdateTabBadges();
    }

    private string PlaceText()
    {
        if (!D.AddressId.HasValue) return "";
        var loc = D.LocationId.HasValue ? D.State.Locations[D.LocationId.Value].Name : "";
        return $"{D.State.PlaceName(D.AddressId.Value)} - {loc}";
    }

    private string HereKey() =>
        (D.Travelling ? "T" : "A") + $"{D.AddressId}:{D.LocationId}:{D.OnStakeout}:{D.StakeoutLog.Count}:" + string.Join(",", D.PeopleHere());

    /// <summary>Starts a drive and lets the clock run so it takes a couple of real seconds.</summary>
    public void TravelTo(int addressId)
    {
        if (D.Travelling) return;
        var trip = Session.Do("travel", addressId) as Stakeout.Simulation.History.TripFact;
        if (trip == null)
        {
            AfterAction();
            return;
        }
        double seconds = (trip.Arrive - Session.World.Now).TotalSeconds;
        _travelSpeed = Math.Max(120, seconds / 2.5);
        AppendLog($"You head for {D.State.PlaceName(addressId)}. About {Math.Max(1, (int)Math.Round(seconds / 60))} minutes.", Ui.Dim);
        ShowTab("Map");
        _map.FollowPlayer = true;
        RebuildHere();
    }

    // ---- actions and messages ----

    /// <summary>Call after any action: refreshes what is shown.</summary>
    public void AfterAction()
    {
        RebuildHere();
        if (_tab == "Notebook") _notebook.Refresh();
        if (_tab == "People") _people.Refresh();
        if (_tab == "Case") _caseFile.Refresh();
        UpdateTabBadges();
    }

    private void OnMessage(string text)
    {
        var color = text.StartsWith("SCANNER") ? Ui.Red : text.StartsWith("WIRETAP") ? Ui.Amber : text.StartsWith("STAKEOUT") ? Ui.Blue : Ui.Text;
        AppendLog(text, color);
        if (text.StartsWith("SCANNER") || text.StartsWith("Hayes calls") || text.StartsWith("WIRETAP")) SetSpeed(0);
        if (text.StartsWith("SCANNER")) CallDeferred(MethodName.ShowScannerAlert);
    }

    public void AppendLog(string text, Color color)
    {
        if (_log == null) return;
        _log.AppendText($"[color=#{Ui.Hex(Ui.Dim)}]{Session.World.Now:HH:mm}[/color]  [color=#{Ui.Hex(color)}]{Ui.Escape(text)}[/color]\n");
    }

    public void Say(string text) => AppendLog(text, Ui.Text);

    // ---- modals ----

    public void ShowModal(Control content, float width = 760)
    {
        CloseModal();
        var holder = new Control();
        holder.SetAnchorsPreset(LayoutPreset.FullRect);
        var dim = new ColorRect { Color = new Color(0, 0, 0, 0.6f) };
        dim.SetAnchorsPreset(LayoutPreset.FullRect);
        holder.AddChild(dim);
        var center = new CenterContainer();
        center.SetAnchorsPreset(LayoutPreset.FullRect);
        holder.AddChild(center);
        var panel = Ui.PanelBox(Ui.Panel, Ui.Amber, 14);
        panel.CustomMinimumSize = new Vector2(width, 0);
        panel.AddChild(content);
        center.AddChild(panel);
        _overlay.AddChild(holder);
        _modal = holder;
    }

    public void CloseModal()
    {
        _modal?.QueueFree();
        _modal = null;
        AfterAction();
    }

    public bool ModalOpen => _modal != null;

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("ui_cancel"))
        {
            if (_modal != null) CloseModal();
            else OpenMenu();
            GetViewport().SetInputAsHandled();
        }
        else if (@event is InputEventKey { Pressed: true } key && _modal == null)
        {
            if (key.Keycode == Key.Space) SetSpeed(Speed > 0 ? 0 : 60);
        }
    }

    private void OpenMenu()
    {
        SetSpeed(0);
        GetTree().ChangeSceneToFile("res://scenes/main_menu/MainMenu.tscn");
    }

    private void OpenBoard()
    {
        SetSpeed(0);
        GetTree().ChangeSceneToFile("res://scenes/evidence_board/EvidenceBoard.tscn");
    }

    public void PinToBoard(Stakeout.Evidence.EvidenceEntityType type, int id)
    {
        var board = Game.EvidenceBoard;
        if (board.HasItem(type, id))
        {
            Say("Already on the board.");
            return;
        }
        var rng = new Random(id);
        board.AddItem(type, id, new Vector2(1500 + rng.Next(0, 900), 800 + rng.Next(0, 600)));
        Say("Pinned to the evidence board.");
    }
}
