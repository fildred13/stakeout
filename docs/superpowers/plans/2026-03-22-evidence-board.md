# Evidence Board Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the evidence board — a pannable/zoomable corkboard where the player pins polaroid evidence items and connects them with red strings.

**Architecture:** Pure C# data model in `/src/evidence/` (decoupled from simulation), Godot scenes in `/scenes/evidence_board/`. A new `GameManager` autoload singleton owns both `SimulationState` and `EvidenceBoard`, replacing the current pattern of `SimulationManager` as a child of `SimulationDebug`. Scene scripts are thin UI wrappers that read from the data model.

**Tech Stack:** Godot 4.6, C# (.NET 8), xUnit for tests

**Spec:** `docs/superpowers/specs/2026-03-22-evidence-board-design.md`

---

## File Structure

### New Files — Data Model (`/src/evidence/`)
- `src/evidence/EvidenceEntityType.cs` — enum: Person, Address
- `src/evidence/EvidenceItem.cs` — board item: Id, EntityType, EntityId, BoardPosition
- `src/evidence/EvidenceConnection.cs` — normalized pair with Equals/GetHashCode
- `src/evidence/EvidenceBoard.cs` — central board state: items, connections, add/remove/query

### New Files — Tests (`/stakeout.tests/Evidence/`)
- `stakeout.tests/Evidence/EvidenceConnectionTests.cs`
- `stakeout.tests/Evidence/EvidenceBoardTests.cs`

### New Files — Scenes (`/scenes/evidence_board/`)
- `scenes/evidence_board/EvidenceBoard.tscn` — corkboard scene
- `scenes/evidence_board/EvidenceBoard.cs` — board script (pan/zoom, polaroid management, string drawing)
- `scenes/evidence_board/EvidencePolaroid.tscn` — polaroid packed scene
- `scenes/evidence_board/EvidencePolaroid.cs` — polaroid script (drag, click, right-click)
- `scenes/evidence_board/DossierWindow.tscn` — floating dossier panel
- `scenes/evidence_board/DossierWindow.cs` — dossier script (drag, populate content)
- `scenes/evidence_board/StringLayer.cs` — custom draw layer for red strings

### New Files — GameManager
- `src/GameManager.cs` — autoload singleton owning SimulationState + EvidenceBoard

### Modified Files
- `src/simulation/SimulationManager.cs` — remove SimulationState ownership, receive it via constructor
- `scenes/simulation_debug/SimulationDebug.cs` — get SimulationManager from GameManager instead of instantiating it; add sidebar and right-click context menus
- `scenes/simulation_debug/SimulationDebug.tscn` — add sidebar VBoxContainer
- `project.godot` — register GameManager autoload

---

## Task 1: EvidenceEntityType and EvidenceItem

**Files:**
- Create: `src/evidence/EvidenceEntityType.cs`
- Create: `src/evidence/EvidenceItem.cs`

- [ ] **Step 1: Create `EvidenceEntityType.cs`**

```csharp
namespace Stakeout.Evidence;

public enum EvidenceEntityType
{
    Person,
    Address
}
```

- [ ] **Step 2: Create `EvidenceItem.cs`**

```csharp
using Godot;

namespace Stakeout.Evidence;

public class EvidenceItem
{
    public int Id { get; set; }
    public EvidenceEntityType EntityType { get; set; }
    public int EntityId { get; set; }
    public Vector2 BoardPosition { get; set; }
}
```

- [ ] **Step 3: Verify build**

Run: `dotnet build stakeout.csproj`
Expected: Build succeeds

- [ ] **Step 4: Commit**

```
git add src/evidence/EvidenceEntityType.cs src/evidence/EvidenceItem.cs
git commit -m "Add EvidenceEntityType enum and EvidenceItem class"
```

---

## Task 2: EvidenceConnection with Tests

**Files:**
- Create: `src/evidence/EvidenceConnection.cs`
- Create: `stakeout.tests/Evidence/EvidenceConnectionTests.cs`

- [ ] **Step 1: Write failing tests for EvidenceConnection**

Create `stakeout.tests/Evidence/EvidenceConnectionTests.cs`:

```csharp
using Stakeout.Evidence;
using Xunit;

namespace Stakeout.Tests.Evidence;

public class EvidenceConnectionTests
{
    [Fact]
    public void Constructor_NormalizesOrder_SmallerIdFirst()
    {
        var conn = new EvidenceConnection(5, 3);

        Assert.Equal(3, conn.FromItemId);
        Assert.Equal(5, conn.ToItemId);
    }

    [Fact]
    public void Constructor_AlreadyNormalized_PreservesOrder()
    {
        var conn = new EvidenceConnection(2, 7);

        Assert.Equal(2, conn.FromItemId);
        Assert.Equal(7, conn.ToItemId);
    }

    [Fact]
    public void Equals_SamePair_ReturnsTrue()
    {
        var a = new EvidenceConnection(1, 2);
        var b = new EvidenceConnection(1, 2);

        Assert.Equal(a, b);
    }

    [Fact]
    public void Equals_ReversedPair_ReturnsTrue()
    {
        var a = new EvidenceConnection(1, 2);
        var b = new EvidenceConnection(2, 1);

        Assert.Equal(a, b);
    }

    [Fact]
    public void Equals_DifferentPair_ReturnsFalse()
    {
        var a = new EvidenceConnection(1, 2);
        var b = new EvidenceConnection(1, 3);

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void GetHashCode_ReversedPair_SameHash()
    {
        var a = new EvidenceConnection(1, 2);
        var b = new EvidenceConnection(2, 1);

        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test stakeout.tests/ --filter "FullyQualifiedName~EvidenceConnectionTests"`
Expected: Build error — `EvidenceConnection` does not exist

- [ ] **Step 3: Implement EvidenceConnection**

Create `src/evidence/EvidenceConnection.cs`:

```csharp
using System;

namespace Stakeout.Evidence;

public class EvidenceConnection : IEquatable<EvidenceConnection>
{
    public int FromItemId { get; }
    public int ToItemId { get; }

    public EvidenceConnection(int itemIdA, int itemIdB)
    {
        FromItemId = Math.Min(itemIdA, itemIdB);
        ToItemId = Math.Max(itemIdA, itemIdB);
    }

    public bool Equals(EvidenceConnection other)
    {
        if (other is null) return false;
        return FromItemId == other.FromItemId && ToItemId == other.ToItemId;
    }

    public override bool Equals(object obj) => Equals(obj as EvidenceConnection);

    public override int GetHashCode() => HashCode.Combine(FromItemId, ToItemId);
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test stakeout.tests/ --filter "FullyQualifiedName~EvidenceConnectionTests"`
Expected: All 6 tests pass

- [ ] **Step 5: Commit**

```
git add src/evidence/EvidenceConnection.cs stakeout.tests/Evidence/EvidenceConnectionTests.cs
git commit -m "Add EvidenceConnection with normalized ordering and equality"
```

---

## Task 3: EvidenceBoard with Tests

**Files:**
- Create: `src/evidence/EvidenceBoard.cs`
- Create: `stakeout.tests/Evidence/EvidenceBoardTests.cs`

- [ ] **Step 1: Write failing tests for EvidenceBoard**

Create `stakeout.tests/Evidence/EvidenceBoardTests.cs`:

