# Procedural City Map Design

## Problem

The current city map is a random scatter of address icons on a fixed-size canvas. It doesn't feel like a city — there's no spatial structure, no streets, no neighborhoods. This limits both immersion and gameplay possibilities (e.g., investigating neighboring buildings for witnesses).

## Solution

Replace the point-cloud map with a procedurally generated grid-based city. Each city is a 100x100 grid of 48x48-pixel plots. Roads form a hierarchical grid creating city blocks. Buildings are placed within blocks using an urbanness gradient — dense apartments and offices in the center, suburban homes at the edges. The CityView becomes a pannable, zoomable viewport.

## Data Model

### Cell (struct, stored in CityGrid's 2D array)

Every position in the 100x100 grid is a Cell:

- `PlotType` (enum) — Road, SuburbanHome, ApartmentBuilding, Office, Diner, DiveBar, Park, Empty. Building values map directly to `AddressType` values; `PlotType` extends `AddressType` with grid-only types (Road, Empty).
- `AddressId` (int?) — set for building plots, null for roads and empty plots. Shared across all cells of a multi-plot building.
- `StreetId` (int?) — set for road plots, null otherwise
- `FacingDirection` (enum: North, South, East, West) — which side of the plot faces its connected road. Set for building plots.

### CityGrid

Holds the `Cell[100, 100]` array. Provides lookup methods:
- Get cell at position
- Get all cells for an address (by AddressId)
- Find adjacent road for a building plot
- Get plots by type (for choosing homes/workplaces during person generation)

### Changes to Address

- Remove `Position` (Vector2) — replaced by grid coordinates
- Add `GridX`, `GridY` (int) — position of the top-left cell of the building (anchor cell for multi-plot buildings)
- Pixel position derived: `new Vector2(GridX * 48, GridY * 48)`
- Street and number derived from grid position along the street
- Interiors (sublocations, connections) start empty and are generated lazily when a person is assigned or the player enters

### Changes to Street

- Add list of road cell coordinates representing the physical path of each street

### Plot Type Sizes

| PlotType | Grid Size | Visual Description |
|---|---|---|
| Road | 1x1 | Light gray |
| SuburbanHome | 1x1 | Dark gray rectangle on green background |
| ApartmentBuilding | 2x2 | Dark gray rectangle (96x96) |
| Office | 2x2 | Dark gray rectangle (96x96) |
| Diner | 1x1 | Dark gray rectangle |
| DiveBar | 1x1 | Dark gray rectangle |
| Park | 2x2 | Green with tree circles (96x96) |
| Empty | 1x1 | Plain green |

All buildings are the same dark gray color. Parks are the exception (green). This ensures highlight states (blue for player location, red for evidence board) pop visually.

## City Generation Pipeline

### Step 1: Place Arterial Roads

- Place full-span horizontal roads at roughly every 10 plots (±2 random variance)
- Place full-span vertical roads at roughly every 10 plots (±2 random variance)
- Edges of the map (row 0, row 99, col 0, col 99) are always arterial roads, bounding the city
- This creates large super-blocks of roughly 8x8 to 12x12 plots

### Step 2: Subdivide Super-Blocks

Each super-block's urbanness is computed from distance to center (gradient 0.0 at edges to 1.0 at center):

- **Urban (> 0.7)**: 1–2 secondary roads in each direction, creating blocks of ~4x4 to 6x6
- **Transitional (0.3–0.7)**: 0–1 secondary roads, creating blocks of ~5x5 to 8x8
- **Suburban (< 0.3)**: no subdivision or one road, leaving large blocks of 8x10+

Secondary roads span wall-to-wall within their super-block (between the bounding arterials), guaranteeing connectivity to the road network.

### Step 3: Assign Street Names

Each continuous run of road plots in a line gets a StreetId and a generated name. Arterials get grander names ("Boulevard", "Avenue"). Secondary roads get "Street", "Lane", etc.

### Step 4: Assign Plot Types to Blocks

