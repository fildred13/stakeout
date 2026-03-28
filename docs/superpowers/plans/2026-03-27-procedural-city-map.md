# Procedural City Map Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the random point-cloud city map with a procedurally generated grid-based city featuring roads, city blocks, and a pannable/zoomable viewport.

**Architecture:** A 100x100 grid of cells forms the city. A two-pass generation pipeline first lays roads (arterials + secondary), then fills city blocks with weighted building types based on distance from center. CityView is rewritten as a pannable viewport rendering visible cells. Address loses its Vector2 Position in favor of grid coordinates. Travel and rendering derive pixel positions from grid positions.

**Tech Stack:** Godot 4.6, C# (.NET), xUnit for tests

**Spec:** `docs/superpowers/specs/2026-03-27-procedural-city-map-design.md`

---

## File Map

| File | Action | Responsibility |
|------|--------|---------------|
| `src/simulation/city/PlotType.cs` | Create | PlotType enum (Road, SuburbanHome, ApartmentBuilding, etc.) with size lookup |
| `src/simulation/city/Cell.cs` | Create | Cell struct: PlotType, AddressId?, StreetId?, FacingDirection |
| `src/simulation/city/CityGrid.cs` | Create | 100x100 Cell array, lookup methods |
| `src/simulation/city/CityGenerator.cs` | Create | Full generation pipeline (roads, subdivision, naming, plot assignment, addresses) |
| `src/simulation/entities/Address.cs` | Modify | Remove Position, add GridX/GridY |
| `src/simulation/entities/Street.cs` | Modify | Add road cell coordinate list |
| `src/simulation/MapConfig.cs` | Modify | Grid-based bounds (100x100 at 48px), update travel time computation |
| `src/simulation/SimulationState.cs` | Modify | Add CityGrid field |
| `src/simulation/SimulationManager.cs` | Modify | Replace LocationGenerator with CityGenerator, update travel methods |
| `src/simulation/LocationGenerator.cs` | Modify | Remove address positioning; keep street name generation as utility |
| `src/simulation/entities/TravelInfo.cs` | Modify | Positions use grid-derived pixel coords |
| `src/simulation/entities/Player.cs` | No change | CurrentPosition stays Vector2, derived from grid |
| `src/simulation/entities/Person.cs` | No change | CurrentPosition stays Vector2, derived from grid |
| `src/simulation/scheduling/PersonBehavior.cs` | Modify | Use grid-derived positions for travel |
| `src/simulation/PersonGenerator.cs` | Modify | Pick plots from CityGrid instead of calling LocationGenerator.GenerateAddress |
| `scenes/city/CityView.cs` | Rewrite | Grid rendering, pan/zoom viewport, selection, highlights, sidebar integration |
| `scenes/city/CityView.tscn` | Modify | Update node structure for new rendering approach |
| `stakeout.tests/Simulation/City/CellTests.cs` | Create | Cell struct tests |
| `stakeout.tests/Simulation/City/CityGridTests.cs` | Create | Grid lookup tests |
| `stakeout.tests/Simulation/City/CityGeneratorTests.cs` | Create | Generation pipeline tests |
| `stakeout.tests/Simulation/LocationGeneratorTests.cs` | Modify | Update for new generation approach |
| `stakeout.tests/Simulation/MapConfigTests.cs` | Modify | Update for grid-based config |

---

### Task 1: Create PlotType enum and Cell struct

**Files:**
- Create: `src/simulation/city/PlotType.cs`
- Create: `src/simulation/city/Cell.cs`
- Create: `stakeout.tests/Simulation/City/CellTests.cs`

- [ ] **Step 1: Write tests for PlotType and Cell**

Create `stakeout.tests/Simulation/City/CellTests.cs`:

```csharp
using Stakeout.Simulation.City;
using Stakeout.Simulation.Entities;
using Xunit;

namespace Stakeout.Tests.Simulation.City;

public class PlotTypeTests
{
    [Theory]
    [InlineData(PlotType.SuburbanHome, 1, 1)]
    [InlineData(PlotType.Diner, 1, 1)]
    [InlineData(PlotType.DiveBar, 1, 1)]
    [InlineData(PlotType.ApartmentBuilding, 2, 2)]
    [InlineData(PlotType.Office, 2, 2)]
    [InlineData(PlotType.Park, 2, 2)]
    [InlineData(PlotType.Road, 1, 1)]
    [InlineData(PlotType.Empty, 1, 1)]
    public void GetSize_ReturnsCorrectSize(PlotType type, int expectedWidth, int expectedHeight)
    {
        var (w, h) = type.GetSize();
        Assert.Equal(expectedWidth, w);
        Assert.Equal(expectedHeight, h);
    }

    [Theory]
    [InlineData(PlotType.SuburbanHome, AddressType.SuburbanHome)]
    [InlineData(PlotType.ApartmentBuilding, AddressType.ApartmentBuilding)]
    [InlineData(PlotType.Office, AddressType.Office)]
    [InlineData(PlotType.Diner, AddressType.Diner)]
    [InlineData(PlotType.DiveBar, AddressType.DiveBar)]
    [InlineData(PlotType.Park, AddressType.Park)]
    public void ToAddressType_MapsCorrectly(PlotType plotType, AddressType expected)
    {
        Assert.Equal(expected, plotType.ToAddressType());
    }

    [Theory]
    [InlineData(PlotType.Road)]
    [InlineData(PlotType.Empty)]
    public void ToAddressType_ThrowsForNonBuildingTypes(PlotType plotType)
    {
        Assert.Throws<InvalidOperationException>(() => plotType.ToAddressType());
    }

    [Theory]
    [InlineData(PlotType.SuburbanHome, true)]
    [InlineData(PlotType.ApartmentBuilding, true)]
    [InlineData(PlotType.Office, true)]
    [InlineData(PlotType.Diner, true)]
    [InlineData(PlotType.DiveBar, true)]
    [InlineData(PlotType.Park, true)]
    [InlineData(PlotType.Road, false)]
    [InlineData(PlotType.Empty, false)]
    public void IsBuilding_ReturnsCorrectly(PlotType type, bool expected)
    {
        Assert.Equal(expected, type.IsBuilding());
    }
}

public class CellTests
{
    [Fact]
    public void DefaultCell_HasEmptyType()
    {
        var cell = new Cell();
        Assert.Equal(PlotType.Empty, cell.PlotType);
        Assert.Null(cell.AddressId);
        Assert.Null(cell.StreetId);
    }

    [Fact]
    public void RoadCell_HasStreetId()
    {
        var cell = new Cell { PlotType = PlotType.Road, StreetId = 5 };
        Assert.Equal(PlotType.Road, cell.PlotType);
        Assert.Equal(5, cell.StreetId);
        Assert.Null(cell.AddressId);
    }

    [Fact]
    public void BuildingCell_HasAddressIdAndFacing()
    {
        var cell = new Cell
        {
            PlotType = PlotType.SuburbanHome,
            AddressId = 10,
            FacingDirection = FacingDirection.South
        };
        Assert.Equal(10, cell.AddressId);
        Assert.Equal(FacingDirection.South, cell.FacingDirection);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test stakeout.tests/ --filter "FullyQualifiedName~City" -v minimal`
Expected: Build failure — PlotType, Cell, FacingDirection don't exist yet.

