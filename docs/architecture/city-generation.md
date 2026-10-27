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
| `src/simulation/space/RoadGraph.cs` | The road cells as a graph for routing (see `simulation-core.md`) |
| `scenes/city/CityMap.cs` | Draws the grid, live traffic and the places the player knows |

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

Interiors are generated right after, for every address, by `WorldBuilder` (see `location-hierarchy.md`).

## Key Decisions
- **Grid plus road graph:** positions are `GridX/GridY * 48` px; trips follow road cells (about 80 m each).
- **One city per game, seeded by the game seed:** the same world every run for a seed, which tests and bug reports rely on.

## Connection Points
- **WorldBuilder** calls it and stores the result in `state.CityGrids[cityId]`; `RoadGraph` is built from it.
- **Adding a building type:** follow the `add-address-type` skill.
