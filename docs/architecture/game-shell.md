# Game Shell (UI)

## Purpose
The Godot screens around the investigation API: menu, loading, the main play screen with its panels and dialogs,
and the evidence board. All UI is built in C# code; scenes are just a root node and a script.

## Key Files
| File | Role |
|------|------|
| `src/GameManager.cs` | Autoload: the running `GameSession`, the evidence board, building games on a worker thread, saving |
| `scenes/main_menu/MainMenu.cs` | New game (random seed), load (user://saves), save, options |
| `scenes/loading/Loading.cs` | Waits for the world build and pre-run, then opens the shell |
| `scenes/game_shell/GameShell.cs` | Layout, clock and speeds, tabs, message log, modals, travel animation |
| `scenes/game_shell/GameShell.Here.cs` | Left panel: people, room actions, other rooms (go in, knock, pick lock), stakeout |
| `scenes/game_shell/GameShell.Dialogs.cs` | Interview, telephone, lockpick minigame, security tapes, scanner alert, help, verdict |
| `scenes/game_shell/Views.cs` | Notebook (handwritten clues), People, Case file (DA assessment and charging) |
| `scenes/city/CityMap.cs` | Map: buildings, live traffic from trips, known places, the scene, you; click to drive |
| `scenes/ui/Ui.cs` | Theme, palette, fonts (pixel UI, Caveat handwriting), control builders |
| `src/UiTestDriver.cs` | `-- --ui-test=DIR [--seed=N]`: plays the opening through the real screens and saves screenshots |

## How It Works
1. Time is paused unless the player presses play (space) or a speed, travels (the clock runs so a drive takes about
   2.5 s), or sits on a stakeout. Modals pause the clock. Actions spend their own game time.
2. Every player action goes through `Session.Do(...)` (the command log) and then `AfterAction()` refreshes panels.
3. The left panel is rebuilt when the place, the people present or the stakeout log change.
4. `Detective.Message` feeds the log; scanner alerts open a modal; Hayes' results and wiretaps pause the clock.

## Key Decisions
- **Code-built UI:** one theme, no fragile .tscn layouts; screens are easy to change and to drive from the test driver.
- **Turn-like time by default:** reading and deciding never cost game time.

## Connection Points
- Uses **investigation** (`GameSession`, `Detective`, `CaseEvaluator`); the **evidence board** scene shares the
  `GameManager`. UI test runs must stay windowed and unfocused (see `CLAUDE.md`).
