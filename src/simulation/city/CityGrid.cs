using System.Collections.Generic;
using Stakeout.Simulation.Entities;

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
