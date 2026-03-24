using Godot;
using Stakeout;

public partial class MainMenu : Control
{
    private GameManager _gameManager;
    private Button _resumeButton;
    private Button _saveButton;

    public override void _Ready()
    {
        _gameManager = GetNode<GameManager>("/root/GameManager");

        _resumeButton = GetNode<Button>("VBoxContainer/ResumeButton");
        var newGameButton = GetNode<Button>("VBoxContainer/NewGameButton");
        _saveButton = GetNode<Button>("VBoxContainer/SaveButton");
        var loadButton = GetNode<Button>("VBoxContainer/LoadButton");
        var optionsButton = GetNode<Button>("VBoxContainer/OptionsButton");
        var quitButton = GetNode<Button>("VBoxContainer/QuitButton");

        _resumeButton.Pressed += OnResumePressed;
        newGameButton.Pressed += OnNewGamePressed;
        // Save and Load are no-ops for now
        optionsButton.Pressed += OnOptionsPressed;
        quitButton.Pressed += OnQuitPressed;

        // Show/hide buttons based on context
        bool inGame = _gameManager.IsGameActive;
        _resumeButton.Visible = inGame;
        _saveButton.Visible = inGame;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("ui_cancel") && _gameManager.IsGameActive)
        {
            ResumeGame();
            GetViewport().SetInputAsHandled();
        }
    }

    private void OnResumePressed()
    {
        ResumeGame();
    }

    private void ResumeGame()
    {
        // Restore time scale and return to game
        _gameManager.SimulationManager.State.Clock.TimeScale = _gameManager.PreviousTimeScale;
        GetTree().ChangeSceneToFile("res://scenes/game_shell/GameShell.tscn");
    }

    private void OnNewGamePressed()
    {
        // Start fresh game — reinitialize GameManager state
        _gameManager.IsGameActive = true;
        _gameManager.ActiveContentView = "res://scenes/city/CityView.tscn";
        _gameManager.Reinitialize();
        GetTree().ChangeSceneToFile("res://scenes/game_shell/GameShell.tscn");
    }

    private void OnOptionsPressed()
    {
        GetTree().ChangeSceneToFile("res://scenes/options_menu/OptionsMenu.tscn");
    }

    private void OnQuitPressed()
    {
        GetTree().Quit();
    }
}
