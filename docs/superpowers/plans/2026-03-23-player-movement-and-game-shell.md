# Player Movement & Game Shell Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> **CRITICAL: Never prefix shell commands with `cd`. The working directory is already the project root. Run commands directly (e.g., `git add file.cs`, not `cd path && git add file.cs`). This breaks permission matching and is strictly prohibited.**

**Goal:** Add player movement across the city map, introduce a persistent GameShell UI frame with a left-hand sidebar and swappable content area, create an address content view, and rename simulation_debug to city.

**Architecture:** GameShell is a persistent scene containing a left sidebar (clock, time controls, context-sensitive menu) and a content area that hosts swappable content views (CityView, AddressView). Player movement reuses NPC TravelInfo and MapConfig travel formulas, with interpolation driven by SimulationManager. Scene changes to Evidence Board or minigames leave GameShell entirely; state is persisted on the GameManager autoload.

**Tech Stack:** Godot 4.6, C# (.NET 8), xUnit for tests

**Spec:** `docs/superpowers/specs/2026-03-23-player-movement-and-game-shell-design.md`

---

## File Structure

### New Files
| File | Responsibility |
|------|---------------|
| `scenes/game_shell/GameShell.tscn` | Persistent UI frame scene: sidebar, content area, debug panel |
| `scenes/game_shell/GameShell.cs` | GameShell logic: clock display, time controls, menu rendering, debug sidebar, content view swapping |
| `scenes/address/AddressView.tscn` | Address content view scene: placeholder label |
| `scenes/address/AddressView.cs` | Address view logic: sidebar menus, people list submenu |
| `stakeout.tests/Simulation/PlayerTravelTests.cs` | Tests for player travel interpolation, arrival, interruption |

### Modified Files
| File | Changes |
|------|---------|
| `scenes/simulation_debug/SimulationDebug.tscn` → `scenes/city/CityView.tscn` | Rename, strip sidebar/clock/time controls/debug panel. Keep only map content. Update script reference. |
| `scenes/simulation_debug/SimulationDebug.cs` → `scenes/city/CityView.cs` | Rename class to `CityView`. Remove sidebar, clock, time controls, debug panel code. Add "Go here" context menu. Provide sidebar menu items to GameShell. Update player dot from `Player.CurrentPosition`. |
| `src/simulation/entities/Player.cs` | Add `Id`, `CurrentPosition`, `TravelInfo` properties |
| `src/simulation/SimulationManager.cs` | Add player travel interpolation in `_Process`. Initialize `Player.Id` and `Player.CurrentPosition`. |
| `src/GameManager.cs` | Add `ActiveContentView` field for scene-change state restoration |
| `scenes/evidence_board/EvidenceBoardScene.cs` | Change close destination to `GameShell.tscn` |
| `scenes/main_menu/MainMenu.cs` | Change career start destination to `GameShell.tscn` |

---

## Task 1: Extend Player Entity

**Files:**
- Modify: `src/simulation/entities/Player.cs`
- Create: `stakeout.tests/Simulation/PlayerTravelTests.cs`

- [ ] **Step 1: Write failing test for Player.Id and CurrentPosition**

```csharp
// stakeout.tests/Simulation/PlayerTravelTests.cs
using System;
using Godot;
using Stakeout.Simulation;
using Stakeout.Simulation.Entities;
using Xunit;

namespace Stakeout.Tests.Simulation;

public class PlayerTravelTests
{
    [Fact]
    public void Player_HasIdAndCurrentPosition()
    {
        var player = new Player
        {
            Id = 42,
            CurrentPosition = new Vector2(100, 200),
            HomeAddressId = 1,
            CurrentAddressId = 1
        };

        Assert.Equal(42, player.Id);
        Assert.Equal(new Vector2(100, 200), player.CurrentPosition);
        Assert.Null(player.TravelInfo);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test stakeout.tests/ --filter "PlayerTravelTests.Player_HasIdAndCurrentPosition" -v minimal`
Expected: FAIL — `Player` doesn't have `Id`, `CurrentPosition`, or `TravelInfo` properties.

- [ ] **Step 3: Add properties to Player**

```csharp
// src/simulation/entities/Player.cs
using Godot;

namespace Stakeout.Simulation.Entities;

public class Player
{
    public int Id { get; set; }
    public int HomeAddressId { get; set; }
    public int CurrentAddressId { get; set; }
    public Vector2 CurrentPosition { get; set; }
    public TravelInfo TravelInfo { get; set; }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test stakeout.tests/ --filter "PlayerTravelTests.Player_HasIdAndCurrentPosition" -v minimal`
Expected: PASS

- [ ] **Step 5: Update SimulationManager to initialize Player.Id and CurrentPosition**

In `SimulationManager._Ready()`, change the player creation block to:

```csharp
// Replace the existing player creation block (lines 51-60)
var playerHome = _locationGenerator.GenerateAddress(State, AddressType.SuburbanHome);
AddressAdded?.Invoke(playerHome);

State.Player = new Player
{
    Id = State.GenerateEntityId(),
    HomeAddressId = playerHome.Id,
    CurrentAddressId = playerHome.Id,
    CurrentPosition = playerHome.Position
};
PlayerCreated?.Invoke();
```

- [ ] **Step 6: Run all tests to verify nothing broke**

Run: `dotnet test stakeout.tests/ -v minimal`
Expected: All tests pass.

- [ ] **Step 7: Commit**

```
git add src/simulation/entities/Player.cs stakeout.tests/Simulation/PlayerTravelTests.cs src/simulation/SimulationManager.cs
git commit -m "feat: add Id, CurrentPosition, TravelInfo to Player entity"
```

---

## Task 2: Player Travel Interpolation in SimulationManager

**Files:**
- Modify: `src/simulation/SimulationManager.cs`
- Modify: `stakeout.tests/Simulation/PlayerTravelTests.cs`

- [ ] **Step 1: Write failing test for player travel interpolation**

Add to `PlayerTravelTests.cs`:

```csharp
[Fact]
public void PlayerTravel_InterpolatesPosition()
{
    var state = new SimulationState(new GameClock(new DateTime(1980, 1, 1, 8, 0, 0)));
    var from = new Address { Id = state.GenerateEntityId(), Position = new Vector2(100, 100), Type = AddressType.SuburbanHome, Number = 1, StreetId = 1 };
    var to = new Address { Id = state.GenerateEntityId(), Position = new Vector2(500, 100), Type = AddressType.Office, Number = 2, StreetId = 1 };
    state.Addresses[from.Id] = from;
    state.Addresses[to.Id] = to;

    var mapConfig = new MapConfig();
    var travelHours = mapConfig.ComputeTravelTimeHours(from.Position, to.Position);
    var departureTime = state.Clock.CurrentTime;
    var arrivalTime = departureTime.AddHours(travelHours);

    state.Player = new Player
    {
        Id = state.GenerateEntityId(),
        HomeAddressId = from.Id,
        CurrentAddressId = from.Id,
        CurrentPosition = from.Position,
        TravelInfo = new TravelInfo
        {
            FromPosition = from.Position,
            ToPosition = to.Position,
            DepartureTime = departureTime,
            ArrivalTime = arrivalTime,
            FromAddressId = from.Id,
            ToAddressId = to.Id
        }
    };

    // Advance clock to 50% of travel time
    var halfTravelSeconds = travelHours * 3600 / 2;
    state.Clock.Tick(halfTravelSeconds);

    SimulationManager.UpdatePlayerTravel(state);

    // Should be roughly halfway between from and to
    Assert.InRange(state.Player.CurrentPosition.X, 250, 350);
    Assert.NotNull(state.Player.TravelInfo); // Still traveling
}
```

- [ ] **Step 2: Write failing test for player arrival**

Add to `PlayerTravelTests.cs`:

```csharp
[Fact]
public void PlayerTravel_ArrivesAtDestination()
{
    var state = new SimulationState(new GameClock(new DateTime(1980, 1, 1, 8, 0, 0)));
    var from = new Address { Id = state.GenerateEntityId(), Position = new Vector2(100, 100), Type = AddressType.SuburbanHome, Number = 1, StreetId = 1 };
    var to = new Address { Id = state.GenerateEntityId(), Position = new Vector2(500, 100), Type = AddressType.Office, Number = 2, StreetId = 1 };
    state.Addresses[from.Id] = from;
    state.Addresses[to.Id] = to;

    var mapConfig = new MapConfig();
    var travelHours = mapConfig.ComputeTravelTimeHours(from.Position, to.Position);
    var departureTime = state.Clock.CurrentTime;
    var arrivalTime = departureTime.AddHours(travelHours);

    state.Player = new Player
    {
        Id = state.GenerateEntityId(),
        HomeAddressId = from.Id,
        CurrentAddressId = from.Id,
        CurrentPosition = from.Position,
        TravelInfo = new TravelInfo
        {
            FromPosition = from.Position,
            ToPosition = to.Position,
            DepartureTime = departureTime,
            ArrivalTime = arrivalTime,
            FromAddressId = from.Id,
            ToAddressId = to.Id
        }
    };

    // Advance past arrival time
    state.Clock.Tick(travelHours * 3600 + 1);

    SimulationManager.UpdatePlayerTravel(state);

    Assert.Equal(to.Position, state.Player.CurrentPosition);
    Assert.Equal(to.Id, state.Player.CurrentAddressId);
    Assert.Null(state.Player.TravelInfo);

    // Check ArrivedAtAddress event was logged
    var events = state.Journal.GetEventsForPerson(state.Player.Id);
    Assert.Single(events);
    Assert.Equal(SimulationEventType.ArrivedAtAddress, events[0].EventType);
    Assert.Equal(to.Id, events[0].AddressId);
}
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test stakeout.tests/ --filter "PlayerTravelTests" -v minimal`
Expected: FAIL — `SimulationManager.UpdatePlayerTravel` doesn't exist.

- [ ] **Step 4: Implement UpdatePlayerTravel as a static method on SimulationManager**

Add to `SimulationManager.cs` (as a `public static` method so tests can call it without Godot scene tree):

```csharp
public static void UpdatePlayerTravel(SimulationState state)
{
    var player = state.Player;
    if (player?.TravelInfo == null) return;

    var travel = player.TravelInfo;
    var currentTime = state.Clock.CurrentTime;

    if (currentTime >= travel.ArrivalTime)
    {
        // Arrived
        player.CurrentPosition = travel.ToPosition;
        player.CurrentAddressId = travel.ToAddressId;
        player.TravelInfo = null;

        state.Journal.Append(new SimulationEvent
        {
            Timestamp = currentTime,
            PersonId = player.Id,
            EventType = SimulationEventType.ArrivedAtAddress,
            AddressId = travel.ToAddressId
        });
    }
    else
    {
        // Interpolate
        var totalSeconds = (travel.ArrivalTime - travel.DepartureTime).TotalSeconds;
        var elapsedSeconds = (currentTime - travel.DepartureTime).TotalSeconds;
        var progress = Math.Clamp(elapsedSeconds / totalSeconds, 0.0, 1.0);
        player.CurrentPosition = travel.FromPosition.Lerp(travel.ToPosition, (float)progress);
    }
}
```

Add the required using at the top of SimulationManager.cs:
```csharp
using Stakeout.Simulation.Events;
```

Call `UpdatePlayerTravel(State)` from `_Process()` after the NPC update loop:

```csharp
// At the end of _Process, after the foreach loop over people:
UpdatePlayerTravel(State);
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test stakeout.tests/ --filter "PlayerTravelTests" -v minimal`
Expected: All PASS.

- [ ] **Step 6: Write failing test for DepartedAddress event**

Add to `PlayerTravelTests.cs`:

```csharp
[Fact]
public void StartPlayerTravel_LogsDepartedAddressEvent()
{
    var state = new SimulationState(new GameClock(new DateTime(1980, 1, 1, 8, 0, 0)));
    var from = new Address { Id = state.GenerateEntityId(), Position = new Vector2(100, 100), Type = AddressType.SuburbanHome, Number = 1, StreetId = 1 };
    var to = new Address { Id = state.GenerateEntityId(), Position = new Vector2(500, 100), Type = AddressType.Office, Number = 2, StreetId = 1 };
    state.Addresses[from.Id] = from;
    state.Addresses[to.Id] = to;

    var mapConfig = new MapConfig();

    state.Player = new Player
    {
        Id = state.GenerateEntityId(),
        HomeAddressId = from.Id,
        CurrentAddressId = from.Id,
        CurrentPosition = from.Position
    };

    SimulationManager.StartPlayerTravel(state, to.Id, mapConfig);

    var events = state.Journal.GetEventsForPerson(state.Player.Id);
    Assert.Single(events);
    Assert.Equal(SimulationEventType.DepartedAddress, events[0].EventType);
    Assert.Equal(from.Id, events[0].FromAddressId);
    Assert.Equal(to.Id, events[0].ToAddressId);
}
```

