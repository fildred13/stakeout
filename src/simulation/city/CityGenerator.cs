using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Stakeout.Simulation.Data;
using Stakeout.Simulation.Entities;
using NumericsVector2 = System.Numerics.Vector2;

namespace Stakeout.Simulation.City;

public class CityGenerator
{
    private readonly Random _rng;
    private const int GridSize = 100;
    private const int ArterialSpacing = 10;
    private const int ArterialVariance = 2;

    // Center of the grid for urbanness calculation
    private static readonly NumericsVector2 Center = new NumericsVector2(50f, 50f);
    // Max distance from center to corner
    private static readonly float MaxDist = NumericsVector2.Distance(Center, new NumericsVector2(0f, 0f));

    private List<int> _horizontalArterials = new();
    private List<int> _verticalArterials = new();

    public CityGenerator(int seed)
    {
        _rng = new Random(seed);
    }

    public CityGrid Generate(SimulationState state)
    {
        var grid = new CityGrid(GridSize, GridSize);
        PlaceArterialRoads(grid);
        SubdivideSuperBlocks(grid);
        AssignStreetNames(grid, state);
        AssignPlotTypes(grid);
        ResolveFacingAndCreateAddresses(grid, state);
        return grid;
    }

    private void PlaceArterialRoads(CityGrid grid)
    {
        _horizontalArterials.Clear();
        _verticalArterials.Clear();

        // Edge rows are always roads
        _horizontalArterials.Add(0);
        _horizontalArterials.Add(GridSize - 1);

        // Edge columns are always roads
        _verticalArterials.Add(0);
        _verticalArterials.Add(GridSize - 1);

        // Interior horizontal arterials
        int y = ArterialSpacing;
        while (y < GridSize - 1)
        {
            int variance = _rng.Next(-ArterialVariance, ArterialVariance + 1);
            int arterialY = Math.Clamp(y + variance, 1, GridSize - 2);
            _horizontalArterials.Add(arterialY);
            y += ArterialSpacing;
        }

        // Interior vertical arterials
        int x = ArterialSpacing;
        while (x < GridSize - 1)
        {
            int variance = _rng.Next(-ArterialVariance, ArterialVariance + 1);
            int arterialX = Math.Clamp(x + variance, 1, GridSize - 2);
            _verticalArterials.Add(arterialX);
            x += ArterialSpacing;
        }

        _horizontalArterials.Sort();
        _verticalArterials.Sort();

        // Paint all horizontal arterial rows as roads
        foreach (int row in _horizontalArterials)
        {
            for (int cx = 0; cx < GridSize; cx++)
            {
                var cell = grid.GetCell(cx, row);
                cell.PlotType = PlotType.Road;
                grid.SetCell(cx, row, cell);
            }
        }

        // Paint all vertical arterial columns as roads
        foreach (int col in _verticalArterials)
        {
            for (int cy = 0; cy < GridSize; cy++)
            {
                var cell = grid.GetCell(col, cy);
                cell.PlotType = PlotType.Road;
                grid.SetCell(col, cy, cell);
            }
        }
    }

    private void SubdivideSuperBlocks(CityGrid grid)
    {
        // Iterate over each super-block rectangle defined by adjacent arterials
        for (int hi = 0; hi < _horizontalArterials.Count - 1; hi++)
        {
            int blockTop = _horizontalArterials[hi];
            int blockBottom = _horizontalArterials[hi + 1];

            for (int vi = 0; vi < _verticalArterials.Count - 1; vi++)
            {
                int blockLeft = _verticalArterials[vi];
                int blockRight = _verticalArterials[vi + 1];

                // Interior of block (exclusive of arterial edges)
                int innerTop = blockTop + 1;
                int innerBottom = blockBottom - 1;
                int innerLeft = blockLeft + 1;
                int innerRight = blockRight - 1;

                if (innerTop > innerBottom || innerLeft > innerRight)
                    continue;

                SubdivideBlock(grid, innerLeft, innerTop, innerRight, innerBottom);
            }
        }
    }

