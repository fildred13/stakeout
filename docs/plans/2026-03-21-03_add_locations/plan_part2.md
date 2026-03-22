# Plan Part 2: Fixes & Polish for Locations

## Issues to Fix

1. **Background PNG not loading** — The scene uses a `ColorRect` for `MapBackground` instead of a `TextureRect` referencing the PNG at `assets/textures/PLACEHOLDER_city_map.png`.
2. **Hover only shows one entity** — When a person overlaps a location, only the person name shows. Need to collect ALL entities under the mouse and show them all in the hover label.
3. **Icons need black borders** — Location and person icons should have a chunky black border to pop off the background. `ColorRect` can't do this natively; switch to using `Panel` nodes with `StyleBoxFlat` (which supports border color/width), or draw the border manually.
4. **Work locations not appearing** — `PersonGenerator` assigns `WorkAddressId` from commercial addresses, which do exist (Diner, DiveBar). These addresses already appear on the map. The likely issue is that there are no dedicated workplace-type addresses (like offices). We should add a workplace address type (e.g., `Office`) and ensure it's generated alongside existing types, so there are clearly visible workplaces on the map.

## Implementation Steps

### Step 1: Fix background texture
- **SimulationDebug.tscn**: Change `MapBackground` from `ColorRect` to `TextureRect`.
  - Add an `[ext_resource]` for `res://assets/textures/PLACEHOLDER_city_map.png`.
  - Set `stretch_mode` to cover/fill the map area.
  - Keep the dark color fallback or remove it (the texture replaces it).

### Step 2: Multi-entity hover tooltip
- **SimulationDebug.cs** `UpdateHoverLabel()`: Instead of returning after finding the first match, collect ALL matching entities (player, persons, addresses) into a `List<string>`, then join them with newlines for the hover text.
  - Check player distance → add "You" if close enough.
  - Check ALL persons → add each matching person's name.
  - Check ALL addresses → add each matching address string.
  - Join all results with `\n` and display.
  - If list is empty, hide the label.

### Step 3: Black borders on icons
- **SimulationDebug.cs**: Replace `ColorRect` creation for both address icons and entity dots with `Panel` nodes using `StyleBoxFlat`:
  - Create a helper method `CreateIconPanel(Vector2 size, Color fillColor, Color borderColor, int borderWidth)` that returns a `Panel` with a `StyleBoxFlat` override.
  - Address icons: fill color as before, 2px black border.
  - Entity dots: fill color as before, 1–2px black border.
  - Update dictionary types from `Dictionary<int, ColorRect>` to `Dictionary<int, Panel>` (and `_playerNode` to `Panel`).
  - Update hover distance calculations to use the same size constants.

### Step 4: Add Office address type and ensure work locations appear
- **AddressType.cs**: Add `Office` to the `AddressType` enum. It maps to `AddressCategory.Commercial`.
- **LocationGenerator.cs**: Adjust `PickAddressType()` distribution to include `Office`:
  - 50% SuburbanHome, 20% Office, 15% Diner, 15% DiveBar.
- **SimulationDebug.cs**: Add a color for `Office` (e.g., cyan/teal: `0.2, 0.7, 0.9`).
- **PersonGenerator.cs**: No changes needed — it already picks from commercial addresses, which now includes Office.

### Step 5: Update changes.md
- Document all part 2 changes.
