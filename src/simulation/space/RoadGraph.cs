using System;
using System.Collections.Generic;
using Godot;
using Stakeout.Simulation.City;
using Stakeout.Simulation.Entities;

namespace Stakeout.Simulation.Space;

/// <summary>
/// The street network: every road cell of the CityGrid is a node joined to its road neighbours.
/// One cell is about 80 m. Each address has an access cell (the road cell in front of it), where
/// its vehicles park and its people step onto the street.
/// </summary>
public class RoadGraph
{
    public const float MetersPerCell = 80f;
    public const int PixelsPerCell = Address.CellSize;

    public int Width { get; }
    public int Height { get; }
    private readonly bool[] _road;
    private readonly bool[] _arterial;
    private readonly int[] _streetId;
    private readonly Dictionary<int, int> _addressAccess = new();

    public RoadGraph(CityGrid grid, IEnumerable<Address> addresses)
    {
        Width = grid.Width;
        Height = grid.Height;
        _road = new bool[Width * Height];
        _arterial = new bool[Width * Height];
        _streetId = new int[Width * Height];

        for (int x = 0; x < Width; x++)
            for (int y = 0; y < Height; y++)
            {
                var cell = grid.GetCell(x, y);
                if (cell.PlotType != PlotType.Road) continue;
                _road[Index(x, y)] = true;
                _streetId[Index(x, y)] = cell.StreetId ?? 0;
            }

        // Arterials run the whole width or height of the city; secondary streets stop at block edges.
        for (int y = 0; y < Height; y++)
        {
            int count = 0;
            for (int x = 0; x < Width; x++) if (_road[Index(x, y)]) count++;
            if (count >= Width * 0.8) for (int x = 0; x < Width; x++) _arterial[Index(x, y)] |= _road[Index(x, y)];
        }
        for (int x = 0; x < Width; x++)
        {
            int count = 0;
            for (int y = 0; y < Height; y++) if (_road[Index(x, y)]) count++;
            if (count >= Height * 0.8) for (int y = 0; y < Height; y++) _arterial[Index(x, y)] |= _road[Index(x, y)];
        }

        foreach (var address in addresses)
            _addressAccess[address.Id] = FindAccessCell(grid, address);
    }

    public int Index(int x, int y) => y * Width + x;
    public (int X, int Y) Coords(int cell) => (cell % Width, cell / Width);
    public bool IsRoad(int cell) => cell >= 0 && cell < _road.Length && _road[cell];
    public bool IsArterial(int cell) => _arterial[cell];
    public int StreetIdAt(int cell) => _streetId[cell];
    public int AccessCell(int addressId) => _addressAccess[addressId];
    public bool HasAccess(int addressId) => _addressAccess.ContainsKey(addressId);

    public Vector2 CellCenter(int cell)
    {
        var (x, y) = Coords(cell);
        return new Vector2(x * PixelsPerCell + PixelsPerCell / 2f, y * PixelsPerCell + PixelsPerCell / 2f);
    }

    public int Neighbors(int cell, Span<int> result)
    {
        var (x, y) = Coords(cell);
        int n = 0;
        if (x > 0 && _road[cell - 1]) result[n++] = cell - 1;
        if (x < Width - 1 && _road[cell + 1]) result[n++] = cell + 1;
        if (y > 0 && _road[cell - Width]) result[n++] = cell - Width;
        if (y < Height - 1 && _road[cell + Width]) result[n++] = cell + Width;
        return n;
    }

    /// <summary>Distance in cells between two cells (Manhattan).</summary>
    public int CellDistance(int a, int b)
    {
        var (ax, ay) = Coords(a);
        var (bx, by) = Coords(b);
        return Math.Abs(ax - bx) + Math.Abs(ay - by);
    }

    private int FindAccessCell(CityGrid grid, Address address)
    {
        var cells = grid.GetCellsForAddress(address.Id);
        if (cells.Count == 0) cells = new List<(int X, int Y)> { (address.GridX, address.GridY) };

        float cx = 0, cy = 0;
        foreach (var (x, y) in cells) { cx += x; cy += y; }
        cx /= cells.Count;
        cy /= cells.Count;

        int best = -1;
        float bestScore = float.MaxValue;
        foreach (var (x, y) in cells)
        {
            foreach (var (dx, dy) in new[] { (0, 1), (1, 0), (0, -1), (-1, 0) })
            {
                int nx = x + dx, ny = y + dy;
                if (nx < 0 || ny < 0 || nx >= Width || ny >= Height) continue;
                int idx = Index(nx, ny);
                if (!_road[idx]) continue;
                float score = (nx - cx) * (nx - cx) + (ny - cy) * (ny - cy);
                if (_streetId[idx] != address.StreetId) score += 1000f;
                if (score < bestScore) { bestScore = score; best = idx; }
            }
        }
        if (best >= 0) return best;

        // Not touching a road (should not happen): nearest road cell by search.
        int start = Index(address.GridX, address.GridY);
        for (int radius = 1; radius < Math.Max(Width, Height); radius++)
            for (int dx = -radius; dx <= radius; dx++)
                for (int dy = -radius; dy <= radius; dy++)
                {
                    int nx = address.GridX + dx, ny = address.GridY + dy;
                    if (nx < 0 || ny < 0 || nx >= Width || ny >= Height) continue;
                    if (_road[Index(nx, ny)]) return Index(nx, ny);
                }
        return start;
    }
}
