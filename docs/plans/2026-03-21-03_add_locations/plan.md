# Plan: Add Locations & Player Entity

## Overview

This plan adds the location hierarchy (Country → City → Street → Address) to the simulation, introduces the Player as a located entity, assigns home/work locations to people, and replaces the current debug text list with an interactive city map view.

## Data Model

### Location Hierarchy

```
Country
├── name: string ("United States")
└── Cities[]

City
├── id: int (entity ID)
├── name: string ("Boston")
├── countryName: string
└── Streets[]

Street
├── id: int (entity ID)
├── name: string ("Main Street")
└── cityId: int

Address
├── id: int (entity ID)
├── number: int (1–10000, skewed bell curve centered ~200)
├── streetId: int
├── type: AddressType enum (SuburbanHome, Diner, DiveBar)
├── category: AddressCategory enum (Residential, Commercial)
├── position: Vector2 (x, y in city map space)
└── DisplayName => "{number} {street.Name}" (e.g., "42 Main Street")
```

### Enums

```csharp
public enum AddressType { SuburbanHome, Diner, DiveBar }
public enum AddressCategory { Residential, Commercial }
```

`AddressType` maps to `AddressCategory` via a helper — `SuburbanHome` → `Residential`, `Diner`/`DiveBar` → `Commercial`. This keeps it simple while allowing future types to be added easily.

### Player Entity

The Player is not a `Person` — it's its own lightweight class:

```csharp
public class Player
{
    public int HomeAddressId { get; set; }
    public int CurrentAddressId { get; set; }
}
```

The Player doesn't need an entity ID or name in the simulation — they're the user. They do need a location.

### Person Changes

`Person` gains two location fields:

```csharp
public int HomeAddressId { get; set; }
public int WorkAddressId { get; set; }
public int CurrentAddressId { get; set; }
```

When generated, each Person is assigned a random residential address as home and a random commercial address as work. Their `CurrentAddressId` starts at their home.

### SimulationState Changes

```
SimulationState
├── GameClock Clock
├── Dictionary<int, Person> People
├── Player Player                          // NEW
├── List<Country> Countries                // NEW (small, no ID needed)
├── Dictionary<int, City> Cities           // NEW
├── Dictionary<int, Street> Streets        // NEW
├── Dictionary<int, Address> Addresses     // NEW
└── GenerateEntityId()
```

Countries are stored as a simple list since there will be very few and they don't need IDs. Cities, Streets, and Addresses use the same entity ID / Dictionary pattern as People.

## Street Name Data

A new `StreetData.cs` alongside `NameData.cs` will hold ~60 realistic U.S. street names:

```
Main, Elm, Oak, Maple, Cedar, Pine, Walnut, Chestnut, Birch, Spruce,
Washington, Lincoln, Franklin, Jefferson, Adams, Madison, Monroe, Jackson,
Park, Church, School, Mill, River, Lake, Hill, Valley, Spring, Meadow,
Broadway, Central, Market, Commerce, Union, Liberty, Court, Pleasant,
Highland, Prospect, Summit, Forest, Garden, Orchard,
First, Second, Third, Fourth, Fifth
```

Each name gets " Street", " Avenue", " Road", " Drive", or " Lane" appended randomly during generation.

## Address Number Generation

The prompt asks for 1–10000 with a skewed bell curve centered on 200. This is a log-normal-ish distribution. Implementation:

```csharp
// Box-Muller to generate normal(0,1), then transform
double u1 = 1.0 - random.NextDouble();
double u2 = random.NextDouble();
double normal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
// Target: mean=200, allow spread toward 10000, clamp [1, 10000]
double logMean = Math.Log(200);
double logStd = 1.0;
int number = (int)Math.Clamp(Math.Exp(logMean + logStd * normal), 1, 10000);
```

This gives most addresses in the low hundreds with a long tail toward higher numbers.

## City Map Generation