- [ ] **Step 3: Implement PlotType enum**

Create `src/simulation/city/PlotType.cs`:

```csharp
using Stakeout.Simulation.Entities;

namespace Stakeout.Simulation.City;

public enum PlotType
{
    Empty,
    Road,
    SuburbanHome,
    ApartmentBuilding,
    Office,
    Diner,
    DiveBar,
    Park
}

public enum FacingDirection
{
    North,
    South,
    East,
    West
}

public static class PlotTypeExtensions
{
    public static (int Width, int Height) GetSize(this PlotType type) => type switch
    {
        PlotType.ApartmentBuilding => (2, 2),
        PlotType.Office => (2, 2),
        PlotType.Park => (2, 2),
        _ => (1, 1)
    };

    public static bool IsBuilding(this PlotType type) =>
        type != PlotType.Road && type != PlotType.Empty;

    public static AddressType ToAddressType(this PlotType type) => type switch
    {
        PlotType.SuburbanHome => AddressType.SuburbanHome,
        PlotType.ApartmentBuilding => AddressType.ApartmentBuilding,
        PlotType.Office => AddressType.Office,
        PlotType.Diner => AddressType.Diner,
        PlotType.DiveBar => AddressType.DiveBar,
        PlotType.Park => AddressType.Park,
        _ => throw new InvalidOperationException($"{type} is not a building type")
    };
}
```

- [ ] **Step 4: Implement Cell struct**

Create `src/simulation/city/Cell.cs`:

```csharp
namespace Stakeout.Simulation.City;

public struct Cell
{
    public PlotType PlotType { get; set; }
    public int? AddressId { get; set; }
    public int? StreetId { get; set; }
    public FacingDirection FacingDirection { get; set; }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test stakeout.tests/ --filter "FullyQualifiedName~City" -v minimal`
Expected: All tests pass.

- [ ] **Step 6: Commit**

```bash
git add src/simulation/city/PlotType.cs src/simulation/city/Cell.cs stakeout.tests/Simulation/City/CellTests.cs
git commit -m "feat: add PlotType enum, FacingDirection enum, and Cell struct"
```

---

### Task 2: Create CityGrid with lookup methods

**Files:**
- Create: `src/simulation/city/CityGrid.cs`
- Create: `stakeout.tests/Simulation/City/CityGridTests.cs`

- [ ] **Step 1: Write tests for CityGrid**

Create `stakeout.tests/Simulation/City/CityGridTests.cs`:

```csharp
using Stakeout.Simulation.City;
using Xunit;

namespace Stakeout.Tests.Simulation.City;

public class CityGridTests
{
    [Fact]
    public void Constructor_CreatesGridOfCorrectSize()
    {
        var grid = new CityGrid(100, 100);
        Assert.Equal(100, grid.Width);
        Assert.Equal(100, grid.Height);
    }

    [Fact]
    public void GetCell_ReturnsDefaultCellInitially()
    {
        var grid = new CityGrid(10, 10);
        var cell = grid.GetCell(5, 5);
        Assert.Equal(PlotType.Empty, cell.PlotType);
    }

    [Fact]
    public void SetCell_UpdatesGrid()
    {
        var grid = new CityGrid(10, 10);
        grid.SetCell(3, 4, new Cell { PlotType = PlotType.Road, StreetId = 1 });
        var cell = grid.GetCell(3, 4);
        Assert.Equal(PlotType.Road, cell.PlotType);
        Assert.Equal(1, cell.StreetId);
    }

    [Fact]
    public void GetCellsForAddress_ReturnsAllMatchingCells()
    {
        var grid = new CityGrid(10, 10);
        grid.SetCell(2, 2, new Cell { PlotType = PlotType.ApartmentBuilding, AddressId = 5 });
        grid.SetCell(2, 3, new Cell { PlotType = PlotType.ApartmentBuilding, AddressId = 5 });
        grid.SetCell(3, 2, new Cell { PlotType = PlotType.ApartmentBuilding, AddressId = 5 });
        grid.SetCell(3, 3, new Cell { PlotType = PlotType.ApartmentBuilding, AddressId = 5 });

        var cells = grid.GetCellsForAddress(5);
        Assert.Equal(4, cells.Count);
    }

    [Fact]
    public void GetPlotsByType_ReturnsMatchingPositions()
    {
        var grid = new CityGrid(10, 10);
        grid.SetCell(1, 1, new Cell { PlotType = PlotType.SuburbanHome });
        grid.SetCell(3, 5, new Cell { PlotType = PlotType.SuburbanHome });
        grid.SetCell(2, 2, new Cell { PlotType = PlotType.Office });

        var homes = grid.GetPlotsByType(PlotType.SuburbanHome);
        Assert.Equal(2, homes.Count);
    }

    [Fact]
    public void GetUnresolvedAddressIdsByType_ReturnsOnlyThoseWithEmptyInteriors()
    {
        var grid = new CityGrid(10, 10);
        var addresses = new Dictionary<int, Address>
        {
            [10] = new Address { Id = 10, Type = AddressType.SuburbanHome },
            [20] = new Address { Id = 20, Type = AddressType.SuburbanHome }
        };
        // Address 20 has a sublocation (resolved interior)
        addresses[20].Sublocations[1] = new Sublocation { Id = 1 };

        grid.SetCell(1, 1, new Cell { PlotType = PlotType.SuburbanHome, AddressId = 10 });
        grid.SetCell(2, 2, new Cell { PlotType = PlotType.SuburbanHome, AddressId = 20 });

        var unresolved = grid.GetUnresolvedAddressIdsByType(PlotType.SuburbanHome, addresses);
        Assert.Single(unresolved);
        Assert.Equal(10, unresolved[0]);
    }

    [Fact]
    public void IsInBounds_ReturnsTrueForValidCoords()
    {
        var grid = new CityGrid(10, 10);
        Assert.True(grid.IsInBounds(0, 0));
        Assert.True(grid.IsInBounds(9, 9));
        Assert.False(grid.IsInBounds(-1, 0));
        Assert.False(grid.IsInBounds(10, 0));
    }

    [Fact]
    public void FindAdjacentRoad_ReturnsCorrectDirection()
    {
        var grid = new CityGrid(10, 10);
        grid.SetCell(3, 5, new Cell { PlotType = PlotType.Road, StreetId = 1 });
        grid.SetCell(3, 4, new Cell { PlotType = PlotType.SuburbanHome });

        var (direction, streetId) = grid.FindAdjacentRoad(3, 4);
        Assert.Equal(FacingDirection.South, direction);
        Assert.Equal(1, streetId);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test stakeout.tests/ --filter "FullyQualifiedName~CityGridTests" -v minimal`
Expected: Build failure — CityGrid doesn't exist yet.

- [ ] **Step 3: Implement CityGrid**

Create `src/simulation/city/CityGrid.cs`:

```csharp
namespace Stakeout.Simulation.City;

public class CityGrid
{
    private readonly Cell[,] _cells;

    public int Width { get; }
    public int Height { get; }

    public CityGrid(int width, int height)
    {
        Width = width;
        Height = height;
        _cells = new Cell[width, height];
    }

    public Cell GetCell(int x, int y) => _cells[x, y];

    public void SetCell(int x, int y, Cell cell) => _cells[x, y] = cell;

    public bool IsInBounds(int x, int y) =>
        x >= 0 && x < Width && y >= 0 && y < Height;

    public List<(int X, int Y)> GetCellsForAddress(int addressId)
    {
        var result = new List<(int, int)>();
        for (int x = 0; x < Width; x++)
            for (int y = 0; y < Height; y++)
                if (_cells[x, y].AddressId == addressId)
                    result.Add((x, y));
        return result;
    }

    public List<(int X, int Y)> GetPlotsByType(PlotType type)
    {
        var result = new List<(int, int)>();
        for (int x = 0; x < Width; x++)
            for (int y = 0; y < Height; y++)
                if (_cells[x, y].PlotType == type)
                    result.Add((x, y));
        return result;
    }

    /// <summary>
    /// Returns addresses of the given type whose interiors have not been generated yet
    /// (i.e., Sublocations is empty). Used by PersonGenerator to find available homes/workplaces.
    /// </summary>
    public List<int> GetUnresolvedAddressIdsByType(PlotType type, Dictionary<int, Address> addresses)
    {
        var seen = new HashSet<int>();
        var result = new List<int>();
        for (int x = 0; x < Width; x++)
            for (int y = 0; y < Height; y++)
            {
                var cell = _cells[x, y];
                if (cell.PlotType == type && cell.AddressId.HasValue && seen.Add(cell.AddressId.Value))
                {
                    if (addresses.TryGetValue(cell.AddressId.Value, out var addr) && addr.Sublocations.Count == 0)
                        result.Add(cell.AddressId.Value);
                }
            }
        return result;
    }

    public (FacingDirection Direction, int? StreetId) FindAdjacentRoad(int x, int y)
    {
        // Check all four neighbors, return the first road found
        // Priority: South, East, North, West (prefer front-facing)
        (int dx, int dy, FacingDirection dir)[] neighbors =
        {
            (0, 1, FacingDirection.South),
            (1, 0, FacingDirection.East),
            (0, -1, FacingDirection.North),
            (-1, 0, FacingDirection.West)
        };

        foreach (var (dx, dy, dir) in neighbors)
        {
            int nx = x + dx, ny = y + dy;
            if (IsInBounds(nx, ny) && _cells[nx, ny].PlotType == PlotType.Road)
                return (dir, _cells[nx, ny].StreetId);
        }

        return (FacingDirection.South, null);
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test stakeout.tests/ --filter "FullyQualifiedName~CityGridTests" -v minimal`
Expected: All tests pass.

- [ ] **Step 5: Commit**

```bash
git add src/simulation/city/CityGrid.cs stakeout.tests/Simulation/City/CityGridTests.cs
git commit -m "feat: add CityGrid with cell lookup methods"
```

---

### Task 3: Create CityGenerator — road placement and subdivision

This is the first half of the generation pipeline: placing arterial roads and subdividing super-blocks.

**Files:**
- Create: `src/simulation/city/CityGenerator.cs`
- Create: `stakeout.tests/Simulation/City/CityGeneratorTests.cs`

- [ ] **Step 1: Write tests for road placement**

Create `stakeout.tests/Simulation/City/CityGeneratorTests.cs`:

```csharp
using Stakeout.Simulation.City;
using Xunit;

namespace Stakeout.Tests.Simulation.City;

public class CityGeneratorTests
{
    [Fact]
    public void Generate_EdgesAreRoads()
    {
        var state = new SimulationState();
        var generator = new CityGenerator(seed: 42);
        var grid = generator.Generate(state);

        // All edge cells should be roads
        for (int x = 0; x < grid.Width; x++)
        {
            Assert.Equal(PlotType.Road, grid.GetCell(x, 0).PlotType);
            Assert.Equal(PlotType.Road, grid.GetCell(x, grid.Height - 1).PlotType);
        }
        for (int y = 0; y < grid.Height; y++)
        {
            Assert.Equal(PlotType.Road, grid.GetCell(0, y).PlotType);
            Assert.Equal(PlotType.Road, grid.GetCell(grid.Width - 1, y).PlotType);
        }
    }

    [Fact]
    public void Generate_HasArterialRoads()
    {
        var state = new SimulationState();
        var generator = new CityGenerator(seed: 42);
        var grid = generator.Generate(state);

        // There should be interior horizontal and vertical roads (not just edges)
        int interiorHorizontalRoadRows = 0;
        for (int y = 1; y < grid.Height - 1; y++)
        {
            bool fullRow = true;
            for (int x = 0; x < grid.Width; x++)
            {
                if (grid.GetCell(x, y).PlotType != PlotType.Road)
                {
                    fullRow = false;
                    break;
                }
            }
            if (fullRow) interiorHorizontalRoadRows++;
        }
        Assert.True(interiorHorizontalRoadRows >= 5, $"Expected >=5 arterial rows, got {interiorHorizontalRoadRows}");
    }

    [Fact]
    public void Generate_AllRoadsConnected()
    {
        var state = new SimulationState();
        var generator = new CityGenerator(seed: 42);
        var grid = generator.Generate(state);

        // Find all road cells
        var roadCells = grid.GetPlotsByType(PlotType.Road);
        Assert.NotEmpty(roadCells);

        // BFS from first road cell — all road cells should be reachable
        var visited = new HashSet<(int, int)>();
        var queue = new Queue<(int, int)>();
        queue.Enqueue(roadCells[0]);
        visited.Add(roadCells[0]);

        while (queue.Count > 0)
        {
            var (x, y) = queue.Dequeue();
            foreach (var (dx, dy) in new[] { (0, 1), (0, -1), (1, 0), (-1, 0) })
            {
                int nx = x + dx, ny = y + dy;
                if (grid.IsInBounds(nx, ny) &&
                    grid.GetCell(nx, ny).PlotType == PlotType.Road &&
                    visited.Add((nx, ny)))
                {
                    queue.Enqueue((nx, ny));
                }
            }
        }

        Assert.Equal(roadCells.Count, visited.Count);
    }

    [Fact]
    public void Generate_SameSeedProducesSameGrid()
    {
        var grid1 = new CityGenerator(seed: 123).Generate(new SimulationState());
        var grid2 = new CityGenerator(seed: 123).Generate(new SimulationState());

        for (int x = 0; x < grid1.Width; x++)
            for (int y = 0; y < grid1.Height; y++)
                Assert.Equal(grid1.GetCell(x, y).PlotType, grid2.GetCell(x, y).PlotType);
    }

    [Fact]
    public void Generate_UrbanCenterHasSmallerBlocks()
    {
        var state = new SimulationState();
        var generator = new CityGenerator(seed: 42);
        var grid = generator.Generate(state);

        // Count road density in center vs edge
        int centerRoads = 0, edgeRoads = 0;
        int centerTotal = 0, edgeTotal = 0;

        for (int x = 0; x < grid.Width; x++)
        {
            for (int y = 0; y < grid.Height; y++)
            {
                bool isCenter = x >= 30 && x < 70 && y >= 30 && y < 70;
                if (isCenter)
                {
                    centerTotal++;
                    if (grid.GetCell(x, y).PlotType == PlotType.Road) centerRoads++;
                }
                else
                {
                    edgeTotal++;
                    if (grid.GetCell(x, y).PlotType == PlotType.Road) edgeRoads++;
                }
            }
        }

        float centerDensity = (float)centerRoads / centerTotal;
        float edgeDensity = (float)edgeRoads / edgeTotal;
        Assert.True(centerDensity > edgeDensity,
            $"Center road density ({centerDensity:F3}) should exceed edge ({edgeDensity:F3})");
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test stakeout.tests/ --filter "FullyQualifiedName~CityGeneratorTests" -v minimal`
Expected: Build failure — CityGenerator doesn't exist.

