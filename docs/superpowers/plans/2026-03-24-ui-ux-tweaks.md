# UI/UX Tweaks Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add display settings (resolution, fullscreen), rework the main menu as a context-aware startup/pause menu, create an options menu, and replace the city view background with a gray color.

**Architecture:** A new `DisplaySettings` static helper manages resolution/fullscreen/viewport logic and persists to `user://settings.cfg`. The MainMenu scene becomes context-aware via `GameManager.IsGameActive`, doubling as a pause menu accessible via Escape. A new OptionsMenu scene hosts resolution and fullscreen controls with a 15-second revert safety dialog. The city view background changes from a TextureRect to a ColorRect.

**Tech Stack:** Godot 4.6, C# (.NET 8), GL Compatibility renderer

---

## File Structure

| File | Action | Responsibility |
|---|---|---|
| `src/DisplaySettings.cs` | Create | Static helper: apply resolution, toggle fullscreen, save/load `user://settings.cfg`, viewport width calculation |
| `scenes/options_menu/OptionsMenu.tscn` | Create | Options menu scene: resolution dropdown, fullscreen toggle, back button |
| `scenes/options_menu/OptionsMenu.cs` | Create | Options menu logic: wire controls, trigger display changes, show revert dialog |
| `scenes/main_menu/MainMenu.tscn` | Modify | Remove GodMode button, rename buttons, add Resume/Save/Quit buttons |
| `scenes/main_menu/MainMenu.cs` | Modify | Context-aware menu (startup vs pause), Escape handling, Resume/Quit logic |
| `scenes/game_shell/GameShell.cs` | Modify | Add Escape key handler to open pause menu, save/restore time scale |
| `src/GameManager.cs` | Modify | Add `IsGameActive` bool, `PreviousTimeScale` float |
| `scenes/city/CityView.tscn` | Modify | Replace MapBackground TextureRect with ColorRect |
| `project.godot` | Modify | Set `stretch/aspect="expand"`, set default window size to 1920x1080 |

---

### Task 1: City View Background

**Files:**
- Modify: `scenes/city/CityView.tscn`

The simplest change — swap the background from a texture to a gray color.

- [ ] **Step 1: Replace MapBackground TextureRect with ColorRect in CityView.tscn**

In `scenes/city/CityView.tscn`, remove the `ext_resource` for the map texture (`id="3_map_texture"`), update `load_steps` from 4 to 3, and replace the MapBackground node:

```
[node name="MapBackground" type="ColorRect" parent="CityMap"]
layout_mode = 1
anchors_preset = 15
anchor_right = 1.0
anchor_bottom = 1.0
color = Color(0.3, 0.3, 0.3, 1)
mouse_filter = 2
```

- [ ] **Step 2: Run the game and verify the city view shows a gray background**

Run: Launch the game, click New Career, verify the city view has a gray background with location icons and entity dots visible on top.

- [ ] **Step 3: Commit**

```bash
git add scenes/city/CityView.tscn
git commit -m "Replace city view background texture with gray ColorRect"
```

---

### Task 2: Project Display Settings

**Files:**
- Modify: `project.godot`

Update the project-level display configuration to support flexible-width viewports and a 1920x1080 default.

- [ ] **Step 1: Update project.godot display settings**

In the `[display]` section of `project.godot`, add the stretch aspect setting. Keep viewport at 1280x720 — this is the logical viewport, not the window size. The window size is controlled at runtime by `DisplaySettings.Apply()`.

```ini
[display]

window/size/viewport_width=1280
window/size/viewport_height=720
window/stretch/mode="canvas_items"
window/stretch/aspect="expand"
```

Key change:
- Add `stretch/aspect="expand"` so the viewport width grows on ultrawide instead of letterboxing
- Viewport stays at 1280x720 (the logical resolution all UI is designed for)

- [ ] **Step 2: Run the game and verify expand stretch works**

Launch the game. Verify the UI fills the window without letterboxing. If you resize the window to a wider aspect ratio, the content area should expand horizontally rather than showing black bars.

- [ ] **Step 3: Commit**

```bash
git add project.godot
git commit -m "Add expand stretch aspect for ultrawide support"
```

---

