# Game Shell

## Purpose
Persistent UI frame with a left sidebar (clock, time controls, context-sensitive menu) and a swappable content area.
Content views plug into the shell without full scene changes; immersive views (evidence board) use full scene
transitions instead.

## Key Files
| File | Role |
|------|------|
| `scenes/game_shell/GameShell.cs` | Shell controller - sidebar, time scale buttons, content view loading, debug sidebar (crime generator, people list, person inspector windows) |
| `scenes/game_shell/GameShell.tscn` | Layout - LeftSidebar (1/4 width), ContentArea (3/4, clip_contents), DebugSidebar, DebugMenuButton |
| `scenes/city/CityView.cs` | City map - draws the CityGrid (roads, buildings, parks, street labels) with pan/zoom, NPC/player dots, hover tooltips, right-click menus (travel, add to evidence board) |
| `scenes/address/AddressView.cs` | Address view - location name, occupants, submenu navigation |
| `scenes/address/BlueprintView.cs`, `GraphView.cs` | Stubs, to be rebuilt in P8 (Player UI) |
| `scenes/evidence_board/*` | Evidence board scene (polaroids, red string, dossier windows) reached by full scene change |
| `scenes/main_menu/`, `scenes/options_menu/` | Entry menu and display options (`src/DisplaySettings.cs`) |
| `src/GameManager.cs` | Autoload - owns SimulationState, SimulationManager, EvidenceBoard; remembers `ActiveContentView` across scene changes |

## How It Works
MainMenu starts the game and switches to GameShell. `_Ready()` builds the sidebar and loads the active content view
(default CityView) into ContentArea. Content views implement `IContentView` to get a GameShell reference and call
`SetMenuItems()` to fill the sidebar menu.

The debug sidebar (DebugMenuButton) holds the crime generator and a people list. Clicking a person opens a Person
Inspector window showing identity, location, objectives (current one marked), the DayPlan timeline, and recent
EventJournal entries; open inspectors refresh on a timer.

Time controls set `GameClock.TimeScale`; `SimulationManager._Process` advances the world by `delta * TimeScale`.

## Key Decisions
- **IContentView interface over Godot signals/Call:** C# `is` matching is more reliable than `HasMethod`/`Call` across the Variant bridge.
- **Content views set their own menus** via `SetMenuItems()`.
- **Debug sidebar rebuilt on open:** `PopulateDebugPeopleList` clears and recreates all children, including the crime generator controls.
- **One inspector per person;** opening an existing one focuses it.

## Connection Points
- **SimulationManager:** views read `State`; CityView calls `StartPlayerTravel()`; the crime button uses `SimulationManager.CrimeGenerator`.
- **Evidence board:** CityView right-click menus add Persons/Addresses to `GameManager.EvidenceBoard`; the shell opens the board scene.
- **NPC brain:** inspector reads `Person.Objectives`, `DayPlan`, `CurrentActivity`.
