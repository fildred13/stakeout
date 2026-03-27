// src/simulation/entities/SublocationGraph.cs
using System.Collections.Generic;
using System.Linq;

namespace Stakeout.Simulation.Entities;

public class SublocationGraph
{
    private readonly Dictionary<int, Sublocation> _sublocations;
    private readonly Dictionary<int, List<SublocationConnection>> _edges;
    private readonly List<SublocationConnection> _allConnections;

    public SublocationGraph(
        Dictionary<int, Sublocation> sublocations,
        List<SublocationConnection> connections)
    {
        _sublocations = sublocations;
        _edges = new Dictionary<int, List<SublocationConnection>>();
        _allConnections = connections;

        foreach (var sub in sublocations.Keys)
            _edges[sub] = new List<SublocationConnection>();

        foreach (var conn in connections)
        {
            _edges[conn.FromSublocationId].Add(conn);

            // All connections are bidirectional — add a reverse entry
            var reverse = new SublocationConnection
            {
                Id = conn.Id,
                Name = conn.Name,
                Tags = conn.Tags,
                Type = conn.Type,
                FromSublocationId = conn.ToSublocationId,
                ToSublocationId = conn.FromSublocationId,
                Lockable = conn.Lockable,
                Concealable = conn.Concealable,
                Transparent = conn.Transparent,
                Breakable = conn.Breakable,
            };
            _edges[conn.ToSublocationId].Add(reverse);
        }
    }

    public IReadOnlyList<SublocationConnection> AllConnections => _allConnections;

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
        if (!_edges.TryGetValue(sublocationId, out var conns))
            return new List<Sublocation>();

        return conns
            .Where(c => _sublocations.ContainsKey(c.ToSublocationId))
            .Select(c => _sublocations[c.ToSublocationId])
            .ToList();
    }

    /// <summary>
    /// Finds the first connection with the given tag.
    /// Returns the connection and its ToSublocationId node (the "interior" side).
    /// </summary>
    public (SublocationConnection conn, Sublocation target)? FindConnectionByTag(string tag)
    {
        var conn = _allConnections.FirstOrDefault(c => c.HasTag(tag));
        if (conn == null)
            return null;

        _sublocations.TryGetValue(conn.ToSublocationId, out var target);
        return (conn, target);
    }

    public List<(SublocationConnection conn, Sublocation target)> FindAllConnectionsByTag(string tag)
    {
        return _allConnections
            .Where(c => c.HasTag(tag))
            .Select(c =>
            {
                _sublocations.TryGetValue(c.ToSublocationId, out var target);
                return (c, target);
            })
            .ToList();
    }

    /// <summary>
    /// Searches connection tags first, then sublocation tags.
    /// Returns (conn, target) where conn is null when found via sublocation tag.
    /// </summary>
    public (SublocationConnection conn, Sublocation target)? FindEntryPoint(string tag)
    {
        var byConn = FindConnectionByTag(tag);
        if (byConn != null)
            return byConn;

        var sub = FindByTag(tag);
        if (sub != null)
            return (null, sub);

        return null;
    }

    public SublocationConnection GetConnectionBetween(int fromId, int toId)
    {
        if (!_edges.TryGetValue(fromId, out var conns))
            return null;
        return conns.FirstOrDefault(c => c.ToSublocationId == toId);
    }

    public List<PathStep> FindPath(int fromId, int toId, TraversalContext context = null)
    {
        if (fromId == toId)
            return new List<PathStep>
            {
                new PathStep { Location = _sublocations[fromId], Via = null }
            };

        var visited = new HashSet<int> { fromId };
        var queue = new Queue<int>();
        var parentNode = new Dictionary<int, int>();
        var parentEdge = new Dictionary<int, SublocationConnection>();
        queue.Enqueue(fromId);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current == toId)
            {
                var path = new List<PathStep>();
                var node = toId;
                while (node != fromId)
                {
                    path.Add(new PathStep
                    {
                        Location = _sublocations[node],
                        Via = parentEdge[node]
                    });
                    node = parentNode[node];
                }
                path.Add(new PathStep { Location = _sublocations[fromId], Via = null });
                path.Reverse();
                return path;
            }

            if (!_edges.TryGetValue(current, out var conns))
                continue;

            foreach (var conn in conns)
            {
                var neighbor = conn.ToSublocationId;
                // Check visited FIRST — don't block the node permanently if
                // a different (unlocked) path could reach it later in BFS.
                if (visited.Contains(neighbor))
                    continue;
                // Now check traversal constraint for THIS edge.
                if (context != null && !context.CanTraverse(conn))
                    continue;
                visited.Add(neighbor);
                parentNode[neighbor] = current;
                parentEdge[neighbor] = conn;
                queue.Enqueue(neighbor);
            }
        }

        return new List<PathStep>();
    }

    public IReadOnlyDictionary<int, Sublocation> AllSublocations => _sublocations;
}
