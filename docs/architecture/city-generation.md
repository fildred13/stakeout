# City Generation

## Purpose
Builds each city as a 100x100 cell grid of roads and building plots, creates an Address and Street records for
every plot, places an airport, and creates the businesses that occupy commercial addresses. Deterministic per seed.

## Key Files
| File | Role |
|------|------|
| `src/simulation/city/CityGenerator.cs` | The whole pipeline (seeded `Random`); placement weight tables `LargeBuildingTypes` / `SmallBuildingTypes` |
| `src/simulation/city/CityGrid.cs` | Cell storage plus lookups (`GetCellsForAddress`, `GetUnresolvedAddressIdsByType`) |
| `src/simulation/city/Cell.cs` | One cell: PlotType, AddressId, StreetId, FacingDirection |
| `src/simulation/city/PlotType.cs` | Plot kinds, plot sizes (`GetSize`), PlotType to AddressType mapping |
| `src/simulation/entities/AddressType.cs` | AddressType, AddressCategory (Residential/Commercial/Public), AddressType to PlotType mapping |
| `src/simulation/data/StreetData.cs`, `NameData.cs`, `BusinessNameData.cs` | Name tables |
| `scenes/city/CityView.cs` | Draws the grid (roads, buildings with driveways, parks, street labels) |

## How It Works
`CityGenerator.Generate(state, city)`:
1. Arterial roads every ~10 cells (with jitter) plus the map edges.
2. Super-blocks are subdivided with secondary roads; more roads near the center ("urbanness" = closeness to center).
3. Streets are created and named for every road run; arterials get major names.
4. Plots are filled block by block: large types (apartments, offices, parks) and small types (homes, diners, bars,
   empty lots) are chosen with weights interpolated between urban and suburban values.
5. Each building gets a facing direction toward an adjacent road and an Address (number + street) in `state.Addresses`.
6. A 10x20 airport is placed where it fits; `City.AirportAddressId` points to it.
7. `BusinessGenerator.CreateBusinessesForCity` creates one Business per Commercial address (see `businesses.md`).

Address interiors are *not* generated here; see `location-hierarchy.md` for lazy resolution.

## Key Decisions
- **Grid, not graph:** positions are `GridX/GridY * 48` px; travel time is straight-line distance (`MapConfig`), roads are visual only.
- **One seed per city** (Boston 42, NYC 84): the same world every run, which tests and bug reports rely on.
- **Everything placed up front, resolved lazily:** cheap addresses for the whole map, expensive interiors only on demand.

## Connection Points
- **SimulationManager** calls it once per city during `_Ready()` and stores the result in `state.CityGrids[cityId]`.
- **LocationGenerator.PickAndResolveAddress** picks unclaimed addresses of a type from the grid for homes.
- **Adding a building type:** follow the `add-address-type` skill.
