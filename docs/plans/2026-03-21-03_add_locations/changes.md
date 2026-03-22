# Changes: Add Locations & Player Entity

## Step 1: Location entity classes
Created in `src/simulation/entities/`:
- **AddressType.cs** — `AddressType` enum (SuburbanHome, Diner, DiveBar), `AddressCategory` enum (Residential, Commercial), and `AddressTypeExtensions` helper with `GetCategory()` extension method
- **Country.cs** — Simple data class with `Name` property
- **City.cs** — Data class with `Id`, `Name`, `CountryName`
- **Street.cs** — Data class with `Id`, `Name`, `CityId`
- **Address.cs** — Data class with `Id`, `Number`, `StreetId`, `Type`, computed `Category`, and `Position` (Vector2)
- **Player.cs** — Lightweight class with `HomeAddressId` and `CurrentAddressId`

## Step 2: Street name data pool
Created `src/simulation/data/StreetData.cs`:
- 47 street base names (nature, presidents, landmarks, ordinals)
- 5 suffixes: Street, Avenue, Road, Drive, Lane
- Follows same static data pattern as `NameData.cs`

## Step 3: LocationGenerator
Created `src/simulation/LocationGenerator.cs`:
- `GenerateCity(SimulationState)` creates "United States" country and "Boston" city
- Generates 15 streets with unique random names from the pool (name + suffix)
- Each street gets 3–8 addresses with:
  - Log-normal distributed address numbers (centered ~200, range 1–10000)
  - Weighted type distribution: 60% SuburbanHome, 20% Diner, 20% DiveBar
  - Random positions within map bounds (40–1240 x, 40–680 y)

## Step 4: SimulationState updated
Added to `SimulationState`:
- `Player Player` property
- `List<Country> Countries`
- `Dictionary<int, City> Cities`
- `Dictionary<int, Street> Streets`
- `Dictionary<int, Address> Addresses`

## Step 5: Person and PersonGenerator updated
- **Person.cs** — Added `HomeAddressId`, `WorkAddressId`, `CurrentAddressId` properties
- **PersonGenerator.cs** — Now assigns random residential address as home, random commercial address as work, and sets `CurrentAddressId` to home. Added LINQ using statements.

## Step 6: SimulationManager updated
- Added `LocationGenerator` and calls `GenerateCity()` in `_Ready()`
- Added `AddressAdded` event (fired for each address after city generation)
- Added `PlayerCreated` event
- Creates Player with random residential home address in `_Ready()`
- Changed from generating 1 person to generating 5 people after 1 second
- Renamed `_initialPersonGenerated` → `_initialPeopleGenerated`

## Step 7: SimulationDebug scene overhauled
Rebuilt `SimulationDebug.tscn`:
- Removed MarginContainer/VBoxContainer/TitleLabel/PeopleHeaderLabel/PeopleList layout
- Added CityMap (full-screen Control) with:
  - MapBackground (dark ColorRect, color 0.08/0.08/0.12)
  - LocationIcons (Control container for address markers)
  - EntityDots (Control container for person/player dots)
- ClockLabel repositioned to top-right corner (24pt)
- Added HoverLabel (hidden by default, 14pt, positioned dynamically near mouse)

## Step 8: SimulationDebug.cs rewritten
- Subscribes to `AddressAdded`, `PersonAdded`, `PlayerCreated` events
- Creates 12×12 ColorRect icons for addresses at their positions, color-coded:
  - SuburbanHome: green (0.2, 0.8, 0.2)
  - Diner: yellow (0.9, 0.9, 0.2)
  - DiveBar: red (0.9, 0.2, 0.2)
- Creates 8×8 ColorRect dots for entities:
  - Persons: white
  - Player: blue (0.3, 0.5, 1.0)
- Hover tooltip: checks mouse distance (≤10px) to player dot, person dots, then address icons. Shows "You", person name, or "42 Main Street (SuburbanHome)" respectively.

## Step 9: Build and verify
- Unable to build via CLI due to .NET SDK version mismatch (shell resolves to 7.0, project targets 8.0). Build deferred to VSCode/Godot editor.

---

## Part 2: Fixes & Polish

### Step 1: Fix background texture
- **SimulationDebug.tscn**: Changed `MapBackground` from `ColorRect` to `TextureRect`.
  - Added `ext_resource` for `res://assets/textures/PLACEHOLDER_city_map.png` (uid `uid://stwfxvxd88xp`).
  - Set `stretch_mode = 1` (STRETCH_SCALE) to fill the map area.
  - Incremented `load_steps` from 3 to 4.

### Step 2: Multi-entity hover tooltip
- **SimulationDebug.cs** `UpdateHoverLabel()`: Replaced single-match early-exit logic with a `List<string>` that collects all matching entities. Checks player, then all persons, then all addresses — all without breaking early. Joined with `\n`.

### Step 3: Black borders on icons
- **SimulationDebug.cs**: Added `CreateIconPanel(Vector2 size, Color fillColor, Color borderColor, int borderWidth)` helper that returns a `Panel` with a `StyleBoxFlat` override.
- Changed `_addressNodes` and `_personNodes` from `Dictionary<int, ColorRect>` to `Dictionary<int, Panel>`; `_playerNode` from `ColorRect` to `Panel`.
- Address icons: 2px black border. Entity dots: 1px black border.
- Added `IconBorderWidth = 2` and `DotBorderWidth = 1` constants.

### Step 4: Add Office address type
- **AddressType.cs**: Added `Office` to the `AddressType` enum. Falls through to `Commercial` in `GetCategory()` via the existing wildcard case.
- **LocationGenerator.cs**: Updated `PickAddressType()` distribution — 50% SuburbanHome, 20% Office, 15% Diner, 15% DiveBar.
- **SimulationDebug.cs**: Added `OfficeColor = (0.2, 0.7, 0.9)` (cyan/teal) and a case in `GetAddressColor()`.

---

## Part 3: Fix Event Subscription Race Condition

### Root cause
Both remaining issues (no address icons on map, no multi-entity tooltips) shared a single root cause: in `SimulationDebug._Ready()`, event subscriptions were registered **after** `AddChild(_simulationManager)`. Since `AddChild` triggers `SimulationManager._Ready()`, which fires `AddressAdded` (for all ~60-90 addresses) and `PlayerCreated` immediately, those events fired before any handler was attached. Only `PersonAdded` worked because it fires after a 1-second delay in `_Process`.

### Fix
- **SimulationDebug.cs**: Moved the three event subscriptions (`AddressAdded`, `PersonAdded`, `PlayerCreated`) to **before** the `AddChild(_simulationManager)` call so handlers are in place when `_Ready()` fires events.