- [ ] **Step 3: Implement CityGenerator — road placement and subdivision**

Create `src/simulation/city/CityGenerator.cs`:

```csharp
namespace Stakeout.Simulation.City;

public class CityGenerator
{
    private readonly Random _rng;
    private const int GridSize = 100;
    private const int ArterialSpacing = 10;
    private const int ArterialVariance = 2;

    public CityGenerator(int seed)
    {
        _rng = new Random(seed);
    }

    public CityGrid Generate(SimulationState state)
    {
        var grid = new CityGrid(GridSize, GridSize);
        PlaceArterialRoads(grid);
        SubdivideSuperBlocks(grid);
        return grid;
    }

    private void PlaceArterialRoads(CityGrid grid)
    {
        // Edge roads
        for (int x = 0; x < grid.Width; x++)
        {
            grid.SetCell(x, 0, new Cell { PlotType = PlotType.Road });
            grid.SetCell(x, grid.Height - 1, new Cell { PlotType = PlotType.Road });
        }
        for (int y = 0; y < grid.Height; y++)
        {
            grid.SetCell(0, y, new Cell { PlotType = PlotType.Road });
            grid.SetCell(grid.Width - 1, y, new Cell { PlotType = PlotType.Road });
        }

        // Interior horizontal arterials
        _horizontalArterials = new List<int> { 0, grid.Height - 1 };
        int y_pos = ArterialSpacing;
        while (y_pos < grid.Height - ArterialSpacing)
        {
            int actual = y_pos + _rng.Next(-ArterialVariance, ArterialVariance + 1);
            actual = Math.Clamp(actual, 1, grid.Height - 2);
            for (int x = 0; x < grid.Width; x++)
                grid.SetCell(x, actual, new Cell { PlotType = PlotType.Road });
            _horizontalArterials.Add(actual);
            y_pos += ArterialSpacing;
        }
        _horizontalArterials.Sort();

        // Interior vertical arterials
        _verticalArterials = new List<int> { 0, grid.Width - 1 };
        int x_pos = ArterialSpacing;
        while (x_pos < grid.Width - ArterialSpacing)
        {
            int actual = x_pos + _rng.Next(-ArterialVariance, ArterialVariance + 1);
            actual = Math.Clamp(actual, 1, grid.Width - 2);
            for (int y = 0; y < grid.Height; y++)
                grid.SetCell(actual, y, new Cell { PlotType = PlotType.Road });
            _verticalArterials.Add(actual);
            x_pos += ArterialSpacing;
        }
        _verticalArterials.Sort();
    }

    private List<int> _horizontalArterials = new();
    private List<int> _verticalArterials = new();

    private void SubdivideSuperBlocks(CityGrid grid)
    {
        // For each super-block (region between adjacent arterials), subdivide based on urbanness
        for (int i = 0; i < _verticalArterials.Count - 1; i++)
        {
            for (int j = 0; j < _horizontalArterials.Count - 1; j++)
            {
                int left = _verticalArterials[i] + 1;
                int right = _verticalArterials[i + 1] - 1;
                int top = _horizontalArterials[j] + 1;
                int bottom = _horizontalArterials[j + 1] - 1;

                if (left > right || top > bottom) continue;

                float centerX = (left + right) / 2f;
                float centerY = (top + bottom) / 2f;
                float urbanness = ComputeUrbanness(centerX, centerY);

                SubdivideBlock(grid, left, right, top, bottom, urbanness);
            }
        }
    }

    private void SubdivideBlock(CityGrid grid, int left, int right, int top, int bottom, float urbanness)
    {
        int blockWidth = right - left + 1;
        int blockHeight = bottom - top + 1;

        // Urban blocks get more subdivisions
        int hDivisions = 0, vDivisions = 0;
        if (urbanness > 0.7f)
        {
            hDivisions = blockWidth > 8 ? _rng.Next(1, 3) : (blockWidth > 5 ? 1 : 0);
            vDivisions = blockHeight > 8 ? _rng.Next(1, 3) : (blockHeight > 5 ? 1 : 0);
        }
        else if (urbanness > 0.3f)
        {
            hDivisions = blockWidth > 10 ? 1 : 0;
            vDivisions = blockHeight > 10 ? 1 : 0;
        }
        // Suburban: no subdivision

        // Place vertical secondary roads
        if (hDivisions > 0)
        {
            int step = blockWidth / (hDivisions + 1);
            for (int d = 1; d <= hDivisions; d++)
            {
                int roadX = left + step * d;
                if (roadX >= left && roadX <= right)
                {
                    for (int y = top; y <= bottom; y++)
                        grid.SetCell(roadX, y, new Cell { PlotType = PlotType.Road });
                }
            }
        }

        // Place horizontal secondary roads
        if (vDivisions > 0)
        {
            int step = blockHeight / (vDivisions + 1);
            for (int d = 1; d <= vDivisions; d++)
            {
                int roadY = top + step * d;
                if (roadY >= top && roadY <= bottom)
                {
                    for (int x = left; x <= right; x++)
                        grid.SetCell(x, roadY, new Cell { PlotType = PlotType.Road });
                }
            }
        }
    }

    private float ComputeUrbanness(float x, float y)
    {
        float centerX = GridSize / 2f;
        float centerY = GridSize / 2f;
        float maxDist = new System.Numerics.Vector2(centerX, centerY).Length();
        float dist = new System.Numerics.Vector2(x - centerX, y - centerY).Length();
        return 1.0f - Math.Clamp(dist / maxDist, 0f, 1f);
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test stakeout.tests/ --filter "FullyQualifiedName~CityGeneratorTests" -v minimal`
Expected: All tests pass.

- [ ] **Step 5: Commit**

```bash
git add src/simulation/city/CityGenerator.cs stakeout.tests/Simulation/City/CityGeneratorTests.cs
git commit -m "feat: add CityGenerator with arterial roads and block subdivision"
```

---

### Task 4: CityGenerator — street naming

Add street name assignment to the generation pipeline. Each continuous run of road cells in a line gets a StreetId and name.

**Files:**
- Modify: `src/simulation/city/CityGenerator.cs`
- Modify: `src/simulation/entities/Street.cs`
- Modify: `stakeout.tests/Simulation/City/CityGeneratorTests.cs`

- [ ] **Step 1: Write tests for street naming**

Add to `stakeout.tests/Simulation/City/CityGeneratorTests.cs`:

```csharp
[Fact]
public void Generate_AllRoadCellsHaveStreetIds()
{
    var state = new SimulationState();
    var generator = new CityGenerator(seed: 42);
    var grid = generator.Generate(state);

    var roadCells = grid.GetPlotsByType(PlotType.Road);
    foreach (var (x, y) in roadCells)
    {
        var cell = grid.GetCell(x, y);
        Assert.NotNull(cell.StreetId);
    }
}

[Fact]
public void Generate_StreetsHaveNames()
{
    var state = new SimulationState();
    var generator = new CityGenerator(seed: 42);
    generator.Generate(state);

    Assert.NotEmpty(state.Streets);
    foreach (var street in state.Streets.Values)
    {
        Assert.False(string.IsNullOrEmpty(street.Name));
    }
}

[Fact]
public void Generate_ArterialStreetsHaveGrandNames()
{
    var state = new SimulationState();
    var generator = new CityGenerator(seed: 42);
    generator.Generate(state);

    // At least some streets should have Boulevard or Avenue
    var names = state.Streets.Values.Select(s => s.Name).ToList();
    Assert.Contains(names, n => n.Contains("Boulevard") || n.Contains("Avenue"));
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test stakeout.tests/ --filter "FullyQualifiedName~CityGeneratorTests" -v minimal`
Expected: Fail — `Generate` doesn't accept `SimulationState` yet.

- [ ] **Step 3: Update Street entity to track road coordinates**

Modify `src/simulation/entities/Street.cs`:

```csharp
using Godot;

namespace Stakeout.Simulation.Entities;

public class Street
{
    public int Id { get; set; }
    public string Name { get; set; }
    public int CityId { get; set; }
    public List<Vector2I> RoadCells { get; set; } = new();
}
```

- [ ] **Step 4: Update CityGenerator to accept SimulationState and assign street names**

Modify `src/simulation/city/CityGenerator.cs` — update `Generate` to accept `SimulationState`, and add street assignment after road placement. The `Generate` method should:

1. Place arterial roads (existing)
2. Subdivide super-blocks (existing)
3. **NEW:** Assign street names — scan each row for horizontal road runs and each column for vertical road runs. Each continuous run becomes a street. Arterial roads (those in `_horizontalArterials`/`_verticalArterials`) get suffixes like "Boulevard" and "Avenue". Secondary roads get "Street", "Lane", "Drive", etc. Use `StreetData.StreetNames` and `StreetData.StreetSuffixes` for name generation (same source as `LocationGenerator`). Create `Street` entities in `SimulationState` and set `StreetId` on each road cell.

Key implementation detail: a full-span arterial is one street. A secondary road within a super-block is a separate street. Streets along the same physical line but separated by perpendicular roads are the same street (they share the same `StreetId`).

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test stakeout.tests/ --filter "FullyQualifiedName~CityGeneratorTests" -v minimal`
Expected: All tests pass.

- [ ] **Step 6: Commit**

```bash
git add src/simulation/city/CityGenerator.cs src/simulation/entities/Street.cs stakeout.tests/Simulation/City/CityGeneratorTests.cs
git commit -m "feat: assign street names to road cells during city generation"
```

---

### Task 5: CityGenerator — plot type assignment

Fill city blocks with building types using the urbanness-weighted distribution.

**Files:**
- Modify: `src/simulation/city/CityGenerator.cs`
- Modify: `stakeout.tests/Simulation/City/CityGeneratorTests.cs`

- [ ] **Step 1: Write tests for plot assignment**

Add to `stakeout.tests/Simulation/City/CityGeneratorTests.cs`:

```csharp
[Fact]
public void Generate_HasBuildingPlots()
{
    var state = new SimulationState();
    var grid = new CityGenerator(seed: 42).Generate(state);

    int buildings = 0;
    for (int x = 0; x < grid.Width; x++)
        for (int y = 0; y < grid.Height; y++)
            if (grid.GetCell(x, y).PlotType.IsBuilding())
                buildings++;

    Assert.True(buildings > 100, $"Expected >100 building plots, got {buildings}");
}

[Fact]
public void Generate_UrbanCenterHasMoreApartmentsAndOffices()
{
    var state = new SimulationState();
    var grid = new CityGenerator(seed: 42).Generate(state);

    int centerUrban = 0, edgeUrban = 0;
    int centerTotal = 0, edgeTotal = 0;

    for (int x = 0; x < grid.Width; x++)
    {
        for (int y = 0; y < grid.Height; y++)
        {
            var type = grid.GetCell(x, y).PlotType;
            if (type == PlotType.Road) continue;

            bool isCenter = x >= 30 && x < 70 && y >= 30 && y < 70;
            bool isUrbanType = type == PlotType.ApartmentBuilding || type == PlotType.Office;

            if (isCenter) { centerTotal++; if (isUrbanType) centerUrban++; }
            else { edgeTotal++; if (isUrbanType) edgeUrban++; }
        }
    }

    float centerRatio = centerTotal > 0 ? (float)centerUrban / centerTotal : 0;
    float edgeRatio = edgeTotal > 0 ? (float)edgeUrban / edgeTotal : 0;
    Assert.True(centerRatio > edgeRatio,
        $"Center urban ratio ({centerRatio:F3}) should exceed edge ({edgeRatio:F3})");
}

[Fact]
public void Generate_MultiPlotBuildingsOccupy2x2()
{
    var state = new SimulationState();
    var grid = new CityGenerator(seed: 42).Generate(state);

    // Find an apartment building cell — all 4 cells in the 2x2 should be ApartmentBuilding
    for (int x = 0; x < grid.Width - 1; x++)
    {
        for (int y = 0; y < grid.Height - 1; y++)
        {
            var cell = grid.GetCell(x, y);
            if (cell.PlotType == PlotType.ApartmentBuilding)
            {
                // Check that adjacent cells in 2x2 are the same type
                Assert.Equal(PlotType.ApartmentBuilding, grid.GetCell(x + 1, y).PlotType);
                Assert.Equal(PlotType.ApartmentBuilding, grid.GetCell(x, y + 1).PlotType);
                Assert.Equal(PlotType.ApartmentBuilding, grid.GetCell(x + 1, y + 1).PlotType);
                return; // Found and verified one, that's enough
            }
        }
    }
    Assert.Fail("No 2x2 apartment building found in generated city");
}

