# Unit Test Framework Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add an xUnit test project with comprehensive tests for all simulation logic, plus extract testable query methods from UI code.

**Architecture:** A separate `stakeout.tests/` xUnit project references the main `stakeout.csproj` and tests all pure C# simulation classes. One UI logic extraction (tooltip entity query) establishes the "thin scene script" pattern. TDD throughout — write failing tests first, then implement.

**Tech Stack:** xUnit, Microsoft.NET.Test.Sdk, xunit.runner.visualstudio, .NET 8.0

**Spec:** `docs/superpowers/specs/2026-03-22-unit-test-framework-design.md`

---

## File Structure

**Create:**
- `stakeout.tests/stakeout.tests.csproj` — test project configuration
- `stakeout.tests/Simulation/GameClockTests.cs` — GameClock unit tests
- `stakeout.tests/Simulation/SimulationStateTests.cs` — SimulationState unit tests (including new GetEntityNamesAtAddress)
- `stakeout.tests/Simulation/LocationGeneratorTests.cs` — LocationGenerator unit tests
- `stakeout.tests/Simulation/PersonGeneratorTests.cs` — PersonGenerator unit tests
- `stakeout.tests/Simulation/Entities/AddressTypeTests.cs` — AddressType/AddressCategory enum + extension tests
- `stakeout.tests/Simulation/Entities/PersonTests.cs` — Person entity tests

**Modify:**
- `stakeout.sln` — add test project to solution
- `src/simulation/SimulationState.cs` — add `GetEntityNamesAtAddress(Address)` and `GetEntityNamesAtPosition(float, float, float)` methods
- `scenes/simulation_debug/SimulationDebug.cs` — refactor `UpdateHoverLabel()` to use new SimulationState query methods

---

### Task 1: Create Test Project and Validate Godot Type Resolution

This task validates the critical assumption that the test project can reference Godot types transitively through the main project.

**Files:**
- Create: `stakeout.tests/stakeout.tests.csproj`
- Modify: `stakeout.sln`

- [ ] **Step 1: Create test project .csproj**

Create `stakeout.tests/stakeout.tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <RootNamespace>Stakeout.Tests</RootNamespace>
    <ImplicitUsings>disable</ImplicitUsings>
    <Nullable>disable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.*" />
    <PackageReference Include="xunit" Version="2.*" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.*">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\stakeout.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Add test project to solution**

Run:
```bash
cd h:/Dropbox/sean-tower/Documents/git/stakeout && dotnet sln add stakeout.tests/stakeout.tests.csproj
```

Expected: `Project 'stakeout.tests/stakeout.tests.csproj' added to the solution.`

- [ ] **Step 3: Write a smoke test that references a Godot type**

Create `stakeout.tests/Simulation/GodotTypeResolutionTest.cs`:

```csharp
using Stakeout.Simulation.Entities;
using Xunit;

namespace Stakeout.Tests.Simulation;

public class GodotTypeResolutionTest
{
    [Fact]
    public void CanCreateAddressWithVector2Position()
    {
        var address = new Address
        {
            Id = 1,
            Number = 100,
            StreetId = 1,
            Type = AddressType.SuburbanHome,
            Position = new Godot.Vector2(50f, 100f)
        };

        Assert.Equal(50f, address.Position.X);
        Assert.Equal(100f, address.Position.Y);
    }
}
```

- [ ] **Step 4: Run the test to validate Godot types resolve**

Run:
```bash
cd h:/Dropbox/sean-tower/Documents/git/stakeout && dotnet test stakeout.tests/ --verbosity normal
```

Expected: 1 test passed. If this fails with Godot type resolution errors, add an explicit `<PackageReference Include="GodotSharp" Version="4.6.*" />` to the test `.csproj` and retry.

- [ ] **Step 5: Delete the smoke test file**

Remove `stakeout.tests/Simulation/GodotTypeResolutionTest.cs` — it was only for validation. Real tests follow in subsequent tasks.

- [ ] **Step 6: Commit**

```bash
git add stakeout.tests/stakeout.tests.csproj stakeout.sln
git commit -m "Add xUnit test project with Godot type resolution validated"
```

---

### Task 2: GameClock Tests

**Files:**
- Create: `stakeout.tests/Simulation/GameClockTests.cs`

Reference: `src/simulation/GameClock.cs` — has constructor with optional start time, `Tick(double deltaSec)` method, `CurrentTime` and `ElapsedSeconds` properties.

- [ ] **Step 1: Write failing tests**

Create `stakeout.tests/Simulation/GameClockTests.cs`:

```csharp
using System;
using Stakeout.Simulation;
using Xunit;

