using System;
using System.Collections.Generic;
using Stakeout.Simulation.Entities;

namespace Stakeout.Simulation.Sublocations;

public class ApartmentBuildingGenerator : ISublocationGenerator
{
    public SublocationGraph Generate(Address address, SimulationState state, Random rng)
    {
        var subs = new Dictionary<int, Sublocation>();
        var conns = new List<SublocationConnection>();

        Sublocation Make(string name, string[] tags, int? floor)
        {
            var sub = new Sublocation
            {
                Id = state.GenerateEntityId(),
                AddressId = address.Id,
                Name = name,
                Tags = tags,
                Floor = floor
            };
            subs[sub.Id] = sub;
            address.Sublocations[sub.Id] = sub;
            return sub;
        }

        void Connect(Sublocation from, Sublocation to, SublocationConnection template = null)
        {
            var conn = template ?? new SublocationConnection();
            conn.Id = state.GenerateEntityId();
            conn.FromSublocationId = from.Id;
            conn.ToSublocationId = to.Id;
            conns.Add(conn);
            address.Connections.Add(conn);
        }

        var road = Make("Road", new[] { "road" }, 0);
        var lobby = Make("Lobby", new[] { "entrance", "public" }, 0);
        var elevator = Make("Elevator", new[] { "elevator" }, null);

        Connect(road, lobby, new SublocationConnection
        {
            Type = ConnectionType.Door,
            Name = "Front Door",
            Tags = new[] { "entrance" },
            Lockable = new LockableProperty { Mechanism = LockMechanism.Key },
            Breakable = new BreakableProperty()
        });
        Connect(lobby, elevator, new SublocationConnection
        {
            Type = ConnectionType.Door,
            Name = "Elevator Doors (Lobby)"
        });

        int floorCount = rng.Next(4, 21);
        Sublocation prevHallway = lobby;

        for (int n = 1; n <= floorCount; n++)
        {
            var floorHallway = Make($"Floor {n} Hallway", new[] { "hallway" }, n);

            Connect(elevator, floorHallway, new SublocationConnection
            {
                Type = ConnectionType.Door,
                Name = $"Elevator Doors (Floor {n})"
            });
            Connect(prevHallway, floorHallway, new SublocationConnection
            {
                Type = ConnectionType.Stairs,
                Name = n == 1 ? "Stairs (Lobby to Floor 1)" : $"Stairs (Floor {n - 1} to {n})"
            });

            int unitCount = rng.Next(4, 9);
            for (int i = 1; i <= unitCount; i++)
            {
                var unitTag = $"unit_f{n}_{i}";

                var bedroom = Make($"Apt {i} Bedroom", new[] { "bedroom", "private", unitTag }, n);
                var kitchen = Make($"Apt {i} Kitchen", new[] { "kitchen", "food", unitTag }, n);
                var living = Make($"Apt {i} Living Room", new[] { "living", "social", unitTag }, n);
                var bathroom = Make($"Apt {i} Bathroom", new[] { "restroom", unitTag }, n);

                Connect(floorHallway, living, new SublocationConnection { Type = ConnectionType.Door });
                Connect(living, bedroom);
                Connect(living, kitchen);
                Connect(living, bathroom);
            }

            prevHallway = floorHallway;
        }

        return new SublocationGraph(subs, conns);
    }
}
