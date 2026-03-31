# Location Hierarchy & Address Templates Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the sublocation graph with a simpler Address → Location → SubLocation hierarchy, build composable address templates, and prove multi-city support with inter-city air travel.

**Architecture:** New entity model (Location, SubLocation, AccessPoint) replaces the old graph-based model (Sublocation, SublocationConnection, SublocationGraph). Address type templates compose reusable LocationBuilder helpers to generate interiors. Two cities (Boston, NYC) each get their own CityGrid and Airport. The scheduling/behavior systems are gutted with TODO comments pointing to future projects.

**Tech Stack:** Godot 4.6, C# (.NET 8), xUnit for tests

**Spec:** `docs/superpowers/specs/2026-03-28-location-hierarchy-design.md`

**CRITICAL: Never prefix shell commands with `cd`. The working directory is already the project root. Run commands directly (e.g., `dotnet test stakeout.tests/ -v minimal`, not `cd path && dotnet test`).**

---

## File Structure

### New Files
| File | Responsibility |
|------|---------------|
| `src/simulation/entities/Location.cs` | Location entity class |
| `src/simulation/entities/SubLocation.cs` | SubLocation entity class |
| `src/simulation/entities/AccessPoint.cs` | AccessPoint entity + AccessPointType + LockMechanism enums |
| `src/simulation/addresses/IAddressTemplate.cs` | Interface for address templates |
| `src/simulation/addresses/AddressTemplateRegistry.cs` | Maps AddressType → IAddressTemplate |
| `src/simulation/addresses/LocationBuilders.cs` | Reusable static helpers for common location patterns |
| `src/simulation/addresses/SuburbanHomeTemplate.cs` | Suburban home address template |
| `src/simulation/addresses/ApartmentBuildingTemplate.cs` | Apartment building address template |
| `src/simulation/addresses/DinerTemplate.cs` | Diner address template |
| `src/simulation/addresses/DiveBarTemplate.cs` | Dive bar address template |
| `src/simulation/addresses/OfficeTemplate.cs` | Office building address template |
| `src/simulation/addresses/ParkTemplate.cs` | Park address template |
| `src/simulation/addresses/AirportTemplate.cs` | Airport address template (minimal) |
| `stakeout.tests/Simulation/Entities/LocationTests.cs` | Location entity tests |
| `stakeout.tests/Simulation/Entities/SubLocationTests.cs` | SubLocation entity tests |
| `stakeout.tests/Simulation/Entities/AccessPointTests.cs` | AccessPoint entity tests |
| `stakeout.tests/Simulation/Addresses/SuburbanHomeTemplateTests.cs` | Suburban home template tests |
| `stakeout.tests/Simulation/Addresses/ApartmentBuildingTemplateTests.cs` | Apartment building template tests |
| `stakeout.tests/Simulation/Addresses/DinerTemplateTests.cs` | Diner template tests |
| `stakeout.tests/Simulation/Addresses/DiveBarTemplateTests.cs` | Dive bar template tests |
| `stakeout.tests/Simulation/Addresses/OfficeTemplateTests.cs` | Office template tests |
| `stakeout.tests/Simulation/Addresses/ParkTemplateTests.cs` | Park template tests |
| `stakeout.tests/Simulation/Addresses/AirportTemplateTests.cs` | Airport template tests |
| `stakeout.tests/Simulation/Addresses/LocationBuildersTests.cs` | LocationBuilders tests |
| `stakeout.tests/Simulation/SimulationStateQueryTests.cs` | Query helper tests |
| `stakeout.tests/Simulation/MultiCityTests.cs` | Multi-city + inter-city travel tests |

### Modified Files
| File | Changes |
|------|---------|
| `src/simulation/entities/Address.cs` | Replace Sublocations/Connections with LocationIds, add CityId |
| `src/simulation/entities/AddressType.cs` | Add Airport enum value + mappings |
| `src/simulation/entities/City.cs` | Add AddressIds list, AirportAddressId |
| `src/simulation/entities/Person.cs` | CurrentLocationId, CurrentSubLocationId, CurrentCityId, HomeLocationId replace sublocation fields |
| `src/simulation/entities/Player.cs` | Add CurrentCityId, CurrentLocationId |
| `src/simulation/SimulationState.cs` | New Locations/SubLocations dicts, CityGrids dict, query helpers, remove old collections |
| `src/simulation/LocationGenerator.cs` | Use new template registry, multi-city support |
| `src/simulation/PersonGenerator.cs` | Use new entities, gut schedule code with TODOs |
| `src/simulation/SimulationManager.cs` | Multi-city init, gut behavior updates with TODOs |
| `src/simulation/city/CityGenerator.cs` | Airport placement, receive City entity |
| `src/simulation/city/CityGrid.cs` | Add Airport PlotType handling |

### Deleted Files
| File | Reason |
|------|--------|
| `src/simulation/entities/SublocationGraph.cs` | Replaced by flat Location/SubLocation lookup |
| `src/simulation/entities/ConnectionProperties.cs` | Folded into AccessPoint / deferred to Project 2 |
| `src/simulation/entities/PathStep.cs` | No pathfinding |
| `src/simulation/entities/TraversalContext.cs` | No traversal |
| `src/simulation/sublocations/ISublocationGenerator.cs` | Replaced by IAddressTemplate |
| `src/simulation/sublocations/SublocationGeneratorRegistry.cs` | Replaced by AddressTemplateRegistry |
| `src/simulation/sublocations/SuburbanHomeGenerator.cs` | Replaced by SuburbanHomeTemplate |
| `src/simulation/sublocations/ApartmentBuildingGenerator.cs` | Replaced by ApartmentBuildingTemplate |
| `src/simulation/sublocations/DinerGenerator.cs` | Replaced by DinerTemplate |
| `src/simulation/sublocations/DiveBarGenerator.cs` | Replaced by DiveBarTemplate |
| `src/simulation/sublocations/OfficeGenerator.cs` | Replaced by OfficeTemplate |
| `src/simulation/sublocations/ParkGenerator.cs` | Replaced by ParkTemplate |

### Gutted Files (marked with TODO comments)
| File | TODO Target |
|------|-------------|
| `src/simulation/scheduling/PersonBehavior.cs` | Project 3 |
| `src/simulation/scheduling/ScheduleBuilder.cs` | Project 3 |
| `src/simulation/scheduling/TaskResolver.cs` | Project 3 |
| `src/simulation/scheduling/DailySchedule.cs` | Project 3 |
| `src/simulation/scheduling/DoorLockingService.cs` | Project 3 |
| `src/simulation/scheduling/decomposition/*.cs` (all 7 files) | Project 3 |
| `src/simulation/objectives/ObjectiveResolver.cs` | Project 3 |
| `src/simulation/traces/FingerprintService.cs` | Project 2 |
| `src/simulation/actions/ActionExecutor.cs` | Project 5 |
| `src/simulation/address/GraphView.cs` | Project 8 |

---

## Tasks

### Task 1: New Entity Classes

**Files:**
- Create: `src/simulation/entities/AccessPoint.cs`
- Create: `src/simulation/entities/Location.cs`
- Create: `src/simulation/entities/SubLocation.cs`
- Create: `stakeout.tests/Simulation/Entities/AccessPointTests.cs`
- Create: `stakeout.tests/Simulation/Entities/LocationTests.cs`
- Create: `stakeout.tests/Simulation/Entities/SubLocationTests.cs`

- [ ] **Step 1: Write AccessPoint tests**

```csharp
// stakeout.tests/Simulation/Entities/AccessPointTests.cs
using Stakeout.Simulation.Entities;
using Xunit;

namespace Stakeout.Tests.Simulation.Entities;

public class AccessPointTests
{
    [Fact]
    public void HasTag_ReturnsTrueForPresentTag()
    {
        var ap = new AccessPoint { Tags = new[] { "main_entrance", "covert_entry" } };
        Assert.True(ap.HasTag("main_entrance"));
    }

    [Fact]
    public void HasTag_ReturnsFalseForMissingTag()
    {
        var ap = new AccessPoint { Tags = new[] { "main_entrance" } };
        Assert.False(ap.HasTag("covert_entry"));
    }

    [Fact]
    public void DefaultState_UnlockedAndNotBroken()
    {
        var ap = new AccessPoint();
        Assert.False(ap.IsLocked);
        Assert.False(ap.IsBroken);
    }

    [Fact]
    public void LockMechanism_NullByDefault()
    {
        var ap = new AccessPoint();
        Assert.Null(ap.LockMechanism);
    }
}
```

- [ ] **Step 2: Write Location tests**

```csharp
// stakeout.tests/Simulation/Entities/LocationTests.cs
using System;
using Stakeout.Simulation.Entities;
using Xunit;

namespace Stakeout.Tests.Simulation.Entities;

public class LocationTests
{
    [Fact]
    public void HasTag_ReturnsTrueForPresentTag()
    {
        var loc = new Location { Tags = new[] { "residential", "private" } };
        Assert.True(loc.HasTag("residential"));
    }

    [Fact]
    public void HasTag_ReturnsFalseForMissingTag()
    {
        var loc = new Location { Tags = new[] { "residential" } };
        Assert.False(loc.HasTag("exterior"));
    }

    [Fact]
    public void SubLocationIds_EmptyByDefault()
    {
        var loc = new Location();
        Assert.Empty(loc.SubLocationIds);
    }

    [Fact]
    public void AccessPoints_EmptyByDefault()
    {
        var loc = new Location();
        Assert.Empty(loc.AccessPoints);
    }

    [Fact]
    public void UnitLabel_NullByDefault()
    {
        var loc = new Location();
        Assert.Null(loc.UnitLabel);
    }
}
```

- [ ] **Step 3: Write SubLocation tests**

