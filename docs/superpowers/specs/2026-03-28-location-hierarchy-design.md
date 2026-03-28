# Project 1: Location Hierarchy & Address Templates — Design Spec

Part of the [Simulation Overhaul Master Plan](../../future_plans/simulation_overhaul_plan.md).

## Purpose

Replace the sublocation graph (BFS pathfinding, connection edges, traversal contexts) with a simpler Address → Location → SubLocation hierarchy. Build a composable address type template system. Add a second city (NYC) with functional inter-city air travel to prove the multi-city data model works.

## Entity Model

### Address

Mostly unchanged. Represents a physical building/property at a street address on the city grid.

```
Address
  Id: int
  CityId: int                    # NEW — which city this address belongs to
  Number: int                    # street number
  StreetId: int                  # which street
  Type: AddressType              # SuburbanHome, ApartmentBuilding, Diner, etc.
  Category: AddressCategory      # Residential, Commercial, Public
  GridX, GridY: int              # position on city grid
  LocationIds: List<int>         # NEW — replaces Sublocations dict and Connections list
```

**Changes from current:** `Dictionary<int, Sublocation> Sublocations` and `List<SublocationConnection> Connections` replaced by `List<int> LocationIds`. Added `CityId`.

### Location

New entity. Replaces the concept of a top-level sublocation group. Represents a distinct area within an address: a room, a unit, a parking lot, a floor, etc.

```
Location
  Id: int
  AddressId: int                 # parent address
  Name: string                   # "Unit 2B", "Exterior Parking Lot", "Lobby"
  Tags: string[]                 # ["residential", "private"]
  Floor: int?                    # nullable, for multi-story buildings
  UnitLabel: string?             # nullable, for apartment units ("2B")
  SubLocationIds: List<int>      # optional child sub-locations
  AccessPoints: List<AccessPoint># doors, windows, gates guarding this location
```

### SubLocation

New entity. An optional detail layer within a Location. Represents a room within a unit, an area within a floor, etc.

```
SubLocation
  Id: int
  LocationId: int                # parent location
  Name: string                   # "Bedroom", "Kitchen", "Manager's Office"
  Tags: string[]                 # ["bedroom"], ["work_area", "food"]
```

### AccessPoint

New entity. Replaces `SublocationConnection`. A barrier/entry point that guards a Location or SubLocation. Not a graph edge — just a property of the thing it protects.

```
AccessPoint
  Id: int
  Name: string                   # "Front Door", "Window", "Back Gate"
  Type: AccessPointType          # Door, Window, Gate, Hatch, etc.
  Tags: string[]                 # ["main_entrance"], ["covert_entry"], ["staff_entry"]
  IsLocked: bool                 # mutable state
  IsBroken: bool                 # mutable state
  LockMechanism: LockMechanism?  # Key, Combination, Keypad, Electronic (nullable if not lockable)
  KeyItemId: int?                # which key item opens this (nullable)
```

**Key difference from SublocationConnection:** No `FromSublocationId`/`ToSublocationId`. An AccessPoint doesn't connect two nodes — it's a barrier on the thing it guards. "Unit 2B has a locked front door" rather than "Connection #47 links Hallway to Unit2B."

No `FingerprintSurface` — fingerprint traces on doors move to the trace system in Project 2.

### Enums

```
AccessPointType: Door, Window, Gate, Hatch, SecurityGate
LockMechanism: Key, Combination, Keypad, Electronic
AddressType: SuburbanHome, ApartmentBuilding, Diner, DiveBar, Office, Park, Airport  # Airport is NEW
```

## Tag Vocabulary

Tags are string arrays on Locations and SubLocations for querying and behavioral logic.

**Location-level tags:**
- `exterior` — outdoors (parking lot, yard, alley)
- `publicly_accessible` — anyone can enter without breaking in
- `private` — requires access/breaking in
- `staff_only` — accessible to employees
- `entrance` — main entry point to the address
- `residential` — a living space
- `commercial` — a business space
- `parking` — vehicle storage
- `security` — security room/system

**SubLocation-level tags:**
- `bedroom`, `kitchen`, `living`, `restroom`, `office`, `storage` — room types
- `work_area` — where employees do their jobs
- `service_area` — where customers are served
- `social` — where people gather casually
- `food` — where food is available

**AccessPoint-level tags:**
- `main_entrance`, `staff_entry`, `covert_entry` — entry type

This vocabulary will evolve as we add more location types and gameplay.

## SimulationState & Storage

