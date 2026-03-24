# UI/UX Tweaks Design

## Overview

A set of UI/UX improvements covering display scaling, main menu rework, options menu, and city view cleanup.

## 1. Display System

### Viewport Strategy: Fixed Height, Flexible Width

- Base viewport height: 720 logical pixels (unchanged from current 1280x720 base)
- Viewport width: calculated dynamically from the chosen resolution's aspect ratio
- Stretch mode: `canvas_items` (unchanged)
- Stretch aspect: changed from default `keep` to `expand`, so viewport width grows to fill without letterboxing

### Logical Viewport Widths by Resolution

| Resolution | Aspect Ratio | Logical Viewport |
|---|---|---|
| 1280x720 | 16:9 | 1280x720 |
| 1920x1080 | 16:9 | 1280x720 |
| 2560x1080 | 21.3:9 | 1707x720 |
| 2560x1440 | 16:9 | 1280x720 |
| 3440x1440 | 21.5:9 | 1720x720 |
| 3840x2160 | 16:9 | 1280x720 |

### Supported Resolutions

- 1280x720 (720p)
- 1920x1080 (1080p)
- 2560x1080 (ultrawide 1080p)
- 2560x1440 (1440p)
- 3440x1440 (ultrawide 1440p)
- 3840x2160 (4K)

### Fullscreen

- Toggle between windowed (at chosen resolution) and borderless fullscreen (at native monitor resolution)
- In fullscreen, the viewport width adjusts to the monitor's actual aspect ratio

### Settings Persistence

- Resolution and fullscreen settings saved to `user://settings.cfg` via Godot's `ConfigFile`
- Loaded on startup (in an autoload or at the start of MainMenu `_Ready`)

### Revert Safety Net

- When resolution or fullscreen changes, a 15-second countdown dialog appears over the options menu
- Shows: "Keep these display settings?" with countdown timer
- Buttons: Keep, Revert
- If timer expires or Revert is clicked, previous settings are restored
- Shared dialog for both resolution and fullscreen changes

## 2. Main Menu

### Context-Aware Menu

The MainMenu scene serves as both the startup screen and the in-game pause menu. `GameManager.IsGameActive` (new bool property) determines which mode.

### Startup Menu (IsGameActive = false)

1. New Game
2. Load (no-op)
3. Options
4. Quit

### In-Game Pause Menu (IsGameActive = true)

1. New Game
2. Save (no-op)
3. Load (no-op)
4. Options
5. Quit

### Escape Key

- `GameShell` listens for `ui_cancel` (Escape) in `_UnhandledInput`
- Transitions to MainMenu scene
- Returning from MainMenu restores the game via `GameManager.ActiveContentView` (existing mechanism)

### Removed

- God Mode button: deleted from scene and script entirely

### Behavior

- **New Game**: always starts a fresh game session (transitions to GameShell with new state)
- **Save**: no-op button for now
- **Load**: no-op button for now
- **Options**: transitions to OptionsMenu scene
- **Quit**: calls `GetTree().Quit()`

## 3. Options Menu

### Scene

- New scene: `scenes/options_menu/OptionsMenu.tscn` with `OptionsMenu.cs`
- Styled consistently: EXEPixelPerfect font, white text, blue hover

### Contents

- **Resolution**: dropdown/selector showing the 6 supported resolutions. Changes apply immediately, triggering the 15-second revert dialog.
- **Fullscreen**: toggle (checkbox or on/off button). Changes apply immediately, triggering the 15-second revert dialog.
- **Back**: returns to MainMenu

### Revert Dialog

- Centered panel overlaying the options menu
- "Keep these display settings?" with countdown (15 → 0)
- Keep button: confirms the new settings
- Revert button: restores previous settings immediately
- Timer expiry: same as clicking Revert

## 4. City View Background

- Replace the `MapBackground` TextureRect (which loads `PLACEHOLDER_city_map.png`) with a `ColorRect`
- Color: medium gray `Color(0.3, 0.3, 0.3)`
- Full-screen anchors and mouse_filter = pass-through remain the same
- Location icons and entity dots render on top as before