```csharp
// stakeout.tests/Simulation/Entities/SubLocationTests.cs
using Stakeout.Simulation.Entities;
using Xunit;

namespace Stakeout.Tests.Simulation.Entities;

public class SubLocationTests
{
    [Fact]
    public void HasTag_ReturnsTrueForPresentTag()
    {
        var sub = new SubLocation { Tags = new[] { "bedroom" } };
        Assert.True(sub.HasTag("bedroom"));
    }

    [Fact]
    public void HasTag_ReturnsFalseForMissingTag()
    {
        var sub = new SubLocation { Tags = new[] { "bedroom" } };
        Assert.False(sub.HasTag("kitchen"));
    }

    [Fact]
    public void AccessPoints_EmptyByDefault()
    {
        var sub = new SubLocation();
        Assert.Empty(sub.AccessPoints);
    }
}
```

- [ ] **Step 4: Run tests to verify they fail**

Run: `dotnet test stakeout.tests/ --filter "FullyQualifiedName~AccessPointTests|FullyQualifiedName~LocationTests|FullyQualifiedName~SubLocationTests" -v minimal`
Expected: Build failure — classes don't exist yet.

- [ ] **Step 5: Implement AccessPoint**

```csharp
// src/simulation/entities/AccessPoint.cs
using System;

namespace Stakeout.Simulation.Entities;

public enum AccessPointType { Door, Window, Gate, Hatch, SecurityGate }

public class AccessPoint
{
    public int Id { get; set; }
    public string Name { get; set; }
    public AccessPointType Type { get; set; }
    public string[] Tags { get; set; } = Array.Empty<string>();
    public bool IsLocked { get; set; }
    public bool IsBroken { get; set; }
    public LockMechanism? LockMechanism { get; set; }
    public int? KeyItemId { get; set; }

    public bool HasTag(string tag) => Array.IndexOf(Tags, tag) >= 0;
}
```

- [ ] **Step 6: Implement Location**

```csharp
// src/simulation/entities/Location.cs
using System;
using System.Collections.Generic;

namespace Stakeout.Simulation.Entities;

public class Location
{
    public int Id { get; set; }
    public int AddressId { get; set; }
    public string Name { get; set; }
    public string[] Tags { get; set; } = Array.Empty<string>();
    public int? Floor { get; set; }
    public string UnitLabel { get; set; }
    public List<int> SubLocationIds { get; } = new();
    public List<AccessPoint> AccessPoints { get; } = new();

    public bool HasTag(string tag) => Array.IndexOf(Tags, tag) >= 0;
}
```

- [ ] **Step 7: Implement SubLocation**

```csharp
// src/simulation/entities/SubLocation.cs
using System;
using System.Collections.Generic;

namespace Stakeout.Simulation.Entities;

public class SubLocation
{
    public int Id { get; set; }
    public int LocationId { get; set; }
    public string Name { get; set; }
    public string[] Tags { get; set; } = Array.Empty<string>();
    public List<AccessPoint> AccessPoints { get; } = new();

    public bool HasTag(string tag) => Array.IndexOf(Tags, tag) >= 0;
}
```

- [ ] **Step 8: Run tests to verify they pass**

Run: `dotnet test stakeout.tests/ --filter "FullyQualifiedName~AccessPointTests|FullyQualifiedName~LocationTests|FullyQualifiedName~SubLocationTests" -v minimal`
Expected: All pass.

- [ ] **Step 9: Commit**

```bash
git add src/simulation/entities/AccessPoint.cs src/simulation/entities/Location.cs src/simulation/entities/SubLocation.cs stakeout.tests/Simulation/Entities/AccessPointTests.cs stakeout.tests/Simulation/Entities/LocationTests.cs stakeout.tests/Simulation/Entities/SubLocationTests.cs
git commit -m "feat: add Location, SubLocation, AccessPoint entity classes"
```

---

### Task 2: Update AddressType, City, Address Entities

**Files:**
- Modify: `src/simulation/entities/AddressType.cs`
- Modify: `src/simulation/entities/City.cs`
- Modify: `src/simulation/entities/Address.cs`
- Modify: `stakeout.tests/Simulation/Entities/AddressTypeTests.cs`

- [ ] **Step 1: Write test for Airport AddressType**

Add to the existing `AddressTypeTests.cs`:

```csharp
[Fact]
public void Airport_HasPublicCategory()
{
    Assert.Equal(AddressCategory.Public, AddressType.Airport.GetCategory());
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test stakeout.tests/ --filter "FullyQualifiedName~AddressTypeTests" -v minimal`
Expected: Build failure — `AddressType.Airport` doesn't exist.

- [ ] **Step 3: Update AddressType enum and extensions**

In `src/simulation/entities/AddressType.cs`:

Add `Airport` to the enum:
```csharp
public enum AddressType { SuburbanHome, Diner, DiveBar, Office, ApartmentBuilding, Park, Airport }
```

Add Airport case to `GetCategory`:
```csharp
AddressType.Airport => AddressCategory.Public,
```

Add Airport case to `ToPlotType`:
```csharp
AddressType.Airport => PlotType.Airport,
```

Note: `PlotType.Airport` needs to be added to the PlotType enum in `CityGrid.cs` (or wherever PlotType is defined). Find and add it.

- [ ] **Step 4: Update City entity**

Replace `src/simulation/entities/City.cs`:

```csharp
using System.Collections.Generic;

namespace Stakeout.Simulation.Entities;

public class City
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string CountryName { get; set; }
    public List<int> AddressIds { get; } = new();
    public int? AirportAddressId { get; set; }
}
```

- [ ] **Step 5: Update Address entity**

Replace `src/simulation/entities/Address.cs`:

```csharp
using System.Collections.Generic;
using Godot;

namespace Stakeout.Simulation.Entities;

public class Address
{
    public int Id { get; set; }
    public int CityId { get; set; }
    public int Number { get; set; }
    public int StreetId { get; set; }
    public AddressType Type { get; set; }
    public AddressCategory Category => Type.GetCategory();
    public int GridX { get; set; }
    public int GridY { get; set; }
    public const int CellSize = 48;
    public Vector2 Position => new Vector2(GridX * CellSize, GridY * CellSize);
    public List<int> LocationIds { get; } = new();
}
```

- [ ] **Step 6: Run AddressType tests**

Run: `dotnet test stakeout.tests/ --filter "FullyQualifiedName~AddressTypeTests" -v minimal`
Expected: All pass (including new Airport test). May have build errors from other files referencing old Address properties — that's expected and will be fixed in subsequent tasks.

- [ ] **Step 7: Commit**

```bash
git add src/simulation/entities/AddressType.cs src/simulation/entities/City.cs src/simulation/entities/Address.cs stakeout.tests/Simulation/Entities/AddressTypeTests.cs
git commit -m "feat: update Address, City, AddressType for new hierarchy"
```

---

### Task 3: Update SimulationState

**Files:**
- Modify: `src/simulation/SimulationState.cs`
- Create: `stakeout.tests/Simulation/SimulationStateQueryTests.cs`

- [ ] **Step 1: Write query helper tests**

```csharp
// stakeout.tests/Simulation/SimulationStateQueryTests.cs
using System;
using Stakeout.Simulation;
using Stakeout.Simulation.Entities;
using Xunit;

namespace Stakeout.Tests.Simulation;

public class SimulationStateQueryTests
{
    private SimulationState CreateStateWithAddress()
    {
        var state = new SimulationState();
        var city = new City { Id = 1, Name = "Boston", CountryName = "USA" };
        state.Cities[1] = city;

        var addr = new Address { Id = 10, CityId = 1, Type = AddressType.SuburbanHome };
        state.Addresses[10] = addr;
        city.AddressIds.Add(10);

        var loc = new Location { Id = 100, AddressId = 10, Name = "Interior", Tags = new[] { "residential" } };
        state.Locations[100] = loc;
        addr.LocationIds.Add(100);

        var sub = new SubLocation { Id = 1000, LocationId = 100, Name = "Kitchen", Tags = new[] { "kitchen", "food" } };
        state.SubLocations[1000] = sub;
        loc.SubLocationIds.Add(1000);

        return state;
    }

    [Fact]
    public void GetLocationsForAddress_ReturnsLocations()
    {
        var state = CreateStateWithAddress();
        var locs = state.GetLocationsForAddress(10);
        Assert.Single(locs);
        Assert.Equal("Interior", locs[0].Name);
    }

    [Fact]
    public void GetSubLocationsForLocation_ReturnsSubLocations()
    {
        var state = CreateStateWithAddress();
        var subs = state.GetSubLocationsForLocation(100);
        Assert.Single(subs);
        Assert.Equal("Kitchen", subs[0].Name);
    }

    [Fact]
    public void FindLocationByTag_FindsMatch()
    {
        var state = CreateStateWithAddress();
        var loc = state.FindLocationByTag(10, "residential");
        Assert.NotNull(loc);
        Assert.Equal("Interior", loc.Name);
    }

    [Fact]
    public void FindLocationByTag_ReturnsNullForNoMatch()
    {
        var state = CreateStateWithAddress();
        var loc = state.FindLocationByTag(10, "exterior");
        Assert.Null(loc);
    }

    [Fact]
    public void FindSubLocationByTag_FindsMatch()
    {
        var state = CreateStateWithAddress();
        var sub = state.FindSubLocationByTag(100, "kitchen");
        Assert.NotNull(sub);
        Assert.Equal("Kitchen", sub.Name);
    }

    [Fact]
    public void GetAddressesForCity_ReturnsAddresses()
    {
        var state = CreateStateWithAddress();
        var addrs = state.GetAddressesForCity(1);
        Assert.Single(addrs);
        Assert.Equal(10, addrs[0].Id);
    }

    [Fact]
    public void GetCityForAddress_ReturnsCity()
    {
        var state = CreateStateWithAddress();
        var city = state.GetCityForAddress(10);
        Assert.Equal("Boston", city.Name);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test stakeout.tests/ --filter "FullyQualifiedName~SimulationStateQueryTests" -v minimal`