- [ ] **Step 7: Run test to verify it fails, then verify it passes after Step 8's implementation**

Run: `dotnet test stakeout.tests/ --filter "StartPlayerTravel_LogsDepartedAddressEvent" -v minimal`

(This test will pass after implementing `StartPlayerTravel` in Step 8 — the event logging is already in the implementation.)

- [ ] **Step 8: Write failing test for travel interruption**

Add to `PlayerTravelTests.cs`:

```csharp
[Fact]
public void PlayerTravel_CanBeInterrupted()
{
    var state = new SimulationState(new GameClock(new DateTime(1980, 1, 1, 8, 0, 0)));
    var from = new Address { Id = state.GenerateEntityId(), Position = new Vector2(100, 100), Type = AddressType.SuburbanHome, Number = 1, StreetId = 1 };
    var to = new Address { Id = state.GenerateEntityId(), Position = new Vector2(500, 100), Type = AddressType.Office, Number = 2, StreetId = 1 };
    var newDest = new Address { Id = state.GenerateEntityId(), Position = new Vector2(100, 500), Type = AddressType.Diner, Number = 3, StreetId = 1 };
    state.Addresses[from.Id] = from;
    state.Addresses[to.Id] = to;
    state.Addresses[newDest.Id] = newDest;

    var mapConfig = new MapConfig();
    var travelHours = mapConfig.ComputeTravelTimeHours(from.Position, to.Position);

    state.Player = new Player
    {
        Id = state.GenerateEntityId(),
        HomeAddressId = from.Id,
        CurrentAddressId = from.Id,
        CurrentPosition = from.Position,
        TravelInfo = new TravelInfo
        {
            FromPosition = from.Position,
            ToPosition = to.Position,
            DepartureTime = state.Clock.CurrentTime,
            ArrivalTime = state.Clock.CurrentTime.AddHours(travelHours),
            FromAddressId = from.Id,
            ToAddressId = to.Id
        }
    };

    // Advance to 50% of travel
    state.Clock.Tick(travelHours * 3600 / 2);
    SimulationManager.UpdatePlayerTravel(state);
    var midpoint = state.Player.CurrentPosition;

    // Interrupt: redirect to newDest
    SimulationManager.StartPlayerTravel(state, newDest.Id, mapConfig);

    Assert.Equal(newDest.Id, state.Player.TravelInfo.ToAddressId);
    Assert.Equal(midpoint, state.Player.TravelInfo.FromPosition);
    Assert.NotNull(state.Player.TravelInfo);
}
```

- [ ] **Step 7: Run test to verify it fails**

Run: `dotnet test stakeout.tests/ --filter "PlayerTravel_CanBeInterrupted" -v minimal`
Expected: FAIL — `SimulationManager.StartPlayerTravel` doesn't exist.

- [ ] **Step 8: Implement StartPlayerTravel**

Add to `SimulationManager.cs`:

```csharp
public static void StartPlayerTravel(SimulationState state, int destinationAddressId, MapConfig mapConfig)
{
    var player = state.Player;
    var destAddress = state.Addresses[destinationAddressId];
    var currentTime = state.Clock.CurrentTime;

    // Log departure if currently at an address (not already traveling)
    if (player.TravelInfo == null && player.CurrentAddressId != 0)
    {
        state.Journal.Append(new SimulationEvent
        {
            Timestamp = currentTime,
            PersonId = player.Id,
            EventType = SimulationEventType.DepartedAddress,
            FromAddressId = player.CurrentAddressId,
            ToAddressId = destinationAddressId
        });
    }

    var fromPosition = player.CurrentPosition;
    var travelHours = mapConfig.ComputeTravelTimeHours(fromPosition, destAddress.Position);
    var arrivalTime = currentTime.AddHours(travelHours);

    player.TravelInfo = new TravelInfo
    {
        FromPosition = fromPosition,
        ToPosition = destAddress.Position,
        DepartureTime = currentTime,
        ArrivalTime = arrivalTime,
        FromAddressId = player.CurrentAddressId,
        ToAddressId = destinationAddressId
    };

    // Clear current address — player is now in transit
    player.CurrentAddressId = 0;
}
```

Note: We use `0` for "no address" rather than making `CurrentAddressId` nullable on Player, since the existing `Player.CurrentAddressId` is `int` not `int?`. If the implementer prefers, this can be changed to nullable to match Person, but that's a larger refactor.

- [ ] **Step 9: Run tests to verify they pass**

Run: `dotnet test stakeout.tests/ --filter "PlayerTravelTests" -v minimal`
Expected: All PASS.

- [ ] **Step 10: Run all tests**

Run: `dotnet test stakeout.tests/ -v minimal`
Expected: All tests pass.

- [ ] **Step 11: Commit**

```
git add src/simulation/SimulationManager.cs stakeout.tests/Simulation/PlayerTravelTests.cs
git commit -m "feat: add player travel interpolation, arrival, and interruption"
```

---

## Task 3: Rename simulation_debug → city

**Files:**
- Rename: `scenes/simulation_debug/` → `scenes/city/`
- Rename: `SimulationDebug.tscn` → `CityView.tscn`
- Rename: `SimulationDebug.cs` → `CityView.cs`
- Modify: Class name in `CityView.cs`
- Modify: Script path in `CityView.tscn`
- Modify: `EvidenceBoardScene.cs` (will point to GameShell later, but for now update the path so it compiles)
- Modify: `MainMenu.cs` (same — temporary update to `CityView.tscn`, will change to `GameShell.tscn` in Task 4)

- [ ] **Step 1: Create the city directory and move files**

```bash
mkdir -p scenes/city
git mv scenes/simulation_debug/SimulationDebug.cs scenes/city/CityView.cs
git mv scenes/simulation_debug/SimulationDebug.tscn scenes/city/CityView.tscn
rmdir scenes/simulation_debug 2>/dev/null; true
```

- [ ] **Step 2: Rename the class in CityView.cs**

