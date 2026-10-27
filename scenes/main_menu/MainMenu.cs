using System;
using System.Linq;
using Godot;
using Stakeout;
using Stakeout.UI;

public partial class MainMenu : Control
{
    private GameManager _game;
    private VBoxContainer _menu;
    private Control _loadPanel;
    private Label _note;

    public override void _Ready()
    {
        _game = GetNode<GameManager>("/root/GameManager");
        _menu = GetNode<VBoxContainer>("VBoxContainer");
        Theme = Ui.Theme;

        var resume = GetNode<Button>("VBoxContainer/ResumeButton");
        var newGame = GetNode<Button>("VBoxContainer/NewGameButton");
        var save = GetNode<Button>("VBoxContainer/SaveButton");
        var load = GetNode<Button>("VBoxContainer/LoadButton");
        var options = GetNode<Button>("VBoxContainer/OptionsButton");
        var quit = GetNode<Button>("VBoxContainer/QuitButton");
        foreach (var b in new[] { resume, newGame, save, load, options, quit }) b.Alignment = HorizontalAlignment.Center;

        resume.Pressed += Resume;
        newGame.Pressed += () => StartNewGame((int)(Time.GetUnixTimeFromSystem() % 100000));
        save.Pressed += () =>
        {
            var slot = $"{_game.Session.World.Now:yyyy-MM-dd_HHmm}_seed{_game.Session.Seed}";
            var path = _game.SaveGame(slot);
            _note.Text = $"Saved: {slot}";
            GD.Print($"Saved to {ProjectSettings.GlobalizePath(path)}");
        };
        load.Pressed += ShowLoadPanel;
        options.Pressed += () => GetTree().ChangeSceneToFile("res://scenes/options_menu/OptionsMenu.tscn");
        quit.Pressed += () => GetTree().Quit();

        resume.Visible = _game.IsGameActive;
        save.Visible = _game.IsGameActive;
        resume.Text = "Back to the case";

        _note = Ui.Label("", 16, Ui.Dim);
        _note.HorizontalAlignment = HorizontalAlignment.Center;
        _menu.AddChild(_note);
        var tagline = Ui.Label("Boston, October 1984. A private eye, a city, and the people in it.", 16, Ui.Dim);
        tagline.HorizontalAlignment = HorizontalAlignment.Center;
        _menu.AddChild(tagline);
        _menu.MoveChild(tagline, 1);
    }

    public void StartNewGame(int seed)
    {
        _game.EndGame();
        _game.PendingSeed = seed;
        GetTree().ChangeSceneToFile("res://scenes/loading/Loading.tscn");
    }

    private void Resume() => GetTree().ChangeSceneToFile("res://scenes/game_shell/GameShell.tscn");

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("ui_cancel") && _game.IsGameActive)
        {
            Resume();
            GetViewport().SetInputAsHandled();
        }
    }

    private void ShowLoadPanel()
    {
        _loadPanel?.QueueFree();
        var panel = Ui.PanelBox(margin: 14);
        panel.SetAnchorsPreset(LayoutPreset.Center);
        panel.GrowHorizontal = GrowDirection.Both;
        panel.GrowVertical = GrowDirection.Both;
        panel.CustomMinimumSize = new Vector2(520, 0);
        var box = Ui.VBox(6);
        panel.AddChild(box);
        box.AddChild(Ui.Heading("Load a saved game"));
        var dir = DirAccess.Open(GameManager.SaveDir);
        var files = dir == null ? Array.Empty<string>() : dir.GetFiles().Where(f => f.EndsWith(".json")).OrderDescending().ToArray();
        if (files.Length == 0) box.AddChild(Ui.Label("No saved games yet.", 16, Ui.Dim));
        foreach (var f in files.Take(12))
        {
            string name = f;
            box.AddChild(Ui.Button(name.Replace(".json", "").Replace('_', ' '), () =>
            {
                using var file = FileAccess.Open($"{GameManager.SaveDir}/{name}", FileAccess.ModeFlags.Read);
                _game.EndGame();
                _game.PendingSave = file.GetAsText();
                GetTree().ChangeSceneToFile("res://scenes/loading/Loading.tscn");
            }));
        }
        box.AddChild(Ui.Button("Cancel", () => { panel.QueueFree(); _loadPanel = null; }));
        AddChild(panel);
        _loadPanel = panel;
    }
}
