# Evidence Board Design Spec

## Overview

The evidence board is a central gameplay feature — a corkboard canvas where the player pins pieces of evidence they've discovered and connects them with red strings to track relationships. It is the player's subjective understanding of the game world, separate from the simulation's objective truth.

## Data Model

Located in `/src/evidence/`, decoupled from `/src/simulation/`.

### EvidenceBoard

Central container for the player's board state.

- `Dictionary<int, EvidenceItem> Items` — all items on the board, keyed by board-specific ID
- `List<EvidenceConnection> Connections` — red strings between items
- `GenerateItemId()` — incrementing unique IDs for board items (separate ID space from simulation entity IDs — these are board-specific and must not be confused with entity IDs)
- `AddItem(EntityType, int entityId, Vector2 boardPosition)` — adds an item, returns the new EvidenceItem
- `RemoveItem(int itemId)` — removes an item and all connections referencing it
- `AddConnection(int fromItemId, int toItemId)` — creates a connection (ignores duplicates)
- `RemoveConnection(int fromItemId, int toItemId)` — removes a specific connection
- `RemoveAllConnections(int itemId)` — removes all connections involving an item
- `HasItem(EntityType, int entityId)` — checks if an entity is already on the board

### EvidenceItem

A single piece of evidence pinned to the board.

- `Id` (int) — board-specific unique ID
- `EntityType` (enum: `Person`, `Address`) — declared in `/src/evidence/EvidenceEntityType.cs`, extensible for future entity types. This is the evidence domain's own enum, not shared with the simulation layer.
- `EntityId` (int) — references the actual entity ID in SimulationState
- `BoardPosition` (Vector2) — position on the corkboard canvas

The data model stores references, not copies. The UI layer resolves entity details (name, address text) from SimulationState at render time.

### EvidenceConnection

A red string between two evidence items.

- `FromItemId` (int)
- `ToItemId` (int)

Connections are unordered pairs — (A, B) and (B, A) are the same connection. To enforce this, `EvidenceConnection` normalizes ordering on construction (smaller ID always stored as `FromItemId`). It should implement `Equals`/`GetHashCode` based on the normalized pair for correct duplicate detection and removal.

## State Sharing Between Scenes

The evidence board and the map screen both need access to the same `SimulationState` and `EvidenceBoard` instances. Since scene changes via `SceneTree.ChangeSceneToFile()` destroy the current scene tree, state must be preserved outside of individual scenes.

**Approach:** Introduce a `GameManager` autoload singleton (registered in Project Settings) that owns both `SimulationState` and `EvidenceBoard`. This replaces the current pattern where `SimulationManager` is manually instantiated as a child of `SimulationDebug`. Any scene can access shared state via `GetNode<GameManager>("/root/GameManager")`. Scene changes swap the active UI but game data persists.

**Migration:** The existing `SimulationManager` (which handles generation and ticking) becomes a non-autoload helper owned by `GameManager`. The manual instantiation in `SimulationDebug._Ready()` is removed. This keeps the simulation domain and evidence domain at the same level under `GameManager`, avoiding a dependency from the simulation layer into the evidence layer.

## Scene Structure

### SimulationDebug Sidebar

A narrow all-black `VBoxContainer` anchored to the right edge of the SimulationDebug scene. Contains an "Evidence Board" button styled with EXEPixelPerfect font. Clicking it changes scene to the evidence board.

### Evidence Board Scene (`scenes/evidence_board/EvidenceBoard.tscn`)

- **Root:** `Control` (full-screen)
- **CorkboardCanvas** — container that is transformed for pan/zoom, holds:
  - **CorkboardBackground** — `ColorRect` or `TextureRect`, cork-colored, large canvas (e.g., 3840x2160)
  - **StringLayer** — `Control` with `_Draw()` override, renders red strings behind polaroids
  - **PolaroidContainer** — `Control` holding all polaroid child nodes
- **CloseButton** — pixelated X icon, top-right corner, fixed to viewport (unaffected by pan/zoom). Returns to SimulationDebug.

### Pan & Zoom

- Left-click drag on empty canvas, middle-mouse drag, or right-mouse drag to pan the canvas
- Scroll wheel to zoom in/out (with min/max limits)
- The close button stays fixed in the viewport
- Dragging a polaroid brings it to the front in z-order
- Newly instantiated polaroids are added on top; z-order is not persisted in the data model for v1 (instantiation order is sufficient)

