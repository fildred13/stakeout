# Player Movement & Game Shell Design

## Overview

Add player movement across the city map, introduce a persistent GameShell UI frame with a left-hand sidebar, create an address content view, and rename simulation_debug to city. This lays the architectural foundation for all future sidebar-based scenes.

## 1. Rename: simulation_debug → city

Rename all references to `simulation_debug` to `city`:

- `scenes/simulation_debug/` → `scenes/city/`
- `SimulationDebug.tscn` → `City.tscn`
- `SimulationDebug.cs` → `City.cs`
- Class name `SimulationDebug` → `City`
- All `ChangeSceneToFile` references pointing to the old path (e.g., in EvidenceBoardScene.cs)

## 2. GameShell: Persistent UI Frame

### Architecture

A new scene (`scenes/game_shell/GameShell.tscn`) acts as the persistent frame for all sidebar-based views. Content views are standalone scenes loaded as children of the content area.

- **Sidebar views** (city, address, globe, conversation, etc.): Swap the content child inside GameShell. No full scene change. The sidebar persists.
- **Immersive views** (minigames): Full scene change away from GameShell. SimulationManager persists as an autoload, so the simulation keeps ticking. On return, GameShell rebuilds from current sim state.
- **Evidence Board**: Full scene change (leaves GameShell), returns to GameShell on close.

### Scene Tree

```
GameShell (Control, full screen)
├── LeftSidebar (PanelContainer, black, left-anchored, 1/4 screen width, full height)
│   ├── VBoxContainer
│   │   ├── ClockLabel (top center of sidebar)
│   │   ├── TimeControls (HBoxContainer: ⏸ ▶ ▶▶ ▶▶▶)
│   │   ├── Spacer/Padding
│   │   └── MenuContainer (VBoxContainer, populated by active content view)
├── ContentArea (Control, remaining 3/4 screen, right side)
│   └── [one child scene at a time: City, Address, etc.]
├── DebugMenuButton (Button, bottom-right corner, overlays everything)
└── DebugSidebar (PanelContainer, right-hand side, toggled by DebugMenuButton)
    └── [same debug NPC list as today, with right-click to add to evidence board]
```

### Left Sidebar Menu System

Content views define their menu items as a simple data structure: a list of `(display name, callback)` pairs. GameShell renders these in MenuContainer as unstyled text labels that act as buttons (no button frame, just clickable text).

When the content view or submenu changes, the content view provides a new list and GameShell clears and repopulates MenuContainer.

**Submenus** work by the content view providing a replacement list. "Go back" is just another menu item that restores the previous list. No special submenu infrastructure.

**Shared items like "Evidence Board"** are explicitly included by each content view that wants them. No automatic injection. If a view doesn't list it, it doesn't appear.

### Debug Menu

- Debug button: bottom-right corner of GameShell (overlays content area and sidebar)
- Debug sidebar: right-hand side, toggled by debug button
- Content: sorted list of all NPCs with right-click context menu to add to evidence board (same as current SimulationDebug debug panel)
- Available in all sidebar views (it's part of GameShell, not any specific content view)

## 3. City Content View

The current SimulationDebug scene becomes a content view (`scenes/city/City.tscn`) stripped of sidebar, clock, time controls, and debug panel (those now live in GameShell).

City.tscn contains only the map content:
- MapBackground (TextureRect)
- LocationIcons (Control)
- EntityDots (Control) — NPC and player dots
- HoverLabel (Label) — tooltip on hover

### City Sidebar Menu Items

- **"Enter Location"** — visible only when the player is at an address (not traveling). Switches content to Address view for the player's current address.
- **"Evidence Board"** — full scene change to evidence board scene.

### Right-Click Context Menu on Address Icons

Existing behavior (add to evidence board) plus a new item:
- **"Go here"** — begins player travel to that address.
- **"Add to Evidence Board"** — existing behavior (disabled if already added).

## 4. Player Movement

### Travel Model

Player travel uses the same speed formula as NPCs, defined in MapConfig:
- `travelTime = distance / mapDiagonal * MaxTravelTimeHours`
- MaxTravelTimeHours = 1.0 (corner-to-corner)

### Player State

Add travel fields to the Player entity (mirroring NPC TravelInfo pattern):
- `IsMoving` (bool)
- `TravelFromPosition` (Vector2)
- `TravelToPosition` (Vector2)
- `TravelStartTime` (DateTime)
- `TravelDurationSeconds` (double)
- `TravelDestinationAddressId` (int)
- `CurrentPosition` (Vector2) — interpolated each frame during travel

### Triggering Travel

Right-click an address icon on the city map → context menu → "Go here":
1. Calculate travel time from player's current position to destination address position
2. Set travel fields on Player
3. Log `DepartedAddress` to EventJournal (if currently at an address)
4. Player dot begins interpolating toward destination

### Interrupting Travel

If the player right-clicks a different address while traveling:
1. Player's current interpolated position becomes the new `TravelFromPosition`
2. New travel time recalculates based on distance from current position to new destination
3. `TravelStartTime` resets to current sim time
4. No DepartedAddress event (already departed)

### Arrival

When interpolation progress reaches 1.0:
1. `Player.CurrentAddressId` updates to destination
2. `Player.IsMoving` = false
3. `Player.CurrentPosition` = destination position
4. Log `ArrivedAtAddress` to EventJournal
5. "Enter Location" appears in sidebar menu

The player does **not** auto-enter the address. They remain on the city map and choose when to enter.

### Event Journal Logging

Player actions log to the same EventJournal as NPCs, using the same event types and the player's entity ID. This ensures future replay features can reconstruct the full picture — player and NPC movements together.

Events logged:
- `DepartedAddress` — when player starts travel from an address
- `ArrivedAtAddress` — when player arrives at destination

### No Schedule System

The player has no goal/schedule pipeline. Travel is driven by direct input: either idle at an address, or traveling between two points. Simple state on the Player class, updated by the City content view in response to user input.

## 5. Address Content View

### Scene: `scenes/address/Address.tscn`

A new content scene loaded into GameShell's ContentArea when the player chooses "Enter Location."

### Visual Content

For this iteration: a placeholder. A label displaying the address name and type so the player knows where they are. Future iterations will add address art and interactive elements.

### Address Sidebar Menu Items (Default)

- **"List People"** — swaps sidebar menu to people submenu
- **"Evidence Board"** — full scene change to evidence board
- **"Leave"** — switches content back to City view. Player remains at the address on the map (does not teleport away).

### Address Sidebar Menu Items (People Submenu)

- One entry per person currently at this address
  - Right-click on a person entry → context menu → "Add to Evidence Board" (disabled if already added)
- **"Go back"** — restores default address menu

People list reflects current simulation state when the submenu is entered. It does not live-update while viewing — re-entering the submenu refreshes it.

### Simulation Continuity

While in the address view, the simulation keeps running. NPCs arrive and leave. The clock ticks. The player just has a different viewport into the same simulation.

## 6. Evidence Board Integration

Evidence board remains a full scene change (`ChangeSceneToFile`). When closing the evidence board, it returns to GameShell. GameShell needs to restore the correct content view (city or address) based on what the player was doing before they opened the evidence board.

This means GameShell should track which content view is active so it can restore state on return from full scene changes.

## Non-Goals

- Address art or interactive address content (placeholder only for now)
- Live-updating people list while viewing the submenu
- Any minigame implementation
- Globe scene, conversation scene, or other future sidebar views
- Player auto-entering locations on arrival