### Task 3: DisplaySettings Helper

**Files:**
- Create: `src/DisplaySettings.cs`

A static helper class that handles all display-related logic: applying resolution, toggling fullscreen, calculating viewport width, and persisting settings to disk.

- [ ] **Step 1: Create src/DisplaySettings.cs**

```csharp
using Godot;

namespace Stakeout;

public static class DisplaySettings
{
    private const string SettingsPath = "user://settings.cfg";
    private const int BaseViewportHeight = 720;

    public static readonly Vector2I[] SupportedResolutions =
    [
        new(1280, 720),
        new(1920, 1080),
        new(2560, 1080),
        new(2560, 1440),
        new(3440, 1440),
        new(3840, 2160),
    ];

    public static Vector2I CurrentResolution { get; private set; } = new(1920, 1080);
    public static bool IsFullscreen { get; private set; } = false;

    /// <summary>
    /// Load settings from disk. Call once at startup (e.g. in GameManager._Ready).
    /// </summary>
    public static void Load()
    {
        var config = new ConfigFile();
        if (config.Load(SettingsPath) != Error.Ok)
        {
            // No settings file yet — apply defaults
            Apply();
            return;
        }

        var width = (int)config.GetValue("display", "resolution_width", 1920);
        var height = (int)config.GetValue("display", "resolution_height", 1080);
        var fullscreen = (bool)config.GetValue("display", "fullscreen", false);

        CurrentResolution = new Vector2I(width, height);
        IsFullscreen = fullscreen;
        Apply();
    }

    /// <summary>
    /// Save current settings to disk. Call when the user confirms "Keep".
    /// </summary>
    public static void Save()
    {
        var config = new ConfigFile();
        config.SetValue("display", "resolution_width", CurrentResolution.X);
        config.SetValue("display", "resolution_height", CurrentResolution.Y);
        config.SetValue("display", "fullscreen", IsFullscreen);
        config.Save(SettingsPath);
    }

    /// <summary>
    /// Change resolution and apply immediately. Does NOT save to disk.
    /// </summary>
    public static void SetResolution(Vector2I resolution)
    {
        CurrentResolution = resolution;
        Apply();
    }

    /// <summary>
    /// Toggle fullscreen and apply immediately. Does NOT save to disk.
    /// </summary>
    public static void SetFullscreen(bool fullscreen)
    {
        IsFullscreen = fullscreen;
        Apply();
    }

    /// <summary>
    /// Apply current settings to the window and viewport.
    /// </summary>
    private static void Apply()
    {
        var window = ((SceneTree)Engine.GetMainLoop()).Root.GetWindow();

        if (IsFullscreen)
        {
            window.Mode = Window.ModeEnum.Fullscreen;
        }
        else
        {
            window.Mode = Window.ModeEnum.Windowed;
            window.Size = CurrentResolution;
            // Center the window on the screen
            var screenSize = DisplayServer.ScreenGetSize();
            window.Position = (screenSize - CurrentResolution) / 2;
        }
    }

    /// <summary>
    /// Get a display string for a resolution (e.g. "1920 x 1080").
    /// </summary>
    public static string ResolutionToString(Vector2I resolution)
    {
        return $"{resolution.X} x {resolution.Y}";
    }

    /// <summary>
    /// Find the index of the current resolution in SupportedResolutions.
    /// Returns 1 (1920x1080) if not found.
    /// </summary>
    public static int GetCurrentResolutionIndex()
    {
        for (int i = 0; i < SupportedResolutions.Length; i++)
        {
            if (SupportedResolutions[i] == CurrentResolution)
                return i;
        }
        return 1; // default to 1920x1080
    }
}
```

- [ ] **Step 2: Wire DisplaySettings.Load() into GameManager._Ready**

In `src/GameManager.cs`, add `DisplaySettings.Load();` as the first line of `_Ready()`:

```csharp
public override void _Ready()
{
    DisplaySettings.Load();
    State = new SimulationState();
    EvidenceBoard = new EvidenceBoard();
    SimulationManager = new SimulationManager(State);
    AddChild(SimulationManager);
}
```

- [ ] **Step 3: Run the game and verify it loads at 1920x1080 windowed**