In `scenes/city/CityView.cs`, replace:
- `public partial class SimulationDebug : Control` → `public partial class CityView : Control`

- [ ] **Step 3: Update the script path in CityView.tscn**

In `scenes/city/CityView.tscn`, replace:
- `path="res://scenes/simulation_debug/SimulationDebug.cs"` → `path="res://scenes/city/CityView.cs"`
- `[node name="SimulationDebug"` → `[node name="CityView"`

- [ ] **Step 4: Update scene references in EvidenceBoardScene.cs**

In `scenes/evidence_board/EvidenceBoardScene.cs`, change `OnClosePressed`:
- `"res://scenes/simulation_debug/SimulationDebug.tscn"` → `"res://scenes/city/CityView.tscn"` (temporary — will change to GameShell in Task 4)

- [ ] **Step 5: Update scene reference in MainMenu.cs**

In `scenes/main_menu/MainMenu.cs`, change `_OnNewCareerPressed`:
- `"res://scenes/simulation_debug/SimulationDebug.tscn"` → `"res://scenes/city/CityView.tscn"` (temporary — will change to GameShell in Task 4)

- [ ] **Step 6: Verify build**

Run: `dotnet build stakeout.sln`
Expected: Build succeeds with no errors.

- [ ] **Step 7: Run all tests**

Run: `dotnet test stakeout.tests/ -v minimal`
Expected: All tests pass.

- [ ] **Step 8: Commit**

```
git add -A
git commit -m "refactor: rename simulation_debug to city, class SimulationDebug to CityView"
```

---

## Task 4: Create GameShell Scene

**Files:**
- Create: `scenes/game_shell/GameShell.tscn`
- Create: `scenes/game_shell/GameShell.cs`
- Modify: `scenes/city/CityView.tscn` — strip sidebar, clock, time controls
- Modify: `scenes/city/CityView.cs` — remove sidebar/clock/debug code, add menu item provider interface
- Modify: `scenes/main_menu/MainMenu.cs` — point to GameShell
- Modify: `scenes/evidence_board/EvidenceBoardScene.cs` — point to GameShell
- Modify: `src/GameManager.cs` — add ActiveContentView state

This is the largest task. It involves creating the shell, stripping CityView, and wiring everything together.

- [ ] **Step 1: Add ActiveContentView to GameManager**

In `src/GameManager.cs`, add a field to track what content view GameShell should load:

```csharp
public string ActiveContentView { get; set; } = "res://scenes/city/CityView.tscn";
```

- [ ] **Step 2: Create GameShell.cs**