Addresses need x,y positions within the city map. The map area corresponds to the viewport (1280×720) minus UI margins. During city generation, each address gets a random position within this space. No clustering for now — just uniform random placement.

## Debug Screen Overhaul

The current debug screen shows a text-based clock and people list. We'll replace the main area with a city map view while keeping the clock in a corner.

### Scene Structure

```
SimulationDebug (Control, full-screen)
├── CityMap (Control, full-screen)
│   ├── MapBackground (TextureRect — placeholder .png)
│   ├── LocationIcons (Control — container for address markers)
│   │   └── [dynamically added ColorRect nodes, small rounded squares]
│   └── EntityDots (Control — container for person/player dots)
│       └── [dynamically added ColorRect nodes, small circles]
├── ClockLabel (top-right corner, existing font)
└── HoverLabel (Label, follows mouse, hidden by default)
```

### Map Interactions

- **Location icons**: Small (12×12) rounded-corner squares drawn at each address's (x,y). Color-coded by type:
  - Suburban Home: green
  - Diner: yellow
  - Dive Bar: red
- **Entity dots**: Small (8×8) circles at the current address position of each Person and the Player.
  - Persons: white dots
  - Player: blue dot
- **Hover text**: When the mouse is within ~10px of an icon/dot, show a tooltip Label near the cursor:
  - For locations: "42 Main Street (Suburban Home)"
  - For entities: "John Smith" or "You" for the player

### Placeholder Map Background

A simple placeholder `.png` will serve as the city map background. Place it at:

```
assets/textures/PLACEHOLDER_city_map.png
```

This should be a simple dark-toned image (1280×720).

## New & Modified Files

### New Files

| File | Purpose |
|------|---------|
| `src/simulation/entities/Address.cs` | Address data class with type, position, street reference |
| `src/simulation/entities/Street.cs` | Street data class with name, city reference |
| `src/simulation/entities/City.cs` | City data class with name, country reference |
| `src/simulation/entities/Country.cs` | Country data class (name only) |
| `src/simulation/entities/Player.cs` | Player data class with location |
| `src/simulation/entities/AddressType.cs` | AddressType and AddressCategory enums |
| `src/simulation/LocationGenerator.cs` | Generates streets and addresses for a city |
| `src/simulation/data/StreetData.cs` | Static pool of street names |

### Modified Files

| File | Change |
|------|--------|
| `src/simulation/entities/Person.cs` | Add HomeAddressId, WorkAddressId, CurrentAddressId |
| `src/simulation/SimulationState.cs` | Add Player, Countries, Cities, Streets, Addresses collections |
| `src/simulation/SimulationManager.cs` | Generate city & locations on startup, assign locations to people, add events |
| `src/simulation/PersonGenerator.cs` | Assign home/work/current address when generating a person |
| `scenes/simulation_debug/SimulationDebug.tscn` | Replace text list with map-based layout |
| `scenes/simulation_debug/SimulationDebug.cs` | Implement city map rendering, hover tooltips, location/entity display |

## Implementation Steps

### Step 1: Location entity classes

Create the entity classes: `Country.cs`, `City.cs`, `Street.cs`, `Address.cs`, `AddressType.cs`, and `Player.cs` in `src/simulation/entities/`.

These are pure data classes following the same pattern as `Person.cs` — properties, no behavior.

**Update changes.md after this step.**

### Step 2: Street name data pool

Create `src/simulation/data/StreetData.cs` with ~60 street names and ~5 suffixes (Street, Avenue, Road, Drive, Lane).

**Update changes.md after this step.**

### Step 3: LocationGenerator

Create `src/simulation/LocationGenerator.cs` that:

1. Creates the "United States" Country and "Boston" City
2. Generates a configurable number of streets (start with ~15) with random names from the pool
3. Generates addresses along those streets:
   - Each street gets 3–8 addresses
   - Address numbers use the skewed bell curve distribution
   - Address types are randomly assigned with a weighted distribution: ~60% SuburbanHome, ~20% Diner, ~20% DiveBar
   - Each address gets a random (x,y) position within the map bounds