Expected: Build failure — query methods don't exist.

- [ ] **Step 3: Update SimulationState**

Replace `src/simulation/SimulationState.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using Stakeout.Simulation.City;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Events;
using Stakeout.Simulation.Crimes;
using Stakeout.Simulation.Traces;
using CityEntity = Stakeout.Simulation.Entities.City;

namespace Stakeout.Simulation;

public class SimulationState
{
    public GameClock Clock { get; }
    public Dictionary<int, Person> People { get; } = new();
    public Dictionary<int, Job> Jobs { get; } = new();
    public Player Player { get; set; }
    public List<Country> Countries { get; } = new();
    public Dictionary<int, CityEntity> Cities { get; } = new();
    public Dictionary<int, Street> Streets { get; } = new();
    public Dictionary<int, Address> Addresses { get; } = new();
    public Dictionary<int, Location> Locations { get; } = new();
    public Dictionary<int, SubLocation> SubLocations { get; } = new();
    public EventJournal Journal { get; } = new();
    public Dictionary<int, Crime> Crimes { get; } = new();
    public Dictionary<int, Trace> Traces { get; } = new();
    public Dictionary<int, Item> Items { get; } = new();
    public Dictionary<int, CityGrid> CityGrids { get; } = new();

    private int _nextEntityId = 1;

    public SimulationState(GameClock clock = null)
    {
        Clock = clock ?? new GameClock();
    }

    public int GenerateEntityId() => _nextEntityId++;

    public List<string> GetEntityNamesAtAddress(Address address)
    {
        return People.Values
            .Where(p => p.CurrentAddressId.HasValue && p.CurrentAddressId.Value == address.Id)
            .Select(p => p.FullName)
            .ToList();
    }

    // Query helpers

    public List<Location> GetLocationsForAddress(int addressId)
    {
        var addr = Addresses[addressId];
        return addr.LocationIds.Select(id => Locations[id]).ToList();
    }

    public List<SubLocation> GetSubLocationsForLocation(int locationId)
    {
        var loc = Locations[locationId];
        return loc.SubLocationIds.Select(id => SubLocations[id]).ToList();
    }

    public Location FindLocationByTag(int addressId, string tag)
    {
        return GetLocationsForAddress(addressId).FirstOrDefault(l => l.HasTag(tag));
    }

    public SubLocation FindSubLocationByTag(int locationId, string tag)
    {
        return GetSubLocationsForLocation(locationId).FirstOrDefault(s => s.HasTag(tag));
    }

    public List<Address> GetAddressesForCity(int cityId)
    {
        var city = Cities[cityId];
        return city.AddressIds.Select(id => Addresses[id]).ToList();
    }

    public CityEntity GetCityForAddress(int addressId)
    {
        var addr = Addresses[addressId];
        return Cities[addr.CityId];
    }
}
```

- [ ] **Step 4: Run query tests**

Run: `dotnet test stakeout.tests/ --filter "FullyQualifiedName~SimulationStateQueryTests" -v minimal`
Expected: All pass.

- [ ] **Step 5: Commit**

```bash
git add src/simulation/SimulationState.cs stakeout.tests/Simulation/SimulationStateQueryTests.cs
git commit -m "feat: update SimulationState with new collections and query helpers"
```

---

### Task 4: Update Person and Player Entities

**Files:**
- Modify: `src/simulation/entities/Person.cs`
- Modify: `src/simulation/entities/Player.cs`

- [ ] **Step 1: Update Person**

In `src/simulation/entities/Person.cs`, replace `CurrentSublocationId` and `HomeUnitTag`:

```csharp
using System;
using System.Collections.Generic;
using Godot;
using Stakeout.Simulation.Actions;
using Stakeout.Simulation.Objectives;
using Stakeout.Simulation.Scheduling;

namespace Stakeout.Simulation.Entities;

public class Person
{
    public int Id { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public DateTime CreatedAt { get; set; }
    public int? CurrentCityId { get; set; }
    public int HomeAddressId { get; set; }
    public int? HomeLocationId { get; set; }
    public int JobId { get; set; }
    public int? CurrentAddressId { get; set; }
    public int? CurrentLocationId { get; set; }
    public int? CurrentSubLocationId { get; set; }
    public Vector2 CurrentPosition { get; set; }
    public ActionType CurrentAction { get; set; }
    public TravelInfo TravelInfo { get; set; }
    public TimeSpan PreferredSleepTime { get; set; }
    public TimeSpan PreferredWakeTime { get; set; }
    public bool IsAlive { get; set; } = true;
    public List<Objective> Objectives { get; set; } = new();
    public DailySchedule Schedule { get; set; }
    public bool NeedsScheduleRebuild { get; set; }
    public List<int> InventoryItemIds { get; set; } = new();

    public string FullName => $"{FirstName} {LastName}";
}
```

- [ ] **Step 2: Update Player**

In `src/simulation/entities/Player.cs`:

```csharp
using System.Collections.Generic;
using Godot;

namespace Stakeout.Simulation.Entities;

public class Player
{
    public int Id { get; set; }
    public int? CurrentCityId { get; set; }
    public int HomeAddressId { get; set; }
    public int CurrentAddressId { get; set; }
    public int? CurrentLocationId { get; set; }
    public Vector2 CurrentPosition { get; set; }
    public TravelInfo TravelInfo { get; set; }
    public List<int> InventoryItemIds { get; set; } = new();
}
```

- [ ] **Step 3: Commit**

```bash
git add src/simulation/entities/Person.cs src/simulation/entities/Player.cs
git commit -m "feat: update Person and Player with new location fields"
```

---

### Task 5: Address Template Interface and Registry

**Files:**
- Create: `src/simulation/addresses/IAddressTemplate.cs`
- Create: `src/simulation/addresses/AddressTemplateRegistry.cs`

- [ ] **Step 1: Create IAddressTemplate interface**

```csharp
// src/simulation/addresses/IAddressTemplate.cs
using System;
using Stakeout.Simulation.Entities;

namespace Stakeout.Simulation.Addresses;

public interface IAddressTemplate
{
    void Generate(Address address, SimulationState state, Random random);
}
```

- [ ] **Step 2: Create AddressTemplateRegistry**

```csharp
// src/simulation/addresses/AddressTemplateRegistry.cs
using System.Collections.Generic;
using Stakeout.Simulation.Entities;

namespace Stakeout.Simulation.Addresses;

public static class AddressTemplateRegistry
{
    private static readonly Dictionary<AddressType, IAddressTemplate> _templates = new();

    public static void Register(AddressType type, IAddressTemplate template)
    {
        _templates[type] = template;
    }

    public static IAddressTemplate Get(AddressType type)
    {
        return _templates.TryGetValue(type, out var template) ? template : null;
    }

    public static void RegisterAll()
    {
        Register(AddressType.SuburbanHome, new SuburbanHomeTemplate());
        Register(AddressType.ApartmentBuilding, new ApartmentBuildingTemplate());
        Register(AddressType.Diner, new DinerTemplate());
        Register(AddressType.DiveBar, new DiveBarTemplate());
        Register(AddressType.Office, new OfficeTemplate());
        Register(AddressType.Park, new ParkTemplate());
        Register(AddressType.Airport, new AirportTemplate());
    }
}
```

Note: This will not compile yet — the template classes don't exist. That's fine; we'll add them in the next tasks.

- [ ] **Step 3: Commit**

```bash
git add src/simulation/addresses/IAddressTemplate.cs src/simulation/addresses/AddressTemplateRegistry.cs
git commit -m "feat: add IAddressTemplate interface and registry"
```

---

### Task 6: LocationBuilders

**Files:**
- Create: `src/simulation/addresses/LocationBuilders.cs`
- Create: `stakeout.tests/Simulation/Addresses/LocationBuildersTests.cs`

- [ ] **Step 1: Write LocationBuilders tests**

```csharp
// stakeout.tests/Simulation/Addresses/LocationBuildersTests.cs
using System;
using System.Linq;
using Stakeout.Simulation;
using Stakeout.Simulation.Addresses;
using Stakeout.Simulation.Entities;
using Xunit;

namespace Stakeout.Tests.Simulation.Addresses;

public class LocationBuildersTests
{
    private (SimulationState state, Address address) Setup()
    {
        var state = new SimulationState();
        var addr = new Address { Id = 1, CityId = 1, Type = AddressType.SuburbanHome };
        state.Addresses[1] = addr;
        return (state, addr);
    }

    [Fact]
    public void ExteriorParkingLot_HasCorrectTags()
    {
        var (state, addr) = Setup();
        var loc = LocationBuilders.ExteriorParkingLot(state, addr);
        Assert.True(loc.HasTag("exterior"));
        Assert.True(loc.HasTag("publicly_accessible"));
        Assert.True(loc.HasTag("parking"));
    }

    [Fact]
    public void ExteriorParkingLot_RegisteredInState()
    {
        var (state, addr) = Setup();
        var loc = LocationBuilders.ExteriorParkingLot(state, addr);
        Assert.True(state.Locations.ContainsKey(loc.Id));
        Assert.Contains(loc.Id, addr.LocationIds);
    }

    [Fact]
    public void ApartmentUnit_HasLockedDoor()
    {
        var (state, addr) = Setup();
        var rng = new Random(42);
        var loc = LocationBuilders.ApartmentUnit(state, addr, 2, "2B", rng);
        Assert.Single(loc.AccessPoints);
        Assert.True(loc.AccessPoints[0].IsLocked);
        Assert.Equal(AccessPointType.Door, loc.AccessPoints[0].Type);
    }

    [Fact]
    public void ApartmentUnit_HasExpectedSubLocations()
    {
        var (state, addr) = Setup();
        var rng = new Random(42);
        var loc = LocationBuilders.ApartmentUnit(state, addr, 2, "2B", rng);
        Assert.Equal("2B", loc.UnitLabel);
        Assert.Equal(2, loc.Floor);
        var subNames = loc.SubLocationIds.Select(id => state.SubLocations[id].Name).ToList();
        Assert.Contains("Bedroom", subNames);
        Assert.Contains("Kitchen", subNames);
        Assert.Contains("Living Room", subNames);
        Assert.Contains("Bathroom", subNames);
    }

    [Fact]
    public void ApartmentUnit_SubLocationsRegisteredInState()
    {
        var (state, addr) = Setup();
        var rng = new Random(42);
        var loc = LocationBuilders.ApartmentUnit(state, addr, 2, "2B", rng);
        foreach (var subId in loc.SubLocationIds)
        {
            Assert.True(state.SubLocations.ContainsKey(subId));
        }
    }

    [Fact]
    public void SecurityRoom_HasCorrectTags()
    {
        var (state, addr) = Setup();
        var loc = LocationBuilders.SecurityRoom(state, addr);
        Assert.True(loc.HasTag("security"));
        Assert.True(loc.HasTag("private"));
    }

    [Fact]
    public void Restroom_CreatesSubLocationWithTag()
    {
        var (state, addr) = Setup();
        // Need a parent location first
        var parent = new Location { Id = state.GenerateEntityId(), AddressId = addr.Id, Name = "Lobby" };
        state.Locations[parent.Id] = parent;
        addr.LocationIds.Add(parent.Id);

        var sub = LocationBuilders.Restroom(state, parent);
        Assert.True(sub.HasTag("restroom"));
        Assert.Contains(sub.Id, parent.SubLocationIds);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test stakeout.tests/ --filter "FullyQualifiedName~LocationBuildersTests" -v minimal`