Launch the game. Verify the window opens at 1920x1080, centered on screen.

- [ ] **Step 4: Commit**

```bash
git add src/DisplaySettings.cs src/GameManager.cs
git commit -m "Add DisplaySettings helper with load/save/apply logic"
```

---

### Task 4: GameManager State for Pause Menu

**Files:**
- Modify: `src/GameManager.cs`

Add the state properties needed for the pause menu system.

- [ ] **Step 1: Add IsGameActive and PreviousTimeScale to GameManager**

In `src/GameManager.cs`, add two properties:

```csharp
public bool IsGameActive { get; set; } = false;
public float PreviousTimeScale { get; set; } = 1.0f;
```

- [ ] **Step 2: Commit**

```bash
git add src/GameManager.cs
git commit -m "Add IsGameActive and PreviousTimeScale to GameManager"
```

---

### Task 5: Main Menu Rework

**Files:**
- Modify: `scenes/main_menu/MainMenu.tscn`
- Modify: `scenes/main_menu/MainMenu.cs`

Rework the main menu to be context-aware (startup vs pause) with renamed buttons and new functionality.

- [ ] **Step 1: Rebuild MainMenu.tscn**

Replace the contents of `scenes/main_menu/MainMenu.tscn`. The new scene has all possible buttons (Resume, New Game, Save, Load, Options, Quit) — the script will show/hide based on context:

```
[gd_scene format=3 uid="uid://dgutb6udvsro5"]

[ext_resource type="Script" uid="uid://dwq3mofua1mu4" path="res://scenes/main_menu/MainMenu.cs" id="1_script"]
[ext_resource type="FontFile" uid="uid://cywjcf06f322g" path="res://fonts/karma-future/Karma Future.otf" id="2_title_font"]
[ext_resource type="FontFile" uid="uid://b1rib3oax2wve" path="res://fonts/exepixelperfect/EXEPixelPerfect.ttf" id="3_menu_font"]

[node name="MainMenu" type="Control"]
layout_mode = 3
anchors_preset = 15
anchor_right = 1.0
anchor_bottom = 1.0
grow_horizontal = 2
grow_vertical = 2
script = ExtResource("1_script")

[node name="VBoxContainer" type="VBoxContainer" parent="."]
layout_mode = 1
anchors_preset = 8
anchor_left = 0.5
anchor_top = 0.5
anchor_right = 0.5
anchor_bottom = 0.5
offset_left = -150.0
offset_top = -150.0
offset_right = 150.0
offset_bottom = 150.0
grow_horizontal = 2
grow_vertical = 2
alignment = 1

[node name="TitleLabel" type="Label" parent="VBoxContainer"]
layout_mode = 2
theme_override_fonts/font = ExtResource("2_title_font")
theme_override_font_sizes/font_size = 72
text = "STAKEOUT"
horizontal_alignment = 1

[node name="ResumeButton" type="Button" parent="VBoxContainer"]
layout_mode = 2
theme_override_fonts/font = ExtResource("3_menu_font")
theme_override_font_sizes/font_size = 16
text = "Resume"

[node name="NewGameButton" type="Button" parent="VBoxContainer"]
layout_mode = 2
theme_override_fonts/font = ExtResource("3_menu_font")
theme_override_font_sizes/font_size = 16
text = "New Game"

[node name="SaveButton" type="Button" parent="VBoxContainer"]
layout_mode = 2
theme_override_fonts/font = ExtResource("3_menu_font")
theme_override_font_sizes/font_size = 16
text = "Save"

[node name="LoadButton" type="Button" parent="VBoxContainer"]
layout_mode = 2
theme_override_fonts/font = ExtResource("3_menu_font")
theme_override_font_sizes/font_size = 16
text = "Load"

[node name="OptionsButton" type="Button" parent="VBoxContainer"]
layout_mode = 2
theme_override_fonts/font = ExtResource("3_menu_font")
theme_override_font_sizes/font_size = 16
text = "Options"

[node name="QuitButton" type="Button" parent="VBoxContainer"]
layout_mode = 2
theme_override_fonts/font = ExtResource("3_menu_font")
theme_override_font_sizes/font_size = 16
text = "Quit"
```