namespace Stakeout.Tests.Simulation;

public class GameClockTests
{
    [Fact]
    public void Constructor_Default_StartsAt1984Jan1Midnight()
    {
        var clock = new GameClock();

        Assert.Equal(new DateTime(1984, 1, 1, 0, 0, 0), clock.CurrentTime);
        Assert.Equal(0.0, clock.ElapsedSeconds);
    }

    [Fact]
    public void Constructor_CustomStartTime_UsesProvidedTime()
    {
        var start = new DateTime(1984, 6, 15, 12, 0, 0);
        var clock = new GameClock(start);

        Assert.Equal(start, clock.CurrentTime);
    }

    [Fact]
    public void Tick_OneSecond_AdvancesCurrentTimeByOneSecond()
    {
        var clock = new GameClock();

        clock.Tick(1.0);

        Assert.Equal(new DateTime(1984, 1, 1, 0, 0, 1), clock.CurrentTime);
        Assert.Equal(1.0, clock.ElapsedSeconds);
    }

    [Fact]
    public void Tick_MultipleCalls_AccumulatesTime()
    {
        var clock = new GameClock();

        clock.Tick(0.5);
        clock.Tick(0.5);
        clock.Tick(1.0);

        Assert.Equal(2.0, clock.ElapsedSeconds);
        Assert.Equal(new DateTime(1984, 1, 1, 0, 0, 2), clock.CurrentTime);
    }

    [Fact]
    public void Tick_LargeDelta_AdvancesCorrectly()
    {
        var clock = new GameClock();

        clock.Tick(3600.0); // one hour

        Assert.Equal(new DateTime(1984, 1, 1, 1, 0, 0), clock.CurrentTime);
        Assert.Equal(3600.0, clock.ElapsedSeconds);
    }
}
```

- [ ] **Step 2: Run tests to verify they pass**

Run:
```bash
cd h:/Dropbox/sean-tower/Documents/git/stakeout && dotnet test stakeout.tests/ --filter "FullyQualifiedName~GameClockTests" --verbosity normal
```

Expected: 5 tests passed (these test existing code, so they should pass immediately).

- [ ] **Step 3: Commit**

```bash
git add stakeout.tests/Simulation/GameClockTests.cs
git commit -m "Add GameClock unit tests"
```

---

### Task 3: AddressType and Entity Tests

**Files:**
- Create: `stakeout.tests/Simulation/Entities/AddressTypeTests.cs`
- Create: `stakeout.tests/Simulation/Entities/PersonTests.cs`

Reference: `src/simulation/entities/AddressType.cs` — `AddressType` enum, `AddressCategory` enum, `GetCategory()` extension method. `src/simulation/entities/Person.cs` — `FullName` computed property.

- [ ] **Step 1: Write AddressType tests**

Create `stakeout.tests/Simulation/Entities/AddressTypeTests.cs`:

```csharp
using Stakeout.Simulation.Entities;
using Xunit;

namespace Stakeout.Tests.Simulation.Entities;

public class AddressTypeTests
{
    [Fact]
    public void GetCategory_SuburbanHome_ReturnsResidential()
    {
        Assert.Equal(AddressCategory.Residential, AddressType.SuburbanHome.GetCategory());
    }

    [Theory]
    [InlineData(AddressType.Diner)]
    [InlineData(AddressType.DiveBar)]
    [InlineData(AddressType.Office)]
    public void GetCategory_CommercialTypes_ReturnsCommercial(AddressType type)
    {
        Assert.Equal(AddressCategory.Commercial, type.GetCategory());
    }
}
```

- [ ] **Step 2: Write Person entity tests**

Create `stakeout.tests/Simulation/Entities/PersonTests.cs`:

```csharp
using Stakeout.Simulation.Entities;
using Xunit;