    private void SubdivideBlock(CityGrid grid, int left, int top, int right, int bottom)
    {
        // Compute urbanness from the block center
        float blockCenterX = (left + right) / 2f;
        float blockCenterY = (top + bottom) / 2f;
        float dist = NumericsVector2.Distance(new NumericsVector2(blockCenterX, blockCenterY), Center);
        float urbanness = 1.0f - Math.Clamp(dist / MaxDist, 0f, 1f);

        int width = right - left + 1;
        int height = bottom - top + 1;

        // Determine number of secondary roads in each direction based on urbanness
        int hCount = SecondaryRoadCount(urbanness, height);
        int vCount = SecondaryRoadCount(urbanness, width);

        // Place horizontal secondary roads (span wall-to-wall within block)
        if (hCount > 0)
        {
            PlaceSecondaryRoads(grid, hCount, top, bottom, isHorizontal: true, left, right);
        }

        // Place vertical secondary roads (span wall-to-wall within block)
        if (vCount > 0)
        {
            PlaceSecondaryRoads(grid, vCount, left, right, isHorizontal: false, top, bottom);
        }
    }

    private int SecondaryRoadCount(float urbanness, int blockSize)
    {
        // Minimum block size needed to fit a road with space on either side
        if (blockSize < 3)
            return 0;

        // Cap roads so each resulting sub-block is at least 3 cells wide (fits 3x3 buildings).
        // Each road consumes 1 cell, so n roads split blockSize into (n+1) segments.
        // We need blockSize - n >= 3*(n+1), i.e. n <= (blockSize - 3) / 4.
        int maxRoads = (blockSize - 3) / 4;
        if (maxRoads <= 0)
            return 0;

        if (urbanness > 0.7f)
        {
            // Urban: 1 to maxRoads secondary roads
            return _rng.Next(1, Math.Min(3, maxRoads + 1));
        }
        else if (urbanness >= 0.3f)
        {
            // Transitional: 0-1 secondary roads
            return Math.Min(_rng.Next(0, 2), maxRoads);
        }
        else
        {
            // Suburban: no secondary roads
            return 0;
        }
    }

    private void PlaceSecondaryRoads(CityGrid grid, int count, int start, int end, bool isHorizontal, int spanStart, int spanEnd)
    {
        int interior = end - start - 1;
        if (interior <= 0 || count <= 0)
            return;

        // Divide the interior evenly and place roads
        for (int i = 1; i <= count; i++)
        {
            int pos = start + (interior * i) / (count + 1) + 1;
            if (pos <= start || pos >= end)
                continue;

            for (int s = spanStart; s <= spanEnd; s++)
            {
                int cx = isHorizontal ? s : pos;
                int cy = isHorizontal ? pos : s;
                var cell = grid.GetCell(cx, cy);
                cell.PlotType = PlotType.Road;
                grid.SetCell(cx, cy, cell);
            }
        }
    }