For each city block (contiguous rectangle of non-road plots):

1. Compute urbanness weight from distance to center
2. Place multi-plot buildings (2x2) first using weighted random selection
3. Fill remaining 1x1 spaces with single-plot types
4. Any leftover space that can't fit a building becomes Empty

### Plot Type Weights

| PlotType | Urban Weight | Suburban Weight | Size |
|---|---|---|---|
| ApartmentBuilding | 30 | 5 | 2x2 |
| Office | 25 | 5 | 2x2 |
| SuburbanHome | 5 | 40 | 1x1 |
| Diner | 5 | 5 | 1x1 |
| DiveBar | 5 | 3 | 1x1 |
| Park | 10 | 10 | 2x2 |
| Empty | 5 | 20 | 1x1 |

The urbanness gradient interpolates between columns. At the center, apartments and offices dominate. At the edges, suburban homes and empty lots dominate.

### Step 5: Resolve Facing Directions and Street Connections

For each building plot:
- Determine which adjacent road it faces (prefer the side facing the more major road)
- Corner plots pick one road
- Record the StreetId from the faced road

### Step 6: Create Address Entities

For each building plot (or group of plots for multi-plot buildings):
- Create a lightweight Address with type, grid position, street, and facing direction
- Street number derived from position along the street — sequential by plot index, even numbers on one side, odd on the other
- Interiors left empty — generated lazily when needed

## Lazy Interior Generation

Plot types are assigned during city generation, but interiors are not. When a person is generated and needs a home, the algorithm:

1. Queries `CityGrid.GetPlotsByType()` for plots of the appropriate type that don't yet have resolved interiors (or whose Addresses have capacity)
2. Picks one using appropriate selection criteria
3. Calls a resolution method on CityGrid/Address that generates the interior (sublocations, connections)

Same process for workplaces. The player entering a building also triggers interior generation if not already resolved. The existing sublocation generation logic moves into this lazy resolution path.

## CityView Refactor

### Viewport

The CityView becomes a pannable, zoomable viewport over the 4800x4800 pixel grid (100 plots × 48px):

- **Left-click drag on empty space** — pan the viewport
- **Mouse wheel** — zoom in/out (with min/max bounds)
- Matches the evidence board's interaction model

### Rendering

- Only render plots visible in the current viewport (cull off-screen plots for performance)
- Roads: light gray fill with street names rendered along them
- Buildings: dark gray rectangles (all same color)
- Parks: green with tree circles
- Empty: plain green
- Driveways: small gray strip on the facing-direction edge connecting building to road
- Street names: always visible, repeating every ~8-10 plots along roads, no overlap. Horizontal text for east-west roads, rotated for north-south roads.
- Entity dots: people rendered as small circles at their current position (white = active, gray = sleeping, red = dead, blue = player)

### Highlights

- **Player's current building**: blue fill
- **Evidence board addresses**: red fill
- **Selected building**: white/yellow outline

### Interaction

- **Left-click a building plot**: selects it (or the full multi-plot group). Sidebar shows address info at top.
- **Sidebar actions** appear contextually:
  - "Go here" — if player is at a different address
  - "Enter building" — if player is at the selected address
- **Left-click road/empty/background**: deselects current selection

### Sidebar

The selected address appears at the top of the right-hand sidebar, showing:
- Address (e.g., "742 Elm Street")
- Type (e.g., "Suburban Home")
- Contextual action buttons below

## Movement

People continue to move in straight lines between grid positions (no road pathfinding yet). Travel time is computed from distance between grid positions, same formula as current system but using grid coordinates.

## Migration from Current System

- `LocationGenerator` is replaced entirely by the new city generation pipeline
- `Address.Position` (Vector2) is removed, replaced by `GridX`/`GridY`
- `MapConfig` bounds change from 1200x640 to 4800x4800 (or grid-relative equivalents)
- `CityView` is rewritten to render the grid with viewport panning/zooming
- `TravelInfo` and movement interpolation adapt to grid-based positions
