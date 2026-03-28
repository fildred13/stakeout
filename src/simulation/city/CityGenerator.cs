using System;
using System.Collections.Generic;
using System.Numerics;

namespace Stakeout.Simulation.City;

public class CityGenerator
{
    private readonly Random _rng;
    private const int GridSize = 100;
    private const int ArterialSpacing = 10;
    private const int ArterialVariance = 2;

    // Center of the grid for urbanness calculation
    private static readonly Vector2 Center = new Vector2(50f, 50f);
    // Max distance from center to corner
    private static readonly float MaxDist = Vector2.Distance(Center, new Vector2(0f, 0f));

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
        float dist = Vector2.Distance(new Vector2(blockCenterX, blockCenterY), Center);
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

    // Expose arterial positions for later pipeline stages
    public IReadOnlyList<int> HorizontalArterials => _horizontalArterials;
    public IReadOnlyList<int> VerticalArterials => _verticalArterials;
}