## Polaroid Design

Each polaroid is a packed scene (`scenes/evidence_board/EvidencePolaroid.tscn`).

### Visual Structure (top to bottom)

1. **Thumbtack** — small colored circle at top-center, extends slightly above the polaroid frame. Anchor point for red strings.
2. **Image area** — square region with placeholder content:
   - Person: text initials (e.g., "JD" for John Doe)
   - Address: icon/abbreviation for the address type (house symbol for residential, etc.)
3. **Caption** — text below the image area in Permanent Marker font:
   - Person: full name (e.g., "John Doe")
   - Address: street address + type (e.g., "42 Elm St — Diner")

### Styling

White/off-white background with subtle drop shadow. Feels like a pinned photo.

### Interactions

- **Click & drag polaroid body** — moves it on the board, updates `BoardPosition` in data model
- **Click & drag from thumbtack** — begins drawing a red string preview to mouse cursor; release on another thumbtack to create connection, release elsewhere to cancel
- **Single click (no drag)** — opens dossier floating window for the entity
- **Right-click polaroid body** — context menu with "Remove from Board" (also removes attached strings)
- **Right-click thumbtack** — context menu with "Remove all strings"

## Red Strings

### Rendering

- Drawn on `StringLayer` via `_Draw()`, behind polaroids in z-order
- Red lines, 2-3px width, straight lines for v1
- From thumbtack center to thumbtack center

### Creating

- Drag from a thumbtack to start
- Preview string drawn from source thumbtack to mouse cursor while dragging
- Hovering over another thumbtack gives it a glow effect indicating a valid drop target
- Release on a glowing thumbtack to create the connection
- Release elsewhere to cancel
- Duplicate connections (same pair) are silently ignored

### Removing

- Right-click a string → context menu with "Remove string" (hit-test via proximity to line segment, 8px tolerance)
- Right-click a thumbtack → context menu with "Remove all strings"
- Removing a polaroid removes all its attached strings

## Dossier Floating Window

### Structure

- Floating `Panel` node, spawned on polaroid click
- Draggable by title bar area
- Close button (X) in top-right corner
- Styled as a bordered panel with off-white/beige background (manila folder feel)

### Content (v1, minimal)

**Person dossier:**
- Name (header)
- Home address (if known)
- Work address (if known)

**Address dossier:**
- Street address + type (header, e.g., "42 Elm St — Diner")
- List of people whose home or work is at this address (stable relationships, queried by `HomeAddressId`/`WorkAddressId`, not `CurrentAddressId`) — for v1, this reads from simulation truth (all people the simulation knows about), not limited to evidence on the board. Future versions may restrict this to the player's discovered knowledge.

### Behavior

- Only one dossier open at a time — clicking a different polaroid replaces the current one
- Reads live from SimulationState to resolve entity references (the UI layer bridges evidence references to simulation truth)
- Must be closed via X button; clicking the board behind it does not close it
- All context menus (polaroid, thumbtack, string) are dismissed by clicking elsewhere on the board
- Font: Caveat Regular for body text, EXEPixelPerfect for labels/headers

## Adding Evidence to the Board

### Initial Implementation (Map Screen)

- Right-click a person dot or address icon on the SimulationDebug map
- Context menu appears with "Add to Evidence Board"
- Creates an `EvidenceItem` in `EvidenceBoard` with the entity type + ID
- Default `BoardPosition`: center of the canvas (since the board may not be open when items are added), with a random offset of +/- 50px on each axis to prevent stacking
- If entity is already on the board, option is grayed out / shows "Already on Board"

### Extensibility

The "add to board" action is a data model operation — any future screen (dossier, wiretap, etc.) can add evidence by writing to the same `EvidenceBoard` instance. The board scene reads from the model when opened.

## Fonts

See `docs/design/UI.md` for the full font reference. Key fonts for this feature:

- **Polaroid captions:** Permanent Marker (`fonts/PermanentMarker/PermanentMarker.ttf`)
- **Dossier body:** Caveat Regular (`fonts/Caveat/Caveat-Regular.ttf`)
- **UI chrome (buttons, close icon):** EXEPixelPerfect (`fonts/exepixelperfect/EXEPixelPerfect.ttf`)
