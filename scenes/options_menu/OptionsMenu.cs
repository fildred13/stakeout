using Godot;
using Stakeout;

public partial class OptionsMenu : Control
{
    private OptionButton _resolutionDropdown;
    private CheckButton _fullscreenCheck;
    private PanelContainer _revertDialog;
    private Label _countdownLabel;
    private Timer _revertTimer;

    // Saved state before a change, for reverting
    private Vector2I _previousResolution;
    private bool _previousFullscreen;
    private float _revertCountdown;

    public override void _Ready()
    {
        _resolutionDropdown = GetNode<OptionButton>("VBoxContainer/ResolutionDropdown");
        _fullscreenCheck = GetNode<CheckButton>("VBoxContainer/FullscreenCheck");
        var backButton = GetNode<Button>("VBoxContainer/BackButton");

        _revertDialog = GetNode<PanelContainer>("RevertDialog");
        _countdownLabel = GetNode<Label>("RevertDialog/VBox/CountdownLabel");
        var keepButton = GetNode<Button>("RevertDialog/VBox/ButtonRow/KeepButton");
        var revertButton = GetNode<Button>("RevertDialog/VBox/ButtonRow/RevertButton");

        // Populate resolution dropdown
        foreach (var res in DisplaySettings.SupportedResolutions)
        {
            _resolutionDropdown.AddItem(DisplaySettings.ResolutionToString(res));
        }
        _resolutionDropdown.Selected = DisplaySettings.GetCurrentResolutionIndex();
        _fullscreenCheck.ButtonPressed = DisplaySettings.IsFullscreen;

        // Wire signals
        _resolutionDropdown.ItemSelected += OnResolutionChanged;
        _fullscreenCheck.Toggled += OnFullscreenToggled;
        backButton.Pressed += OnBackPressed;
        keepButton.Pressed += OnKeepPressed;
        revertButton.Pressed += OnRevertPressed;

        // Create the countdown timer
        _revertTimer = new Timer { WaitTime = 1.0, Autostart = false };
        _revertTimer.Timeout += OnRevertTimerTick;
        AddChild(_revertTimer);
    }

    private void OnResolutionChanged(long index)
    {
        _previousResolution = DisplaySettings.CurrentResolution;
        _previousFullscreen = DisplaySettings.IsFullscreen;

        DisplaySettings.SetResolution(DisplaySettings.SupportedResolutions[index]);
        ShowRevertDialog();
    }

    private void OnFullscreenToggled(bool pressed)
    {
        _previousResolution = DisplaySettings.CurrentResolution;
        _previousFullscreen = DisplaySettings.IsFullscreen;

        DisplaySettings.SetFullscreen(pressed);
        ShowRevertDialog();
    }

    private void ShowRevertDialog()
    {
        _revertCountdown = 15f;
        UpdateCountdownText();
        _revertDialog.Visible = true;
        _revertTimer.Start();
    }

    private void HideRevertDialog()
    {
        _revertDialog.Visible = false;
        _revertTimer.Stop();
    }

    private void OnRevertTimerTick()
    {
        _revertCountdown -= 1f;
        UpdateCountdownText();

        if (_revertCountdown <= 0f)
        {
            OnRevertPressed();
        }
    }

    private void UpdateCountdownText()
    {
        _countdownLabel.Text = $"Reverting in {(int)_revertCountdown}...";
    }

    private void OnKeepPressed()
    {
        DisplaySettings.Save();
        HideRevertDialog();
    }

    private void OnRevertPressed()
    {
        HideRevertDialog();
        DisplaySettings.SetResolution(_previousResolution);
        DisplaySettings.SetFullscreen(_previousFullscreen);
        DisplaySettings.Save();

        // Update UI to reflect reverted state
        _resolutionDropdown.Selected = DisplaySettings.GetCurrentResolutionIndex();
        _fullscreenCheck.ButtonPressed = DisplaySettings.IsFullscreen;
    }

    private void OnBackPressed()
    {
        GetTree().ChangeSceneToFile("res://scenes/main_menu/MainMenu.tscn");
    }
}
