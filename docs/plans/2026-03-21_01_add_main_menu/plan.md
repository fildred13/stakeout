# Plan: Add Main Menu Screen

## Goal
Create a basic Main Menu scene with a title and four no-op menu options.

## Files to Create
- `scenes/main_menu/MainMenu.tscn` — the scene
- `scenes/main_menu/MainMenu.cs` — script attached to the root node

## Scene Structure

```
MainMenu (Control)               ← full-rect, MainMenu.cs attached
└── VBoxContainer (VBoxContainer)
    ├── TitleLabel (Label)       ← "STAKEOUT", Karma Future.otf
    ├── NewCareerButton (Button) ← "New Career", EXEPixelPerfect.ttf
    ├── LoadCareerButton (Button)← "Load Career", EXEPixelPerfect.ttf
    ├── GodModeButton (Button)   ← "God Mode",   EXEPixelPerfect.ttf
    └── OptionsButton (Button)   ← "Options",    EXEPixelPerfect.ttf
```

The `VBoxContainer` is centered on screen via an `AnchorPreset` of `Center` with a fixed minimum size wide enough to hold the content.

## Font Setup
Both fonts are referenced directly from the `fonts/` directory — no copies needed.

- `TitleLabel`: `fonts/karma-future/Karma Future.otf`, size ~72
- All four buttons: `fonts/exepixelperfect/EXEPixelPerfect.ttf`, size ~16

## Script (MainMenu.cs)
A minimal `Control` subclass. Each button's `Pressed` signal is connected to a dedicated handler that does nothing (`_OnNewCareerPressed`, `_OnLoadCareerPressed`, `_OnGodModePressed`, `_OnOptionsPressed`).

## Project Entry Point
Set `MainMenu.tscn` as the main scene in `project.godot` (`application/run/main_scene`).

## Steps
1. Create `scenes/main_menu/` directory.
2. Write `MainMenu.cs`.
3. Build the scene in `MainMenu.tscn`: root Control → VBoxContainer → TitleLabel + 4 Buttons, assign fonts and sizes inline.
4. Wire button signals to the no-op handlers.
5. Update `project.godot` to set the main scene.