Single source of truth — entities live in SimulationState dictionaries, parent objects hold ID references only. Eliminates the current redundancy where sublocations exist in both Address and SimulationState.

```
SimulationState
  Cities: Dictionary<int, City>
  Streets: Dictionary<int, Street>
  Addresses: Dictionary<int, Address>
  Locations: Dictionary<int, Location>         # NEW — replaces Sublocations
  SubLocations: Dictionary<int, SubLocation>   # NEW
  People: Dictionary<int, Person>
  Jobs: Dictionary<int, Job>
  CityGrids: Dictionary<int, CityGrid>        # NEW — one grid per city (was single CityGrid)
```

### City Entity (expanded)

```
City
  Id: int
  Name: string                   # "Boston"
  CountryId: int
  AddressIds: List<int>          # all addresses in this city
  AirportAddressId: int          # direct reference to airport
```

### Query Helpers on SimulationState

```
GetLocationsForAddress(addressId) → List<Location>
GetSubLocationsForLocation(locationId) → List<SubLocation>
FindLocationByTag(addressId, tag) → Location?
FindSubLocationByTag(locationId, tag) → SubLocation?
GetAddressesForCity(cityId) → List<Address>
GetCityForAddress(addressId) → City
```

### Person & Player Changes

```
Person
  CurrentCityId: int?            # NEW — which city the person is in
  CurrentAddressId: int?         # unchanged
  CurrentLocationId: int?        # NEW — replaces CurrentSublocationId
  CurrentSubLocationId: int?     # NEW
  HomeAddressId: int             # unchanged
  HomeLocationId: int?           # NEW — replaces HomeUnitTag for apartment residents
```

`HomeUnitTag` (the `"unit_f2_3"` string convention) is replaced by `HomeLocationId` which directly references the Location entity for the person's apartment unit. Cleaner — no tag string parsing.

## Address Type Template System

### Interface

```csharp
public interface IAddressTemplate
{
    void Generate(Address address, SimulationState state, Random random);
}
```

Replaces `ISublocationGenerator`. Instead of returning a `SublocationGraph`, templates directly populate the address with Locations, SubLocations, and AccessPoints via SimulationState.

### AddressTemplateRegistry

Maps `AddressType → IAddressTemplate`. Replaces `SublocationGeneratorRegistry`.

### Location Builders

Reusable static methods that create common location patterns. Any template can call these.

```csharp
public static class LocationBuilders
{
    // Creates a Location with exterior, publicly_accessible, parking tags
    public static Location ExteriorParkingLot(SimulationState state, int addressId, Random random);

    // Creates a Location with residential, private tags + bedroom/kitchen/living/restroom SubLocations + locked front door
    public static Location ApartmentUnit(SimulationState state, int addressId, int floor, string unitLabel, Random random);

    // Creates a SubLocation with restroom tag
    public static SubLocation Restroom(SimulationState state, int locationId, Random random);

    // Creates a Location with security, private tags
    public static Location SecurityRoom(SimulationState state, int addressId, Random random);

    // etc.
}
```

### The Seven Address Templates

**SuburbanHomeTemplate**
- Locations: Front Yard (`exterior`, `entrance`), Interior (`residential`, `private`)
- Interior SubLocations: hallway, kitchen, living room, bathroom, 2-3 bedrooms, optional office
- AccessPoints: front door (locked, `main_entrance`), optional back door (locked), window (`covert_entry`)

**ApartmentBuildingTemplate**
- Locations: ExteriorParkingLot (builder), Lobby (`publicly_accessible`, `entrance`), SecurityRoom (builder), N units per floor via ApartmentUnit builder
- Each unit: Location with `residential`, `private`, UnitLabel, Floor, locked door, bedroom/kitchen/living/restroom SubLocations
- Configurable: 4-20 floors, 4-8 units per floor

**DinerTemplate**
- Locations: ExteriorParkingLot (builder), Dining Area (`publicly_accessible`, `service_area`, `entrance`), Kitchen (`staff_only`, `work_area`), Storage (`staff_only`, `storage`), Manager's Office (`staff_only`, `private`), Restroom (`publicly_accessible`)
- AccessPoints: front door (`main_entrance`), back door (locked, `staff_entry`)

**DiveBarTemplate**
- Locations: Alley (`exterior`, `covert_entry`), Bar Area (`publicly_accessible`, `service_area`, `entrance`), Back Hallway (`staff_only`), Storage (`staff_only`), Manager's Office (`staff_only`, `private`), Restroom (`publicly_accessible`)
- AccessPoints: front door (`main_entrance`), back door (locked, `staff_entry`)

