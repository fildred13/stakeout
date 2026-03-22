# Unit Test Framework Design

## Overview

Add an xUnit test framework to STAKEOUT so that automated tests can verify game logic before manual review. The framework covers all pure C# simulation code and establishes a pattern for extracting testable logic from UI scene scripts.

## Goals

1. Enable `dotnet test` from the command line so the agent can run tests after every change
2. Provide VS Code Test Explorer integration for manual browsing/running of tests
3. Test all simulation logic: GameClock, SimulationState, generators, entities
4. Establish a "thin scene script" pattern where decision-making logic is extracted into testable pure C# methods

## Non-Goals

- Testing Godot rendering, input handling, or scene tree behavior
- Running tests inside the Godot editor (no GdUnit4)
- Exhaustive refactoring of all existing UI code (we extract one example and establish the pattern)

## Architecture

### Test Project Structure

```
stakeout/
├── stakeout.tests/
│   ├── stakeout.tests.csproj
│   └── Simulation/
│       ├── GameClockTests.cs
│       ├── SimulationStateTests.cs
│       ├── LocationGeneratorTests.cs
│       ├── PersonGeneratorTests.cs
│       └── Entities/
│           ├── PersonTests.cs
│           ├── AddressTests.cs
│           └── ...
```

The test project mirrors the `src/` folder structure.

### Test Project Configuration

`stakeout.tests.csproj` targets `net8.0`, references the main `stakeout.csproj`, and includes:

- `xunit` — test framework
- `xunit.runner.visualstudio` — VS Code Test Explorer integration
- `Microsoft.NET.Test.Sdk` — required by `dotnet test`

The solution file `stakeout.sln` is updated to include the test project.

### Godot Type Dependencies

Several entity and simulation classes (`Address`, `LocationGenerator`) use `Godot.Vector2`. Since the test project references the main `stakeout.csproj` which uses `Godot.NET.Sdk/4.6.1`, the Godot types resolve transitively through the project reference. The test `.csproj` itself uses the standard `Microsoft.NET.Sdk` (not Godot's SDK) — it doesn't need to run the Godot engine, it just needs the types to compile. This is validated by the fact that `GodotSharp` is a NuGet package that the Godot SDK pulls in, and project references carry transitive dependencies.

If transitive resolution proves problematic, the fallback is to add an explicit `PackageReference` to `GodotSharp` in the test project.

### Extracting Testable Logic from UI Code

Scene scripts (e.g., SimulationDebug.cs) currently contain decision-making logic interleaved with Godot node manipulation. The pattern going forward:

1. **Decision logic** lives in pure C# methods on simulation classes (testable)
2. **Scene scripts** become thin wrappers that call those methods and update the visual tree (not tested)

**Example — tooltip entity query:**

Currently, SimulationDebug.cs contains `UpdateHoverLabel()` which iterates dictionaries mapping entity IDs to Panel nodes, checking mouse proximity to each node's position to determine what to show in the tooltip. This proximity/query logic gets extracted:

```csharp
// SimulationState.cs — pure C#, testable
public List<string> GetEntityNamesAtAddress(Address address)
{
    // Returns all person names whose HomeAddress matches
}

// SimulationDebug.cs — thin wrapper, not tested
// 1. Detect which address node the mouse is near (Godot proximity check)
// 2. Call SimulationState.GetEntityNamesAtAddress(address)
// 3. Display the returned names in the tooltip label
```

The scene script still owns the Godot-specific proximity detection (mouse position vs. node positions), but the decision of *which entities to display for a given address* becomes a testable query. Test setup for this will need to pre-populate SimulationState with addresses and people.

This is applied to the tooltip case as a working example. Future features follow the pattern from the start.

## Test Conventions

### Naming

- Test classes: `{ClassUnderTest}Tests` (e.g., `GameClockTests`)
- Test methods: `{MethodName}_{Scenario}_{ExpectedResult}` (e.g., `Tick_OneSecond_AdvancesGameTimeByOneMinute`)
- One test class per source class, matching folder structure

### Initial Test Coverage

| Class | What's Tested |
|-------|--------------|
| `GameClock` | Tick advancement, time formatting |
| `SimulationState` | Entity registration, ID generation, `GetEntityNamesAtAddress` query |
| `LocationGenerator` | Generates expected number of streets/addresses, address type distribution |
| `PersonGenerator` | Generates people with valid names and assigned addresses |
| Entity classes | Constructor logic, property defaults |

### What Is NOT Tested

- Scene scripts (thin Godot wrappers)
- Godot rendering, input handling, scene tree behavior
- Anything requiring a running Godot engine

## Running Tests

- **CLI:** `dotnet test stakeout.tests/` from the repo root
- **VS Code:** Test Explorer sidebar auto-discovers tests via xunit.runner.visualstudio
- **During development sessions:** Agent runs `dotnet test` after changes, before handing off for manual review

## VS Code Integration

No additional configuration needed beyond the standard C# Dev Kit extension (`ms-dotnettools.csdevkit`). The xunit runner package enables automatic test discovery in the Test Explorer.
