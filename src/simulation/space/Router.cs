using System;
using System.Collections.Generic;
using Godot;

namespace Stakeout.Simulation.Space;

/// <summary>How a trip is made. A taxi is an anonymous cab: it drives the roads but is not a simulated vehicle.</summary>
public enum TravelMode { Drive, Walk, Taxi }

/// <summary>
/// A path along road cells with the time (seconds after departure) at which the traveller enters each cell.
/// Routes are shared between trips through the router cache, so treat them as immutable.
/// </summary>
public sealed class Route
{
    public int[] Cells { get; }
    public int[] Offsets { get; }
    public int DurationSeconds { get; }
    public TravelMode Mode { get; }
    private HashSet<int> _cellSet;

    public Route(int[] cells, int[] offsets, int duration, TravelMode mode)
    {
        Cells = cells;
        Offsets = offsets;
        DurationSeconds = duration;
        Mode = mode;
    }

    public TimeSpan Duration => TimeSpan.FromSeconds(DurationSeconds);

    public bool Contains(int cell)
    {
        _cellSet ??= new HashSet<int>(Cells);
        return _cellSet.Contains(cell);
    }

    /// <summary>Seconds after departure at which the traveller is in <paramref name="cell"/>, or -1.</summary>
    public int OffsetAt(int cell)
    {
        for (int i = 0; i < Cells.Length; i++)
            if (Cells[i] == cell) return Offsets[i];
        return -1;
    }

    /// <summary>Pixel position <paramref name="seconds"/> after departure, interpolated between cell centres.</summary>
    public Vector2 PositionAt(RoadGraph roads, double seconds)
    {
        if (Cells.Length == 1 || seconds <= Offsets[0]) return roads.CellCenter(Cells[0]);
        int last = Cells.Length - 1;
        if (seconds >= Offsets[last]) return roads.CellCenter(Cells[last]);
        int i = Array.BinarySearch(Offsets, (int)seconds);
        if (i < 0) i = ~i - 1;
        i = Math.Clamp(i, 0, last - 1);
        double span = Offsets[i + 1] - Offsets[i];
        float f = span <= 0 ? 0f : (float)((seconds - Offsets[i]) / span);
        return roads.CellCenter(Cells[i]).Lerp(roads.CellCenter(Cells[i + 1]), Math.Clamp(f, 0f, 1f));
    }
}

/// <summary>A* over road cells with a route cache. Single-threaded.</summary>
public class Router
{
    public const int ArterialSecondsPerCell = 6;   // ~48 km/h
    public const int StreetSecondsPerCell = 10;    // ~29 km/h
    public const int WalkSecondsPerCell = 60;      // ~4.8 km/h
    public const int DriveOverheadSeconds = 60;    // walk to car, start, park
    public const int WalkOverheadSeconds = 30;
    public const int TaxiWaitSeconds = 360;     // phoning for a cab and waiting for it
    /// <summary>Beyond this many cells on foot, people without a car take a cab.</summary>
    public const int MaxWalkCells = 12;

    private readonly RoadGraph _roads;
    private readonly Dictionary<(int, int, TravelMode), Route> _cache = new();
    private readonly int[] _g;
    private readonly int[] _from;
    private readonly int[] _stamp;
    private int _generation;

    public int CacheSize => _cache.Count;

    public Router(RoadGraph roads)
    {
        _roads = roads;
        int n = roads.Width * roads.Height;
        _g = new int[n];
        _from = new int[n];
        _stamp = new int[n];
    }

    public RoadGraph Roads => _roads;

    public Route Between(int fromAddressId, int toAddressId, TravelMode mode) =>
        Find(_roads.AccessCell(fromAddressId), _roads.AccessCell(toAddressId), mode);

    /// <summary>Walking, unless it is far enough that someone without a car would call a cab.</summary>
    public TravelMode OnFootOrTaxi(int fromAddressId, int toAddressId) =>
        Between(fromAddressId, toAddressId, TravelMode.Walk).Cells.Length > MaxWalkCells ? TravelMode.Taxi : TravelMode.Walk;

    public int CellCost(int cell, TravelMode mode) => mode == TravelMode.Walk
        ? WalkSecondsPerCell
        : _roads.IsArterial(cell) ? ArterialSecondsPerCell : StreetSecondsPerCell;

    public Route Find(int fromCell, int toCell, TravelMode mode)
    {
        if (_cache.TryGetValue((fromCell, toCell, mode), out var cached)) return cached;
        var route = Search(fromCell, toCell, mode);
        _cache[(fromCell, toCell, mode)] = route;
        return route;
    }

    private Route Search(int fromCell, int toCell, TravelMode mode)
    {
        int overhead = mode switch
        {
            TravelMode.Drive => DriveOverheadSeconds,
            TravelMode.Taxi => DriveOverheadSeconds + TaxiWaitSeconds,
            _ => WalkOverheadSeconds
        };
        if (fromCell == toCell)
            return new Route(new[] { fromCell }, new[] { overhead / 2 }, overhead, mode);

        _generation++;
        int minCost = mode == TravelMode.Walk ? WalkSecondsPerCell : ArterialSecondsPerCell;
        var open = new PriorityQueue<int, (int F, int Tie)>();
        _g[fromCell] = 0;
        _from[fromCell] = -1;
        _stamp[fromCell] = _generation;
        open.Enqueue(fromCell, (0, 0));
        Span<int> neighbors = stackalloc int[4];
        bool found = false;
        int tie = 0;

        while (open.TryDequeue(out int cell, out var pri))
        {
            if (cell == toCell) { found = true; break; }
            int g = _g[cell];
            if (pri.F - _roads.CellDistance(cell, toCell) * minCost > g) continue; // stale entry
            int n = _roads.Neighbors(cell, neighbors);
            for (int i = 0; i < n; i++)
            {
                int next = neighbors[i];
                int ng = g + CellCost(next, mode);
                if (_stamp[next] == _generation && _g[next] <= ng) continue;
                _stamp[next] = _generation;
                _g[next] = ng;
                _from[next] = cell;
                open.Enqueue(next, (ng + _roads.CellDistance(next, toCell) * minCost, ++tie));
            }
        }

        if (!found)
            return new Route(new[] { fromCell, toCell }, new[] { overhead / 2, overhead / 2 + 600 }, overhead + 600, mode);

        var path = new List<int>();
        for (int c = toCell; c != -1; c = _from[c]) path.Add(c);
        path.Reverse();
        var offsets = new int[path.Count];
        offsets[0] = overhead / 2;
        for (int i = 1; i < path.Count; i++) offsets[i] = offsets[i - 1] + CellCost(path[i], mode);
        return new Route(path.ToArray(), offsets, offsets[^1] + overhead / 2, mode);
    }
}