**OfficeTemplate**
- Locations: Lobby (`publicly_accessible`, `entrance`), SecurityRoom (builder), office floors
- Each floor: Location with SubLocations: reception, cubicle area (`work_area`), manager's office (`private`), break room (`food`), restroom
- Configurable: 1-5 floors

**ParkTemplate**
- Locations: Parking Lot (builder), Main Entrance (`exterior`, `entrance`), Jogging Path, Picnic Area, Playground, Wooded Area (`exterior`, `covert_entry`), optional Shore, Restroom Building
- All locations: `publicly_accessible`, `exterior` (except restroom building interior)

**AirportTemplate**
- Single Location: Terminal (`publicly_accessible`, `entrance`)
- No SubLocations, no staff, no interior detail
- Grid footprint: 10x20
- One per city, referenced by `City.AirportAddressId`
- Gameplay: player enters, selects destination city, travels

## Multi-City Setup

### Initialization

On simulation start, two cities are created:

1. **Boston, USA** — the starting city. Full city grid generation with all address types. Airport placed on grid.
2. **New York City, USA** — second city. Full city grid generation. Airport placed on grid.

Each city gets its own `CityGrid` instance, stored in `SimulationState.CityGrids`.

### Inter-City Travel

Player can enter either city's Airport address and see a list of other cities. Selecting a destination:

1. Fixed travel time (2 hours).
2. Game clock advances by travel time.
3. Player's `CurrentCityId`, `CurrentAddressId` update to destination city's Airport.
4. Player appears at the destination Airport.

Both cities' NPCs simulate concurrently regardless of which city the player is in.

### CityGenerator Changes

CityGenerator now takes a City entity and generates its grid. Called once per city during initialization. After normal layout generation, it places one Airport on the grid (10x20 footprint, special-cased placement to ensure it fits).

## What Gets Removed

### Deleted Entirely
- `SublocationGraph.cs` — no more graph/pathfinding
- `SublocationConnection` class from `Sublocation.cs` — replaced by AccessPoint
- `ConnectionProperties.cs` — folded into AccessPoint / deferred to Project 2
- `TraversalContext.cs` — no traversal
- `PathStep.cs` — no pathfinding
- `ISublocationGenerator.cs` — replaced by `IAddressTemplate`
- All 6 current generators — replaced by 7 new templates
- `SublocationGeneratorRegistry.cs` — replaced by `AddressTemplateRegistry`

### Gutted with TODO Comments
- `PersonBehavior.cs` — `// TODO: Project 3 (NPC Brain) replaces this entirely`
- `ScheduleBuilder.cs` — `// TODO: Project 3`
- `TaskResolver.cs` — `// TODO: Project 3`
- `DailySchedule.cs`, `ScheduleEntry.cs` — `// TODO: Project 3`
- All 6 decomposition strategies — `// TODO: Project 3`
- `ObjectiveResolver.cs`, `Task.cs` — `// TODO: Project 3`
- `DoorLockingService.cs` — `// TODO: Project 3`
- `FingerprintService.cs` — `// TODO: Project 2 (Fixtures & Traces)`
- `ActionExecutor.cs` — `// TODO: Project 5 (Action Templates)`
- `GraphView.cs` — `// TODO: Project 8 (Player UI)`

### Modified
- `Address.cs` — new structure (LocationIds, CityId)
- `Sublocation.cs` — replaced by new `Location.cs` and `SubLocation.cs`
- `SimulationState.cs` — new collections, query helpers, per-city grids
- `City.cs` — expanded with AddressIds, AirportAddressId
- `Person.cs` — CurrentLocationId, CurrentSubLocationId, CurrentCityId, HomeLocationId
- `Player.cs` — same changes as Person
- `SimulationManager.cs` — multi-city init, both cities simulating
- `PersonGenerator.cs` — new entities; schedule-related parts get `// TODO: Project 3`
- `CityGenerator.cs` — Airport placement, receives City entity
- `LocationGenerator.cs` — new template registry
- `AddressType.cs` — add Airport enum value

## Testing Strategy

- Unit tests for each AddressTemplate: verify they produce the expected Locations, SubLocations, AccessPoints, and tags
- Unit tests for LocationBuilders: verify reusable builders produce correct entities
- Unit tests for SimulationState query helpers
- Integration test: generate both cities, verify airports exist, verify address counts, verify city-address linkage
- Integration test: player flies from Boston to NYC, verify CurrentCityId/CurrentAddressId update correctly
- Verify all entity ID references are consistent (no dangling IDs)