```csharp
using System.Linq;
using Godot;
using Stakeout.Evidence;
using Xunit;

namespace Stakeout.Tests.Evidence;

public class EvidenceBoardTests
{
    [Fact]
    public void AddItem_ReturnsItemWithCorrectFields()
    {
        var board = new EvidenceBoard();
        var item = board.AddItem(EvidenceEntityType.Person, 42, new Vector2(100, 200));

        Assert.Equal(EvidenceEntityType.Person, item.EntityType);
        Assert.Equal(42, item.EntityId);
        Assert.Equal(new Vector2(100, 200), item.BoardPosition);
    }

    [Fact]
    public void AddItem_AssignsUniqueIds()
    {
        var board = new EvidenceBoard();
        var item1 = board.AddItem(EvidenceEntityType.Person, 1, Vector2.Zero);
        var item2 = board.AddItem(EvidenceEntityType.Address, 2, Vector2.Zero);

        Assert.NotEqual(item1.Id, item2.Id);
    }

    [Fact]
    public void HasItem_ExistingItem_ReturnsTrue()
    {
        var board = new EvidenceBoard();
        board.AddItem(EvidenceEntityType.Person, 42, Vector2.Zero);

        Assert.True(board.HasItem(EvidenceEntityType.Person, 42));
    }

    [Fact]
    public void HasItem_WrongType_ReturnsFalse()
    {
        var board = new EvidenceBoard();
        board.AddItem(EvidenceEntityType.Person, 42, Vector2.Zero);

        Assert.False(board.HasItem(EvidenceEntityType.Address, 42));
    }

    [Fact]
    public void HasItem_NoItems_ReturnsFalse()
    {
        var board = new EvidenceBoard();

        Assert.False(board.HasItem(EvidenceEntityType.Person, 1));
    }

    [Fact]
    public void RemoveItem_RemovesFromDictionary()
    {
        var board = new EvidenceBoard();
        var item = board.AddItem(EvidenceEntityType.Person, 42, Vector2.Zero);

        board.RemoveItem(item.Id);

        Assert.False(board.HasItem(EvidenceEntityType.Person, 42));
        Assert.Empty(board.Items);
    }

    [Fact]
    public void RemoveItem_AlsoRemovesAttachedConnections()
    {
        var board = new EvidenceBoard();
        var a = board.AddItem(EvidenceEntityType.Person, 1, Vector2.Zero);
        var b = board.AddItem(EvidenceEntityType.Person, 2, Vector2.Zero);
        var c = board.AddItem(EvidenceEntityType.Person, 3, Vector2.Zero);
        board.AddConnection(a.Id, b.Id);
        board.AddConnection(a.Id, c.Id);
        board.AddConnection(b.Id, c.Id);

        board.RemoveItem(a.Id);

        // Only b<->c connection should remain
        Assert.Single(board.Connections);
        Assert.Contains(board.Connections, conn =>
            conn.FromItemId == b.Id && conn.ToItemId == c.Id ||
            conn.FromItemId == c.Id && conn.ToItemId == b.Id);
    }

    [Fact]
    public void AddConnection_StoresConnection()
    {
        var board = new EvidenceBoard();
        var a = board.AddItem(EvidenceEntityType.Person, 1, Vector2.Zero);
        var b = board.AddItem(EvidenceEntityType.Person, 2, Vector2.Zero);

        board.AddConnection(a.Id, b.Id);

        Assert.Single(board.Connections);
    }

    [Fact]
    public void AddConnection_DuplicateIgnored()
    {
        var board = new EvidenceBoard();
        var a = board.AddItem(EvidenceEntityType.Person, 1, Vector2.Zero);
        var b = board.AddItem(EvidenceEntityType.Person, 2, Vector2.Zero);

        board.AddConnection(a.Id, b.Id);
        board.AddConnection(a.Id, b.Id);
        board.AddConnection(b.Id, a.Id); // reversed duplicate

        Assert.Single(board.Connections);
    }

    [Fact]
    public void RemoveConnection_RemovesSpecificConnection()
    {
        var board = new EvidenceBoard();
        var a = board.AddItem(EvidenceEntityType.Person, 1, Vector2.Zero);
        var b = board.AddItem(EvidenceEntityType.Person, 2, Vector2.Zero);
        board.AddConnection(a.Id, b.Id);

        board.RemoveConnection(a.Id, b.Id);

        Assert.Empty(board.Connections);
    }

    [Fact]
    public void RemoveConnection_ReversedOrder_StillRemoves()
    {
        var board = new EvidenceBoard();
        var a = board.AddItem(EvidenceEntityType.Person, 1, Vector2.Zero);
        var b = board.AddItem(EvidenceEntityType.Person, 2, Vector2.Zero);
        board.AddConnection(a.Id, b.Id);

        board.RemoveConnection(b.Id, a.Id);

        Assert.Empty(board.Connections);
    }

    [Fact]
    public void RemoveAllConnections_RemovesOnlyTargetItems()
    {
        var board = new EvidenceBoard();
        var a = board.AddItem(EvidenceEntityType.Person, 1, Vector2.Zero);
        var b = board.AddItem(EvidenceEntityType.Person, 2, Vector2.Zero);
        var c = board.AddItem(EvidenceEntityType.Person, 3, Vector2.Zero);
        board.AddConnection(a.Id, b.Id);
        board.AddConnection(a.Id, c.Id);
        board.AddConnection(b.Id, c.Id);

        board.RemoveAllConnections(a.Id);

        Assert.Single(board.Connections);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test stakeout.tests/ --filter "FullyQualifiedName~EvidenceBoardTests"`
Expected: Build error — `EvidenceBoard` does not exist

- [ ] **Step 3: Implement EvidenceBoard**

Create `src/evidence/EvidenceBoard.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Stakeout.Evidence;

public class EvidenceBoard
{
    public Dictionary<int, EvidenceItem> Items { get; } = new();
    public List<EvidenceConnection> Connections { get; } = new();

    private int _nextItemId = 1;

    public EvidenceItem AddItem(EvidenceEntityType entityType, int entityId, Vector2 boardPosition)
    {
        var item = new EvidenceItem
        {
            Id = _nextItemId++,
            EntityType = entityType,
            EntityId = entityId,
            BoardPosition = boardPosition
        };
        Items[item.Id] = item;
        return item;
    }

    public void RemoveItem(int itemId)
    {
        Items.Remove(itemId);
        Connections.RemoveAll(c => c.FromItemId == itemId || c.ToItemId == itemId);
    }

    public bool HasItem(EvidenceEntityType entityType, int entityId)
    {
        return Items.Values.Any(i => i.EntityType == entityType && i.EntityId == entityId);
    }

    public void AddConnection(int fromItemId, int toItemId)
    {
        var conn = new EvidenceConnection(fromItemId, toItemId);
        if (!Connections.Contains(conn))
        {
            Connections.Add(conn);
        }
    }

    public void RemoveConnection(int fromItemId, int toItemId)
    {
        var conn = new EvidenceConnection(fromItemId, toItemId);
        Connections.Remove(conn);
    }

    public void RemoveAllConnections(int itemId)
    {
        Connections.RemoveAll(c => c.FromItemId == itemId || c.ToItemId == itemId);
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test stakeout.tests/ --filter "FullyQualifiedName~EvidenceBoardTests"`
Expected: All 12 tests pass

- [ ] **Step 5: Run all tests to check for regressions**

Run: `dotnet test stakeout.tests/`
Expected: All tests pass (existing 34 + new 18 = 52)

- [ ] **Step 6: Commit**

```
git add src/evidence/EvidenceBoard.cs stakeout.tests/Evidence/EvidenceBoardTests.cs
git commit -m "Add EvidenceBoard with item and connection management"
```

---

## Task 4: GameManager Autoload Singleton

This task introduces the `GameManager` autoload and migrates `SimulationManager` to receive `SimulationState` from it instead of creating its own.

**Files:**
- Create: `src/GameManager.cs`
- Modify: `src/simulation/SimulationManager.cs`
- Modify: `scenes/simulation_debug/SimulationDebug.cs`
- Modify: `project.godot`

- [ ] **Step 1: Create `GameManager.cs`**