    private void AssignStreetNames(CityGrid grid, SimulationState state)
    {
        int cityId = state.Cities.Count > 0 ? state.Cities.Keys.GetEnumerator().Current : 0;
        // Get first city id properly
        foreach (var k in state.Cities.Keys) { cityId = k; break; }

        var usedNames = new HashSet<string>();
        var horizontalArterialSet = new HashSet<int>(_horizontalArterials);
        var verticalArterialSet = new HashSet<int>(_verticalArterials);

        // Map from (x,y) to horizontal street id and vertical street id
        var hStreetMap = new Dictionary<(int, int), int>();
        var vStreetMap = new Dictionary<(int, int), int>();

        // Scan rows for horizontal streets
        for (int y = 0; y < GridSize; y++)
        {
            bool isArterial = horizontalArterialSet.Contains(y);

            if (isArterial)
            {
                // Arterial: entire row is one street
                // Check there are any road cells on this row
                bool hasRoad = false;
                for (int x = 0; x < GridSize; x++)
                    if (grid.GetCell(x, y).PlotType == PlotType.Road) { hasRoad = true; break; }

                if (!hasRoad) continue;

                var street = CreateStreet(state, cityId, isArterial: true, usedNames);
                for (int x = 0; x < GridSize; x++)
                {
                    if (grid.GetCell(x, y).PlotType == PlotType.Road)
                    {
                        hStreetMap[(x, y)] = street.Id;
                        street.RoadCells.Add(new Vector2I(x, y));
                    }
                }
            }
            else
            {
                // Secondary: scan for continuous runs
                int runStart = -1;
                for (int x = 0; x <= GridSize; x++)
                {
                    bool isRoad = x < GridSize && grid.GetCell(x, y).PlotType == PlotType.Road;
                    if (isRoad && runStart < 0)
                    {
                        runStart = x;
                    }
                    else if (!isRoad && runStart >= 0)
                    {
                        // End of run — create a street for [runStart, x-1]
                        var street = CreateStreet(state, cityId, isArterial: false, usedNames);
                        for (int rx = runStart; rx < x; rx++)
                        {
                            hStreetMap[(rx, y)] = street.Id;
                            street.RoadCells.Add(new Vector2I(rx, y));
                        }
                        runStart = -1;
                    }
                }
            }
        }

        // Scan columns for vertical streets
        for (int x = 0; x < GridSize; x++)
        {
            bool isArterial = verticalArterialSet.Contains(x);

            if (isArterial)
            {
                bool hasRoad = false;
                for (int y = 0; y < GridSize; y++)
                    if (grid.GetCell(x, y).PlotType == PlotType.Road) { hasRoad = true; break; }

                if (!hasRoad) continue;

                var street = CreateStreet(state, cityId, isArterial: true, usedNames);
                for (int y = 0; y < GridSize; y++)
                {
                    if (grid.GetCell(x, y).PlotType == PlotType.Road)
                    {
                        vStreetMap[(x, y)] = street.Id;
                        street.RoadCells.Add(new Vector2I(x, y));
                    }
                }
            }
            else
            {
                int runStart = -1;
                for (int y = 0; y <= GridSize; y++)
                {
                    bool isRoad = y < GridSize && grid.GetCell(x, y).PlotType == PlotType.Road;
                    if (isRoad && runStart < 0)
                    {
                        runStart = y;
                    }
                    else if (!isRoad && runStart >= 0)
                    {
                        var street = CreateStreet(state, cityId, isArterial: false, usedNames);
                        for (int ry = runStart; ry < y; ry++)
                        {
                            vStreetMap[(x, ry)] = street.Id;
                            street.RoadCells.Add(new Vector2I(x, ry));
                        }
                        runStart = -1;
                    }
                }
            }
        }

        // Assign StreetId to each road cell: prefer horizontal street
        for (int x = 0; x < GridSize; x++)
        {
            for (int y = 0; y < GridSize; y++)
            {
                var cell = grid.GetCell(x, y);
                if (cell.PlotType != PlotType.Road) continue;

                if (hStreetMap.TryGetValue((x, y), out int hId))
                    cell.StreetId = hId;
                else if (vStreetMap.TryGetValue((x, y), out int vId))
                    cell.StreetId = vId;

                grid.SetCell(x, y, cell);
            }
        }
    }

    private Street CreateStreet(SimulationState state, int cityId, bool isArterial, HashSet<string> usedNames)
    {
        string name = GenerateStreetName(isArterial, usedNames);
        usedNames.Add(name);

        var street = new Street
        {
            Id = state.GenerateEntityId(),
            Name = name,
            CityId = cityId
        };
        state.Streets[street.Id] = street;
        return street;
    }

    private string GenerateStreetName(bool isArterial, HashSet<string> usedNames)
    {
        string suffix;
        if (isArterial)
        {
            suffix = _rng.Next(2) == 0 ? "Boulevard" : "Avenue";
        }
        else
        {
            string[] secondarySuffixes = { "Street", "Road", "Drive", "Lane" };
            suffix = secondarySuffixes[_rng.Next(secondarySuffixes.Length)];
        }

        // Try to find an unused base name with this suffix
        var names = StreetData.StreetNames;
        for (int attempt = 0; attempt < names.Length * 2; attempt++)
        {
            string candidate = $"{names[_rng.Next(names.Length)]} {suffix}";
            if (!usedNames.Contains(candidate))
                return candidate;
        }

        // Fall back to a numbered ordinal name (e.g. "42nd Street")
        for (int i = 1; i < 1000; i++)
        {
            int num = _rng.Next(10, 200);
            string ordinal = OrdinalSuffix(num);
            string candidate = $"{num}{ordinal} {suffix}";
            if (!usedNames.Contains(candidate))
                return candidate;
        }

        return $"{_rng.Next(100, 999)}th {suffix}";
    }

    private static string OrdinalSuffix(int n)
    {
        int lastTwo = n % 100;
        if (lastTwo >= 11 && lastTwo <= 13) return "th";
        return (n % 10) switch
        {
            1 => "st",
            2 => "nd",
            3 => "rd",
            _ => "th"
        };
    }

    private float ComputeUrbanness(float cx, float cy)
    {
        float dist = NumericsVector2.Distance(new NumericsVector2(cx, cy), Center);
        return 1.0f - Math.Clamp(dist / MaxDist, 0f, 1f);
    }

