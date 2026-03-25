# Game Shell

## Purpose
Persistent UI frame with a left sidebar (clock, time controls, context-sensitive menu) and a swappable content area. Content views plug into the shell without full scene changes; immersive views (minigames, evidence board) use full scene transitions instead.

## Key Files
| File | Role |
|------|------|
| `scenes/game_shell/GameShell.cs` | Shell controller — sidebar setup, content view loading, menu rendering, debug panel (crime generator, people list, person inspector dialog) |
| `scenes/game_shell/GameShell.tscn` | Scene layout — LeftSidebar (1/4 width), ContentArea (3/4 width, clip_contents), DebugSidebar, DebugMenuButton |
| `scenes/city/CityView.cs` | City map content view — renders address icons, entity dots (dead NPCs in red), action-aware hover tooltips, right-click context menus |
| `scenes/city/CityView.tscn` | City scene — CityMap with MapBackground texture, LocationIcons, EntityDots layers, HoverLabel |
| `scenes/address/AddressView.cs` | Address content view — shows location name, lists occupants, provides submenu navigation |
| `scenes/address/AddressView.tscn` | Address scene — centered label |
| `src/GameManager.cs` | Stores `ActiveContentView` path so the shell can restore state after full scene changes |

## How It Works
GameShell is the primary game scene (navigated to from MainMenu). On `_Ready()`, it sets up the sidebar and loads the active content view (default: CityView) into ContentArea via `LoadContentView()`. Content views implement `IContentView` to receive a GameShell reference and call `SetMenuItems()` to populate the sidebar menu.

The debug sidebar (toggled via DebugMenuButton) contains a Crime Generator section (generate serial killer crimes on demand) and a People list. Clicking a person name opens a Person Inspector dialog (Window node) showing identity, location, objectives with step status, schedule timeline, and recent events. The crime generator calls CrimeGenerator.Generate() and triggers schedule rebuilds.

CityView renders entity dots with action-aware colors (white=active, grey=sleeping, red=dead) and enhanced hover tooltips showing current action with destination/workplace details (e.g., "TravelByCar → 42 Oak St", "Work at 1 Main St").

## Key Decisions
- **IContentView interface over Godot signals/Call:** C# `is` pattern matching is more reliable than Godot's `HasMethod`/`Call` across the Variant bridge.
- **Content views set their own menus:** Each view calls `SetMenuItems()` with its context-specific options.
- **Crime Generator UI rebuilt in PopulateDebugPeopleList:** The debug sidebar clears and rebuilds all children on each open, so crime generator controls are recreated alongside the people list to survive the rebuild cycle.
- **Single active inspector window:** Only one Person Inspector dialog can be open at a time; opening a new one closes the previous.

## Connection Points
- **SimulationManager:** Content views read state for display; CityView triggers `StartPlayerTravel()` for movement; GameShell calls `RebuildSchedule()` after crime generation
- **Crime system:** GameShell uses CrimeGenerator and ObjectiveResolver to trigger and display crime state
- **Evidence board:** GameShell navigates to EvidenceBoard via full scene change
- **GameManager:** Provides SimulationManager, EvidenceBoard, and ActiveContentView persistence