- [ ] **Step 2: Rewrite MainMenu.cs**

Replace `scenes/main_menu/MainMenu.cs` with:

```csharp
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
```

- [ ] **Step 3: Add Reinitialize method to GameManager**

In `src/GameManager.cs`, add a method to reset game state for New Game:

```csharp
public void Reinitialize()
{
    // Remove old SimulationManager from the tree
    if (SimulationManager != null)
    {
        RemoveChild(SimulationManager);
        SimulationManager.QueueFree();
    }

    State = new SimulationState();
    EvidenceBoard = new EvidenceBoard();
    SimulationManager = new SimulationManager(State);
    AddChild(SimulationManager);
    PreviousTimeScale = 1.0f;
}
```

- [ ] **Step 4: Run the game and verify the startup menu**

Launch the game. Verify:
- Menu shows: New Game, Load, Options, Quit (no Resume, no Save)
- "New Game" transitions to the game
- "Quit" closes the game

- [ ] **Step 5: Commit**

```bash
git add scenes/main_menu/MainMenu.tscn scenes/main_menu/MainMenu.cs src/GameManager.cs
git commit -m "Rework main menu: context-aware startup/pause, remove God Mode"
```

---

### Task 6: Escape Key to Open Pause Menu

**Files:**
- Modify: `scenes/game_shell/GameShell.cs`

Add Escape key handling to GameShell that pauses the simulation and opens the main menu.

- [ ] **Step 1: Add _UnhandledInput to GameShell.cs**

Add this method to the `GameShell` class:

```csharp
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
```

- [ ] **Step 2: Run the game and verify the full pause menu flow**

Launch the game, start a New Game, then:
1. Press Escape — verify the pause menu appears with Resume, New Game, Save, Load, Options, Quit
2. Press Escape again (or click Resume) — verify you return to the game
3. Verify the simulation was paused while in the menu and resumes at the previous speed

- [ ] **Step 3: Commit**

```bash
git add scenes/game_shell/GameShell.cs
git commit -m "Add Escape key to open pause menu from GameShell"
```

---

### Task 7: Options Menu

**Files:**
- Create: `scenes/options_menu/OptionsMenu.tscn`
- Create: `scenes/options_menu/OptionsMenu.cs`

Build the options menu with resolution dropdown, fullscreen toggle, and 15-second revert safety net.

- [ ] **Step 1: Create scenes/options_menu/OptionsMenu.tscn**