Create `scenes/game_shell/GameShell.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Stakeout;
using Stakeout.Evidence;
using Stakeout.Simulation;
using Stakeout.Simulation.Entities;

/// <summary>
/// Interface implemented by content views that live inside GameShell's content area.
/// </summary>
public interface IContentView
{
    void SetGameShell(GameShell shell);
}

public partial class GameShell : Control
{
    private GameManager _gameManager;
    private SimulationManager _simulationManager;

    // Sidebar elements
    private Label _clockLabel;
    private HBoxContainer _timeControls;
    private Button _pauseButton;
    private Button _playButton;
    private Button _fastButton;
    private Button _superFastButton;
    private VBoxContainer _menuContainer;

    // Content area
    private Control _contentArea;
    private Control _currentContentView;

    // Debug
    private Button _debugMenuButton;
    private PanelContainer _debugSidebar;
    private VBoxContainer _debugPeopleList;
    private bool _debugVisible;

    public override void _Ready()
    {
        _gameManager = GetNode<GameManager>("/root/GameManager");
        _simulationManager = _gameManager.SimulationManager;

        SetupSidebar();
        SetupContentArea();
        SetupDebugPanel();

        // Load the active content view
        LoadContentView(_gameManager.ActiveContentView);
    }

    private void SetupSidebar()
    {
        var sidebar = GetNode<PanelContainer>("LeftSidebar");
        var sidebarStyle = new StyleBoxFlat { BgColor = new Color(0, 0, 0, 1) };
        sidebar.AddThemeStyleboxOverride("panel", sidebarStyle);

        _clockLabel = GetNode<Label>("LeftSidebar/VBox/ClockLabel");
        _timeControls = GetNode<HBoxContainer>("LeftSidebar/VBox/TimeControls");

        _pauseButton = GetNode<Button>("LeftSidebar/VBox/TimeControls/PauseButton");
        _playButton = GetNode<Button>("LeftSidebar/VBox/TimeControls/PlayButton");
        _fastButton = GetNode<Button>("LeftSidebar/VBox/TimeControls/FastButton");
        _superFastButton = GetNode<Button>("LeftSidebar/VBox/TimeControls/SuperFastButton");

        _pauseButton.Pressed += () => SetTimeScale(0f);
        _playButton.Pressed += () => SetTimeScale(1f);
        _fastButton.Pressed += () => SetTimeScale(32f);
        _superFastButton.Pressed += () => SetTimeScale(64f);

        HighlightActiveTimeButton();

        _menuContainer = GetNode<VBoxContainer>("LeftSidebar/VBox/MenuContainer");
    }

    private void SetupContentArea()
    {
        _contentArea = GetNode<Control>("ContentArea");
    }

    private void SetupDebugPanel()
    {
        _debugMenuButton = GetNode<Button>("DebugMenuButton");
        _debugMenuButton.Pressed += OnDebugMenuPressed;

        _debugSidebar = GetNode<PanelContainer>("DebugSidebar");
        var debugStyle = new StyleBoxFlat { BgColor = new Color(0.1f, 0.1f, 0.1f, 0.95f) };
        _debugSidebar.AddThemeStyleboxOverride("panel", debugStyle);
        _debugSidebar.Visible = false;

        var scroll = _debugSidebar.GetNode<ScrollContainer>("ScrollContainer");
        _debugPeopleList = scroll.GetNode<VBoxContainer>("PeopleList");
    }

    public void LoadContentView(string scenePath)
    {
        // Remove current content view
        if (_currentContentView != null)
        {
            _currentContentView.QueueFree();
            _currentContentView = null;
        }

        var scene = GD.Load<PackedScene>(scenePath);
        _currentContentView = scene.Instantiate<Control>();
        _contentArea.AddChild(_currentContentView);

        // Store on GameManager for scene-change restoration
        _gameManager.ActiveContentView = scenePath;

        // Let the content view know about us so it can set menu items.
        // Use C# interface check — Godot's Call/HasMethod doesn't reliably
        // pass typed C# objects through Variant.
        if (_currentContentView is IContentView contentView)
        {
            contentView.SetGameShell(this);
        }
    }

    public void SetMenuItems(Godot.Collections.Array<Godot.Collections.Dictionary> items)
    {
        // Clear existing menu items
        foreach (var child in _menuContainer.GetChildren())
            child.QueueFree();

        var font = _clockLabel.GetThemeFont("font");

        foreach (var item in items)
        {
            var label = (string)item["label"];
            var btn = new Button
            {
                Text = label,
                Flat = true,
                Alignment = HorizontalAlignment.Left
            };
            btn.AddThemeFontOverride("font", font);
            btn.AddThemeFontSizeOverride("font_size", 16);
            btn.AddThemeColorOverride("font_color", new Color(1, 1, 1));
            btn.AddThemeColorOverride("font_hover_color", new Color(0.3f, 0.6f, 1.0f));

            if (item.ContainsKey("callback"))
            {
                var callback = (Callable)item["callback"];
                btn.Pressed += () => callback.Call();
            }

            _menuContainer.AddChild(btn);
        }
    }

    public void OpenEvidenceBoard()
    {
        GetTree().ChangeSceneToFile("res://scenes/evidence_board/EvidenceBoard.tscn");
    }

    private void SetTimeScale(float scale)
    {
        _simulationManager.State.Clock.TimeScale = scale;
        HighlightActiveTimeButton();
    }

    private void HighlightActiveTimeButton()
    {
        var scale = _simulationManager.State.Clock.TimeScale;
        var activeColor = new Color(0.3f, 0.6f, 1.0f);
        var normalColor = new Color(1f, 1f, 1f);

        _pauseButton.Modulate = scale == 0f ? activeColor : normalColor;
        _playButton.Modulate = scale == 1f ? activeColor : normalColor;
        _fastButton.Modulate = scale == 32f ? activeColor : normalColor;
        _superFastButton.Modulate = scale == 64f ? activeColor : normalColor;
    }

    public override void _Process(double delta)
    {
        var time = _simulationManager.State.Clock.CurrentTime;
        _clockLabel.Text = time.ToString("ddd MMM dd, yyyy HH:mm:ss");
    }

    private void OnDebugMenuPressed()
    {
        _debugSidebar.Visible = !_debugSidebar.Visible;
        if (_debugSidebar.Visible)
            PopulateDebugPeopleList();
    }

    private void PopulateDebugPeopleList()
    {
        foreach (var child in _debugPeopleList.GetChildren())
            child.QueueFree();

        var header = new Label { Text = "— People —" };
        header.AddThemeFontOverride("font", _clockLabel.GetThemeFont("font"));
        header.AddThemeFontSizeOverride("font_size", 14);
        header.AddThemeColorOverride("font_color", new Color(0.3f, 0.6f, 1.0f));
        header.HorizontalAlignment = HorizontalAlignment.Center;
        _debugPeopleList.AddChild(header);

        var people = _simulationManager.State.People.Values
            .OrderBy(p => p.FullName)
            .ToList();

        var board = _gameManager.EvidenceBoard;
        var font = _clockLabel.GetThemeFont("font");

        foreach (var person in people)
        {
            var btn = new Button { Text = person.FullName };
            btn.AddThemeFontOverride("font", font);
            btn.AddThemeFontSizeOverride("font_size", 12);
            btn.Alignment = HorizontalAlignment.Left;

            var personId = person.Id;
            btn.GuiInput += (@event) =>
            {
                if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Right && mb.Pressed)
                {
                    ShowAddToEvidenceBoardMenu(mb.GlobalPosition, EvidenceEntityType.Person, personId);
                    btn.AcceptEvent();
                }
            };

            _debugPeopleList.AddChild(btn);
        }
    }

    private void ShowAddToEvidenceBoardMenu(Vector2 pos, EvidenceEntityType entityType, int entityId)
    {
        var board = _gameManager.EvidenceBoard;
        var menu = new PopupMenu();
        var alreadyOnBoard = board.HasItem(entityType, entityId);

        menu.AddItem(alreadyOnBoard ? "Already on Board" : "Add to Evidence Board", 0);
        if (alreadyOnBoard)
            menu.SetItemDisabled(0, true);

        menu.IdPressed += (id) =>
        {
            if (id == 0 && !alreadyOnBoard)
            {
                var random = new System.Random();
                var centerX = 3840f / 2 + (float)(random.NextDouble() * 100 - 50);
                var centerY = 2160f / 2 + (float)(random.NextDouble() * 100 - 50);
                board.AddItem(entityType, entityId, new Vector2(centerX, centerY));
            }
            menu.QueueFree();
        };
        menu.PopupHide += () => menu.QueueFree();

        AddChild(menu);
        menu.Position = new Vector2I((int)pos.X, (int)pos.Y);
        menu.Popup();
    }
}
```

- [ ] **Step 3: Create GameShell.tscn**

Create `scenes/game_shell/GameShell.tscn`. Build it in the Godot editor or write as text. The scene tree:

```
GameShell (Control, full screen, anchors_preset=15)
├── LeftSidebar (PanelContainer, anchor_right=0.25, anchor_bottom=1.0)
│   └── VBox (VBoxContainer, layout_mode=2)
│       ├── ClockLabel (Label, font=EXEPixelPerfect, font_size=20, h_align=center)
│       ├── TimeControls (HBoxContainer)
│       │   ├── PauseButton (Button, text="⏸", font=EXEPixelPerfect, font_size=14)
│       │   ├── PlayButton (Button, text="▶", font=EXEPixelPerfect, font_size=14)
│       │   ├── FastButton (Button, text="▶▶", font=EXEPixelPerfect, font_size=14)
│       │   └── SuperFastButton (Button, text="▶▶▶", font=EXEPixelPerfect, font_size=14)
│       ├── Spacer (Control, custom_minimum_size.y=20)
│       └── MenuContainer (VBoxContainer)
├── ContentArea (Control, anchor_left=0.25, anchor_right=1.0, anchor_bottom=1.0)
├── DebugMenuButton (Button, bottom-right corner, text="Debug", font=EXEPixelPerfect, font_size=12)
└── DebugSidebar (PanelContainer, right side, anchor_left=0.75, anchor_right=1.0, anchor_bottom=1.0, visible=false)
    └── ScrollContainer (layout_mode=2)
        └── PeopleList (VBoxContainer)
```