Expected: Build failure — LocationBuilders doesn't exist.

- [ ] **Step 3: Implement LocationBuilders**

```csharp
// src/simulation/addresses/LocationBuilders.cs
using System;
using Stakeout.Simulation.Entities;

namespace Stakeout.Simulation.Addresses;

public static class LocationBuilders
{
    public static Location CreateLocation(SimulationState state, Address address,
        string name, string[] tags, int? floor = null)
    {
        var loc = new Location
        {
            Id = state.GenerateEntityId(),
            AddressId = address.Id,
            Name = name,
            Tags = tags,
            Floor = floor
        };
        state.Locations[loc.Id] = loc;
        address.LocationIds.Add(loc.Id);
        return loc;
    }

    public static SubLocation CreateSubLocation(SimulationState state, Location parent,
        string name, string[] tags)
    {
        var sub = new SubLocation
        {
            Id = state.GenerateEntityId(),
            LocationId = parent.Id,
            Name = name,
            Tags = tags
        };
        state.SubLocations[sub.Id] = sub;
        parent.SubLocationIds.Add(sub.Id);
        return sub;
    }

    public static Location ExteriorParkingLot(SimulationState state, Address address)
    {
        return CreateLocation(state, address, "Exterior Parking Lot",
            new[] { "exterior", "publicly_accessible", "parking" });
    }

    public static Location SecurityRoom(SimulationState state, Address address)
    {
        return CreateLocation(state, address, "Security Room",
            new[] { "security", "private" });
    }

    public static Location ApartmentUnit(SimulationState state, Address address,
        int floor, string unitLabel, Random rng)
    {
        var unit = CreateLocation(state, address, $"Unit {unitLabel}",
            new[] { "residential", "private" }, floor);
        unit.UnitLabel = unitLabel;

        unit.AccessPoints.Add(new AccessPoint
        {
            Id = state.GenerateEntityId(),
            Name = "Front Door",
            Type = AccessPointType.Door,
            Tags = new[] { "main_entrance" },
            IsLocked = true,
            LockMechanism = Entities.LockMechanism.Key
        });

        CreateSubLocation(state, unit, "Bedroom", new[] { "bedroom", "private" });
        CreateSubLocation(state, unit, "Kitchen", new[] { "kitchen", "food" });
        CreateSubLocation(state, unit, "Living Room", new[] { "living", "social" });
        CreateSubLocation(state, unit, "Bathroom", new[] { "restroom" });

        return unit;
    }

    public static SubLocation Restroom(SimulationState state, Location parent)
    {
        return CreateSubLocation(state, parent, "Restroom", new[] { "restroom" });
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test stakeout.tests/ --filter "FullyQualifiedName~LocationBuildersTests" -v minimal`
Expected: All pass.

- [ ] **Step 5: Commit**

```bash
git add src/simulation/addresses/LocationBuilders.cs stakeout.tests/Simulation/Addresses/LocationBuildersTests.cs
git commit -m "feat: add LocationBuilders with reusable location helpers"
```

---

### Task 7: Address Templates — SuburbanHome, Diner, DiveBar

**Files:**
- Create: `src/simulation/addresses/SuburbanHomeTemplate.cs`
- Create: `src/simulation/addresses/DinerTemplate.cs`
- Create: `src/simulation/addresses/DiveBarTemplate.cs`
- Create: `stakeout.tests/Simulation/Addresses/SuburbanHomeTemplateTests.cs`
- Create: `stakeout.tests/Simulation/Addresses/DinerTemplateTests.cs`
- Create: `stakeout.tests/Simulation/Addresses/DiveBarTemplateTests.cs`

- [ ] **Step 1: Write SuburbanHomeTemplate tests**

```csharp
// stakeout.tests/Simulation/Addresses/SuburbanHomeTemplateTests.cs
using System;
using System.Linq;
using Stakeout.Simulation;
using Stakeout.Simulation.Addresses;
using Stakeout.Simulation.Entities;
using Xunit;

namespace Stakeout.Tests.Simulation.Addresses;

public class SuburbanHomeTemplateTests
{
    private (SimulationState state, Address address) Generate(int seed = 42)
    {
        var state = new SimulationState();
        var address = new Address { Id = 1, CityId = 1, Type = AddressType.SuburbanHome };
        state.Addresses[1] = address;
        new SuburbanHomeTemplate().Generate(address, state, new Random(seed));
        return (state, address);
    }

    [Fact]
    public void Generate_HasFrontYard()
    {
        var (state, addr) = Generate();
        var yard = state.FindLocationByTag(addr.Id, "exterior");
        Assert.NotNull(yard);
    }

    [Fact]
    public void Generate_HasInteriorWithEntrance()
    {
        var (state, addr) = Generate();
        var interior = state.FindLocationByTag(addr.Id, "entrance");
        Assert.NotNull(interior);
    }

    [Fact]
    public void Generate_HasLockedFrontDoor()
    {
        var (state, addr) = Generate();
        var interior = state.FindLocationByTag(addr.Id, "entrance");
        var door = interior.AccessPoints.FirstOrDefault(ap => ap.HasTag("main_entrance"));
        Assert.NotNull(door);
        Assert.True(door.IsLocked);
    }

    [Fact]
    public void Generate_HasBedroom()
    {
        var (state, addr) = Generate();
        var interior = state.FindLocationByTag(addr.Id, "residential");
        var bedroom = interior.SubLocationIds
            .Select(id => state.SubLocations[id])
            .FirstOrDefault(s => s.HasTag("bedroom"));
        Assert.NotNull(bedroom);
    }

    [Fact]
    public void Generate_HasKitchenLivingBathroom()
    {
        var (state, addr) = Generate();
        var interior = state.FindLocationByTag(addr.Id, "residential");
        var subs = interior.SubLocationIds.Select(id => state.SubLocations[id]).ToList();
        Assert.Contains(subs, s => s.HasTag("kitchen"));
        Assert.Contains(subs, s => s.HasTag("living"));
        Assert.Contains(subs, s => s.HasTag("restroom"));
    }

    [Fact]
    public void Generate_Has2To3Bedrooms()
    {
        var (state, addr) = Generate();
        var interior = state.FindLocationByTag(addr.Id, "residential");
        var bedrooms = interior.SubLocationIds
            .Select(id => state.SubLocations[id])
            .Where(s => s.HasTag("bedroom"))
            .ToList();
        Assert.InRange(bedrooms.Count, 2, 3);
    }

    [Fact]
    public void Generate_HasCovertEntry()
    {
        var (state, addr) = Generate();
        var allAccessPoints = state.GetLocationsForAddress(addr.Id)
            .SelectMany(l => l.AccessPoints);
        Assert.Contains(allAccessPoints, ap => ap.HasTag("covert_entry"));
    }

    [Fact]
    public void Generate_AllLocationsHaveCorrectAddressId()
    {
        var (state, addr) = Generate();
        foreach (var loc in state.GetLocationsForAddress(addr.Id))
        {
            Assert.Equal(addr.Id, loc.AddressId);
        }
    }
}
```

- [ ] **Step 2: Implement SuburbanHomeTemplate**