```
[gd_scene format=3]

[ext_resource type="Script" path="res://scenes/options_menu/OptionsMenu.cs" id="1_script"]
[ext_resource type="FontFile" uid="uid://cywjcf06f322g" path="res://fonts/karma-future/Karma Future.otf" id="2_title_font"]
[ext_resource type="FontFile" uid="uid://b1rib3oax2wve" path="res://fonts/exepixelperfect/EXEPixelPerfect.ttf" id="3_menu_font"]

[node name="OptionsMenu" type="Control"]
layout_mode = 3
anchors_preset = 15
anchor_right = 1.0
anchor_bottom = 1.0
script = ExtResource("1_script")

[node name="VBoxContainer" type="VBoxContainer" parent="."]
layout_mode = 1
anchors_preset = 8
anchor_left = 0.5
anchor_top = 0.5
anchor_right = 0.5
anchor_bottom = 0.5
offset_left = -200.0
offset_top = -150.0
offset_right = 200.0
offset_bottom = 150.0
grow_horizontal = 2
grow_vertical = 2
alignment = 1

[node name="TitleLabel" type="Label" parent="VBoxContainer"]
layout_mode = 2
theme_override_fonts/font = ExtResource("2_title_font")
theme_override_font_sizes/font_size = 48
text = "OPTIONS"
horizontal_alignment = 1

[node name="ResolutionLabel" type="Label" parent="VBoxContainer"]
layout_mode = 2
theme_override_fonts/font = ExtResource("3_menu_font")
theme_override_font_sizes/font_size = 16
text = "Resolution"

[node name="ResolutionDropdown" type="OptionButton" parent="VBoxContainer"]
layout_mode = 2
theme_override_fonts/font = ExtResource("3_menu_font")
theme_override_font_sizes/font_size = 16

[node name="FullscreenCheck" type="CheckButton" parent="VBoxContainer"]
layout_mode = 2
theme_override_fonts/font = ExtResource("3_menu_font")
theme_override_font_sizes/font_size = 16
text = "Fullscreen"

[node name="BackButton" type="Button" parent="VBoxContainer"]
layout_mode = 2
theme_override_fonts/font = ExtResource("3_menu_font")
theme_override_font_sizes/font_size = 16
text = "Back"

[node name="RevertDialog" type="PanelContainer" parent="."]
visible = false
layout_mode = 1
anchors_preset = 8
anchor_left = 0.5
anchor_top = 0.5
anchor_right = 0.5
anchor_bottom = 0.5
offset_left = -180.0
offset_top = -60.0
offset_right = 180.0
offset_bottom = 60.0
grow_horizontal = 2
grow_vertical = 2

[node name="VBox" type="VBoxContainer" parent="RevertDialog"]
layout_mode = 2
alignment = 1

[node name="RevertLabel" type="Label" parent="RevertDialog/VBox"]
layout_mode = 2
theme_override_fonts/font = ExtResource("3_menu_font")
theme_override_font_sizes/font_size = 14
text = "Keep these display settings?"
horizontal_alignment = 1

[node name="CountdownLabel" type="Label" parent="RevertDialog/VBox"]
layout_mode = 2
theme_override_fonts/font = ExtResource("3_menu_font")
theme_override_font_sizes/font_size = 14
text = "Reverting in 15..."
horizontal_alignment = 1

[node name="ButtonRow" type="HBoxContainer" parent="RevertDialog/VBox"]
layout_mode = 2
alignment = 1

[node name="KeepButton" type="Button" parent="RevertDialog/VBox/ButtonRow"]
layout_mode = 2
theme_override_fonts/font = ExtResource("3_menu_font")
theme_override_font_sizes/font_size = 14
text = "Keep"

[node name="RevertButton" type="Button" parent="RevertDialog/VBox/ButtonRow"]
layout_mode = 2
theme_override_fonts/font = ExtResource("3_menu_font")
theme_override_font_sizes/font_size = 14
text = "Revert"
```

- [ ] **Step 2: Create scenes/options_menu/OptionsMenu.cs**

```csharp
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
```

- [ ] **Step 3: Run the game and verify the options menu**

Launch the game, click Options, then:
1. Verify the resolution dropdown shows all 6 resolutions with 1920x1080 selected
2. Change resolution — verify window resizes immediately and revert dialog appears
3. Wait 15 seconds — verify it reverts to previous resolution
4. Change resolution again — click "Keep" — verify settings persist
5. Toggle fullscreen — verify it goes fullscreen with revert dialog
6. Click "Back" — verify return to main menu

- [ ] **Step 4: Verify settings persist across restarts**

Close the game and relaunch. Verify it opens at the resolution and fullscreen state you last confirmed with "Keep".

- [ ] **Step 5: Commit**

```bash
git add scenes/options_menu/OptionsMenu.tscn scenes/options_menu/OptionsMenu.cs
git commit -m "Add options menu with resolution, fullscreen, and revert dialog"
```

---

### Task 8: Final Integration Test

- [ ] **Step 1: Full flow test**

Run through the complete flow:
1. Launch game — startup menu shows New Game, Load, Options, Quit
2. Click Options — change resolution, confirm with Keep, go back
3. Click New Game — game starts, city view has gray background
4. Press Escape — pause menu shows Resume, New Game, Save, Load, Options, Quit
5. Simulation is paused while in menu
6. Press Escape or click Resume — game resumes at previous speed
7. Press Escape, click Options — change to fullscreen, confirm, go back
8. Click Resume — game resumes in fullscreen
9. Press Escape, click New Game — fresh game starts
10. Press Escape, click Quit — game closes

- [ ] **Step 2: Test ultrawide resolution**

If on ultrawide monitor: set resolution to 3440x1440 in options, confirm. Verify the game fills the screen width without letterboxing and the sidebar stays proportional.

- [ ] **Step 3: Commit any fixes**

If any fixes were needed, commit them with a descriptive message.