4. Registers everything in SimulationState

Expose a method like `GenerateCity(SimulationState state)` that does all of this.

**Update changes.md after this step.**

### Step 4: Update SimulationState

Add to `SimulationState`:
- `Player Player` property
- `List<Country> Countries` collection
- `Dictionary<int, City> Cities` collection
- `Dictionary<int, Street> Streets` collection
- `Dictionary<int, Address> Addresses` collection

**Update changes.md after this step.**

### Step 5: Update Person and PersonGenerator

Add `HomeAddressId`, `WorkAddressId`, and `CurrentAddressId` to `Person`.

Update `PersonGenerator.GeneratePerson()` to:
1. Pick a random residential address → set as HomeAddressId
2. Pick a random commercial address → set as WorkAddressId
3. Set CurrentAddressId = HomeAddressId (people start at home)

**Update changes.md after this step.**

### Step 6: Update SimulationManager

Modify `SimulationManager._Ready()` to:
1. Call `LocationGenerator.GenerateCity(state)` to populate the world
2. Create a Player with a random residential address as their home
3. Set Player.CurrentAddressId = Player.HomeAddressId

Add new events:
- `event Action<Address> AddressAdded` (fired during city generation, so UI can place icons)
- `event Action PlayerCreated`

The existing person generation (at 1 second) continues to work — now people just also get locations.

**Update changes.md after this step.**

### Step 7: Overhaul SimulationDebug scene

Rebuild `SimulationDebug.tscn`:
- Remove the VBoxContainer/ItemList/PeopleHeaderLabel layout
- Add a full-screen CityMap container with a dark background (ColorRect fallback)
- Keep ClockLabel repositioned to top-right corner
- Add a HoverLabel (hidden by default, positioned near mouse)

**Update changes.md after this step.**

### Step 8: Implement city map rendering in SimulationDebug.cs

Rewrite `SimulationDebug.cs` to:

1. On `_Ready()`:
   - Create SimulationManager, subscribe to events
   - After city generates, create a small ColorRect node for each address at its (x,y) position
   - Store references for hit-testing

2. On `PersonAdded` / `PlayerCreated`:
   - Create a small dot node at the entity's current address position
   - Store references for hit-testing

3. On `_Process()`:
   - Update clock label
   - Check mouse position against all icons/dots
   - If hovering over something, show HoverLabel with appropriate text
   - If not hovering, hide HoverLabel

For hit-testing, simple distance check from mouse position to each icon center — with <100 entities total, brute force is fine.

**Update changes.md after this step.**

### Step 9: Test and verify

Build the project, launch, click "New Career", and verify:
- City map appears with colored location icons
- Hovering over locations shows address + type
- After 1 second, a person dot appears at a home address
- Hovering over the person dot shows their name
- Player dot (blue) appears at their home address
- Hovering over player dot shows "You"
- Clock still ticks in the corner

**Update changes.md after this step.**

## Design Decisions & Notes

- **Country/City are mostly scaffolding for now.** We hardcode "United States" / "Boston" and won't reference them much. But having the hierarchy in place means expanding to multiple cities later is just data, not architecture.

- **No Floor or SubLocation yet.** Per the prompt, we're deferring the lower levels of the hierarchy. The Address entity is the leaf node for now. When we add floors/sublocations later, they'll live under Address.

- **Map coordinates are screen-space for now.** Addresses get (x,y) positions in pixel space matching the viewport. If we later want a scrollable/zoomable map, we'd introduce a world-space coordinate system and a camera transform. For now, 1:1 pixel mapping keeps it simple.

- **Generating multiple people.** The current SimulationManager only generates one person after 1 second. We should generate a small batch (e.g., 5 people) so the map has enough dots to be interesting. This is a minor tweak to the existing generation logic.