```csharp
using Godot;
using Stakeout.Evidence;
using Stakeout.Simulation;

namespace Stakeout;

public partial class GameManager : Node
{
    public SimulationState State { get; private set; }
    public SimulationManager SimulationManager { get; private set; }
    public EvidenceBoard EvidenceBoard { get; private set; }

    public override void _Ready()
    {
        State = new SimulationState();
        EvidenceBoard = new EvidenceBoard();
        SimulationManager = new SimulationManager(State);
        AddChild(SimulationManager);
    }
}
```

- [ ] **Step 2: Modify `SimulationManager.cs` to receive SimulationState**

Remove `State = new SimulationState()` from `_Ready()` and accept it via constructor instead:

```csharp
public partial class SimulationManager : Node
{
    public SimulationState State { get; private set; }

    // ... existing fields unchanged ...

    public SimulationManager(SimulationState state)
    {
        State = state;
    }

    public override void _Ready()
    {
        // State is already set via constructor — no longer created here
        _locationGenerator.GenerateCity(State);

        foreach (var address in State.Addresses.Values)
        {
            AddressAdded?.Invoke(address);
        }

        // ... rest of _Ready unchanged (player creation, etc.) ...
    }
}
```

- [ ] **Step 3: Register autoload in `project.godot`**

Add this section after `[application]` block. The path assumes `GameManager.cs` is at the project root under `src/`:

Add to `project.godot` under `[autoload]` section (create section if it doesn't exist, place after `[application]`):

```ini
[autoload]

GameManager="*res://src/GameManager.cs"
```

- [ ] **Step 4: Update `SimulationDebug.cs` to use GameManager**

In `SimulationDebug.cs`, replace the manual `SimulationManager` instantiation with a lookup from the autoload:

Replace the `_Ready()` method contents. The key change: instead of `new SimulationManager()` + `AddChild()`, get it from GameManager. Events still work the same way.

```csharp
public override void _Ready()
{
    _clockLabel = GetNode<Label>("ClockLabel");
    _hoverLabel = GetNode<Label>("HoverLabel");
    _locationIcons = GetNode<Control>("CityMap/LocationIcons");
    _entityDots = GetNode<Control>("CityMap/EntityDots");

    var gameManager = GetNode<GameManager>("/root/GameManager");
    _simulationManager = gameManager.SimulationManager;

    _simulationManager.AddressAdded += OnAddressAdded;
    _simulationManager.PersonAdded += OnPersonAdded;
    _simulationManager.PlayerCreated += OnPlayerCreated;

    // Re-render any addresses/people that were created before this scene loaded
    foreach (var address in _simulationManager.State.Addresses.Values)
    {
        OnAddressAdded(address);
    }
    foreach (var person in _simulationManager.State.People.Values)
    {
        OnPersonAdded(person);
    }
    if (_simulationManager.State.Player != null)
    {
        OnPlayerCreated();
    }
}
```

Note: Since `GameManager._Ready()` runs before scene `_Ready()` (autoloads initialize first), `SimulationManager` will already have generated its initial data. The old pattern relied on subscribing before `AddChild` to catch events during `_Ready()`. Now we need to re-render existing state when the scene loads. This also supports returning from the evidence board to the map — existing entities will be re-rendered.

- [ ] **Step 5: Build and test manually in Godot**

Run: `dotnet build stakeout.csproj`
Expected: Build succeeds. Open the project in Godot and click "New Career" — the map should display exactly as before with addresses, people, and the player dot.

- [ ] **Step 6: Run all unit tests**

Run: `dotnet test stakeout.tests/`
Expected: All tests still pass. (Unit tests don't depend on the autoload pattern — they test pure C# classes.)

- [ ] **Step 7: Commit**

```
git add src/GameManager.cs src/simulation/SimulationManager.cs scenes/simulation_debug/SimulationDebug.cs project.godot
git commit -m "Introduce GameManager autoload singleton, migrate SimulationDebug"
```

---

## Task 5: Evidence Board Scene — Corkboard with Pan & Zoom

**Files:**
- Create: `scenes/evidence_board/EvidenceBoard.tscn`
- Create: `scenes/evidence_board/EvidenceBoard.cs`

- [ ] **Step 1: Create `EvidenceBoard.cs`**

This is the main board script. It manages the corkboard canvas with pan and zoom, instantiates polaroids from the data model, and handles the close button.

```csharp
using Godot;
using Stakeout;
using Stakeout.Evidence;

public partial class EvidenceBoard : Control
{
    private Control _corkboardCanvas;
    private Control _polaroidContainer;
    private Button _closeButton;

    private Vector2 _panOffset = Vector2.Zero;
    private float _zoom = 1.0f;
    private const float MinZoom = 0.25f;
    private const float MaxZoom = 2.0f;
    private const float ZoomStep = 0.1f;

    private bool _isPanning;
    private Vector2 _panStartMouse;
    private Vector2 _panStartOffset;

    public override void _Ready()
    {
        _corkboardCanvas = GetNode<Control>("CorkboardCanvas");
        _polaroidContainer = GetNode<Control>("CorkboardCanvas/PolaroidContainer");
        _closeButton = GetNode<Button>("CloseButton");

        _closeButton.Pressed += OnClosePressed;

        // Center the view on the canvas initially
        var viewportSize = GetViewportRect().Size;
        var canvasSize = _corkboardCanvas.GetNode<Control>("CorkboardBackground").Size;
        _panOffset = (viewportSize - canvasSize) / 2;
        ApplyTransform();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseButton)
        {
            HandleMouseButton(mouseButton);
        }
        else if (@event is InputEventMouseMotion mouseMotion && _isPanning)
        {
            HandlePanMotion(mouseMotion);
        }
    }

    private void HandleMouseButton(InputEventMouseButton mouseButton)
    {
        // String creation release must be checked first
        if (_isCreatingString && !mouseButton.Pressed && mouseButton.ButtonIndex == MouseButton.Left)
        {
            var targetId = FindThumbTackAt(mouseButton.GlobalPosition);
            if (targetId >= 0 && targetId != _stringSourceItemId)
            {
                _gameManager.EvidenceBoard.AddConnection(_stringSourceItemId, targetId);
            }
            _isCreatingString = false;
            _stringLayer.IsDrawingString = false;
            _stringLayer.HoveredThumbTackItemId = -1;
            foreach (var (_, p) in _polaroidNodes)
                p.SetThumbTackGlow(false);
            return;
        }

        // Zoom with scroll wheel
        if (mouseButton.ButtonIndex == MouseButton.WheelUp && mouseButton.Pressed)
        {
            Zoom(ZoomStep, mouseButton.Position);
        }
        else if (mouseButton.ButtonIndex == MouseButton.WheelDown && mouseButton.Pressed)
        {
            Zoom(-ZoomStep, mouseButton.Position);
        }
        // Pan with middle mouse button
        else if (mouseButton.ButtonIndex == MouseButton.Middle)
        {
            if (mouseButton.Pressed)
            {
                _isPanning = true;
                _panStartMouse = mouseButton.Position;
                _panStartOffset = _panOffset;
            }
            else
            {
                _isPanning = false;
            }
        }
        // Right-click: check for string hit-test, then context menus
        else if (mouseButton.ButtonIndex == MouseButton.Right && mouseButton.Pressed)
        {
            var (fromId, toId) = _stringLayer.HitTestString(mouseButton.GlobalPosition);
            if (fromId >= 0)
            {
                var menu = new PopupMenu();
                menu.AddItem("Remove string", 0);
                menu.IdPressed += (id) =>
                {
                    if (id == 0) _gameManager.EvidenceBoard.RemoveConnection(fromId, toId);
                    menu.QueueFree();
                };
                menu.PopupHide += () => menu.QueueFree();
                AddChild(menu);
                menu.Position = new Vector2I((int)mouseButton.GlobalPosition.X, (int)mouseButton.GlobalPosition.Y);
                menu.Popup();
            }
        }
        // Left-click on empty space pans
        else if (mouseButton.ButtonIndex == MouseButton.Left)
        {
            if (mouseButton.Pressed)
            {
                _isPanning = true;
                _panStartMouse = mouseButton.Position;
                _panStartOffset = _panOffset;
            }
            else
            {
                _isPanning = false;
            }
        }
    }

    private void HandlePanMotion(InputEventMouseMotion mouseMotion)
    {
        _panOffset = _panStartOffset + (mouseMotion.Position - _panStartMouse);
        ApplyTransform();
    }

    private void Zoom(float delta, Vector2 mousePos)
    {
        var oldZoom = _zoom;
        _zoom = Mathf.Clamp(_zoom + delta, MinZoom, MaxZoom);

        // Zoom towards mouse position
        var zoomRatio = _zoom / oldZoom;
        _panOffset = mousePos - (mousePos - _panOffset) * zoomRatio;
        ApplyTransform();
    }

    private void ApplyTransform()
    {
        _corkboardCanvas.Position = _panOffset;
        _corkboardCanvas.Scale = new Vector2(_zoom, _zoom);
    }

    private void OnClosePressed()
    {
        GetTree().ChangeSceneToFile("res://scenes/simulation_debug/SimulationDebug.tscn");
    }
}
```

- [ ] **Step 2: Create `EvidenceBoard.tscn`**

Create the scene file. The key structure: root Control > CorkboardCanvas > CorkboardBackground + StringLayer + PolaroidContainer, plus a CloseButton fixed to viewport.

```
[gd_scene load_steps=3 format=3]

[ext_resource type="Script" path="res://scenes/evidence_board/EvidenceBoard.cs" id="1_script"]
[ext_resource type="FontFile" path="res://fonts/exepixelperfect/EXEPixelPerfect.ttf" id="2_font"]

[node name="EvidenceBoard" type="Control"]
layout_mode = 3
anchors_preset = 15
anchor_right = 1.0
anchor_bottom = 1.0
script = ExtResource("1_script")

[node name="CorkboardCanvas" type="Control" parent="."]
layout_mode = 0
offset_right = 3840.0
offset_bottom = 2160.0

[node name="CorkboardBackground" type="ColorRect" parent="CorkboardCanvas"]
layout_mode = 0
offset_right = 3840.0
offset_bottom = 2160.0
color = Color(0.76, 0.6, 0.42, 1)

[node name="PolaroidContainer" type="Control" parent="CorkboardCanvas"]
layout_mode = 0
offset_right = 3840.0
offset_bottom = 2160.0

[node name="CloseButton" type="Button" parent="."]
layout_mode = 1
anchors_preset = 1
anchor_left = 1.0
anchor_right = 1.0
offset_left = -40.0
offset_top = 5.0
offset_right = -5.0
offset_bottom = 40.0
text = "X"
theme_override_fonts/font = ExtResource("2_font")
theme_override_font_sizes/font_size = 20
```

- [ ] **Step 3: Build and test manually in Godot**

Run: `dotnet build stakeout.csproj`
Expected: Build succeeds. You won't be able to navigate to the board yet (sidebar not added), but you can temporarily set the evidence board as the main scene in Godot to test pan and zoom work.

- [ ] **Step 4: Commit**

```
git add scenes/evidence_board/EvidenceBoard.tscn scenes/evidence_board/EvidenceBoard.cs
git commit -m "Add evidence board scene with corkboard pan and zoom"
```

---

## Task 6: SimulationDebug Sidebar & Navigation

**Files:**
- Modify: `scenes/simulation_debug/SimulationDebug.tscn`
- Modify: `scenes/simulation_debug/SimulationDebug.cs`

- [ ] **Step 1: Add sidebar to `SimulationDebug.tscn`**

Add a `PanelContainer` with a `VBoxContainer` to the right side of the scene. Add after the HoverLabel node:

```
[node name="Sidebar" type="PanelContainer" parent="."]
layout_mode = 1
anchors_preset = 3
anchor_left = 1.0
anchor_right = 1.0
anchor_bottom = 1.0
offset_left = -60.0
grow_horizontal = 0

[node name="VBox" type="VBoxContainer" parent="Sidebar"]
layout_mode = 2

[node name="EvidenceBoardButton" type="Button" parent="Sidebar/VBox"]
layout_mode = 2
text = "Evidence\nBoard"
theme_override_fonts/font = ExtResource("2_font")
theme_override_font_sizes/font_size = 12
```

You'll also need to add a `StyleBoxFlat` for the sidebar's black background. This can be done in the C# script or by adding a theme override in the .tscn.

- [ ] **Step 2: Add sidebar wiring in `SimulationDebug.cs`**

Add to `_Ready()`:

```csharp
var evidenceBoardButton = GetNode<Button>("Sidebar/VBox/EvidenceBoardButton");
evidenceBoardButton.Pressed += OnEvidenceBoardPressed;

// Style sidebar black
var sidebar = GetNode<PanelContainer>("Sidebar");
var sidebarStyle = new StyleBoxFlat { BgColor = new Color(0, 0, 0, 1) };
sidebar.AddThemeStyleboxOverride("panel", sidebarStyle);
```

Add the handler method:

```csharp
private void OnEvidenceBoardPressed()
{
    GetTree().ChangeSceneToFile("res://scenes/evidence_board/EvidenceBoard.tscn");
}
```

- [ ] **Step 3: Build and test manually**

Run: `dotnet build stakeout.csproj`
Expected: Build succeeds. In Godot: click "New Career" → see black sidebar on right with "Evidence Board" button → click it → see cork-colored board → click X → return to map.

- [ ] **Step 4: Commit**

```
git add scenes/simulation_debug/SimulationDebug.tscn scenes/simulation_debug/SimulationDebug.cs
git commit -m "Add sidebar with Evidence Board button and scene navigation"
```

---

## Task 7: Polaroid Packed Scene

**Files:**
- Create: `scenes/evidence_board/EvidencePolaroid.tscn`
- Create: `scenes/evidence_board/EvidencePolaroid.cs`

- [ ] **Step 1: Create `EvidencePolaroid.cs`**

The polaroid script handles dragging, clicking (for dossier), and right-click context menus. It emits signals so the parent board scene can respond.

```csharp
using Godot;
using Stakeout.Evidence;

public partial class EvidencePolaroid : Control
{
    [Signal]
    public delegate void PolaroidClickedEventHandler(int itemId);

    [Signal]
    public delegate void PolaroidRemovedEventHandler(int itemId);

    [Signal]
    public delegate void ThumbtackDragStartedEventHandler(int itemId, Vector2 globalPos);

    [Signal]
    public delegate void ThumbtackRightClickedEventHandler(int itemId, Vector2 globalPos);

    public int ItemId { get; set; }

    private Label _initialsLabel;
    private Label _captionLabel;
    private Panel _thumbtack;
    private bool _isDragging;
    private Vector2 _dragOffset;
    private bool _dragStartedOnThumbtrack;
    private const float DragThreshold = 4f;
    private Vector2 _mouseDownPos;
    private bool _mouseDown;

    public override void _Ready()
    {
        _initialsLabel = GetNode<Label>("Body/ImageArea/Initials");
        _captionLabel = GetNode<Label>("Body/Caption");
        _thumbtack = GetNode<Panel>("Thumbtack");
    }

    public void SetContent(string initials, string caption)
    {
        _initialsLabel.Text = initials;
        _captionLabel.Text = caption;
    }

    public Vector2 GetThumbTackGlobalCenter()
    {
        return _thumbtack.GlobalPosition + _thumbtack.Size / 2;
    }

    /// <summary>
    /// Converts a global/viewport position to canvas-local coordinates,
    /// accounting for the parent canvas's pan and zoom transform.
    /// </summary>
    private Vector2 ToCanvasLocal(Vector2 globalPos)
    {
        var canvas = GetParent<Control>();
        return (globalPos - canvas.GlobalPosition) / canvas.Scale;
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mb)
        {
            if (mb.ButtonIndex == MouseButton.Left)
            {
                if (mb.Pressed)
                {
                    _mouseDown = true;
                    _mouseDownPos = mb.GlobalPosition;

                    // Check if click is on the thumbtack
                    var thumbtackRect = new Rect2(_thumbtack.GlobalPosition, _thumbtack.Size);
                    _dragStartedOnThumbtrack = thumbtackRect.HasPoint(mb.GlobalPosition);

                    if (!_dragStartedOnThumbtrack)
                    {
                        _dragOffset = Position - ToCanvasLocal(mb.GlobalPosition);
                    }
                    AcceptEvent();
                }
                else
                {
                    if (_isDragging)
                    {
                        _isDragging = false;
                    }
                    else if (_mouseDown && !_dragStartedOnThumbtrack)
                    {
                        // Single click — open dossier
                        EmitSignal(SignalName.PolaroidClicked, ItemId);
                    }
                    _mouseDown = false;
                    _dragStartedOnThumbtrack = false;
                    AcceptEvent();
                }
            }
            else if (mb.ButtonIndex == MouseButton.Right && mb.Pressed)
            {
                var thumbtackRect = new Rect2(_thumbtack.GlobalPosition, _thumbtack.Size);
                if (thumbtackRect.HasPoint(mb.GlobalPosition))
                {
                    EmitSignal(SignalName.ThumbtackRightClicked, ItemId, mb.GlobalPosition);
                }
                else
                {
                    // Show context menu with "Remove from Board"
                    var menu = new PopupMenu();
                    menu.AddItem("Remove from Board", 0);
                    menu.IdPressed += (id) =>
                    {
                        if (id == 0) EmitSignal(SignalName.PolaroidRemoved, ItemId);
                        menu.QueueFree();
                    };
                    menu.PopupHide += () => menu.QueueFree();
                    AddChild(menu);
                    menu.Position = new Vector2I((int)mb.GlobalPosition.X, (int)mb.GlobalPosition.Y);
                    menu.Popup();
                }
                AcceptEvent();
            }
        }
        else if (@event is InputEventMouseMotion mm && _mouseDown)
        {
            var distance = mm.GlobalPosition.DistanceTo(_mouseDownPos);
            if (distance > DragThreshold)
            {
                if (_dragStartedOnThumbtrack)
                {
                    EmitSignal(SignalName.ThumbtackDragStarted, ItemId, mm.GlobalPosition);
                    _mouseDown = false; // Hand off to board for string creation
                }
                else if (!_isDragging)
                {
                    _isDragging = true;
                    // Bring to front
                    GetParent()?.MoveChild(this, -1);
                }
            }

            if (_isDragging)
            {
                Position = ToCanvasLocal(mm.GlobalPosition) + _dragOffset;
                AcceptEvent();
            }
        }
    }
}
```

- [ ] **Step 2: Create `EvidencePolaroid.tscn`**

```
[gd_scene load_steps=3 format=3]

[ext_resource type="Script" path="res://scenes/evidence_board/EvidencePolaroid.cs" id="1_script"]
[ext_resource type="FontFile" path="res://fonts/PermanentMarker/PermanentMarker.ttf" id="2_marker_font"]

[node name="EvidencePolaroid" type="Control"]
custom_minimum_size = Vector2(100, 130)
size_flags_horizontal = 0
script = ExtResource("1_script")

[node name="Thumbtack" type="Panel" parent="."]
layout_mode = 0
offset_left = 42.0
offset_top = -6.0
offset_right = 58.0
offset_bottom = 10.0

[node name="Body" type="Panel" parent="."]
layout_mode = 0
offset_left = 5.0
offset_top = 5.0
offset_right = 95.0
offset_bottom = 125.0

[node name="ImageArea" type="Panel" parent="Body"]
layout_mode = 0
offset_left = 5.0
offset_top = 5.0
offset_right = 85.0
offset_bottom = 85.0

[node name="Initials" type="Label" parent="Body/ImageArea"]
layout_mode = 1
anchors_preset = 15
anchor_right = 1.0
anchor_bottom = 1.0
horizontal_alignment = 1
vertical_alignment = 1
theme_override_font_sizes/font_size = 28

[node name="Caption" type="Label" parent="Body"]
layout_mode = 0
offset_left = 5.0
offset_top = 87.0
offset_right = 85.0
offset_bottom = 115.0
horizontal_alignment = 1
theme_override_fonts/font = ExtResource("2_marker_font")
theme_override_font_sizes/font_size = 10
autowrap_mode = 2
```

Note: The thumbtack, body, and image area will need `StyleBoxFlat` theme overrides applied in the C# script or scene to get the right colors (thumbtack = red circle, body = white/off-white, image area = light gray). The thumbtack shape approximation (small square with rounded corners via `corner_radius`) is fine for v1.

- [ ] **Step 3: Build**

Run: `dotnet build stakeout.csproj`
Expected: Build succeeds

- [ ] **Step 4: Commit**

```
git add scenes/evidence_board/EvidencePolaroid.tscn scenes/evidence_board/EvidencePolaroid.cs
git commit -m "Add EvidencePolaroid packed scene with drag and click interactions"
```

---

## Task 8: Board Populates Polaroids from Data Model

Wire up the evidence board scene to instantiate polaroids from `EvidenceBoard` data, and update board positions when polaroids are dragged.

**Files:**
- Modify: `scenes/evidence_board/EvidenceBoard.cs`

- [ ] **Step 1: Add polaroid instantiation to `EvidenceBoard.cs`**

Add a `PackedScene` field and load it in `_Ready()`. After loading, iterate `EvidenceBoard.Items` and instantiate a polaroid for each. The board needs access to both `EvidenceBoard` (for item data) and `SimulationState` (for resolving entity names).

Add fields:

```csharp
private PackedScene _polaroidScene;
private GameManager _gameManager;
private readonly Dictionary<int, EvidencePolaroid> _polaroidNodes = new();
```

In `_Ready()`, add before the close button wiring:

```csharp
_gameManager = GetNode<GameManager>("/root/GameManager");
_polaroidScene = GD.Load<PackedScene>("res://scenes/evidence_board/EvidencePolaroid.tscn");
PopulateBoard();
```

Add methods:

```csharp
private void PopulateBoard()
{
    var board = _gameManager.EvidenceBoard;
    foreach (var item in board.Items.Values)
    {
        CreatePolaroidNode(item);
    }
}

private void CreatePolaroidNode(EvidenceItem item)
{
    var polaroid = _polaroidScene.Instantiate<EvidencePolaroid>();
    polaroid.ItemId = item.Id;
    polaroid.Position = item.BoardPosition;

    var (initials, caption) = ResolveEntityDisplay(item);
    polaroid.SetContent(initials, caption);

    polaroid.PolaroidClicked += OnPolaroidClicked;
    polaroid.PolaroidRemoved += OnPolaroidRemoved;
    polaroid.ThumbtackDragStarted += OnThumbtackDragStarted;
    polaroid.ThumbtackRightClicked += OnThumbtackRightClicked;

    _polaroidContainer.AddChild(polaroid);
    _polaroidNodes[item.Id] = polaroid;
}

private (string initials, string caption) ResolveEntityDisplay(EvidenceItem item)
{
    var state = _gameManager.SimulationManager.State;

    if (item.EntityType == EvidenceEntityType.Person && state.People.TryGetValue(item.EntityId, out var person))
    {
        var initials = $"{person.FirstName[0]}{person.LastName[0]}";
        return (initials, person.FullName);
    }
    else if (item.EntityType == EvidenceEntityType.Address && state.Addresses.TryGetValue(item.EntityId, out var address))
    {
        var street = state.Streets[address.StreetId];
        var icon = address.Type switch
        {
            Stakeout.Simulation.Entities.AddressType.SuburbanHome => "🏠",
            Stakeout.Simulation.Entities.AddressType.Diner => "🍴",
            Stakeout.Simulation.Entities.AddressType.DiveBar => "🍺",
            Stakeout.Simulation.Entities.AddressType.Office => "🏢",
            _ => "?"
        };
        var caption = $"{address.Number} {street.Name}";
        return (icon, caption);
    }

    return ("?", "Unknown");
}
```

Add placeholder signal handlers (will be filled in later tasks):

```csharp
private void OnPolaroidClicked(int itemId) { /* Task 10: dossier */ }
private void OnPolaroidRemoved(int itemId)
{
    _gameManager.EvidenceBoard.RemoveItem(itemId);
    if (_polaroidNodes.TryGetValue(itemId, out var node))
    {
        node.QueueFree();
        _polaroidNodes.Remove(itemId);
    }
}
private void OnThumbtackDragStarted(int itemId, Vector2 globalPos) { /* Task 9: strings */ }
private void OnThumbtackRightClicked(int itemId, Vector2 globalPos) { /* Task 9: strings */ }
```

- [ ] **Step 2: Update polaroid position in data model after drag**

The polaroid script updates its own `Position` during drag, but the data model needs updating too. Add to `EvidenceBoard.cs._Process()`:

```csharp
public override void _Process(double delta)
{
    // Sync polaroid positions back to data model
    var board = _gameManager.EvidenceBoard;
    foreach (var (itemId, polaroid) in _polaroidNodes)
    {
        if (board.Items.TryGetValue(itemId, out var item))
        {
            item.BoardPosition = polaroid.Position;
        }
    }
}
```

- [ ] **Step 3: Handle left-click pan vs polaroid interaction**

The current `_UnhandledInput` approach means left-clicks that hit a polaroid (handled via `_GuiInput`) won't bubble to the board's pan handler. This is the correct behavior — clicking/dragging a polaroid should not pan. No code change needed, but verify this works.

- [ ] **Step 4: Build and test**

Run: `dotnet build stakeout.csproj`
Expected: Build succeeds. To test: add evidence items programmatically in `GameManager._Ready()` (temporary), open the board, and verify polaroids appear and can be dragged.

- [ ] **Step 5: Commit**

```
git add scenes/evidence_board/EvidenceBoard.cs
git commit -m "Wire evidence board to populate and manage polaroids from data model"
```

---

## Task 9: Red Strings — StringLayer, Drawing, and Creation

**Files:**
- Create: `scenes/evidence_board/StringLayer.cs`
- Modify: `scenes/evidence_board/EvidenceBoard.tscn` (add StringLayer node)
- Modify: `scenes/evidence_board/EvidenceBoard.cs` (wire string creation/removal)

- [ ] **Step 1: Create `StringLayer.cs`**

```csharp
using System.Collections.Generic;
using Godot;
using Stakeout.Evidence;

public partial class StringLayer : Control
{
    public EvidenceBoard Board { get; set; }
    public Dictionary<int, EvidencePolaroid> PolaroidNodes { get; set; }

    // String creation state
    public bool IsDrawingString { get; set; }
    public int DrawingFromItemId { get; set; }
    public Vector2 DrawingEndPoint { get; set; }

    // Thumbtack hover glow
    public int HoveredThumbTackItemId { get; set; } = -1;

    private static readonly Color StringColor = new(0.8f, 0.1f, 0.1f);
    private const float StringWidth = 2.5f;

    public override void _Draw()
    {
        if (Board == null || PolaroidNodes == null) return;

        // Draw existing connections
        foreach (var conn in Board.Connections)
        {
            if (PolaroidNodes.TryGetValue(conn.FromItemId, out var fromNode) &&
                PolaroidNodes.TryGetValue(conn.ToItemId, out var toNode))
            {
                var from = ToLocal(fromNode.GetThumbTackGlobalCenter());
                var to = ToLocal(toNode.GetThumbTackGlobalCenter());
                DrawLine(from, to, StringColor, StringWidth, true);
            }
        }

        // Draw preview string during creation
        if (IsDrawingString && PolaroidNodes.TryGetValue(DrawingFromItemId, out var sourceNode))
        {
            var from = ToLocal(sourceNode.GetThumbTackGlobalCenter());
            var to = ToLocal(DrawingEndPoint);
            DrawLine(from, to, StringColor, StringWidth, true);
        }
    }

    public override void _Process(double delta)
    {
        QueueRedraw();
    }

    /// <summary>
    /// Returns the item ID of the connection string nearest to the given global position,
    /// or (-1, -1) if no string is within tolerance.
    /// </summary>
    public (int fromId, int toId) HitTestString(Vector2 globalPos, float tolerance = 8f)
    {
        if (Board == null || PolaroidNodes == null) return (-1, -1);

        foreach (var conn in Board.Connections)
        {
            if (PolaroidNodes.TryGetValue(conn.FromItemId, out var fromNode) &&
                PolaroidNodes.TryGetValue(conn.ToItemId, out var toNode))
            {
                var from = fromNode.GetThumbTackGlobalCenter();
                var to = toNode.GetThumbTackGlobalCenter();
                var dist = DistanceToLineSegment(globalPos, from, to);
                if (dist <= tolerance)
                {
                    return (conn.FromItemId, conn.ToItemId);
                }
            }
        }

        return (-1, -1);
    }

    private static float DistanceToLineSegment(Vector2 point, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        var ap = point - a;
        var t = Mathf.Clamp(ap.Dot(ab) / ab.LengthSquared(), 0f, 1f);
        var closest = a + ab * t;
        return point.DistanceTo(closest);
    }
}
```

- [ ] **Step 2: Add StringLayer node to `EvidenceBoard.tscn`**

Insert the StringLayer node as a child of CorkboardCanvas, between CorkboardBackground and PolaroidContainer (so strings draw behind polaroids):

```
[node name="StringLayer" type="Control" parent="CorkboardCanvas"]
layout_mode = 0
offset_right = 3840.0
offset_bottom = 2160.0
script = ExtResource("string_layer_script")
```

(Add the script ext_resource reference at the top of the .tscn file.)

- [ ] **Step 3: Wire string creation in `EvidenceBoard.cs`**

Add a `_stringLayer` field, initialize it in `_Ready()`, and wire the thumbtack drag signals:

```csharp
private StringLayer _stringLayer;
private bool _isCreatingString;
private int _stringSourceItemId;
```

In `_Ready()`:
```csharp
_stringLayer = GetNode<StringLayer>("CorkboardCanvas/StringLayer");
_stringLayer.Board = _gameManager.EvidenceBoard;
_stringLayer.PolaroidNodes = _polaroidNodes;
```

Implement `OnThumbtackDragStarted`:
```csharp
private void OnThumbtackDragStarted(int itemId, Vector2 globalPos)
{
    _isCreatingString = true;
    _stringSourceItemId = itemId;
    _stringLayer.IsDrawingString = true;
    _stringLayer.DrawingFromItemId = itemId;
    _stringLayer.DrawingEndPoint = globalPos;
}
```

Note: String creation release (checking if released over a thumbtack, creating the connection, clearing glow) is already handled in `HandleMouseButton` (Task 5 Step 1) at the top of the method, before any pan/zoom logic.

Add string endpoint tracking in mouse motion handling (in `_UnhandledInput`):
```csharp
if (_isCreatingString && @event is InputEventMouseMotion motionEvent)
{
    _stringLayer.DrawingEndPoint = motionEvent.GlobalPosition;
    // Check thumbtack hover for glow
    var hoverId = FindThumbTackAt(motionEvent.GlobalPosition);
    _stringLayer.HoveredThumbTackItemId = hoverId;
}
```

Add helper to find thumbtack at position:
```csharp
private int FindThumbTackAt(Vector2 globalPos)
{
    foreach (var (itemId, polaroid) in _polaroidNodes)
    {
        var thumbCenter = polaroid.GetThumbTackGlobalCenter();
        if (globalPos.DistanceTo(thumbCenter) <= 15f)
        {
            return itemId;
        }
    }
    return -1;
}
```

- [ ] **Step 4: Wire right-click string removal and thumbtack "remove all strings"**

Implement `OnThumbtackRightClicked`:
```csharp
private void OnThumbtackRightClicked(int itemId, Vector2 globalPos)
{
    // Show context menu with "Remove all strings"
    var menu = new PopupMenu();
    menu.AddItem("Remove all strings", 0);
    menu.IdPressed += (id) =>
    {
        if (id == 0) _gameManager.EvidenceBoard.RemoveAllConnections(itemId);
        menu.QueueFree();
    };
    menu.PopupHide += () => menu.QueueFree();
    AddChild(menu);
    menu.Position = new Vector2I((int)globalPos.X, (int)globalPos.Y);
    menu.Popup();
}
```

Note: Right-click on strings is already handled in `HandleMouseButton` (Task 5 Step 1) where the right-click branch checks `_stringLayer.HitTestString()` before anything else.

- [ ] **Step 5: Build and test**

Run: `dotnet build stakeout.csproj`
Expected: Build succeeds. Test: add items to board, drag from thumbtack to thumbtack to create strings, right-click to remove.

- [ ] **Step 6: Commit**

```
git add scenes/evidence_board/StringLayer.cs scenes/evidence_board/EvidenceBoard.tscn scenes/evidence_board/EvidenceBoard.cs
git commit -m "Add red string layer with creation, preview, and removal"
```

---

## Task 10: Dossier Floating Window

**Files:**
- Create: `scenes/evidence_board/DossierWindow.tscn`
- Create: `scenes/evidence_board/DossierWindow.cs`
- Modify: `scenes/evidence_board/EvidenceBoard.cs` (wire dossier opening)

- [ ] **Step 1: Create `DossierWindow.cs`**

```csharp
using System.Linq;
using Godot;
using Stakeout.Evidence;
using Stakeout.Simulation;
using Stakeout.Simulation.Entities;

public partial class DossierWindow : Panel
{
    private Label _titleLabel;
    private Label _bodyLabel;
    private Button _closeButton;

    private bool _isDragging;
    private Vector2 _dragOffset;

    public override void _Ready()
    {
        _titleLabel = GetNode<Label>("VBox/Title");
        _bodyLabel = GetNode<Label>("VBox/Body");
        _closeButton = GetNode<Button>("CloseButton");

        _closeButton.Pressed += () => QueueFree();
    }

    public void Populate(EvidenceItem item, SimulationState state)
    {
        if (item.EntityType == EvidenceEntityType.Person && state.People.TryGetValue(item.EntityId, out var person))
        {
            _titleLabel.Text = person.FullName;
            var lines = new System.Collections.Generic.List<string>();

            if (state.Addresses.TryGetValue(person.HomeAddressId, out var home))
            {
                var street = state.Streets[home.StreetId];
                lines.Add($"Home: {home.Number} {street.Name}");
            }
            if (state.Addresses.TryGetValue(person.WorkAddressId, out var work))
            {
                var street = state.Streets[work.StreetId];
                lines.Add($"Work: {work.Number} {street.Name} ({work.Type})");
            }

            _bodyLabel.Text = string.Join("\n", lines);
        }
        else if (item.EntityType == EvidenceEntityType.Address && state.Addresses.TryGetValue(item.EntityId, out var address))
        {
            var street = state.Streets[address.StreetId];
            _titleLabel.Text = $"{address.Number} {street.Name} — {address.Type}";

            var people = state.People.Values
                .Where(p => p.HomeAddressId == address.Id || p.WorkAddressId == address.Id)
                .ToList();

            if (people.Count > 0)
            {
                var lines = people.Select(p =>
                {
                    var rel = p.HomeAddressId == address.Id ? "lives here" : "works here";
                    return $"{p.FullName} ({rel})";
                });
                _bodyLabel.Text = string.Join("\n", lines);
            }
            else
            {
                _bodyLabel.Text = "No known associates.";
            }
        }
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left)
        {
            if (mb.Pressed)
            {
                // Only allow dragging from the title bar area (top 30px)
                var localY = mb.Position.Y;
                if (localY <= 30f)
                {
                    _isDragging = true;
                    _dragOffset = Position - mb.GlobalPosition;
                }
                AcceptEvent();
            }
            else
            {
                _isDragging = false;
                AcceptEvent();
            }
        }
        else if (@event is InputEventMouseMotion mm && _isDragging)
        {
            Position = mm.GlobalPosition + _dragOffset;
            AcceptEvent();
        }
    }
}
```

- [ ] **Step 2: Create `DossierWindow.tscn`**

```
[gd_scene load_steps=4 format=3]

[ext_resource type="Script" path="res://scenes/evidence_board/DossierWindow.cs" id="1_script"]
[ext_resource type="FontFile" path="res://fonts/exepixelperfect/EXEPixelPerfect.ttf" id="2_pixel_font"]
[ext_resource type="FontFile" path="res://fonts/Caveat/Caveat-Regular.ttf" id="3_caveat_font"]

[node name="DossierWindow" type="Panel"]
offset_right = 250.0
offset_bottom = 200.0
mouse_filter = 0
script = ExtResource("1_script")

[node name="VBox" type="VBoxContainer" parent="."]
layout_mode = 0
offset_left = 10.0
offset_top = 10.0
offset_right = 210.0
offset_bottom = 190.0

[node name="Title" type="Label" parent="VBox"]
layout_mode = 2
theme_override_fonts/font = ExtResource("2_pixel_font")
theme_override_font_sizes/font_size = 16
text = "Title"

[node name="Body" type="Label" parent="VBox"]
layout_mode = 2
theme_override_fonts/font = ExtResource("3_caveat_font")
theme_override_font_sizes/font_size = 18
text = "Body text"
autowrap_mode = 2

[node name="CloseButton" type="Button" parent="."]
layout_mode = 0
offset_left = 220.0
offset_top = 2.0
offset_right = 248.0
offset_bottom = 26.0
text = "X"
theme_override_fonts/font = ExtResource("2_pixel_font")
theme_override_font_sizes/font_size = 14
```

Style the panel background as off-white/beige in the C# script `_Ready()`:

```csharp
var style = new StyleBoxFlat
{
    BgColor = new Color(0.95f, 0.92f, 0.85f),
    BorderColor = new Color(0.4f, 0.35f, 0.3f),
    BorderWidthLeft = 2, BorderWidthRight = 2,
    BorderWidthTop = 2, BorderWidthBottom = 2
};
AddThemeStyleboxOverride("panel", style);
```

- [ ] **Step 3: Wire dossier opening in `EvidenceBoard.cs`**

Add fields:

```csharp
private PackedScene _dossierScene;
private DossierWindow _currentDossier;
```

In `_Ready()`:
```csharp
_dossierScene = GD.Load<PackedScene>("res://scenes/evidence_board/DossierWindow.tscn");
```

Implement `OnPolaroidClicked`:
```csharp
private void OnPolaroidClicked(int itemId)
{
    // Close existing dossier
    _currentDossier?.QueueFree();

    var board = _gameManager.EvidenceBoard;
    if (!board.Items.TryGetValue(itemId, out var item)) return;

    _currentDossier = _dossierScene.Instantiate<DossierWindow>();
    _currentDossier.Position = GetViewportRect().Size / 2 - new Vector2(125, 100);
    AddChild(_currentDossier);
    _currentDossier.Populate(item, _gameManager.SimulationManager.State);
}
```

- [ ] **Step 4: Build and test**

Run: `dotnet build stakeout.csproj`
Expected: Build succeeds. Test: click a polaroid → dossier appears with correct entity info. Click another → replaces. Click X → closes.

- [ ] **Step 5: Commit**

```
git add scenes/evidence_board/DossierWindow.tscn scenes/evidence_board/DossierWindow.cs scenes/evidence_board/EvidenceBoard.cs
git commit -m "Add floating dossier window with Person and Address content"
```

---

## Task 11: Right-Click to Add Evidence from Map

**Files:**
- Modify: `scenes/simulation_debug/SimulationDebug.cs`

- [ ] **Step 1: Add right-click handling to `SimulationDebug.cs`**

Add to `_UnhandledInput` (or override `_Input`). Check if right-click is near a person dot or address icon, and show a context menu.

Add field:
```csharp
private GameManager _gameManager;
```

In `_Ready()`, add:
```csharp
_gameManager = GetNode<GameManager>("/root/GameManager");
```

Add input handler:
```csharp
public override void _UnhandledInput(InputEvent @event)
{
    if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Right && mb.Pressed)
    {
        TryShowAddToEvidenceBoardMenu(mb.GlobalPosition);
    }
}

private void TryShowAddToEvidenceBoardMenu(Vector2 mousePos)
{
    var state = _simulationManager.State;
    var board = _gameManager.EvidenceBoard;

    // Check addresses
    foreach (var (addressId, icon) in _addressNodes)
    {
        var center = icon.Position + new Vector2(LocationIconSize / 2, LocationIconSize / 2);
        if (mousePos.DistanceTo(center) <= HoverDistance)
        {
            ShowAddToEvidenceBoardMenu(mousePos, EvidenceEntityType.Address, addressId, board);
            return;
        }
    }

    // Check people
    foreach (var (personId, dot) in _personNodes)
    {
        var center = dot.Position + new Vector2(EntityDotSize / 2, EntityDotSize / 2);
        if (mousePos.DistanceTo(center) <= HoverDistance)
        {
            ShowAddToEvidenceBoardMenu(mousePos, EvidenceEntityType.Person, personId, board);
            return;
        }
    }
}

private void ShowAddToEvidenceBoardMenu(Vector2 pos, EvidenceEntityType entityType, int entityId, EvidenceBoard board)
{
    var menu = new PopupMenu();
    var alreadyOnBoard = board.HasItem(entityType, entityId);

    menu.AddItem(alreadyOnBoard ? "Already on Board" : "Add to Evidence Board", 0);
    if (alreadyOnBoard)
    {
        menu.SetItemDisabled(0, true);
    }

    menu.IdPressed += (id) =>
    {
        if (id == 0 && !alreadyOnBoard)
        {
            // Default position: center of canvas with random offset
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
```

You'll need to add the `using` statement at the top:
```csharp
using Stakeout.Evidence;
```

- [ ] **Step 2: Build and test end-to-end**

Run: `dotnet build stakeout.csproj`
Expected: Build succeeds. Test the full flow: map → right-click person → "Add to Evidence Board" → click sidebar "Evidence Board" → polaroid appears on corkboard → drag it → draw string → click for dossier → X to close dossier → X to return to map.

- [ ] **Step 3: Run all unit tests**

Run: `dotnet test stakeout.tests/`
Expected: All tests pass (existing + new evidence tests)

- [ ] **Step 4: Commit**

```
git add scenes/simulation_debug/SimulationDebug.cs
git commit -m "Add right-click 'Add to Evidence Board' on map entities"
```

---

## Task 12: Thumbtack Glow Effect

**Files:**
- Modify: `scenes/evidence_board/EvidencePolaroid.cs`
- Modify: `scenes/evidence_board/EvidenceBoard.cs`

- [ ] **Step 1: Add glow state to polaroid**

In `EvidencePolaroid.cs`, add a method to toggle the glow:

```csharp
public void SetThumbTackGlow(bool glowing)
{
    var style = new StyleBoxFlat
    {
        BgColor = glowing ? new Color(1f, 0.8f, 0.2f) : new Color(0.8f, 0.1f, 0.1f),
        CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6,
        CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6
    };
    if (glowing)
    {
        style.ShadowColor = new Color(1f, 0.9f, 0.3f, 0.6f);
        style.ShadowSize = 4;
    }
    _thumbtack.AddThemeStyleboxOverride("panel", style);
}
```

- [ ] **Step 2: Wire glow in `EvidenceBoard.cs`**

In the string creation mouse motion handler, update glow state:

```csharp
// After updating HoveredThumbTackItemId
foreach (var (id, polaroid) in _polaroidNodes)
{
    polaroid.SetThumbTackGlow(id == hoverId && id != _stringSourceItemId);
}
```

Clear glow when string creation ends:

```csharp
// When string creation finishes (release):
foreach (var (_, polaroid) in _polaroidNodes)
{
    polaroid.SetThumbTackGlow(false);
}
```

- [ ] **Step 3: Build and test**

Run: `dotnet build stakeout.csproj`
Expected: When dragging a string from a thumbtack and hovering over another, the target thumbtack glows gold.

- [ ] **Step 4: Commit**

```
git add scenes/evidence_board/EvidencePolaroid.cs scenes/evidence_board/EvidenceBoard.cs
git commit -m "Add thumbtack glow effect during string creation"
```

---

## Task 13: Polish — Styling and Final Touches

**Files:**
- Modify: `scenes/evidence_board/EvidencePolaroid.cs` (apply styles in _Ready)
- Modify: `scenes/evidence_board/EvidenceBoard.cs` (apply corkboard background style)

- [ ] **Step 1: Style the polaroid in `EvidencePolaroid._Ready()`**

Apply `StyleBoxFlat` overrides for the thumbtack (red circle), body (white with shadow), and image area (light gray):

```csharp
// Thumbtack — red circle
var thumbStyle = new StyleBoxFlat
{
    BgColor = new Color(0.8f, 0.1f, 0.1f),
    CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6,
    CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6
};
_thumbtack.AddThemeStyleboxOverride("panel", thumbStyle);

// Body — white with subtle shadow
var bodyStyle = new StyleBoxFlat
{
    BgColor = new Color(0.97f, 0.96f, 0.94f),
    ShadowColor = new Color(0, 0, 0, 0.3f),
    ShadowSize = 3,
    ShadowOffset = new Vector2(2, 2)
};
GetNode<Panel>("Body").AddThemeStyleboxOverride("panel", bodyStyle);

// Image area — light gray
var imageStyle = new StyleBoxFlat
{
    BgColor = new Color(0.85f, 0.85f, 0.85f)
};
GetNode<Panel>("Body/ImageArea").AddThemeStyleboxOverride("panel", imageStyle);
```

- [ ] **Step 2: Build and do a visual check**

Run: `dotnet build stakeout.csproj`
Expected: Polaroids look like small photos pinned with a red tack. Corkboard is cork-colored. Strings are red. Dossier is beige. Everything reads clearly.

- [ ] **Step 3: Run all tests one final time**

Run: `dotnet test stakeout.tests/`
Expected: All tests pass

- [ ] **Step 4: Commit**

```
git add scenes/evidence_board/EvidencePolaroid.cs scenes/evidence_board/EvidenceBoard.cs
git commit -m "Polish polaroid and board styling"
```

---

## Summary

| Task | Description | New Tests |
|------|-------------|-----------|
| 1 | EvidenceEntityType + EvidenceItem | — |
| 2 | EvidenceConnection + tests | 6 |
| 3 | EvidenceBoard + tests | 12 |
| 4 | GameManager autoload + migration | — |
| 5 | Evidence board scene (pan/zoom) | — |
| 6 | Sidebar + navigation | — |
| 7 | Polaroid packed scene | — |
| 8 | Board populates polaroids | — |
| 9 | Red strings (draw, create, remove) | — |
| 10 | Dossier floating window | — |
| 11 | Right-click add from map | — |
| 12 | Thumbtack glow effect | — |
| 13 | Polish styling | — |