namespace Stakeout.Tests.Simulation.Entities;

public class PersonTests
{
    [Fact]
    public void FullName_ReturnsCombinedFirstAndLastName()
    {
        var person = new Person { FirstName = "James", LastName = "Smith" };

        Assert.Equal("James Smith", person.FullName);
    }
}
```

- [ ] **Step 3: Run tests to verify they pass**

Run:
```bash
cd h:/Dropbox/sean-tower/Documents/git/stakeout && dotnet test stakeout.tests/ --filter "FullyQualifiedName~Entities" --verbosity normal
```

Expected: 4 tests passed.

- [ ] **Step 4: Commit**

```bash
git add stakeout.tests/Simulation/Entities/
git commit -m "Add AddressType and Person entity tests"
```

---

### Task 4: SimulationState Tests

**Files:**
- Create: `stakeout.tests/Simulation/SimulationStateTests.cs`

Reference: `src/simulation/SimulationState.cs` — `GenerateEntityId()` returns incrementing IDs, constructor creates `GameClock`, has dictionary properties for entities.

- [ ] **Step 1: Write tests**

Create `stakeout.tests/Simulation/SimulationStateTests.cs`:

```csharp
using Stakeout.Simulation;
using Stakeout.Simulation.Entities;
using Xunit;

namespace Stakeout.Tests.Simulation;

public class SimulationStateTests
{
    [Fact]
    public void Constructor_InitializesClockAndEmptyCollections()
    {
        var state = new SimulationState();

        Assert.NotNull(state.Clock);
        Assert.Empty(state.People);
        Assert.Empty(state.Countries);
        Assert.Empty(state.Cities);
        Assert.Empty(state.Streets);
        Assert.Empty(state.Addresses);
        Assert.Null(state.Player);
    }

    [Fact]
    public void GenerateEntityId_FirstCall_Returns1()
    {
        var state = new SimulationState();

        Assert.Equal(1, state.GenerateEntityId());
    }

    [Fact]
    public void GenerateEntityId_MultipleCalls_ReturnsIncrementing()
    {
        var state = new SimulationState();

        var id1 = state.GenerateEntityId();
        var id2 = state.GenerateEntityId();
        var id3 = state.GenerateEntityId();

        Assert.Equal(1, id1);
        Assert.Equal(2, id2);
        Assert.Equal(3, id3);
    }
}
```

- [ ] **Step 2: Run tests to verify they pass**

Run:
```bash
cd h:/Dropbox/sean-tower/Documents/git/stakeout && dotnet test stakeout.tests/ --filter "FullyQualifiedName~SimulationStateTests" --verbosity normal
```

Expected: 3 tests passed.

- [ ] **Step 3: Commit**

```bash
git add stakeout.tests/Simulation/SimulationStateTests.cs
git commit -m "Add SimulationState unit tests"
```

---

### Task 5: LocationGenerator Tests

**Files:**
- Create: `stakeout.tests/Simulation/LocationGeneratorTests.cs`

Reference: `src/simulation/LocationGenerator.cs` — `GenerateCity(SimulationState)` creates 1 country, 1 city, 15 streets with 3-8 addresses each. Address types distributed: 50% SuburbanHome, 20% Office, 15% Diner, 15% DiveBar. Addresses have positions within map bounds (40-1240 x, 40-680 y).

- [ ] **Step 1: Write tests**

Create `stakeout.tests/Simulation/LocationGeneratorTests.cs`:

```csharp
using System.Linq;
using Stakeout.Simulation;
using Stakeout.Simulation.Entities;
using Xunit;

namespace Stakeout.Tests.Simulation;

public class LocationGeneratorTests
{
    private static SimulationState GenerateCityState()
    {
        var state = new SimulationState();
        var generator = new LocationGenerator();
        generator.GenerateCity(state);
        return state;
    }

    [Fact]
    public void GenerateCity_CreatesOneCountry()
    {
        var state = GenerateCityState();

        Assert.Single(state.Countries);
        Assert.Equal("United States", state.Countries[0].Name);
    }

    [Fact]
    public void GenerateCity_CreatesOneCity()
    {
        var state = GenerateCityState();

        Assert.Single(state.Cities);
        Assert.Equal("Boston", state.Cities.Values.First().Name);
    }