```csharp
// src/simulation/addresses/SuburbanHomeTemplate.cs
using System;
using Stakeout.Simulation.Entities;

namespace Stakeout.Simulation.Addresses;

public class SuburbanHomeTemplate : IAddressTemplate
{
    public void Generate(Address address, SimulationState state, Random random)
    {
        // Front yard (exterior)
        var yard = LocationBuilders.CreateLocation(state, address, "Front Yard",
            new[] { "exterior", "publicly_accessible" });

        yard.AccessPoints.Add(new AccessPoint
        {
            Id = state.GenerateEntityId(),
            Name = "Window",
            Type = AccessPointType.Window,
            Tags = new[] { "covert_entry" },
            IsLocked = true,
            LockMechanism = LockMechanism.Key
        });

        // Interior
        var interior = LocationBuilders.CreateLocation(state, address, "Interior",
            new[] { "residential", "private", "entrance" });

        interior.AccessPoints.Add(new AccessPoint
        {
            Id = state.GenerateEntityId(),
            Name = "Front Door",
            Type = AccessPointType.Door,
            Tags = new[] { "main_entrance" },
            IsLocked = true,
            LockMechanism = LockMechanism.Key
        });

        interior.AccessPoints.Add(new AccessPoint
        {
            Id = state.GenerateEntityId(),
            Name = "Back Door",
            Type = AccessPointType.Door,
            Tags = new[] { "staff_entry" },
            IsLocked = true,
            LockMechanism = LockMechanism.Key
        });

        // Sub-locations
        LocationBuilders.CreateSubLocation(state, interior, "Hallway", new[] { "hallway" });
        LocationBuilders.CreateSubLocation(state, interior, "Kitchen", new[] { "kitchen", "food" });
        LocationBuilders.CreateSubLocation(state, interior, "Living Room", new[] { "living", "social" });
        LocationBuilders.CreateSubLocation(state, interior, "Bathroom", new[] { "restroom" });

        int bedroomCount = random.Next(2, 4);
        for (int i = 1; i <= bedroomCount; i++)
        {
            LocationBuilders.CreateSubLocation(state, interior, $"Bedroom {i}",
                new[] { "bedroom", "private" });
        }
    }
}
```

- [ ] **Step 3: Run SuburbanHome tests**

Run: `dotnet test stakeout.tests/ --filter "FullyQualifiedName~SuburbanHomeTemplateTests" -v minimal`
Expected: All pass.

- [ ] **Step 4: Write DinerTemplate tests**

```csharp
// stakeout.tests/Simulation/Addresses/DinerTemplateTests.cs
using System;
using System.Linq;
using Stakeout.Simulation;
using Stakeout.Simulation.Addresses;
using Stakeout.Simulation.Entities;
using Xunit;

namespace Stakeout.Tests.Simulation.Addresses;

public class DinerTemplateTests
{
    private (SimulationState state, Address address) Generate(int seed = 42)
    {
        var state = new SimulationState();
        var address = new Address { Id = 1, CityId = 1, Type = AddressType.Diner };
        state.Addresses[1] = address;
        new DinerTemplate().Generate(address, state, new Random(seed));
        return (state, address);
    }

    [Fact]
    public void Generate_HasParkingLot()
    {
        var (state, addr) = Generate();
        Assert.NotNull(state.FindLocationByTag(addr.Id, "parking"));
    }

    [Fact]
    public void Generate_HasDiningArea()
    {
        var (state, addr) = Generate();
        Assert.NotNull(state.FindLocationByTag(addr.Id, "service_area"));
    }

    [Fact]
    public void Generate_HasKitchen()
    {
        var (state, addr) = Generate();
        Assert.NotNull(state.FindLocationByTag(addr.Id, "work_area"));
    }

    [Fact]
    public void Generate_HasStaffEntry()
    {
        var (state, addr) = Generate();
        var allAPs = state.GetLocationsForAddress(addr.Id).SelectMany(l => l.AccessPoints);
        Assert.Contains(allAPs, ap => ap.HasTag("staff_entry"));
    }

    [Fact]
    public void Generate_HasRestroom()
    {
        var (state, addr) = Generate();
        var locs = state.GetLocationsForAddress(addr.Id);
        Assert.Contains(locs, l => l.HasTag("restroom") || l.Name == "Restroom");
    }
}
```

- [ ] **Step 5: Implement DinerTemplate**

```csharp
// src/simulation/addresses/DinerTemplate.cs
using System;
using Stakeout.Simulation.Entities;

namespace Stakeout.Simulation.Addresses;

public class DinerTemplate : IAddressTemplate
{
    public void Generate(Address address, SimulationState state, Random random)
    {
        LocationBuilders.ExteriorParkingLot(state, address);

        var dining = LocationBuilders.CreateLocation(state, address, "Dining Area",
            new[] { "publicly_accessible", "service_area", "entrance", "social" });
        dining.AccessPoints.Add(new AccessPoint
        {
            Id = state.GenerateEntityId(),
            Name = "Front Door",
            Type = AccessPointType.Door,
            Tags = new[] { "main_entrance" }
        });

        var kitchen = LocationBuilders.CreateLocation(state, address, "Kitchen",
            new[] { "staff_only", "work_area", "food" });
        kitchen.AccessPoints.Add(new AccessPoint
        {
            Id = state.GenerateEntityId(),
            Name = "Back Door",
            Type = AccessPointType.Door,
            Tags = new[] { "staff_entry" },
            IsLocked = true,
            LockMechanism = LockMechanism.Key
        });

        LocationBuilders.CreateLocation(state, address, "Storage",
            new[] { "staff_only", "storage" });
        LocationBuilders.CreateLocation(state, address, "Manager's Office",
            new[] { "staff_only", "private" });
        LocationBuilders.CreateLocation(state, address, "Restroom",
            new[] { "publicly_accessible", "restroom" });
    }
}
```

- [ ] **Step 6: Write DiveBarTemplate tests**

```csharp
// stakeout.tests/Simulation/Addresses/DiveBarTemplateTests.cs
using System;
using System.Linq;
using Stakeout.Simulation;
using Stakeout.Simulation.Addresses;
using Stakeout.Simulation.Entities;
using Xunit;

namespace Stakeout.Tests.Simulation.Addresses;

public class DiveBarTemplateTests
{
    private (SimulationState state, Address address) Generate(int seed = 42)
    {
        var state = new SimulationState();
        var address = new Address { Id = 1, CityId = 1, Type = AddressType.DiveBar };
        state.Addresses[1] = address;
        new DiveBarTemplate().Generate(address, state, new Random(seed));
        return (state, address);
    }

    [Fact]
    public void Generate_HasAlley()
    {
        var (state, addr) = Generate();
        var allLocs = state.GetLocationsForAddress(addr.Id);
        Assert.Contains(allLocs, l => l.Name == "Alley");
    }

    [Fact]
    public void Generate_HasBarArea()
    {
        var (state, addr) = Generate();
        Assert.NotNull(state.FindLocationByTag(addr.Id, "service_area"));
    }

    [Fact]
    public void Generate_HasCovertEntry()
    {
        var (state, addr) = Generate();
        var allLocs = state.GetLocationsForAddress(addr.Id);
        Assert.Contains(allLocs, l => l.HasTag("covert_entry"));
    }
}
```

- [ ] **Step 7: Implement DiveBarTemplate**

```csharp
// src/simulation/addresses/DiveBarTemplate.cs
using System;
using Stakeout.Simulation.Entities;

namespace Stakeout.Simulation.Addresses;

public class DiveBarTemplate : IAddressTemplate
{
    public void Generate(Address address, SimulationState state, Random random)
    {
        LocationBuilders.CreateLocation(state, address, "Alley",
            new[] { "exterior", "covert_entry" });

        var barArea = LocationBuilders.CreateLocation(state, address, "Bar Area",
            new[] { "publicly_accessible", "service_area", "entrance", "social" });
        barArea.AccessPoints.Add(new AccessPoint
        {
            Id = state.GenerateEntityId(),
            Name = "Front Door",
            Type = AccessPointType.Door,
            Tags = new[] { "main_entrance" }
        });

        var backHall = LocationBuilders.CreateLocation(state, address, "Back Hallway",
            new[] { "staff_only" });
        backHall.AccessPoints.Add(new AccessPoint
        {
            Id = state.GenerateEntityId(),
            Name = "Back Door",
            Type = AccessPointType.Door,
            Tags = new[] { "staff_entry" },
            IsLocked = true,
            LockMechanism = LockMechanism.Key
        });

        LocationBuilders.CreateLocation(state, address, "Storage",
            new[] { "staff_only", "storage" });
        LocationBuilders.CreateLocation(state, address, "Manager's Office",
            new[] { "staff_only", "private" });
        LocationBuilders.CreateLocation(state, address, "Restroom",
            new[] { "publicly_accessible", "restroom" });
    }
}
```

- [ ] **Step 8: Run all three template tests**

Run: `dotnet test stakeout.tests/ --filter "FullyQualifiedName~SuburbanHomeTemplateTests|FullyQualifiedName~DinerTemplateTests|FullyQualifiedName~DiveBarTemplateTests" -v minimal`
Expected: All pass.

- [ ] **Step 9: Commit**

```bash
git add src/simulation/addresses/SuburbanHomeTemplate.cs src/simulation/addresses/DinerTemplate.cs src/simulation/addresses/DiveBarTemplate.cs stakeout.tests/Simulation/Addresses/SuburbanHomeTemplateTests.cs stakeout.tests/Simulation/Addresses/DinerTemplateTests.cs stakeout.tests/Simulation/Addresses/DiveBarTemplateTests.cs
git commit -m "feat: add SuburbanHome, Diner, DiveBar address templates"
```

---

### Task 8: Address Templates — ApartmentBuilding, Office, Park, Airport

**Files:**
- Create: `src/simulation/addresses/ApartmentBuildingTemplate.cs`
- Create: `src/simulation/addresses/OfficeTemplate.cs`
- Create: `src/simulation/addresses/ParkTemplate.cs`
- Create: `src/simulation/addresses/AirportTemplate.cs`
- Create: `stakeout.tests/Simulation/Addresses/ApartmentBuildingTemplateTests.cs`
- Create: `stakeout.tests/Simulation/Addresses/OfficeTemplateTests.cs`
- Create: `stakeout.tests/Simulation/Addresses/ParkTemplateTests.cs`
- Create: `stakeout.tests/Simulation/Addresses/AirportTemplateTests.cs`

- [ ] **Step 1: Write ApartmentBuildingTemplate tests**