Write this as a .tscn file. The implementer should reference the existing CityView.tscn for font resource IDs and style patterns.

- [ ] **Step 4: Strip CityView.tscn of sidebar, clock, time controls**

Remove from `scenes/city/CityView.tscn`:
- The `ClockLabel` node
- The `TimeControls` node and all children
- The `Sidebar` node and all children

Keep only:
- Root `CityView` (Control)
- `CityMap` and its children (MapBackground, LocationIcons, EntityDots)
- `HoverLabel`

- [ ] **Step 5: Strip CityView.cs of sidebar, clock, debug, and time control code**

Remove from `scenes/city/CityView.cs`:
- All sidebar-related fields and setup (`_debugMenuButton`, `_debugSidebar`, `_debugPeopleList`, `PopulateDebugPeopleList`, `OnDebugMenuPressed`)
- All time control fields and setup (`_pauseButton`, `_playButton`, etc., `SetTimeScale`, `HighlightActiveTimeButton`)
- The `_clockLabel` field and clock display in `_Process`
- The `OnEvidenceBoardPressed` method and evidence board button setup
- The `ShowAddToEvidenceBoardMenu` method (now in GameShell)

Add to `CityView.cs`:
- A `GameShell` reference field
- A `SetGameShell(GameShell shell)` method that stores the reference and calls `UpdateMenuItems()`
- An `UpdateMenuItems()` method that provides menu items to GameShell
- Update `_Process` to update player dot position from `Player.CurrentPosition`

The stripped `CityView.cs` should look approximately like:

```csharp
using System.Collections.Generic;
using System.Linq;
using Godot;
using Stakeout;
using Stakeout.Evidence;
using Stakeout.Simulation;
using Stakeout.Simulation.Entities;

public partial class CityView : Control, IContentView
{
    private GameManager _gameManager;
    private SimulationManager _simulationManager;
    private GameShell _gameShell;
    private Label _hoverLabel;
    private Control _locationIcons;
    private Control _entityDots;

    private const float LocationIconSize = 12f;
    private const float EntityDotSize = 8f;
    private const float HoverDistance = 10f;
    private const int IconBorderWidth = 2;
    private const int DotBorderWidth = 1;

    private readonly Dictionary<int, Panel> _addressNodes = [];
    private readonly Dictionary<int, Panel> _personNodes = [];
    private Panel _playerNode;
    private bool _wasPlayerTraveling;

    private static readonly Color SuburbanHomeColor = new(0.2f, 0.8f, 0.2f);
    private static readonly Color DinerColor = new(0.9f, 0.9f, 0.2f);
    private static readonly Color DiveBarColor = new(0.9f, 0.2f, 0.2f);
    private static readonly Color OfficeColor = new(0.2f, 0.7f, 0.9f);
    private static readonly Color PersonColor = new(1f, 1f, 1f);
    private static readonly Color SleepingPersonColor = new(0.5f, 0.5f, 0.5f);
    private static readonly Color PlayerColor = new(0.3f, 0.5f, 1f);
    private static readonly Color BorderColor = new(0f, 0f, 0f);

    public void SetGameShell(GameShell shell)
    {
        _gameShell = shell;
        UpdateMenuItems();
    }

    public override void _Ready()
    {
        _hoverLabel = GetNode<Label>("HoverLabel");
        _locationIcons = GetNode<Control>("CityMap/LocationIcons");
        _entityDots = GetNode<Control>("CityMap/EntityDots");

        _gameManager = GetNode<GameManager>("/root/GameManager");
        _simulationManager = _gameManager.SimulationManager;

        _simulationManager.AddressAdded += OnAddressAdded;
        _simulationManager.PersonAdded += OnPersonAdded;
        _simulationManager.PlayerCreated += OnPlayerCreated;

        foreach (var address in _simulationManager.State.Addresses.Values)
            OnAddressAdded(address);
        foreach (var person in _simulationManager.State.People.Values)
            OnPersonAdded(person);
        if (_simulationManager.State.Player != null)
            OnPlayerCreated();
    }

    private void UpdateMenuItems()
    {
        if (_gameShell == null) return;

        var items = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        var player = _simulationManager.State.Player;

        // "Enter Location" — only when player is at an address
        if (player?.TravelInfo == null && player?.CurrentAddressId > 0)
        {
            var enterItem = new Godot.Collections.Dictionary
            {
                { "label", "Enter Location" },
                { "callback", Callable.From(OnEnterLocation) }
            };
            items.Add(enterItem);
        }

        var ebItem = new Godot.Collections.Dictionary
        {
            { "label", "Evidence Board" },
            { "callback", Callable.From(() => _gameShell.OpenEvidenceBoard()) }
        };
        items.Add(ebItem);

        _gameShell.SetMenuItems(items);
    }

    private void OnEnterLocation()
    {
        _gameShell.LoadContentView("res://scenes/address/AddressView.tscn");
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Right && mb.Pressed)
        {
            TryShowContextMenu(mb.GlobalPosition);
        }
    }

    private void TryShowContextMenu(Vector2 mousePos)
    {
        var state = _simulationManager.State;
        var board = _gameManager.EvidenceBoard;

        // Check address icons first
        foreach (var (addressId, icon) in _addressNodes)
        {
            var center = icon.Position + new Vector2(LocationIconSize / 2, LocationIconSize / 2);
            if (mousePos.DistanceTo(center) <= HoverDistance)
            {
                ShowAddressContextMenu(mousePos, addressId, board);
                return;
            }
        }

        // Check person dots
        foreach (var (personId, dot) in _personNodes)
        {
            var center = dot.Position + new Vector2(EntityDotSize / 2, EntityDotSize / 2);
            if (mousePos.DistanceTo(center) <= HoverDistance)
            {
                ShowPersonContextMenu(mousePos, personId, board);
                return;
            }
        }
    }

    private void ShowAddressContextMenu(Vector2 pos, int addressId, EvidenceBoard board)
    {
        var menu = new PopupMenu();
        var alreadyOnBoard = board.HasItem(EvidenceEntityType.Address, addressId);

        menu.AddItem("Go here", 0);
        menu.AddItem(alreadyOnBoard ? "Already on Board" : "Add to Evidence Board", 1);
        if (alreadyOnBoard)
            menu.SetItemDisabled(1, true);

        menu.IdPressed += (id) =>
        {
            if (id == 0)
            {
                SimulationManager.StartPlayerTravel(_simulationManager.State, addressId, new MapConfig());
                UpdateMenuItems(); // "Enter Location" disappears while traveling
            }
            else if (id == 1 && !alreadyOnBoard)
            {
                var random = new System.Random();
                var centerX = 3840f / 2 + (float)(random.NextDouble() * 100 - 50);
                var centerY = 2160f / 2 + (float)(random.NextDouble() * 100 - 50);
                board.AddItem(EvidenceEntityType.Address, addressId, new Vector2(centerX, centerY));
            }
            menu.QueueFree();
        };
        menu.PopupHide += () => menu.QueueFree();

        AddChild(menu);
        menu.Position = new Vector2I((int)pos.X, (int)pos.Y);
        menu.Popup();
    }

    private void ShowPersonContextMenu(Vector2 pos, int personId, EvidenceBoard board)
    {
        var menu = new PopupMenu();
        var alreadyOnBoard = board.HasItem(EvidenceEntityType.Person, personId);

        menu.AddItem(alreadyOnBoard ? "Already on Board" : "Add to Evidence Board", 0);
        if (alreadyOnBoard)
            menu.SetItemDisabled(0, true);

        menu.IdPressed += (id) =>
        {
            if (id == 0 && !alreadyOnBoard)
            {
                var random = new System.Random();
                var centerX = 3840f / 2 + (float)(random.NextDouble() * 100 - 50);
                var centerY = 2160f / 2 + (float)(random.NextDouble() * 100 - 50);
                board.AddItem(EvidenceEntityType.Person, personId, new Vector2(centerX, centerY));
            }
            menu.QueueFree();
        };
        menu.PopupHide += () => menu.QueueFree();

        AddChild(menu);
        menu.Position = new Vector2I((int)pos.X, (int)pos.Y);
        menu.Popup();
    }

    public override void _Process(double delta)
    {
        // Update person dot positions and colors
        var size = new Vector2(EntityDotSize, EntityDotSize);
        foreach (var (personId, dot) in _personNodes)
        {
            var person = _simulationManager.State.People[personId];
            dot.Position = person.CurrentPosition - size / 2;

            var color = person.CurrentActivity == ActivityType.Sleeping ? SleepingPersonColor : PersonColor;
            var style = new StyleBoxFlat
            {
                BgColor = color,
                BorderColor = BorderColor,
                BorderWidthLeft = DotBorderWidth,
                BorderWidthRight = DotBorderWidth,
                BorderWidthTop = DotBorderWidth,
                BorderWidthBottom = DotBorderWidth
            };
            dot.AddThemeStyleboxOverride("panel", style);
        }

        // Update player dot position
        if (_playerNode != null && _simulationManager.State.Player != null)
        {
            _playerNode.Position = _simulationManager.State.Player.CurrentPosition - size / 2;
        }

        // Refresh menu items only when travel state changes (not every frame)
        var player = _simulationManager.State.Player;
        var isTraveling = player?.TravelInfo != null;
        if (_wasPlayerTraveling && !isTraveling)
        {
            UpdateMenuItems(); // Player just arrived
        }
        _wasPlayerTraveling = isTraveling;

        UpdateHoverLabel();
    }

    // ... (keep all existing OnAddressAdded, OnPersonAdded, OnPlayerCreated, UpdateHoverLabel, CreateIconPanel, GetAddressColor methods)
    // OnPlayerCreated changes slightly — use Player.CurrentPosition instead of looking up address:
    private void OnPlayerCreated()
    {
        var player = _simulationManager.State.Player;
        var size = new Vector2(EntityDotSize, EntityDotSize);
        _playerNode = CreateIconPanel(size, PlayerColor, BorderColor, DotBorderWidth);
        _playerNode.Position = player.CurrentPosition - size / 2;
        _entityDots.AddChild(_playerNode);
    }

    // Keep these methods unchanged:
    // OnAddressAdded, OnPersonAdded, UpdateHoverLabel, CreateIconPanel, GetAddressColor
}
```