    [Fact]
    public void GenerateCity_Creates15Streets()
    {
        var state = GenerateCityState();

        Assert.Equal(15, state.Streets.Count);
    }

    [Fact]
    public void GenerateCity_AllStreetNamesAreUnique()
    {
        var state = GenerateCityState();

        var names = state.Streets.Values.Select(s => s.Name).ToList();
        Assert.Equal(names.Count, names.Distinct().Count());
    }

    [Fact]
    public void GenerateCity_EachStreetHas3To8Addresses()
    {
        var state = GenerateCityState();

        foreach (var street in state.Streets.Values)
        {
            var addressCount = state.Addresses.Values.Count(a => a.StreetId == street.Id);
            Assert.InRange(addressCount, 3, 8);
        }
    }

    [Fact]
    public void GenerateCity_TotalAddressCountInExpectedRange()
    {
        var state = GenerateCityState();

        // 15 streets * 3-8 addresses = 45-120 addresses
        Assert.InRange(state.Addresses.Count, 45, 120);
    }

    [Fact]
    public void GenerateCity_AddressPositionsWithinMapBounds()
    {
        var state = GenerateCityState();

        foreach (var address in state.Addresses.Values)
        {
            Assert.InRange(address.Position.X, 40f, 1240f);
            Assert.InRange(address.Position.Y, 40f, 680f);
        }
    }

    [Fact]
    public void GenerateCity_ContainsBothResidentialAndCommercialAddresses()
    {
        var state = GenerateCityState();

        Assert.Contains(state.Addresses.Values, a => a.Category == AddressCategory.Residential);
        Assert.Contains(state.Addresses.Values, a => a.Category == AddressCategory.Commercial);
    }