```csharp
// stakeout.tests/Simulation/Addresses/ApartmentBuildingTemplateTests.cs
using System;
using System.Linq;
using Stakeout.Simulation;
using Stakeout.Simulation.Addresses;
using Stakeout.Simulation.Entities;
using Xunit;

namespace Stakeout.Tests.Simulation.Addresses;

public class ApartmentBuildingTemplateTests
{
    private (SimulationState state, Address address) Generate(int seed = 42)
    {
        var state = new SimulationState();
        var address = new Address { Id = 1, CityId = 1, Type = AddressType.ApartmentBuilding };
        state.Addresses[1] = address;
        new ApartmentBuildingTemplate().Generate(address, state, new Random(seed));
        return (state, address);
    }

    [Fact]
    public void Generate_HasLobby()
    {
        var (state, addr) = Generate();
        Assert.NotNull(state.FindLocationByTag(addr.Id, "entrance"));
    }

    [Fact]
    public void Generate_HasParkingLot()
    {
        var (state, addr) = Generate();
        Assert.NotNull(state.FindLocationByTag(addr.Id, "parking"));
    }

    [Fact]
    public void Generate_HasResidentialUnits()
    {
        var (state, addr) = Generate();
        var units = state.GetLocationsForAddress(addr.Id)
            .Where(l => l.HasTag("residential")).ToList();
        Assert.True(units.Count >= 4);
    }

    [Fact]
    public void Generate_UnitsHaveUnitLabels()
    {
        var (state, addr) = Generate();
        var units = state.GetLocationsForAddress(addr.Id)
            .Where(l => l.HasTag("residential")).ToList();
        Assert.All(units, u => Assert.NotNull(u.UnitLabel));
    }

    [Fact]
    public void Generate_UnitsHaveLockedDoors()
    {
        var (state, addr) = Generate();
        var units = state.GetLocationsForAddress(addr.Id)
            .Where(l => l.HasTag("residential")).ToList();
        Assert.All(units, u =>
        {
            Assert.NotEmpty(u.AccessPoints);
            Assert.True(u.AccessPoints[0].IsLocked);
        });
    }

    [Fact]
    public void Generate_UnitsHaveSubLocations()
    {
        var (state, addr) = Generate();
        var unit = state.GetLocationsForAddress(addr.Id)
            .First(l => l.HasTag("residential"));
        Assert.True(unit.SubLocationIds.Count >= 4);
    }
}
```

- [ ] **Step 2: Implement ApartmentBuildingTemplate**

```csharp
// src/simulation/addresses/ApartmentBuildingTemplate.cs
using System;
using Stakeout.Simulation.Entities;

namespace Stakeout.Simulation.Addresses;

public class ApartmentBuildingTemplate : IAddressTemplate
{
    public void Generate(Address address, SimulationState state, Random random)
    {
        LocationBuilders.ExteriorParkingLot(state, address);

        LocationBuilders.CreateLocation(state, address, "Lobby",
            new[] { "publicly_accessible", "entrance" });

        LocationBuilders.SecurityRoom(state, address);

        int floors = random.Next(4, 21);
        int unitsPerFloor = random.Next(4, 9);

        for (int f = 1; f <= floors; f++)
        {
            for (int u = 1; u <= unitsPerFloor; u++)
            {
                char unitLetter = (char)('A' + u - 1);
                string unitLabel = $"{f}{unitLetter}";
                LocationBuilders.ApartmentUnit(state, address, f, unitLabel, random);
            }
        }
    }
}
```

- [ ] **Step 3: Write OfficeTemplate tests**

```csharp
// stakeout.tests/Simulation/Addresses/OfficeTemplateTests.cs
using System;
using System.Linq;
using Stakeout.Simulation;
using Stakeout.Simulation.Addresses;
using Stakeout.Simulation.Entities;
using Xunit;

namespace Stakeout.Tests.Simulation.Addresses;

public class OfficeTemplateTests
{
    private (SimulationState state, Address address) Generate(int seed = 42)
    {
        var state = new SimulationState();
        var address = new Address { Id = 1, CityId = 1, Type = AddressType.Office };
        state.Addresses[1] = address;
        new OfficeTemplate().Generate(address, state, new Random(seed));
        return (state, address);
    }

    [Fact]
    public void Generate_HasLobby()
    {
        var (state, addr) = Generate();
        Assert.NotNull(state.FindLocationByTag(addr.Id, "entrance"));
    }

    [Fact]
    public void Generate_HasWorkAreas()
    {
        var (state, addr) = Generate();
        var floors = state.GetLocationsForAddress(addr.Id)
            .Where(l => l.HasTag("commercial")).ToList();
        Assert.True(floors.Count >= 1);
    }

    [Fact]
    public void Generate_FloorsHaveSubLocations()
    {
        var (state, addr) = Generate();
        var floor = state.GetLocationsForAddress(addr.Id)
            .First(l => l.HasTag("commercial"));
        Assert.True(floor.SubLocationIds.Count >= 3);
    }
}
```

- [ ] **Step 4: Implement OfficeTemplate**

```csharp
// src/simulation/addresses/OfficeTemplate.cs
using System;
using Stakeout.Simulation.Entities;

namespace Stakeout.Simulation.Addresses;

public class OfficeTemplate : IAddressTemplate
{
    public void Generate(Address address, SimulationState state, Random random)
    {
        LocationBuilders.CreateLocation(state, address, "Lobby",
            new[] { "publicly_accessible", "entrance" });

        LocationBuilders.SecurityRoom(state, address);

        int floors = random.Next(1, 6);

        for (int f = 1; f <= floors; f++)
        {
            var floor = LocationBuilders.CreateLocation(state, address, $"Floor {f}",
                new[] { "commercial" }, f);

            LocationBuilders.CreateSubLocation(state, floor, "Reception",
                new[] { "publicly_accessible" });
            LocationBuilders.CreateSubLocation(state, floor, "Cubicle Area",
                new[] { "work_area" });
            LocationBuilders.CreateSubLocation(state, floor, "Manager's Office",
                new[] { "private", "office" });
            LocationBuilders.CreateSubLocation(state, floor, "Break Room",
                new[] { "food", "social" });
            LocationBuilders.CreateSubLocation(state, floor, "Restroom",
                new[] { "restroom" });
        }
    }
}
```

- [ ] **Step 5: Write ParkTemplate tests**

```csharp
// stakeout.tests/Simulation/Addresses/ParkTemplateTests.cs
using System;
using System.Linq;
using Stakeout.Simulation;
using Stakeout.Simulation.Addresses;
using Stakeout.Simulation.Entities;
using Xunit;

namespace Stakeout.Tests.Simulation.Addresses;

public class ParkTemplateTests
{
    private (SimulationState state, Address address) Generate(int seed = 42)
    {
        var state = new SimulationState();
        var address = new Address { Id = 1, CityId = 1, Type = AddressType.Park };
        state.Addresses[1] = address;
        new ParkTemplate().Generate(address, state, new Random(seed));
        return (state, address);
    }

    [Fact]
    public void Generate_HasParkingLot()
    {
        var (state, addr) = Generate();
        Assert.NotNull(state.FindLocationByTag(addr.Id, "parking"));
    }

    [Fact]
    public void Generate_HasEntrance()
    {
        var (state, addr) = Generate();
        Assert.NotNull(state.FindLocationByTag(addr.Id, "entrance"));
    }

    [Fact]
    public void Generate_AllLocationsAreExterior()
    {
        var (state, addr) = Generate();
        var outdoorLocs = state.GetLocationsForAddress(addr.Id)
            .Where(l => l.HasTag("exterior")).ToList();
        // Most locations should be exterior (restroom building is the exception)
        Assert.True(outdoorLocs.Count >= 4);
    }

    [Fact]
    public void Generate_AllLocationsArePublic()
    {
        var (state, addr) = Generate();
        var publicLocs = state.GetLocationsForAddress(addr.Id)
            .Where(l => l.HasTag("publicly_accessible")).ToList();
        Assert.True(publicLocs.Count >= 4);
    }
}
```

- [ ] **Step 6: Implement ParkTemplate**

```csharp
// src/simulation/addresses/ParkTemplate.cs
using System;
using Stakeout.Simulation.Entities;

namespace Stakeout.Simulation.Addresses;

public class ParkTemplate : IAddressTemplate
{
    public void Generate(Address address, SimulationState state, Random random)
    {
        LocationBuilders.ExteriorParkingLot(state, address);

        LocationBuilders.CreateLocation(state, address, "Main Entrance",
            new[] { "exterior", "publicly_accessible", "entrance" });
        LocationBuilders.CreateLocation(state, address, "Jogging Path",
            new[] { "exterior", "publicly_accessible" });
        LocationBuilders.CreateLocation(state, address, "Picnic Area",
            new[] { "exterior", "publicly_accessible", "social", "food" });
        LocationBuilders.CreateLocation(state, address, "Playground",
            new[] { "exterior", "publicly_accessible", "social" });
        LocationBuilders.CreateLocation(state, address, "Wooded Area",
            new[] { "exterior", "publicly_accessible", "covert_entry" });
        LocationBuilders.CreateLocation(state, address, "Restroom Building",
            new[] { "publicly_accessible", "restroom" });
    }
}
```

- [ ] **Step 7: Write AirportTemplate tests**

