using System;
using System.Collections.Generic;
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

        if (urbanness > 0.7f)
        {
            // Urban: 1-2 secondary roads
            return _rng.Next(1, 3);
        }
        else if (urbanness >= 0.3f)
        {
            // Transitional: 0-1 secondary roads
            return _rng.Next(0, 2);
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
        // Try to find an unused base name
        string baseName = null;
        // Shuffle attempt: try up to all names
        var names = StreetData.StreetNames;
        for (int attempt = 0; attempt < names.Length * 2; attempt++)
        {
            string candidate = names[_rng.Next(names.Length)];
            if (!usedNames.Contains(candidate + " Boulevard") &&
                !usedNames.Contains(candidate + " Avenue") &&
                !usedNames.Contains(candidate + " Street") &&
                !usedNames.Contains(candidate + " Road") &&
                !usedNames.Contains(candidate + " Drive") &&
                !usedNames.Contains(candidate + " Lane"))
            {
                baseName = candidate;
                break;
            }
        }

        // Fall back to a numbered name if all base names are used
        if (baseName == null)
            baseName = $"Street {_rng.Next(100, 999)}";

        string suffix;
        if (isArterial)
        {
            // Arterial: Boulevard or Avenue
            suffix = _rng.Next(2) == 0 ? "Boulevard" : "Avenue";
        }
        else
        {
            // Secondary: Street, Road, Drive, Lane (skip Avenue — reserve for arterials)
            string[] secondarySuffixes = { "Street", "Road", "Drive", "Lane" };
            suffix = secondarySuffixes[_rng.Next(secondarySuffixes.Length)];
        }

        return $"{baseName} {suffix}";
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

    // Weight tables: [ApartmentBuilding, Office, Park] for 2x2; [SuburbanHome, Diner, DiveBar, Empty] for 1x1
    private static readonly (PlotType Type, float Urban, float Suburban)[] TwoByTwoTypes =
    {
        (PlotType.ApartmentBuilding, 30f, 5f),
        (PlotType.Office,            25f, 5f),
        (PlotType.Park,              10f, 10f),
    };

    private static readonly (PlotType Type, float Urban, float Suburban)[] OneByOneTypes =
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

        // Compute interpolated weights for 2x2 types
        float[] twoByTwoWeights = new float[TwoByTwoTypes.Length];
        float twoByTwoTotal = 0f;
        for (int i = 0; i < TwoByTwoTypes.Length; i++)
        {
            var (_, u, s) = TwoByTwoTypes[i];
            twoByTwoWeights[i] = u * urbanness + s * (1f - urbanness);
            twoByTwoTotal += twoByTwoWeights[i];
        }

        // Compute interpolated weights for 1x1 types
        float[] oneByOneWeights = new float[OneByOneTypes.Length];
        float oneByOneTotal = 0f;
        for (int i = 0; i < OneByOneTypes.Length; i++)
        {
            var (_, u, s) = OneByOneTypes[i];
            oneByOneWeights[i] = u * urbanness + s * (1f - urbanness);
            oneByOneTotal += oneByOneWeights[i];
        }

        // Pass 1: place 2x2 buildings in empty 2x2 openings
        for (int y = top; y <= bottom - 1; y++)
        {
            for (int x = left; x <= right - 1; x++)
            {
                if (grid.GetCell(x, y).PlotType != PlotType.Empty) continue;
                if (grid.GetCell(x + 1, y).PlotType != PlotType.Empty) continue;
                if (grid.GetCell(x, y + 1).PlotType != PlotType.Empty) continue;
                if (grid.GetCell(x + 1, y + 1).PlotType != PlotType.Empty) continue;

                PlotType chosen = PickWeighted(twoByTwoWeights, twoByTwoTotal, TwoByTwoTypes);

                // Place all 4 cells
                SetPlotType(grid, x,     y,     chosen);
                SetPlotType(grid, x + 1, y,     chosen);
                SetPlotType(grid, x,     y + 1, chosen);
                SetPlotType(grid, x + 1, y + 1, chosen);
            }
        }

        // Pass 2: fill remaining Empty cells with 1x1 types
        for (int y = top; y <= bottom; y++)
        {
            for (int x = left; x <= right; x++)
            {
                if (grid.GetCell(x, y).PlotType != PlotType.Empty) continue;

                PlotType chosen = PickWeighted(oneByOneWeights, oneByOneTotal, OneByOneTypes);
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

    // Expose arterial positions for later pipeline stages
    public IReadOnlyList<int> HorizontalArterials => _horizontalArterials;
    public IReadOnlyList<int> VerticalArterials => _verticalArterials;
}