    [Fact]
    public void GenerateCity_AddressNumbersArePositive()
    {
        var state = GenerateCityState();

        foreach (var address in state.Addresses.Values)
        {
            Assert.InRange(address.Number, 1, 10000);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they pass**

Run:
```bash
cd h:/Dropbox/sean-tower/Documents/git/stakeout && dotnet test stakeout.tests/ --filter "FullyQualifiedName~LocationGeneratorTests" --verbosity normal
```

Expected: 9 tests passed.

- [ ] **Step 3: Commit**

```bash
git add stakeout.tests/Simulation/LocationGeneratorTests.cs
git commit -m "Add LocationGenerator unit tests"
```

---

### Task 6: PersonGenerator Tests

**Files:**
- Create: `stakeout.tests/Simulation/PersonGeneratorTests.cs`

Reference: `src/simulation/PersonGenerator.cs` — `GeneratePerson(SimulationState)` requires pre-populated state with residential and commercial addresses. Sets FirstName, LastName from NameData pools, HomeAddressId from residential, WorkAddressId from commercial, CurrentAddressId = HomeAddressId.

- [ ] **Step 1: Write tests**

Create `stakeout.tests/Simulation/PersonGeneratorTests.cs`:

```csharp
using System.Linq;
using Stakeout.Simulation;
using Stakeout.Simulation.Data;
using Stakeout.Simulation.Entities;
using Xunit;

namespace Stakeout.Tests.Simulation;

public class PersonGeneratorTests
{
    private static SimulationState CreatePopulatedState()
    {
        var state = new SimulationState();
        var locationGen = new LocationGenerator();
        locationGen.GenerateCity(state);
        return state;
    }

    [Fact]
    public void GeneratePerson_ReturnsPersonWithValidId()
    {
        var state = CreatePopulatedState();
        var generator = new PersonGenerator();

        var person = generator.GeneratePerson(state);

        Assert.True(person.Id > 0);
    }

    [Fact]
    public void GeneratePerson_AddsPersonToState()
    {
        var state = CreatePopulatedState();
        var generator = new PersonGenerator();

        var person = generator.GeneratePerson(state);

        Assert.Contains(person.Id, state.People.Keys);
        Assert.Same(person, state.People[person.Id]);
    }

    [Fact]
    public void GeneratePerson_NameComesFromNameDataPools()
    {
        var state = CreatePopulatedState();
        var generator = new PersonGenerator();

        var person = generator.GeneratePerson(state);

        Assert.Contains(person.FirstName, NameData.FirstNames);
        Assert.Contains(person.LastName, NameData.LastNames);
    }

    [Fact]
    public void GeneratePerson_HomeAddressIsResidential()
    {
        var state = CreatePopulatedState();
        var generator = new PersonGenerator();

        var person = generator.GeneratePerson(state);

        var home = state.Addresses[person.HomeAddressId];
        Assert.Equal(AddressCategory.Residential, home.Category);
    }

    [Fact]
    public void GeneratePerson_WorkAddressIsCommercial()
    {
        var state = CreatePopulatedState();
        var generator = new PersonGenerator();

        var person = generator.GeneratePerson(state);

        var work = state.Addresses[person.WorkAddressId];
        Assert.Equal(AddressCategory.Commercial, work.Category);
    }

    [Fact]
    public void GeneratePerson_CurrentAddressStartsAtHome()
    {
        var state = CreatePopulatedState();
        var generator = new PersonGenerator();

        var person = generator.GeneratePerson(state);

        Assert.Equal(person.HomeAddressId, person.CurrentAddressId);
    }

    [Fact]
    public void GeneratePerson_SetsCreatedAtToClockTime()
    {
        var state = CreatePopulatedState();
        var generator = new PersonGenerator();

        var person = generator.GeneratePerson(state);

        Assert.Equal(state.Clock.CurrentTime, person.CreatedAt);
    }

    [Fact]
    public void GeneratePerson_MultiplePeople_GetUniqueIds()
    {
        var state = CreatePopulatedState();
        var generator = new PersonGenerator();

        var p1 = generator.GeneratePerson(state);
        var p2 = generator.GeneratePerson(state);
        var p3 = generator.GeneratePerson(state);

        Assert.NotEqual(p1.Id, p2.Id);
        Assert.NotEqual(p2.Id, p3.Id);
        Assert.NotEqual(p1.Id, p3.Id);
    }
}
```

- [ ] **Step 2: Run tests to verify they pass**

Run:
```bash
cd h:/Dropbox/sean-tower/Documents/git/stakeout && dotnet test stakeout.tests/ --filter "FullyQualifiedName~PersonGeneratorTests" --verbosity normal
```

Expected: 8 tests passed.

- [ ] **Step 3: Commit**

```bash
git add stakeout.tests/Simulation/PersonGeneratorTests.cs
git commit -m "Add PersonGenerator unit tests"
```

---

### Task 7: Extract Tooltip Query Logic (TDD)

This is the "thin scene script" extraction. We add `GetEntityNamesAtAddress(Address)` to `SimulationState`, write tests first, then implement, then refactor `SimulationDebug.cs` to use it.

**Files:**
- Modify: `stakeout.tests/Simulation/SimulationStateTests.cs` — add tests for new method
- Modify: `src/simulation/SimulationState.cs` — add `GetEntityNamesAtAddress` method
- Modify: `scenes/simulation_debug/SimulationDebug.cs` — refactor `UpdateHoverLabel` to use new method

- [ ] **Step 1: Write failing tests for GetEntityNamesAtAddress**

Add to `stakeout.tests/Simulation/SimulationStateTests.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using Stakeout.Simulation;
using Stakeout.Simulation.Entities;
using Xunit;

namespace Stakeout.Tests.Simulation;

public class SimulationStateTests
{
    // ... existing tests remain unchanged ...

    [Fact]
    public void GetEntityNamesAtAddress_NoPeople_ReturnsEmptyList()
    {
        var state = new SimulationState();
        var address = new Address { Id = 1, Number = 100, StreetId = 1, Type = AddressType.SuburbanHome };
        state.Addresses[address.Id] = address;

        var names = state.GetEntityNamesAtAddress(address);

        Assert.Empty(names);
    }

    [Fact]
    public void GetEntityNamesAtAddress_OnePersonAtAddress_ReturnsTheirName()
    {
        var state = new SimulationState();
        var address = new Address { Id = 1, Number = 100, StreetId = 1, Type = AddressType.SuburbanHome };
        state.Addresses[address.Id] = address;
        state.People[10] = new Person
        {
            Id = 10, FirstName = "James", LastName = "Smith",
            HomeAddressId = 1, WorkAddressId = 1, CurrentAddressId = 1
        };

        var names = state.GetEntityNamesAtAddress(address);

        Assert.Single(names);
        Assert.Equal("James Smith", names[0]);
    }

    [Fact]
    public void GetEntityNamesAtAddress_MultiplePeopleAtAddress_ReturnsAllNames()
    {
        var state = new SimulationState();
        var address = new Address { Id = 1, Number = 100, StreetId = 1, Type = AddressType.SuburbanHome };
        state.Addresses[address.Id] = address;
        state.People[10] = new Person
        {
            Id = 10, FirstName = "James", LastName = "Smith",
            HomeAddressId = 1, WorkAddressId = 1, CurrentAddressId = 1
        };
        state.People[11] = new Person
        {
            Id = 11, FirstName = "Mary", LastName = "Johnson",
            HomeAddressId = 1, WorkAddressId = 1, CurrentAddressId = 1
        };

        var names = state.GetEntityNamesAtAddress(address);

        Assert.Equal(2, names.Count);
        Assert.Contains("James Smith", names);
        Assert.Contains("Mary Johnson", names);
    }

    [Fact]
    public void GetEntityNamesAtAddress_PeopleAtDifferentAddresses_OnlyReturnsMatching()
    {
        var state = new SimulationState();
        var addr1 = new Address { Id = 1, Number = 100, StreetId = 1, Type = AddressType.SuburbanHome };
        var addr2 = new Address { Id = 2, Number = 200, StreetId = 1, Type = AddressType.Office };
        state.Addresses[addr1.Id] = addr1;
        state.Addresses[addr2.Id] = addr2;
        state.People[10] = new Person
        {
            Id = 10, FirstName = "James", LastName = "Smith",
            HomeAddressId = 1, WorkAddressId = 2, CurrentAddressId = 1
        };
        state.People[11] = new Person
        {
            Id = 11, FirstName = "Mary", LastName = "Johnson",
            HomeAddressId = 2, WorkAddressId = 1, CurrentAddressId = 2
        };

        var names = state.GetEntityNamesAtAddress(addr1);

        Assert.Single(names);
        Assert.Equal("James Smith", names[0]);
    }

    [Fact]
    public void GetEntityNamesAtAddress_MatchesOnCurrentAddressId()
    {
        var state = new SimulationState();
        var home = new Address { Id = 1, Number = 100, StreetId = 1, Type = AddressType.SuburbanHome };
        var work = new Address { Id = 2, Number = 200, StreetId = 1, Type = AddressType.Office };
        state.Addresses[home.Id] = home;
        state.Addresses[work.Id] = work;
        // Person's home is addr1 but they're currently at addr2
        state.People[10] = new Person
        {
            Id = 10, FirstName = "James", LastName = "Smith",
            HomeAddressId = 1, WorkAddressId = 2, CurrentAddressId = 2
        };

        var namesAtHome = state.GetEntityNamesAtAddress(home);
        var namesAtWork = state.GetEntityNamesAtAddress(work);

        Assert.Empty(namesAtHome);
        Assert.Single(namesAtWork);
        Assert.Equal("James Smith", namesAtWork[0]);
    }
}
```

Note: Replace the entire file content — the `using` statements at the top change (add `System.Collections.Generic` and `System.Linq`).

- [ ] **Step 2: Run tests to verify they fail**

Run:
```bash
cd h:/Dropbox/sean-tower/Documents/git/stakeout && dotnet test stakeout.tests/ --filter "FullyQualifiedName~SimulationStateTests" --verbosity normal
```

Expected: FAIL — `SimulationState` does not contain a definition for `GetEntityNamesAtAddress`.

- [ ] **Step 3: Implement GetEntityNamesAtAddress on SimulationState**

Add to `src/simulation/SimulationState.cs`, adding `using System.Linq;` to the top and this method to the class:

```csharp
public List<string> GetEntityNamesAtAddress(Address address)
{
    return People.Values
        .Where(p => p.CurrentAddressId == address.Id)
        .Select(p => p.FullName)
        .ToList();
}
```

The full file should be:

```csharp
using System.Collections.Generic;
using System.Linq;
using Stakeout.Simulation.Entities;

namespace Stakeout.Simulation;

public class SimulationState
{
    public GameClock Clock { get; }
    public Dictionary<int, Person> People { get; } = new();
    public Player Player { get; set; }
    public List<Country> Countries { get; } = new();
    public Dictionary<int, City> Cities { get; } = new();
    public Dictionary<int, Street> Streets { get; } = new();
    public Dictionary<int, Address> Addresses { get; } = new();

    private int _nextEntityId = 1;

    public SimulationState()
    {
        Clock = new GameClock();
    }

    public int GenerateEntityId() => _nextEntityId++;

    public List<string> GetEntityNamesAtAddress(Address address)
    {
        return People.Values
            .Where(p => p.CurrentAddressId == address.Id)
            .Select(p => p.FullName)
            .ToList();
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run:
```bash
cd h:/Dropbox/sean-tower/Documents/git/stakeout && dotnet test stakeout.tests/ --filter "FullyQualifiedName~SimulationStateTests" --verbosity normal
```

Expected: 8 tests passed (3 original + 5 new).

- [ ] **Step 5: Refactor SimulationDebug.UpdateHoverLabel to use new method**

In `scenes/simulation_debug/SimulationDebug.cs`, replace the person-iteration section of `UpdateHoverLabel()`. The method becomes:

```csharp
private void UpdateHoverLabel()
{
    var mousePos = GetGlobalMousePosition();
    var lines = new List<string>();

    if (_playerNode != null)
    {
        var center = _playerNode.Position + new Vector2(EntityDotSize / 2, EntityDotSize / 2);
        if (mousePos.DistanceTo(center) <= HoverDistance)
            lines.Add("You");
    }

    foreach (var (addressId, icon) in _addressNodes)
    {
        var center = icon.Position + new Vector2(LocationIconSize / 2, LocationIconSize / 2);
        if (mousePos.DistanceTo(center) <= HoverDistance)
        {
            var address = _simulationManager.State.Addresses[addressId];
            var street = _simulationManager.State.Streets[address.StreetId];
            lines.Add($"{address.Number} {street.Name} ({address.Type})");

            // Use testable query for people at this address
            lines.AddRange(_simulationManager.State.GetEntityNamesAtAddress(address));
        }
    }

    // Check people at positions not co-located with an address icon
    foreach (var (personId, dot) in _personNodes)
    {
        var center = dot.Position + new Vector2(EntityDotSize / 2, EntityDotSize / 2);
        if (mousePos.DistanceTo(center) <= HoverDistance)
        {
            var person = _simulationManager.State.People[personId];
            // Only add if not already found via address hover above
            if (!lines.Contains(person.FullName))
                lines.Add(person.FullName);
        }
    }

    if (lines.Count > 0)
    {
        _hoverLabel.Text = string.Join("\n", lines);
        _hoverLabel.Position = mousePos + new Vector2(15, -10);
        _hoverLabel.Visible = true;
    }
    else
    {
        _hoverLabel.Visible = false;
    }
}
```

- [ ] **Step 6: Run all tests to verify nothing is broken**

Run:
```bash
cd h:/Dropbox/sean-tower/Documents/git/stakeout && dotnet test stakeout.tests/ --verbosity normal
```

Expected: All tests pass (25 total).

- [ ] **Step 7: Commit**

```bash
git add src/simulation/SimulationState.cs stakeout.tests/Simulation/SimulationStateTests.cs scenes/simulation_debug/SimulationDebug.cs
git commit -m "Extract tooltip entity query into testable SimulationState method"
```

---

### Task 8: Final Validation

- [ ] **Step 1: Run full test suite**

Run:
```bash
cd h:/Dropbox/sean-tower/Documents/git/stakeout && dotnet test stakeout.tests/ --verbosity normal
```

Expected: 25 tests passed, 0 failed.

- [ ] **Step 2: Verify VS Code Test Explorer**

Open VS Code, navigate to the Testing sidebar (beaker icon). Verify that tests appear grouped under `Stakeout.Tests.Simulation`. This is a manual verification step — confirm with the user.

- [ ] **Step 3: Commit any remaining changes**

If any cleanup was needed, commit. Otherwise this step is a no-op.