    private void AssignPlotTypes(CityGrid grid)
    {
        // Collect all non-road rectangular sub-blocks from the grid.
        // A sub-block is a maximal contiguous rectangle of non-road cells bounded by road rows/columns.
        // We scan the road rows/columns to find block boundaries, then process each block interior.
        var hBoundaries = new List<int>();
        var vBoundaries = new List<int>();

        for (int y = 0; y < GridSize; y++)
        {
            bool fullRow = true;
            for (int x = 0; x < GridSize; x++)
            {
                if (grid.GetCell(x, y).PlotType != PlotType.Road) { fullRow = false; break; }
            }
            if (fullRow) hBoundaries.Add(y);
        }

        for (int x = 0; x < GridSize; x++)
        {
            bool fullCol = true;
            for (int y = 0; y < GridSize; y++)
            {
                if (grid.GetCell(x, y).PlotType != PlotType.Road) { fullCol = false; break; }
            }
            if (fullCol) vBoundaries.Add(x);
        }

        hBoundaries.Sort();
        vBoundaries.Sort();

        // Process each super-block (between full road rows/cols)
        for (int hi = 0; hi < hBoundaries.Count - 1; hi++)
        {
            int blockTop = hBoundaries[hi] + 1;
            int blockBottom = hBoundaries[hi + 1] - 1;
            if (blockTop > blockBottom) continue;

            for (int vi = 0; vi < vBoundaries.Count - 1; vi++)
            {
                int blockLeft = vBoundaries[vi] + 1;
                int blockRight = vBoundaries[vi + 1] - 1;
                if (blockLeft > blockRight) continue;

                // Within this super-block there may be secondary roads.
                // Find sub-blocks by scanning for road rows/cols within the super-block.
                FillSubBlocks(grid, blockLeft, blockTop, blockRight, blockBottom);
            }
        }
    }

    private void FillSubBlocks(CityGrid grid, int left, int top, int right, int bottom)
    {
        // Find internal road rows within [top, bottom]
        var internalHRoads = new List<int>();
        for (int y = top; y <= bottom; y++)
        {
            bool isRoadRow = true;
            for (int x = left; x <= right; x++)
            {
                if (grid.GetCell(x, y).PlotType != PlotType.Road) { isRoadRow = false; break; }
            }
            if (isRoadRow) internalHRoads.Add(y);
        }

        // Find internal road columns within [left, right]
        var internalVRoads = new List<int>();
        for (int x = left; x <= right; x++)
        {
            bool isRoadCol = true;
            for (int y = top; y <= bottom; y++)
            {
                if (grid.GetCell(x, y).PlotType != PlotType.Road) { isRoadCol = false; break; }
            }
            if (isRoadCol) internalVRoads.Add(x);
        }

        // Build row and column boundary lists for sub-block detection
        var rowBounds = new List<int> { top - 1 };
        rowBounds.AddRange(internalHRoads);
        rowBounds.Add(bottom + 1);

        var colBounds = new List<int> { left - 1 };
        colBounds.AddRange(internalVRoads);
        colBounds.Add(right + 1);

        rowBounds.Sort();
        colBounds.Sort();

        for (int ri = 0; ri < rowBounds.Count - 1; ri++)
        {
            int subTop = rowBounds[ri] + 1;
            int subBottom = rowBounds[ri + 1] - 1;
            if (subTop > subBottom) continue;

            for (int ci = 0; ci < colBounds.Count - 1; ci++)
            {
                int subLeft = colBounds[ci] + 1;
                int subRight = colBounds[ci + 1] - 1;
                if (subLeft > subRight) continue;

                FillBlock(grid, subLeft, subTop, subRight, subBottom);
            }
        }
    }

    // Weight tables for multi-cell and single-cell building types
    private static readonly (PlotType Type, float Urban, float Suburban)[] LargeBuildingTypes =
    {
        (PlotType.ApartmentBuilding, 30f, 5f),
        (PlotType.Office,            25f, 5f),
        (PlotType.Park,              10f, 10f),
    };

    private static readonly (PlotType Type, float Urban, float Suburban)[] SmallBuildingTypes =
    {
        (PlotType.SuburbanHome, 5f,  40f),
        (PlotType.Diner,        5f,   5f),
        (PlotType.DiveBar,      5f,   3f),
        (PlotType.Empty,        5f,  20f),
    };