[Fact]
public void Generate_NoBuildingsOnRoads()
{
    var state = new SimulationState();
    var grid = new CityGenerator(seed: 42).Generate(state);

    var roads = grid.GetPlotsByType(PlotType.Road);
    foreach (var (x, y) in roads)
    {
        Assert.Equal(PlotType.Road, grid.GetCell(x, y).PlotType);
        Assert.Null(grid.GetCell(x, y).AddressId);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test stakeout.tests/ --filter "FullyQualifiedName~CityGeneratorTests" -v minimal`
Expected: Fail — no building placement yet.

- [ ] **Step 3: Implement plot type assignment in CityGenerator**

Add to `src/simulation/city/CityGenerator.cs` a new step after street naming that:

1. Identifies all rectangular city blocks (contiguous non-road rectangles bounded by roads)
2. For each block, computes urbanness from its center position
3. Interpolates between urban and suburban weight tables:

```
PlotType         | Urban | Suburban
ApartmentBuilding|  30   |    5
Office           |  25   |    5
SuburbanHome     |   5   |   40
Diner            |   5   |    5
DiveBar          |   5   |    3
Park             |  10   |   10
Empty            |   5   |   20
```

4. Places 2x2 buildings first (scan block for 2x2 openings, weighted random pick). Sets all 4 cells of a 2x2 building to the same PlotType.
5. Fills remaining 1x1 cells with 1x1 types (weighted random pick)
6. AddressIds are NOT assigned yet — that happens in Task 6 when Address entities are created.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test stakeout.tests/ --filter "FullyQualifiedName~CityGeneratorTests" -v minimal`
Expected: All tests pass.

- [ ] **Step 5: Commit**

```bash
git add src/simulation/city/CityGenerator.cs stakeout.tests/Simulation/City/CityGeneratorTests.cs
git commit -m "feat: fill city blocks with weighted building types"
```

---

### Task 6: CityGenerator — facing directions and Address creation

Resolve facing directions for all building plots, then create lightweight Address entities.

**Files:**
- Modify: `src/simulation/city/CityGenerator.cs`
- Modify: `src/simulation/entities/Address.cs`
- Modify: `stakeout.tests/Simulation/City/CityGeneratorTests.cs`
- Modify: `stakeout.tests/Simulation/Entities/AddressTests.cs` (if exists, update for GridX/GridY)

- [ ] **Step 1: Write tests for facing directions and address creation**

Add to `stakeout.tests/Simulation/City/CityGeneratorTests.cs`:

```csharp
[Fact]
public void Generate_BuildingAnchorCellsFaceARoad()
{
    var state = new SimulationState();
    var grid = new CityGenerator(seed: 42).Generate(state);

    // Check facing direction only on anchor cells (top-left of each building).
    // Multi-plot buildings share an AddressId; we only check the cell matching
    // the address's GridX/GridY (the anchor).
    var checkedAddresses = new HashSet<int>();

    foreach (var address in state.Addresses.Values)
    {
        if (!checkedAddresses.Add(address.Id)) continue;

        int x = address.GridX, y = address.GridY;
        var cell = grid.GetCell(x, y);

        // Find any cell of this building that borders a road in the facing direction
        var cells = grid.GetCellsForAddress(address.Id);
        bool anyFacesRoad = cells.Any(pos =>
        {
            var c = grid.GetCell(pos.X, pos.Y);
            var (dx, dy) = c.FacingDirection switch
            {
                FacingDirection.North => (0, -1),
                FacingDirection.South => (0, 1),
                FacingDirection.East => (1, 0),
                FacingDirection.West => (-1, 0),
                _ => (0, 0)
            };
            int nx = pos.X + dx, ny = pos.Y + dy;
            return grid.IsInBounds(nx, ny) &&
                   grid.GetCell(nx, ny).PlotType == PlotType.Road;
        });

        Assert.True(anyFacesRoad,
            $"Address {address.Id} at ({x},{y}) has no cell facing a road");
    }
}

[Fact]
public void Generate_CreatesAddressesInSimulationState()
{
    var state = new SimulationState();
    var grid = new CityGenerator(seed: 42).Generate(state);

    Assert.NotEmpty(state.Addresses);
    foreach (var address in state.Addresses.Values)
    {
        Assert.True(address.GridX >= 0 && address.GridX < 100);
        Assert.True(address.GridY >= 0 && address.GridY < 100);
        Assert.True(address.Number > 0);
        Assert.True(address.StreetId > 0);
    }
}

[Fact]
public void Generate_AddressStreetNumbersIncreaseAlongStreet()
{
    var state = new SimulationState();
    var grid = new CityGenerator(seed: 42).Generate(state);

    // Group addresses by street, check numbers are non-decreasing when sorted by position
    var byStreet = state.Addresses.Values.GroupBy(a => a.StreetId);
    foreach (var group in byStreet)
    {
        var sorted = group.OrderBy(a => a.GridX).ThenBy(a => a.GridY).ToList();
        for (int i = 1; i < sorted.Count; i++)
        {
            Assert.True(sorted[i].Number >= sorted[i - 1].Number,
                $"Street {group.Key}: address numbers should increase along street");
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test stakeout.tests/ --filter "FullyQualifiedName~CityGeneratorTests" -v minimal`
Expected: Fail — Address doesn't have GridX/GridY yet.

- [ ] **Step 3: Modify Address entity**

Update `src/simulation/entities/Address.cs`:

```csharp
using Godot;

namespace Stakeout.Simulation.Entities;

public class Address
{
    public int Id { get; set; }
    public int Number { get; set; }
    public int StreetId { get; set; }
    public AddressType Type { get; set; }
    public AddressCategory Category => Type.GetCategory();
    public int GridX { get; set; }
    public int GridY { get; set; }
    public const int CellSize = 48;
    public Vector2 Position => new Vector2(GridX * CellSize, GridY * CellSize);
    public Dictionary<int, Sublocation> Sublocations { get; } = new();
    public List<SublocationConnection> Connections { get; } = new();
}
```

Note: `Position` becomes a computed property from grid coords. This preserves backward compatibility with code that reads `Position` (like travel interpolation) while the source of truth moves to grid coords.

- [ ] **Step 4: Implement facing direction resolution and address creation in CityGenerator**

Add to `src/simulation/city/CityGenerator.cs` — after plot type assignment:

1. For each building cell, call `grid.FindAdjacentRoad()` to determine facing direction and connected street
2. Set `FacingDirection` on the cell
3. For each unique building (1x1 plots, or the top-left cell of 2x2 groups), create an `Address` entity in `SimulationState` with:
   - Type mapped from PlotType via `ToAddressType()`
   - GridX, GridY from the top-left cell position
   - StreetId from the faced road
   - Number derived from position along that street (count buildings on this street sorted by position, multiply by 2 for even/odd spacing)
4. Set `AddressId` on all cells belonging to that address

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test stakeout.tests/ --filter "FullyQualifiedName~CityGeneratorTests" -v minimal`
Expected: All tests pass.

- [ ] **Step 6: Commit**

```bash
git add src/simulation/city/CityGenerator.cs src/simulation/entities/Address.cs stakeout.tests/Simulation/City/CityGeneratorTests.cs
git commit -m "feat: resolve facing directions and create Address entities during generation"
```

---

### Task 7: Update MapConfig and SimulationState for grid

**Files:**
- Modify: `src/simulation/MapConfig.cs`
- Modify: `src/simulation/SimulationState.cs`
- Modify: `stakeout.tests/Simulation/MapConfigTests.cs`

- [ ] **Step 1: Write tests for updated MapConfig**

Update `stakeout.tests/Simulation/MapConfigTests.cs`. The new `MapConfig` should use grid-based dimensions:

```csharp
[Fact]
public void GridDimensions_AreCorrect()
{
    var config = new MapConfig();
    Assert.Equal(100, config.GridWidth);
    Assert.Equal(100, config.GridHeight);
    Assert.Equal(48, config.CellSize);
    Assert.Equal(4800f, config.MapWidth);
    Assert.Equal(4800f, config.MapHeight);
}

[Fact]
public void ComputeTravelTimeHours_UsesGridPositions()
{
    var config = new MapConfig();
    // Opposite corners of the grid
    var from = new Vector2(0, 0);
    var to = new Vector2(config.MapWidth, config.MapHeight);
    var hours = config.ComputeTravelTimeHours(from, to);
    Assert.InRange(hours, 0.9f, 1.1f); // Should be close to MaxTravelTimeHours
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test stakeout.tests/ --filter "FullyQualifiedName~MapConfig" -v minimal`
Expected: Fail — MapConfig doesn't have GridWidth etc.

- [ ] **Step 3: Update MapConfig**

Update `src/simulation/MapConfig.cs`:

```csharp
using Godot;

namespace Stakeout.Simulation;

public class MapConfig
{
    public int GridWidth { get; } = 100;
    public int GridHeight { get; } = 100;
    public int CellSize { get; } = 48;
    public float MapWidth => GridWidth * CellSize;
    public float MapHeight => GridHeight * CellSize;
    public float MaxTravelTimeHours { get; } = 1.0f;

    public float MapDiagonal => new Vector2(MapWidth, MapHeight).Length();

    public float ComputeTravelTimeHours(Vector2 from, Vector2 to)
    {
        var distance = from.DistanceTo(to);
        return distance / MapDiagonal * MaxTravelTimeHours;
    }
}
```

- [ ] **Step 4: Add CityGrid to SimulationState**

Update `src/simulation/SimulationState.cs` — add:

```csharp
using Stakeout.Simulation.City;
// ...
public CityGrid CityGrid { get; set; }
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test stakeout.tests/ --filter "FullyQualifiedName~MapConfig" -v minimal`
Expected: All tests pass.

- [ ] **Step 6: Fix any other tests broken by MapConfig/Address changes**

Run: `dotnet test stakeout.tests/ -v minimal`
Expected: Some existing tests may fail because they reference old `MapConfig.MinX`/`MaxX` etc. or set `Address.Position` directly. Fix these by:
- Updating `LocationGeneratorTests` to use grid-based assertions
- Updating any test that sets `Address.Position` to set `GridX`/`GridY` instead
- Any test referencing `MapConfig.MinX` etc. should use the new properties

- [ ] **Step 7: Commit**

```bash
git add src/simulation/MapConfig.cs src/simulation/SimulationState.cs stakeout.tests/
git commit -m "feat: update MapConfig for grid-based dimensions, add CityGrid to SimulationState"
```

---

### Task 8: Integrate CityGenerator into SimulationManager

Replace `LocationGenerator` usage with `CityGenerator` in the game startup.

**Files:**
- Modify: `src/simulation/SimulationManager.cs`
- Modify: `src/simulation/PersonGenerator.cs`
- Modify: `src/simulation/LocationGenerator.cs`

- [ ] **Step 1: Update SimulationManager._Ready()**

In `src/simulation/SimulationManager.cs`, replace the `LocationGenerator` usage in `_Ready()`:

Current flow (lines 39-75):
1. `_locationGenerator.GenerateCityScaffolding(State)` — creates country + city
2. `_locationGenerator.GenerateAddress(State, AddressType.Park)` — creates 1 park
3. `_personGenerator.GeneratePerson(State, _locationGenerator)` — creates 5 people (each calling GenerateAddress for home/work)
4. Creates player

New flow:
1. `_locationGenerator.GenerateCityScaffolding(State)` — keep this (creates country + city entities)
2. `var cityGenerator = new CityGenerator(seed)` — create city generator
3. `State.CityGrid = cityGenerator.Generate(State)` — generate the grid, streets, and addresses
4. `_personGenerator.GeneratePerson(State)` — person generator picks plots from CityGrid
5. Creates player (picks from available residential addresses)

- [ ] **Step 2: Update PersonGenerator to pick plots from CityGrid**

In `src/simulation/PersonGenerator.cs`, modify the person generation to:
- Instead of calling `LocationGenerator.GenerateAddress()` for home/work, query `State.CityGrid.GetUnresolvedAddressIdsByType(type, State.Addresses)` to find available addresses with empty interiors
- Pick a random available address from the returned list
- Generate the interior for that address by calling `SublocationGeneratorRegistry.Get(addressType).Generate(address, state)` — this is the same sublocation generation that `LocationGenerator.GenerateAddress()` currently calls, just invoked separately

**Lazy generation wiring:**
- Extract the sublocation generation call from `LocationGenerator.GenerateAddress()` into a standalone method: `ResolveAddressInterior(Address address, SimulationState state)` on `LocationGenerator` (or a new `AddressResolver` utility class). This method calls `SublocationGeneratorRegistry.Get(address.Type).Generate(address, state)`.
- `PersonGenerator` calls this when claiming an address for a person's home or workplace.
- In Task 9 (CityView), the "Enter building" action also calls this method before navigating to AddressView, ensuring the interior is generated when the player enters.
- The check is simple: `if (address.Sublocations.Count == 0) ResolveAddressInterior(address, state)`

- [ ] **Step 3: Update travel methods to use grid-derived positions**

In `src/simulation/SimulationManager.cs`, `StartPlayerTravel()` currently reads `Address.Position` — this now returns grid-derived pixel coords automatically (since we made `Position` a computed property in Task 6). Verify that `StartPlayerTravel` and `UpdatePlayerTravel` still work without changes. Same for `PersonBehavior.StartTravel()`.

- [ ] **Step 4: Run all tests**

Run: `dotnet test stakeout.tests/ -v minimal`
Expected: All tests pass. Fix any remaining failures.

- [ ] **Step 5: Commit**

```bash
git add src/simulation/SimulationManager.cs src/simulation/PersonGenerator.cs src/simulation/LocationGenerator.cs
git commit -m "feat: integrate CityGenerator into SimulationManager startup"
```

---

### Task 9: Rewrite CityView — grid rendering with pan/zoom

Replace the current point-cloud rendering with grid-based rendering and evidence-board-style pan/zoom.

**Files:**
- Rewrite: `scenes/city/CityView.cs`
- Modify: `scenes/city/CityView.tscn`

- [ ] **Step 1: Add pan/zoom infrastructure to CityView**

Rewrite `scenes/city/CityView.cs` with the viewport system. Model pan/zoom exactly like `EvidenceBoardScene.cs` (lines 116-196):

```csharp
// Fields
private Vector2 _panOffset = Vector2.Zero;
private float _zoom = 1.0f;
private bool _isPanning;
private Vector2 _panStartMouse;
private Vector2 _panStartOffset;
private const float MinZoom = 0.25f;
private const float MaxZoom = 2.0f;
private const float ZoomStep = 0.1f;
```

Input handling:
- Left-click drag on empty space (no plot hit) → pan
- Middle-click drag → pan
- Mouse wheel → zoom toward cursor
- `ApplyTransform()` sets `_cityMapNode.Position = _panOffset` and `_cityMapNode.Scale = new Vector2(_zoom, _zoom)`

Center the viewport on the middle of the grid at startup: `_panOffset = (viewportSize - gridPixelSize * _zoom) / 2`

- [ ] **Step 2: Implement grid rendering**

Replace icon-based rendering with grid drawing. In `_Draw()` or via child nodes:

For each visible cell (cull using viewport bounds / zoom):
- **Road**: light gray rect (48x48), `new Color(0.6f, 0.6f, 0.6f)`
- **Building** (all types): dark gray rect smaller than cell (e.g., 40x40 centered), `new Color(0.33f, 0.33f, 0.33f)`. For 2x2 buildings, draw one large rect spanning 88x88 (accounting for the gap).
- **Park**: green rect with 2-3 small dark green circles for trees, `new Color(0.29f, 0.54f, 0.2f)`
- **Empty**: green background (the default), `new Color(0.23f, 0.42f, 0.14f)`
- **Driveways**: small gray strip (8x4) on the facing-direction edge of building plots

Street names: draw text along road cells using `DrawString()`. For horizontal roads, draw every ~8 cells. For vertical roads, draw rotated text every ~8 cells. Skip if another label would overlap (track label positions and check distance).

- [ ] **Step 3: Implement highlights**

Three highlight states, drawn as colored overlays or replacement fills:

- **Player location**: blue fill `new Color(0.23f, 0.48f, 0.8f)` — look up player's `CurrentAddressId`, find all cells for that address, draw them blue
- **Evidence board addresses**: red fill `new Color(0.54f, 0.2f, 0.2f)` — iterate evidence board items that reference addresses, draw those cells red
- **Selected building**: white outline (2px) around all cells of the selected address

Priority: selection outline draws on top of blue/red fills.

- [ ] **Step 4: Implement entity dots**

Draw person/player dots on top of grid, same as current but at grid-derived positions:
- People: 8x8 circles at `person.CurrentPosition` (which is pixel-space, interpolated during travel)
- Player: 8x8 blue circle at `player.CurrentPosition`
- Colors: white (active), gray (sleeping), red (dead), blue (player)

- [ ] **Step 5: Implement plot selection and sidebar integration**

Left-click hit detection:
1. Convert mouse position to grid coordinates: `gridX = (int)((mousePos.X - _panOffset.X) / _zoom / 48)`, same for Y
2. Look up cell at (gridX, gridY)
3. If cell has AddressId, select that address (store `_selectedAddressId`)
4. For multi-plot buildings, the AddressId is shared — all cells highlight
5. If cell is road/empty/out-of-bounds, deselect

Sidebar integration:
- When an address is selected, show its info at the top of the sidebar: `"{Number} {StreetName} ({Type})"`
- Show contextual actions:
  - "Go here" if `player.CurrentAddressId != selectedAddressId`
  - "Enter building" if `player.CurrentAddressId == selectedAddressId`
- Wire "Go here" to `SimulationManager.StartPlayerTravel()` (same as current)
- Wire "Enter building" to navigate to AddressView (same as current "Enter" behavior)

- [ ] **Step 6: Update CityView.tscn**

Update the scene to match new node structure. The `CityMap` control node should be the transformable container (like `CorkboardCanvas` in evidence board). Remove `LocationIcons` and `EntityDots` child nodes — rendering is now done in `_Draw()` or with a single drawing node.

- [ ] **Step 7: Run the game and verify visually**

Run: `dotnet build stakeout.sln` to verify compilation.
Then manually launch and verify:
- City grid renders with roads, buildings, parks
- Pan and zoom work smoothly
- Street names visible, not overlapping
- Clicking selects buildings, sidebar shows address
- Player dot visible, travel works
- Blue/red highlights work

- [ ] **Step 8: Commit**

```bash
git add scenes/city/CityView.cs scenes/city/CityView.tscn
git commit -m "feat: rewrite CityView with grid rendering, pan/zoom, selection, and highlights"
```

---

### Task 10: Update existing tests and clean up

Fix any remaining test failures from the refactor and clean up deprecated code.

**Files:**
- Modify: `stakeout.tests/Simulation/LocationGeneratorTests.cs`
- Modify: `stakeout.tests/Simulation/PlayerTravelTests.cs` (if exists)
- Modify: Various test files that reference `Address.Position` setter

- [ ] **Step 1: Run full test suite**

Run: `dotnet test stakeout.tests/ -v minimal`
Note all failures.

- [ ] **Step 2: Fix LocationGeneratorTests**

The old `LocationGeneratorTests` tested random position generation within map bounds. These tests should either:
- Be removed (if LocationGenerator no longer generates positions)
- Be updated to test whatever remains of LocationGenerator (city scaffolding, street name utilities)
- Be replaced by the new `CityGeneratorTests`

- [ ] **Step 3: Fix any tests that set Address.Position directly**

Search for `address.Position =` or `Position = new Vector2` in test files. Replace with `GridX = ..., GridY = ...`. The computed `Position` property will return the correct pixel coords.

- [ ] **Step 4: Fix travel-related tests**

Any tests that create `TravelInfo` with pixel positions should still work since `Address.Position` returns pixel coords. But verify. If tests create addresses with specific positions, they need to set grid coords instead.

- [ ] **Step 5: Run full test suite again**

Run: `dotnet test stakeout.tests/ -v minimal`
Expected: All tests pass.

- [ ] **Step 6: Clean up LocationGenerator**

Remove position-generation code from `LocationGenerator`. Keep `GenerateCityScaffolding()` and any street name utility methods that `CityGenerator` reuses. If `CityGenerator` has fully absorbed all functionality, `LocationGenerator` can be deleted entirely — but verify no other code references it first.

- [ ] **Step 7: Commit**

```bash
git add stakeout.tests/ src/simulation/LocationGenerator.cs
git commit -m "fix: update existing tests for grid-based city map, clean up LocationGenerator"
```

---

### Task 11: Final integration test

End-to-end verification that the full simulation works with the new city map.

**Files:**
- Create: `stakeout.tests/Simulation/City/CityIntegrationTests.cs`

- [ ] **Step 1: Write integration test**

Create `stakeout.tests/Simulation/City/CityIntegrationTests.cs`:

```csharp
using Stakeout.Simulation;
using Stakeout.Simulation.City;
using Stakeout.Simulation.Entities;
using Xunit;

namespace Stakeout.Tests.Simulation.City;

public class CityIntegrationTests
{
    [Fact]
    public void FullCityGeneration_ProducesValidState()
    {
        var state = new SimulationState();
        var mapConfig = new MapConfig();

        // Generate city scaffolding
        var locationGen = new LocationGenerator(mapConfig);
        locationGen.GenerateCityScaffolding(state);

        // Generate city grid
        var cityGen = new CityGenerator(seed: 42);
        state.CityGrid = cityGen.Generate(state);

        // Verify grid exists and has content
        Assert.NotNull(state.CityGrid);
        Assert.Equal(100, state.CityGrid.Width);

        // Verify addresses were created
        Assert.NotEmpty(state.Addresses);

        // Verify streets were created
        Assert.NotEmpty(state.Streets);

        // Verify every address has valid grid position
        foreach (var addr in state.Addresses.Values)
        {
            Assert.True(addr.GridX >= 0 && addr.GridX < 100);
            Assert.True(addr.GridY >= 0 && addr.GridY < 100);
            var cell = state.CityGrid.GetCell(addr.GridX, addr.GridY);
            Assert.True(cell.PlotType.IsBuilding());
            Assert.Equal(addr.Id, cell.AddressId);
        }

        // Verify travel time computation works with grid positions
        var addr1 = state.Addresses.Values.First();
        var addr2 = state.Addresses.Values.Last();
        var hours = mapConfig.ComputeTravelTimeHours(addr1.Position, addr2.Position);
        Assert.True(hours > 0 && hours <= mapConfig.MaxTravelTimeHours);
    }
}
```

- [ ] **Step 2: Run integration test**

Run: `dotnet test stakeout.tests/ --filter "FullyQualifiedName~CityIntegrationTests" -v minimal`
Expected: Pass.

- [ ] **Step 3: Run full test suite one final time**

Run: `dotnet test stakeout.tests/ -v minimal`
Expected: All tests pass.

- [ ] **Step 4: Commit**

```bash
git add stakeout.tests/Simulation/City/CityIntegrationTests.cs
git commit -m "test: add city generation integration test"
```