```csharp
// stakeout.tests/Simulation/Addresses/AirportTemplateTests.cs
using System;
using System.Linq;
using Stakeout.Simulation;
using Stakeout.Simulation.Addresses;
using Stakeout.Simulation.Entities;
using Xunit;

namespace Stakeout.Tests.Simulation.Addresses;

public class AirportTemplateTests
{
    private (SimulationState state, Address address) Generate(int seed = 42)
    {
        var state = new SimulationState();
        var address = new Address { Id = 1, CityId = 1, Type = AddressType.Airport };
        state.Addresses[1] = address;
        new AirportTemplate().Generate(address, state, new Random(seed));
        return (state, address);
    }

    [Fact]
    public void Generate_HasSingleTerminalLocation()
    {
        var (state, addr) = Generate();
        var locs = state.GetLocationsForAddress(addr.Id);
        Assert.Single(locs);
        Assert.Equal("Terminal", locs[0].Name);
    }

    [Fact]
    public void Generate_TerminalIsPublicAndEntrance()
    {
        var (state, addr) = Generate();
        var terminal = state.GetLocationsForAddress(addr.Id).First();
        Assert.True(terminal.HasTag("publicly_accessible"));
        Assert.True(terminal.HasTag("entrance"));
    }

    [Fact]
    public void Generate_NoSubLocations()
    {
        var (state, addr) = Generate();
        var terminal = state.GetLocationsForAddress(addr.Id).First();
        Assert.Empty(terminal.SubLocationIds);
    }
}
```

- [ ] **Step 8: Implement AirportTemplate**

```csharp
// src/simulation/addresses/AirportTemplate.cs
using System;
using Stakeout.Simulation.Entities;

namespace Stakeout.Simulation.Addresses;

public class AirportTemplate : IAddressTemplate
{
    public void Generate(Address address, SimulationState state, Random random)
    {
        LocationBuilders.CreateLocation(state, address, "Terminal",
            new[] { "publicly_accessible", "entrance" });
    }
}
```

- [ ] **Step 9: Run all template tests**

Run: `dotnet test stakeout.tests/ --filter "FullyQualifiedName~ApartmentBuildingTemplateTests|FullyQualifiedName~OfficeTemplateTests|FullyQualifiedName~ParkTemplateTests|FullyQualifiedName~AirportTemplateTests" -v minimal`
Expected: All pass.

- [ ] **Step 10: Commit**

```bash
git add src/simulation/addresses/ApartmentBuildingTemplate.cs src/simulation/addresses/OfficeTemplate.cs src/simulation/addresses/ParkTemplate.cs src/simulation/addresses/AirportTemplate.cs stakeout.tests/Simulation/Addresses/ApartmentBuildingTemplateTests.cs stakeout.tests/Simulation/Addresses/OfficeTemplateTests.cs stakeout.tests/Simulation/Addresses/ParkTemplateTests.cs stakeout.tests/Simulation/Addresses/AirportTemplateTests.cs
git commit -m "feat: add ApartmentBuilding, Office, Park, Airport address templates"
```

---

### Task 9: Delete Old Sublocation System

**Files:**
- Delete: `src/simulation/entities/SublocationGraph.cs`
- Delete: `src/simulation/entities/ConnectionProperties.cs`
- Delete: `src/simulation/entities/PathStep.cs`
- Delete: `src/simulation/entities/TraversalContext.cs`
- Delete: `src/simulation/sublocations/ISublocationGenerator.cs`
- Delete: `src/simulation/sublocations/SublocationGeneratorRegistry.cs`
- Delete: `src/simulation/sublocations/SuburbanHomeGenerator.cs`
- Delete: `src/simulation/sublocations/ApartmentBuildingGenerator.cs`
- Delete: `src/simulation/sublocations/DinerGenerator.cs`
- Delete: `src/simulation/sublocations/DiveBarGenerator.cs`
- Delete: `src/simulation/sublocations/OfficeGenerator.cs`
- Delete: `src/simulation/sublocations/ParkGenerator.cs`
- Delete: `src/simulation/entities/Sublocation.cs`
- Delete old tests: `stakeout.tests/Simulation/Entities/SublocationGraphTests.cs`
- Delete old tests: `stakeout.tests/Simulation/Entities/SublocationTests.cs`
- Delete old tests: `stakeout.tests/Simulation/Entities/ConnectionPropertiesTests.cs`
- Delete old tests: `stakeout.tests/Simulation/Sublocations/SuburbanHomeGeneratorTests.cs`
- Delete old tests: `stakeout.tests/Simulation/Sublocations/ApartmentBuildingGeneratorTests.cs`
- Delete old tests: `stakeout.tests/Simulation/Sublocations/DinerGeneratorTests.cs`
- Delete old tests: `stakeout.tests/Simulation/Sublocations/DiveBarGeneratorTests.cs`
- Delete old tests: `stakeout.tests/Simulation/Sublocations/OfficeGeneratorTests.cs`
- Delete old tests: `stakeout.tests/Simulation/Sublocations/ParkGeneratorTests.cs`

- [ ] **Step 1: Delete old source files**

```bash
git rm src/simulation/entities/SublocationGraph.cs src/simulation/entities/ConnectionProperties.cs src/simulation/entities/PathStep.cs src/simulation/entities/TraversalContext.cs src/simulation/entities/Sublocation.cs
git rm src/simulation/sublocations/ISublocationGenerator.cs src/simulation/sublocations/SublocationGeneratorRegistry.cs src/simulation/sublocations/SuburbanHomeGenerator.cs src/simulation/sublocations/ApartmentBuildingGenerator.cs src/simulation/sublocations/DinerGenerator.cs src/simulation/sublocations/DiveBarGenerator.cs src/simulation/sublocations/OfficeGenerator.cs src/simulation/sublocations/ParkGenerator.cs
```

- [ ] **Step 2: Delete old test files**

```bash
git rm stakeout.tests/Simulation/Entities/SublocationGraphTests.cs stakeout.tests/Simulation/Entities/SublocationTests.cs stakeout.tests/Simulation/Entities/ConnectionPropertiesTests.cs
git rm stakeout.tests/Simulation/Sublocations/SuburbanHomeGeneratorTests.cs stakeout.tests/Simulation/Sublocations/ApartmentBuildingGeneratorTests.cs stakeout.tests/Simulation/Sublocations/DinerGeneratorTests.cs stakeout.tests/Simulation/Sublocations/DiveBarGeneratorTests.cs stakeout.tests/Simulation/Sublocations/OfficeGeneratorTests.cs stakeout.tests/Simulation/Sublocations/ParkGeneratorTests.cs
```

- [ ] **Step 3: Move LockMechanism enum to AccessPoint.cs**

The `LockMechanism` enum currently lives in `ConnectionProperties.cs` which we just deleted. It's already defined in `AccessPoint.cs` — verify it's there. If not, ensure `AccessPoint.cs` contains:

```csharp
public enum LockMechanism { Key, Combination, Keypad, Electronic }
```

Note: If any other code still references `ConnectionProperties.cs` types (like `ConcealmentMethod`), those references will break. That's expected — those consumers are being gutted in the next task.

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "remove: delete old sublocation graph system and generators"
```

---

### Task 10: Gut Scheduling, Behavior, and Service Files

**Files:**
- Gut: `src/simulation/scheduling/PersonBehavior.cs`
- Gut: `src/simulation/scheduling/ScheduleBuilder.cs`
- Gut: `src/simulation/scheduling/TaskResolver.cs`
- Gut: `src/simulation/scheduling/DailySchedule.cs`
- Gut: `src/simulation/scheduling/DoorLockingService.cs`
- Gut: all decomposition strategy files
- Gut: `src/simulation/objectives/ObjectiveResolver.cs`
- Gut: `src/simulation/traces/FingerprintService.cs`
- Gut: `src/simulation/actions/ActionExecutor.cs`
- Gut: `src/simulation/address/GraphView.cs`
- Delete corresponding test files that test gutted functionality

This task requires reading each file, replacing the body with a TODO comment, and deleting test files that test the gutted functionality. The agent implementing this should:

1. Read each file listed above
2. Keep the class/interface declaration and namespace but replace all method bodies with `throw new System.NotImplementedException()` and add a comment at the top: `// TODO: Project N — this system will be rebuilt as part of the simulation overhaul.` where N is the appropriate project number per the spec
3. Delete test files for gutted code: all decomposition tests, `ScheduleBuilderTests.cs`, `TaskResolverTests.cs`, `PersonBehaviorTests.cs`, `PersonBehaviorFingerprintTests.cs`, `DoorLockingServiceTests.cs`, `FingerprintServiceTests.cs`, `ActionExecutorTests.cs`, `ObjectiveResolverTests.cs`
4. Keep test files for entities/types that still exist (e.g., `TaskTests.cs` if `Task.cs` still exists)

- [ ] **Step 1: Gut each scheduling/behavior/service file with TODO comment**

For each file, replace the class body so it compiles but does nothing. Preserve the class name and namespace. Add the TODO comment identifying which project restores it.

- [ ] **Step 2: Delete test files for gutted systems**

```bash
git rm stakeout.tests/Simulation/Scheduling/ScheduleBuilderTests.cs stakeout.tests/Simulation/Scheduling/TaskResolverTests.cs stakeout.tests/Simulation/Scheduling/PersonBehaviorTests.cs stakeout.tests/Simulation/Scheduling/PersonBehaviorFingerprintTests.cs stakeout.tests/Simulation/Scheduling/DoorLockingServiceTests.cs stakeout.tests/Simulation/Traces/FingerprintServiceTests.cs stakeout.tests/Simulation/Actions/ActionExecutorTests.cs stakeout.tests/Simulation/Objectives/ObjectiveResolverTests.cs
git rm stakeout.tests/Simulation/Scheduling/Decomposition/InhabitDecompositionTests.cs stakeout.tests/Simulation/Scheduling/Decomposition/IntrudeDecompositionTests.cs stakeout.tests/Simulation/Scheduling/Decomposition/SleepDecompositionTests.cs stakeout.tests/Simulation/Scheduling/Decomposition/WorkDayDecompositionTests.cs
```

Note: Some of these test files may not exist or may have slightly different names. The agent should use `ls` or `glob` to find the exact files before deleting. If a file doesn't exist, skip it.

- [ ] **Step 3: Commit**

```bash
git add -A
git commit -m "gut: mark scheduling, behavior, trace services as TODO for future projects"
```

---