    private void FillBlock(CityGrid grid, int left, int top, int right, int bottom)
    {
        float centerX = (left + right) / 2f;
        float centerY = (top + bottom) / 2f;
        float urbanness = ComputeUrbanness(centerX, centerY);

        // Compute interpolated weights for large building types
        float[] largeWeights = new float[LargeBuildingTypes.Length];
        float largeTotal = 0f;
        for (int i = 0; i < LargeBuildingTypes.Length; i++)
        {
            var (_, u, s) = LargeBuildingTypes[i];
            largeWeights[i] = u * urbanness + s * (1f - urbanness);
            largeTotal += largeWeights[i];
        }

        // Compute interpolated weights for 1x1 types
        float[] smallWeights = new float[SmallBuildingTypes.Length];
        float smallTotal = 0f;
        for (int i = 0; i < SmallBuildingTypes.Length; i++)
        {
            var (_, u, s) = SmallBuildingTypes[i];
            smallWeights[i] = u * urbanness + s * (1f - urbanness);
            smallTotal += smallWeights[i];
        }

        // Pass 1: place large buildings in empty openings
        // Pick a type first to know the size, then check if it fits
        for (int y = top; y <= bottom; y++)
        {
            for (int x = left; x <= right; x++)
            {
                if (grid.GetCell(x, y).PlotType != PlotType.Empty) continue;

                PlotType chosen = PickWeighted(largeWeights, largeTotal, LargeBuildingTypes);
                var (w, h) = chosen.GetSize();

                // Check if the full footprint fits and is empty
                if (x + w - 1 > right || y + h - 1 > bottom) continue;

                bool fits = true;
                for (int dy = 0; dy < h && fits; dy++)
                    for (int dx = 0; dx < w && fits; dx++)
                        if (grid.GetCell(x + dx, y + dy).PlotType != PlotType.Empty)
                            fits = false;

                if (!fits) continue;

                // At least one cell in the footprint must be adjacent to a road
                if (!FootprintTouchesRoad(grid, x, y, w, h)) continue;

                // Place all cells
                for (int dy = 0; dy < h; dy++)
                    for (int dx = 0; dx < w; dx++)
                        SetPlotType(grid, x + dx, y + dy, chosen);
            }
        }

        // Pass 2: fill remaining Empty cells with 1x1 types (only if adjacent to a road)
        for (int y = top; y <= bottom; y++)
        {
            for (int x = left; x <= right; x++)
            {
                if (grid.GetCell(x, y).PlotType != PlotType.Empty) continue;
                if (!FootprintTouchesRoad(grid, x, y, 1, 1)) continue;

                PlotType chosen = PickWeighted(smallWeights, smallTotal, SmallBuildingTypes);
                SetPlotType(grid, x, y, chosen);
            }
        }
    }

    private PlotType PickWeighted(float[] weights, float total, (PlotType Type, float Urban, float Suburban)[] types)
    {
        float roll = (float)(_rng.NextDouble() * total);
        float cumulative = 0f;
        for (int i = 0; i < weights.Length - 1; i++)
        {
            cumulative += weights[i];
            if (roll < cumulative) return types[i].Type;
        }
        return types[weights.Length - 1].Type;
    }

    private static void SetPlotType(CityGrid grid, int x, int y, PlotType type)
    {
        var cell = grid.GetCell(x, y);
        cell.PlotType = type;
        grid.SetCell(x, y, cell);
    }

    /// <summary>
    /// Returns true if at least one cell in the footprint (x,y)-(x+w-1,y+h-1)
    /// has an adjacent road cell (4-directional). Ensures buildings can attach to a road.
    /// </summary>
    private static bool FootprintTouchesRoad(CityGrid grid, int x, int y, int w, int h)
    {
        for (int dy = 0; dy < h; dy++)
        {
            for (int dx = 0; dx < w; dx++)
            {
                int cx = x + dx, cy = y + dy;
                // Only check outward-facing edges of the footprint
                if (dx == 0 && cx > 0 && grid.GetCell(cx - 1, cy).PlotType == PlotType.Road) return true;
                if (dx == w - 1 && cx + 1 < grid.Width && grid.GetCell(cx + 1, cy).PlotType == PlotType.Road) return true;
                if (dy == 0 && cy > 0 && grid.GetCell(cx, cy - 1).PlotType == PlotType.Road) return true;
                if (dy == h - 1 && cy + 1 < grid.Height && grid.GetCell(cx, cy + 1).PlotType == PlotType.Road) return true;
            }
        }
        return false;
    }

