using System.Threading.Tasks;
using Godot;
using Stakeout;
using Stakeout.UI;

/// <summary>Builds the city and lives through its last few days while the player watches the newspaper spin.</summary>
public partial class Loading : Control
{
    private GameManager _game;
    private Label _status;
    private Task _task;
    private double _time;

    private static readonly string[] Headlines =
    {
        "CITY COUNCIL DEADLOCKED ON PARKING METERS",
        "RED SOX FANS BRACE FOR ANOTHER LONG WINTER",
        "POLICE UNION: \"WE ARE STRETCHED THIN\"",
        "NEW WALKMAN MODELS FLY OFF THE SHELVES",
        "MAYOR PROMISES SAFER STREETS BY SPRING"
    };

    public override void _Ready()
    {
        Theme = Ui.Theme;
        _game = GetNode<GameManager>("/root/GameManager");
        AddChild(new ColorRect { Color = Ui.Bg, AnchorRight = 1, AnchorBottom = 1 });
        var box = Ui.VBox(16);
        box.SetAnchorsPreset(LayoutPreset.Center);
        box.GrowHorizontal = GrowDirection.Both;
        box.GrowVertical = GrowDirection.Both;
        box.CustomMinimumSize = new Vector2(700, 0);
        var title = Ui.Label("STAKEOUT", 64, Ui.Amber, font: Ui.Title);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        box.AddChild(title);
        var paper = Ui.Label(Headlines[(int)(Time.GetTicksMsec() / 1000 % (ulong)Headlines.Length)], 16, Ui.Dim);
        paper.HorizontalAlignment = HorizontalAlignment.Center;
        box.AddChild(paper);
        _status = Ui.Label("", 16, Ui.Text);
        _status.HorizontalAlignment = HorizontalAlignment.Center;
        box.AddChild(_status);
        AddChild(box);
        _task = _game.BuildPendingAsync();
    }

    public override void _Process(double delta)
    {
        _time += delta;
        string dots = new string('.', (int)(_time * 2) % 4);
        _status.Text = $"{_game.LoadStatus}{dots}";
        if (!_task.IsCompleted) return;
        if (_game.LoadError != null)
        {
            _status.Text = "Could not start the game: " + _game.LoadError.Message;
            _status.AddThemeColorOverride("font_color", Ui.Red);
            GD.PrintErr(_game.LoadError.ToString());
            SetProcess(false);
            var back = Ui.Button("Back to the menu", () => GetTree().ChangeSceneToFile("res://scenes/main_menu/MainMenu.tscn"));
            back.Position = new Vector2(20, 20);
            AddChild(back);
            return;
        }
        SetProcess(false);
        GetTree().ChangeSceneToFile("res://scenes/game_shell/GameShell.tscn");
    }
}
