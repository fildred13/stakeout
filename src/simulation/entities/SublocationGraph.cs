// src/simulation/entities/SublocationGraph.cs
using System.Collections.Generic;
using System.Linq;

namespace Stakeout.Simulation.Entities;

public class SublocationGraph
{
    private readonly Dictionary<int, Sublocation> _sublocations;
    private readonly Dictionary<int, List<int>> _adjacency;

    public SublocationGraph(
        Dictionary<int, Sublocation> sublocations,
        List<SublocationConnection> connections)
    {
        _sublocations = sublocations;
        _adjacency = new Dictionary<int, List<int>>();

        foreach (var sub in sublocations.Keys)
            _adjacency[sub] = new List<int>();

        foreach (var conn in connections)
        {
            _adjacency[conn.FromSublocationId].Add(conn.ToSublocationId);
            _adjacency[conn.ToSublocationId].Add(conn.FromSublocationId);
        }
    }

    public Sublocation FindByTag(string tag)
    {
        return _sublocations.Values.FirstOrDefault(s => s.HasTag(tag));
    }

    public List<Sublocation> FindAllByTag(string tag)
    {
        return _sublocations.Values.Where(s => s.HasTag(tag)).ToList();
    }

    public Sublocation GetRoad()
    {
        return FindByTag("road");
    }

    public Sublocation Get(int id)
    {
        return _sublocations.TryGetValue(id, out var sub) ? sub : null;
    }

    public List<Sublocation> GetNeighbors(int sublocationId)
    {
        if (!_adjacency.TryGetValue(sublocationId, out var neighborIds))
            return new List<Sublocation>();

        return neighborIds
            .Where(id => _sublocations.ContainsKey(id))
            .Select(id => _sublocations[id])
            .ToList();
    }

    public List<Sublocation> FindPath(int fromId, int toId)
    {
        if (fromId == toId)
            return new List<Sublocation> { _sublocations[fromId] };

        var visited = new HashSet<int> { fromId };
        var queue = new Queue<int>();
        var parent = new Dictionary<int, int>();
        queue.Enqueue(fromId);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current == toId)
            {
                var path = new List<Sublocation>();
                var node = toId;
                while (node != fromId)
                {
                    path.Add(_sublocations[node]);
                    node = parent[node];
                }
                path.Add(_sublocations[fromId]);
                path.Reverse();
                return path;
            }

            if (!_adjacency.TryGetValue(current, out var neighbors))
                continue;

            foreach (var neighbor in neighbors)
            {
                if (visited.Add(neighbor))
                {
                    parent[neighbor] = current;
                    queue.Enqueue(neighbor);
                }
            }
        }

        return new List<Sublocation>();
    }

    public IReadOnlyDictionary<int, Sublocation> AllSublocations => _sublocations;
}