- [ ] **Step 6: Update MainMenu.cs to point to GameShell**

Change in `scenes/main_menu/MainMenu.cs`:
```csharp
private void _OnNewCareerPressed()
{
    GetTree().ChangeSceneToFile("res://scenes/game_shell/GameShell.tscn");
}
```

- [ ] **Step 7: Update EvidenceBoardScene.cs to return to GameShell**

Change in `scenes/evidence_board/EvidenceBoardScene.cs`:
```csharp
private void OnClosePressed()
{
    GetTree().ChangeSceneToFile("res://scenes/game_shell/GameShell.tscn");
}
```

- [ ] **Step 8: Verify build**

Run: `dotnet build stakeout.sln`
Expected: Build succeeds.

- [ ] **Step 9: Run all tests**

Run: `dotnet test stakeout.tests/ -v minimal`
Expected: All pass.

- [ ] **Step 10: Manual smoke test in Godot**

Launch the game from the Godot editor. Verify:
- MainMenu → New Career loads GameShell with CityView inside
- Left sidebar shows clock, time controls, and "Evidence Board" menu item
- Clock ticks and time controls work
- Debug button in bottom-right toggles right-hand debug sidebar
- City map shows addresses and NPCs as before
- Evidence Board button works and returns to GameShell

- [ ] **Step 11: Commit**

```
git add -A
git commit -m "feat: create GameShell with sidebar, extract CityView as content view"
```

---

## Task 5: Player Movement on City Map

**Files:**
- Modify: `scenes/city/CityView.cs` (already has context menu from Task 4, wire up "Go here")

This task is mostly already implemented in Tasks 2 and 4. The key remaining piece is verifying the end-to-end flow works.

- [ ] **Step 1: Manual smoke test of player movement**

Launch the game. On the city map:
1. Right-click an address icon → verify "Go here" appears in context menu
2. Click "Go here" → verify the player's blue dot starts moving toward the address
3. Verify "Enter Location" disappears from the sidebar while traveling
4. Speed up time → verify the dot arrives
5. On arrival, verify "Enter Location" reappears in the sidebar
6. While traveling, right-click a different address → "Go here" → verify the player redirects