    private void ResolveFacingAndCreateAddresses(CityGrid grid, SimulationState state)
    {
        // Step 1: Assign facing directions to all building cells
        for (int x = 0; x < GridSize; x++)
        {
            for (int y = 0; y < GridSize; y++)
            {
                var cell = grid.GetCell(x, y);
                if (cell.PlotType == PlotType.Road || cell.PlotType == PlotType.Empty)
                    continue;

                var (dir, streetId) = grid.FindNearestRoad(x, y);
                cell.FacingDirection = dir;
                grid.SetCell(x, y, cell);
            }
        }

        // Step 2: Create Address entities, grouping multi-cell buildings
        // Track which cells have already been assigned to an address
        var assigned = new bool[GridSize, GridSize];

        // Collect addresses grouped by street for street numbering
        var addressesByStreet = new Dictionary<int, List<Address>>();

        for (int y = 0; y < GridSize; y++)
        {
            for (int x = 0; x < GridSize; x++)
            {
                if (assigned[x, y]) continue;

                var cell = grid.GetCell(x, y);
                if (cell.PlotType == PlotType.Road || cell.PlotType == PlotType.Empty)
                    continue;

                PlotType plotType = cell.PlotType;
                var (sizeW, sizeH) = plotType.GetSize();
                bool isMultiCell = sizeW > 1 || sizeH > 1;

                if (isMultiCell)
                {
                    // Only create one address per multi-cell group, at the top-left corner
                    // Verify all cells in the footprint match and are unassigned
                    bool isTopLeft = true;
                    for (int dy = 0; dy < sizeH && isTopLeft; dy++)
                        for (int dx = 0; dx < sizeW && isTopLeft; dx++)
                        {
                            int cx = x + dx, cy = y + dy;
                            if (cx >= GridSize || cy >= GridSize
                                || grid.GetCell(cx, cy).PlotType != plotType
                                || assigned[cx, cy])
                                isTopLeft = false;
                        }

                    if (!isTopLeft) continue;
                }

                // Determine which street this address faces
                int? facingStreetId = null;

                // Check all cells in the footprint for the nearest road
                for (int dy = 0; dy < sizeH && !facingStreetId.HasValue; dy++)
                    for (int dx = 0; dx < sizeW && !facingStreetId.HasValue; dx++)
                    {
                        var (_, sid) = grid.FindNearestRoad(x + dx, y + dy);
                        if (sid.HasValue) facingStreetId = sid;
                    }

                if (!facingStreetId.HasValue) continue; // No road found — skip

                var address = new Address
                {
                    Id = state.GenerateEntityId(),
                    Type = plotType.ToAddressType(),
                    GridX = x,
                    GridY = y,
                    StreetId = facingStreetId.Value,
                    Number = 0 // Will be filled in during street numbering pass
                };

                state.Addresses[address.Id] = address;

                // Mark all cells as assigned and set AddressId
                for (int dy = 0; dy < sizeH; dy++)
                    for (int dx = 0; dx < sizeW; dx++)
                    {
                        int cx = x + dx, cy = y + dy;
                        assigned[cx, cy] = true;
                        var c = grid.GetCell(cx, cy);
                        c.AddressId = address.Id;
                        grid.SetCell(cx, cy, c);
                    }

                if (!addressesByStreet.TryGetValue(facingStreetId.Value, out var list))
                {
                    list = new List<Address>();
                    addressesByStreet[facingStreetId.Value] = list;
                }
                list.Add(address);
            }
        }

        // Step 3: Assign street numbers — sort by position along street, number sequentially
        foreach (var (streetId, addresses) in addressesByStreet)
        {
            if (!state.Streets.TryGetValue(streetId, out var street))
                continue;

            // Determine street orientation from its road cells
            bool isHorizontal = false;
            if (street.RoadCells.Count >= 2)
            {
                // If x changes more than y, it's horizontal
                int dxRange = street.RoadCells.Max(c => c.X) - street.RoadCells.Min(c => c.X);
                int dyRange = street.RoadCells.Max(c => c.Y) - street.RoadCells.Min(c => c.Y);
                isHorizontal = dxRange >= dyRange;
            }

            List<Address> sorted = isHorizontal
                ? addresses.OrderBy(a => a.GridX).ThenBy(a => a.GridY).ToList()
                : addresses.OrderBy(a => a.GridY).ThenBy(a => a.GridX).ToList();

            for (int i = 0; i < sorted.Count; i++)
            {
                sorted[i].Number = (i + 1) * 2;
            }
        }
    }

    // Expose arterial positions for later pipeline stages
    public IReadOnlyList<int> HorizontalArterials => _horizontalArterials;
    public IReadOnlyList<int> VerticalArterials => _verticalArterials;
}
