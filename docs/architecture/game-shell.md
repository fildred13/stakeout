# Game Shell

## Purpose
Persistent UI frame with a left sidebar (clock, time controls, context-sensitive menu) and a swappable content area. Content views plug into the shell without full scene changes; immersive views (minigames, evidence board) use full scene transitions instead.

## Key Files
| File | Role |
|------|------|
| `scenes/game_shell/GameShell.cs` | Shell controller — sidebar setup, content view loading, menu rendering, debug panel |
| `scenes/game_shell/GameShell.tscn` | Scene layout — LeftSidebar (1/4 width), ContentArea (3/4 width, clip_contents), DebugSidebar, DebugMenuButton |
| `scenes/city/CityView.cs` | City map content view — renders address icons, entity dots, player dot; handles hover labels and right-click context menus |
| `scenes/city/CityView.tscn` | City scene — CityMap with MapBackground texture, LocationIcons, EntityDots layers, HoverLabel |
| `scenes/address/AddressView.cs` | Address content view — shows location name, lists occupants, provides submenu navigation |
| `scenes/address/AddressView.tscn` | Address scene — centered label |
| `src/GameManager.cs` | Stores `ActiveContentView` path so the shell can restore state after full scene changes |

## How It Works
GameShell is the primary game scene (navigated to from MainMenu). On `_Ready()`, it sets up the sidebar and loads the active content view (default: CityView) into ContentArea via `LoadContentView()`. Content views implement `IContentView` to receive a GameShell reference and call `SetMenuItems()` to populate the sidebar menu.

Menu items are `Godot.Collections.Dictionary` entries with `label`, optional `callback` (Callable), and optional `personId` (enables right-click "Add to Evidence Board"). GameShell renders them as flat pixel-font buttons.

ContentArea has `clip_contents = true` to prevent content (e.g., map textures with Keep Aspect Covered) from overflowing onto the sidebar.

## Key Decisions
- **IContentView interface over Godot signals/Call:** C# `is` pattern matching is more reliable than Godot's `HasMethod`/`Call` across the Variant bridge.
- **Content views set their own menus:** Each view calls `SetMenuItems()` with its context-specific options, rather than GameShell knowing about all possible menus.
- **ActiveContentView on GameManager:** Survives full scene changes (e.g., to Evidence Board and back) so the shell can restore the correct content view.
- **clip_contents on ContentArea:** Prevents stretched/covered textures from overflowing onto the sidebar.

## Connection Points
- **SimulationManager:** Content views read state for display; CityView triggers `StartPlayerTravel()` for movement
- **Evidence board:** GameShell navigates to EvidenceBoard via full scene change; EvidenceBoardScene navigates back to GameShell
- **GameManager:** Provides SimulationManager, EvidenceBoard, and ActiveContentView persistence
