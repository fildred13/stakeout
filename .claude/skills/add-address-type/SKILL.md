---
name: add-address-type
description: Use when adding a new kind of address (building type) to the city, e.g. a hospital, warehouse or police station - covers the interior template, city placement, optional business, and tests.
---

# Add an Address Type

An address type is a building on the city grid. Its interior (Locations, SubLocations, AccessPoints, Fixtures)
is built by an `IAddressTemplate`. See `docs/architecture/location-hierarchy.md` and `city-generation.md` first.

## Files to touch

| Step | File | Change |
|---|---|---|
| 1 | `src/simulation/entities/AddressType.cs` | Add enum value; add it to `GetCategory()` (if not Commercial) and `ToPlotType()` |
| 2 | `src/simulation/city/PlotType.cs` | Add `PlotType` value; add to `GetSize()` if not 1x1, and to `ToAddressType()` |
| 3 | `src/simulation/city/CityGenerator.cs` | Add a weight row to `LargeBuildingTypes` or `SmallBuildingTypes` (urban weight, suburban weight) |
| 4 | `src/simulation/addresses/<Name>Template.cs` | New `IAddressTemplate` (see pattern below) |
| 5 | `src/simulation/addresses/AddressTemplateRegistry.cs` | `Register(AddressType.<Name>, new <Name>Template())` in `RegisterAll()` |
| 6 | `stakeout.tests/Simulation/Addresses/<Name>TemplateTests.cs` | Tests (copy `DinerTemplateTests.cs` shape) |
| 7 | `scenes/evidence_board/EvidenceBoardScene.cs` | Add the one-letter icon in the `AddressType` switch |
| 8 | `docs/design/UI.md` | Add the map color row if the city view should color it |

If people work there, also add a business (step 9-11):

| 9 | `src/simulation/entities/Business.cs` | Add `BusinessType` value |
| 10 | `src/simulation/businesses/<Name>BusinessTemplate.cs` | New `IBusinessTemplate`: hours, positions, name |
| 11 | `src/simulation/businesses/BusinessTemplateRegistry.cs` + `BusinessGenerator.AddressTypeToBusinessType` | Register and map it |

## Template pattern

```csharp
public class WarehouseTemplate : IAddressTemplate
{
    public void Generate(Address address, SimulationState state, Random random)
    {
        LocationBuilders.ExteriorParkingLot(state, address);   // shared helpers first

        var floor = LocationBuilders.CreateLocation(state, address, "Warehouse Floor",
            new[] { "staff_only", "work_area", "entrance" });
        floor.AccessPoints.Add(new AccessPoint
        {
            Id = state.GenerateEntityId(),
            Name = "Loading Door",
            Type = AccessPointType.Door,
            Tags = new[] { "main_entrance" },
            LockMechanism = LockMechanism.Key
        });
        LocationBuilders.CreateFixture(state, FixtureType.TrashCan, "Dumpster",
            locationId: floor.Id, subLocationId: null);
    }
}
```

Use `random` (never `new Random()`) so generation stays reproducible from the city seed.

## Conventions the rest of the sim relies on

- One Location tagged `"entrance"` holding an AccessPoint tagged `"main_entrance"` (visitors touch it; the detective
  examines and picks it). A Location named `"Lobby"` makes everyone cross it for a minute on the way in and out.
- Customers settle in the Location tagged `service_area`; staff by role in `Places.Workplace` (add your role there).
- Common Location tags: `exterior`, `publicly_accessible`, `staff_only`, `private`, `residential`, `work_area`,
  `service_area`, `food`, `social`, `storage`, `restroom`, `parking`. `Places` and the planner find rooms by tag, not by name.
- SubLocations only for rooms inside a Location (e.g. rooms of an apartment unit). No deeper nesting.
- Homes are Locations tagged `home` built with `LocationBuilders.HomeRooms` (phone, desk, knife block, nightstand,
  closet, hamper): `PopulationGenerator` houses people in them and evidence lives in those fixtures.
- Interiors are generated for every address at world creation (`WorldBuilder`), so keep them modest in size.
- Reuse `LocationBuilders` helpers (`ExteriorParkingLot`, `ApartmentUnit`, `Restroom`, `SecurityRoom`); add a new helper
  there when a second template needs the same room.

## Test checklist

- Expected Locations exist (`state.FindLocationByTag(addr.Id, "<tag>")`)
- Exactly one `main_entrance` AccessPoint, on an `entrance` Location
- Fixtures land on the right Location/SubLocation
- Same seed gives the same layout (if the template uses `random`)
- `dotnet test stakeout.tests/` passes, including `CityGeneratorTests` after the placement change