- [ ] **Step 2: Fix any issues found during smoke test**

Address bugs found during manual testing.

- [ ] **Step 3: Commit if fixes were needed**

```
git add -A
git commit -m "fix: player movement smoke test fixes"
```

---

## Task 6: Address Content View

**Files:**
- Create: `scenes/address/AddressView.tscn`
- Create: `scenes/address/AddressView.cs`

- [ ] **Step 1: Create AddressView.cs**

```csharp
using System.Collections.Generic;
using System.Linq;
using Godot;
using Stakeout;
using Stakeout.Evidence;
using Stakeout.Simulation;
using Stakeout.Simulation.Entities;

public partial class AddressView : Control, IContentView
{
    private GameManager _gameManager;
    private SimulationManager _simulationManager;
    private GameShell _gameShell;
    private Label _addressLabel;

    public void SetGameShell(GameShell shell)
    {
        _gameShell = shell;
        ShowDefaultMenu();
    }

    public override void _Ready()
    {
        _gameManager = GetNode<GameManager>("/root/GameManager");
        _simulationManager = _gameManager.SimulationManager;

        _addressLabel = GetNode<Label>("AddressLabel");

        var player = _simulationManager.State.Player;
        if (player != null && _simulationManager.State.Addresses.TryGetValue(player.CurrentAddressId, out var address))
        {
            var street = _simulationManager.State.Streets[address.StreetId];
            _addressLabel.Text = $"{address.Number} {street.Name}\n({address.Type})";
        }
    }

    private void ShowDefaultMenu()
    {
        if (_gameShell == null) return;

        var items = new Godot.Collections.Array<Godot.Collections.Dictionary>();

        items.Add(new Godot.Collections.Dictionary
        {
            { "label", "List People" },
            { "callback", Callable.From(ShowPeopleMenu) }
        });

        items.Add(new Godot.Collections.Dictionary
        {
            { "label", "Evidence Board" },
            { "callback", Callable.From(() => _gameShell.OpenEvidenceBoard()) }
        });

        items.Add(new Godot.Collections.Dictionary
        {
            { "label", "Leave" },
            { "callback", Callable.From(OnLeave) }
        });

        _gameShell.SetMenuItems(items);
    }

    private void ShowPeopleMenu()
    {
        if (_gameShell == null) return;

        var items = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        var player = _simulationManager.State.Player;
        var state = _simulationManager.State;

        if (player != null && state.Addresses.TryGetValue(player.CurrentAddressId, out var address))
        {
            var peopleHere = state.People.Values
                .Where(p => p.CurrentAddressId.HasValue && p.CurrentAddressId.Value == address.Id)
                .OrderBy(p => p.FullName)
                .ToList();

            foreach (var person in peopleHere)
            {
                var personId = person.Id;
                items.Add(new Godot.Collections.Dictionary
                {
                    { "label", person.FullName },
                    { "callback", Callable.From(() => { /* left-click does nothing for now */ }) },
                    { "personId", personId }
                });
            }

            if (peopleHere.Count == 0)
            {
                items.Add(new Godot.Collections.Dictionary
                {
                    { "label", "(nobody here)" }
                });
            }
        }

        items.Add(new Godot.Collections.Dictionary
        {
            { "label", "Go back" },
            { "callback", Callable.From(ShowDefaultMenu) }
        });

        _gameShell.SetMenuItems(items);
    }

    private void OnLeave()
    {
        _gameShell.LoadContentView("res://scenes/city/CityView.tscn");
    }
}
```

- [ ] **Step 2: Create AddressView.tscn**

A minimal scene:

```
AddressView (Control, full screen, anchors_preset=15)
└── AddressLabel (Label, centered in content area, font=EXEPixelPerfect, font_size=24)
```

The label should be positioned roughly centered in the content area.

- [ ] **Step 3: Add right-click context menu support for people in the sidebar**

In `GameShell.cs`, update `SetMenuItems` to support right-click on menu items that have a `personId` key. When the user right-clicks such an item, show the "Add to Evidence Board" popup:

Add to the button creation loop in `SetMenuItems`:

```csharp
if (item.ContainsKey("personId"))
{
    var personId = (int)item["personId"];
    btn.GuiInput += (@event) =>
    {
        if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Right && mb.Pressed)
        {
            ShowAddToEvidenceBoardMenu(mb.GlobalPosition, EvidenceEntityType.Person, personId);
            btn.AcceptEvent();
        }
    };
}
```

- [ ] **Step 4: Verify build**

Run: `dotnet build stakeout.sln`
Expected: Build succeeds.

- [ ] **Step 5: Run all tests**

Run: `dotnet test stakeout.tests/ -v minimal`
Expected: All pass.

- [ ] **Step 6: Manual smoke test**

1. Travel to an address, wait to arrive
2. Click "Enter Location" → verify AddressView loads with address name
3. Click "List People" → verify people at this location are listed (speed up time to get NPCs to arrive if needed)
4. Right-click a person → verify "Add to Evidence Board" context menu
5. Click "Go back" → verify default address menu restores
6. Click "Leave" → verify CityView loads, player still at address on map
7. Click "Evidence Board" from address view → verify it opens and returns correctly

- [ ] **Step 7: Commit**

```
git add -A
git commit -m "feat: add AddressView content scene with people list submenu"
```

---

## Task 7: Final Polish and Verification

- [ ] **Step 1: Run full test suite**

Run: `dotnet test stakeout.tests/ -v minimal`
Expected: All pass.

- [ ] **Step 2: Full end-to-end manual test**

Walk through the complete flow:
1. Main menu → New Career → GameShell loads with CityView
2. Sidebar: clock ticking, time controls work, "Evidence Board" menu item
3. Right-click address → "Go here" → player dot moves
4. Right-click different address mid-travel → player redirects
5. Arrive → "Enter Location" appears
6. Enter Location → AddressView loads
7. List People → see NPCs, right-click to add to evidence board
8. Go back → default menu
9. Leave → CityView returns
10. Evidence Board → opens, close returns to correct view
11. Debug button → right-side debug panel toggles

- [ ] **Step 3: Fix any remaining issues**

- [ ] **Step 4: Commit if fixes were needed**

```
git add -A
git commit -m "fix: final polish for player movement and game shell"
```

- [ ] **Step 5: Update architecture docs**

Invoke the `update-docs` skill to check if architecture docs need updating.