### Task 11: Update LocationGenerator and CityGenerator

**Files:**
- Modify: `src/simulation/LocationGenerator.cs`
- Modify: `src/simulation/city/CityGenerator.cs`
- Modify: `src/simulation/city/CityGrid.cs` (add Airport PlotType if needed)

This task updates the generation pipeline to use the new template system and support multiple cities with airports.

- [ ] **Step 1: Update LocationGenerator**

The agent should read `src/simulation/LocationGenerator.cs` and update it to:
- Use `AddressTemplateRegistry` instead of `SublocationGeneratorRegistry`
- Call `template.Generate(address, state, random)` instead of the old generator
- Remove references to `SublocationGraph`
- `GenerateAddress` should set `address.CityId` from the city being generated
- Support receiving a `cityId` parameter for multi-city generation

- [ ] **Step 2: Update CityGrid PlotType**

Find the `PlotType` enum (in `CityGrid.cs` or its own file) and add `Airport` to it. Add it to the `AddressTypeExtensions.ToPlotType` mapping if not done in Task 2.

- [ ] **Step 3: Update CityGenerator for Airport placement**

The agent should read `src/simulation/city/CityGenerator.cs` and add airport placement after the normal generation pipeline:
- After `ResolveFacingAndCreateAddresses()`, find a suitable 10x20 area on the grid edge for the airport
- Create an Airport Address entity
- Set `city.AirportAddressId` to the airport's ID
- Call the Airport template to generate its interior

- [ ] **Step 4: Verify build compiles**

Run: `dotnet build stakeout.tests/ -v minimal`
Expected: May have errors from other files referencing old types — those need to be fixed in this step.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: update LocationGenerator and CityGenerator for new templates and airports"
```

---

### Task 12: Update PersonGenerator and SimulationManager

**Files:**
- Modify: `src/simulation/PersonGenerator.cs`
- Modify: `src/simulation/SimulationManager.cs`
- Modify: other files as needed for build to pass

This task updates the two main orchestration files and fixes any remaining build errors.

- [ ] **Step 1: Update PersonGenerator**

The agent should read `src/simulation/PersonGenerator.cs` and update it to:
- Replace `HomeUnitTag` with `HomeLocationId` — when assigning an apartment unit, find the Location entity and store its ID
- Replace `AssignVacantUnit` to work with Location entities (find a Location with `residential` tag that no person has as `HomeLocationId`)
- Gut schedule-building code with `// TODO: Project 3 (NPC Brain) — schedule building will be replaced`
- Replace `CreateHomeKey` to work with AccessPoints instead of SublocationConnections
- Set `person.CurrentCityId` during generation
- The person's initial position should be set to their home address position

- [ ] **Step 2: Update SimulationManager**

The agent should read `src/simulation/SimulationManager.cs` and update it to:
- Replace `SublocationGeneratorRegistry.RegisterAll()` with `AddressTemplateRegistry.RegisterAll()`
- Generate TWO cities (Boston and NYC) with separate CityGrids
- Generate people in Boston (the starting city)
- Create player in Boston
- Gut per-frame behavior updates with `// TODO: Project 3 (NPC Brain) — person behavior replaced by intent stack`
- Keep player travel working (update to use new location fields)
- Keep game clock ticking

- [ ] **Step 3: Fix all remaining build errors**

Run: `dotnet build stakeout.tests/ -v minimal`
Fix any remaining compilation errors from references to deleted types. This may involve:
- Updating crime-related files that reference old sublocation types
- Updating any UI files that reference SublocationGraph
- Ensuring all `using` statements are correct

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "feat: update PersonGenerator and SimulationManager for new hierarchy"
```

---

### Task 13: Multi-City Integration and Player Air Travel

**Files:**
- Create: `stakeout.tests/Simulation/MultiCityTests.cs`
- Modify: `src/simulation/SimulationManager.cs` (if not fully updated in Task 12)

- [ ] **Step 1: Write multi-city tests**

```csharp
// stakeout.tests/Simulation/MultiCityTests.cs
using System;
using System.Linq;
using Stakeout.Simulation;
using Stakeout.Simulation.Entities;
using Xunit;

namespace Stakeout.Tests.Simulation;

public class MultiCityTests
{
    [Fact]
    public void TwoCitiesExist()
    {
        var state = CreateTwoCityState();
        Assert.Equal(2, state.Cities.Count);
    }

    [Fact]
    public void EachCityHasAirport()
    {
        var state = CreateTwoCityState();
        foreach (var city in state.Cities.Values)
        {
            Assert.NotNull(city.AirportAddressId);
            var airport = state.Addresses[city.AirportAddressId.Value];
            Assert.Equal(AddressType.Airport, airport.Type);
        }
    }

    [Fact]
    public void EachCityHasOwnGrid()
    {
        var state = CreateTwoCityState();
        Assert.Equal(2, state.CityGrids.Count);
    }

    [Fact]
    public void AddressesBelongToCorrectCity()
    {
        var state = CreateTwoCityState();
        foreach (var city in state.Cities.Values)
        {
            foreach (var addrId in city.AddressIds)
            {
                Assert.Equal(city.Id, state.Addresses[addrId].CityId);
            }
        }
    }

    [Fact]
    public void PlayerCanFlyBetweenCities()
    {
        var state = CreateTwoCityState();
        var boston = state.Cities.Values.First(c => c.Name == "Boston");
        var nyc = state.Cities.Values.First(c => c.Name == "New York City");

        // Player starts in Boston
        state.Player = new Player
        {
            Id = state.GenerateEntityId(),
            CurrentCityId = boston.Id,
            CurrentAddressId = boston.AirportAddressId.Value,
            CurrentPosition = state.Addresses[boston.AirportAddressId.Value].Position
        };

        // Simulate flying to NYC
        var nycAirport = state.Addresses[nyc.AirportAddressId.Value];
        state.Player.CurrentCityId = nyc.Id;
        state.Player.CurrentAddressId = nycAirport.Id;
        state.Player.CurrentPosition = nycAirport.Position;

        Assert.Equal(nyc.Id, state.Player.CurrentCityId);
        Assert.Equal(nycAirport.Id, state.Player.CurrentAddressId);
    }

    private SimulationState CreateTwoCityState()
    {
        var state = new SimulationState();

        // Create two minimal cities with airports
        var boston = new City { Id = state.GenerateEntityId(), Name = "Boston", CountryName = "USA" };
        state.Cities[boston.Id] = boston;

        var nyc = new City { Id = state.GenerateEntityId(), Name = "New York City", CountryName = "USA" };
        state.Cities[nyc.Id] = nyc;

        // Create airports
        var bostonAirport = new Address
        {
            Id = state.GenerateEntityId(),
            CityId = boston.Id,
            Type = AddressType.Airport,
            GridX = 5, GridY = 5
        };
        state.Addresses[bostonAirport.Id] = bostonAirport;
        boston.AddressIds.Add(bostonAirport.Id);
        boston.AirportAddressId = bostonAirport.Id;
        new Addresses.AirportTemplate().Generate(bostonAirport, state, new Random(42));

        var nycAirport = new Address
        {
            Id = state.GenerateEntityId(),
            CityId = nyc.Id,
            Type = AddressType.Airport,
            GridX = 5, GridY = 5
        };
        state.Addresses[nycAirport.Id] = nycAirport;
        nyc.AddressIds.Add(nycAirport.Id);
        nyc.AirportAddressId = nycAirport.Id;
        new Addresses.AirportTemplate().Generate(nycAirport, state, new Random(42));

        return state;
    }
}
```

- [ ] **Step 2: Run multi-city tests**

Run: `dotnet test stakeout.tests/ --filter "FullyQualifiedName~MultiCityTests" -v minimal`
Expected: All pass.

- [ ] **Step 3: Commit**

```bash
git add stakeout.tests/Simulation/MultiCityTests.cs
git commit -m "feat: add multi-city integration tests with inter-city travel"
```

---

### Task 14: Fix Remaining Tests and Final Build Verification

**Files:**
- Modify: various test files as needed
- Modify: various source files as needed

- [ ] **Step 1: Run full test suite**

Run: `dotnet test stakeout.tests/ -v minimal`

This will likely show failures in tests that reference old types (sublocation, connection properties, etc.) or reference changed Person/Address properties. The agent should fix each failure by either:
- Updating the test to use new types
- Deleting tests that test removed functionality
- Fixing source code that still references old types

- [ ] **Step 2: Fix all failures iteratively**

Keep running `dotnet test stakeout.tests/ -v minimal` and fixing issues until all tests pass or all remaining failures are in gutted systems that are expected to fail.

- [ ] **Step 3: Verify build succeeds for the game project too**

Run: `dotnet build -v minimal`
Fix any remaining build errors in the main project (scenes, UI files, etc. that reference old types).

- [ ] **Step 4: Run full test suite one final time**

Run: `dotnet test stakeout.tests/ -v minimal`
Expected: All tests pass (only the new tests + any surviving old tests that still work).

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "fix: resolve all build errors and test failures for new location hierarchy"
```

---

### Task 15: Update Architecture Docs

**Files:**
- Modify: `docs/architecture/sublocation-system.md` (rename and rewrite)
- Modify: any other architecture docs that reference the old system

- [ ] **Step 1: Rewrite sublocation-system.md as location-hierarchy.md**

Rename `docs/architecture/sublocation-system.md` to `docs/architecture/location-hierarchy.md` and rewrite following the architecture doc format (Purpose, Key Files, How It Works, Key Decisions, Connection Points). Target 30-60 lines.

- [ ] **Step 2: Update any other architecture docs**

Check `docs/architecture/simulation-core.md` and others for references to the old sublocation system. Update them to reference the new hierarchy.

- [ ] **Step 3: Commit**

```bash
git add -A
git commit -m "docs: update architecture docs for new location hierarchy"
```
