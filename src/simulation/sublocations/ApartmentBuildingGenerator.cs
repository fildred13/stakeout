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

        Sublocation Make(string name, string[] tags, int floor, bool isGenerated = true)
        {
            var sub = new Sublocation
            {
                Id = state.GenerateEntityId(),
                AddressId = address.Id,
                Name = name,
                Tags = tags,
                Floor = floor,
                IsGenerated = isGenerated
            };
            subs[sub.Id] = sub;
            address.Sublocations[sub.Id] = sub;
            return sub;
        }

        void Connect(Sublocation from, Sublocation to, ConnectionType type = ConnectionType.OpenPassage)
        {
            var conn = new SublocationConnection
            {
                FromSublocationId = from.Id,
                ToSublocationId = to.Id,
                Type = type
            };
            conns.Add(conn);
            address.Connections.Add(conn);
        }

        var road = Make("Road", new[] { "road" }, 0);
        var lobby = Make("Lobby", new[] { "entrance", "public" }, 0);
        var elevator = Make("Elevator", new[] { "elevator" }, 0);
        var stairwell = Make("Stairwell", new[] { "stairs" }, 0);

        Connect(road, lobby, ConnectionType.Door);
        Connect(lobby, elevator, ConnectionType.Door);
        Connect(lobby, stairwell, ConnectionType.Stairs);

        int floorCount = rng.Next(4, 21);
        Sublocation prevFloorElevator = elevator;
        Sublocation prevFloorStairwell = stairwell;

        for (int n = 1; n <= floorCount; n++)
        {
            var floorPlaceholder = Make($"Floor {n}", new[] { "floor_placeholder" }, n, isGenerated: false);
            floorPlaceholder.ParentId = null;

            var floorElevator = Make($"Floor {n} Elevator", new[] { "elevator" }, n);
            var floorStairwell = Make($"Floor {n} Stairwell", new[] { "stairs" }, n);

            Connect(prevFloorElevator, floorElevator, ConnectionType.Door);
            Connect(prevFloorStairwell, floorStairwell, ConnectionType.Stairs);

            prevFloorElevator = floorElevator;
            prevFloorStairwell = floorStairwell;
        }

        return new SublocationGraph(subs, conns);
    }

    public static SublocationGraph ExpandFloor(Sublocation floorPlaceholder, SimulationState state, Random rng)
    {
        var subs = new Dictionary<int, Sublocation>();
        var conns = new List<SublocationConnection>();
        var address = state.Addresses[floorPlaceholder.AddressId];
        int floor = floorPlaceholder.Floor ?? 1;

        Sublocation Make(string name, string[] tags)
        {
            var sub = new Sublocation
            {
                Id = state.GenerateEntityId(),
                AddressId = address.Id,
                Name = name,
                Tags = tags,
                Floor = floor,
                ParentId = floorPlaceholder.Id
            };
            subs[sub.Id] = sub;
            address.Sublocations[sub.Id] = sub;
            return sub;
        }

        void Connect(Sublocation from, Sublocation to, ConnectionType type = ConnectionType.Door)
        {
            var conn = new SublocationConnection
            {
                FromSublocationId = from.Id,
                ToSublocationId = to.Id,
                Type = type
            };
            conns.Add(conn);
            address.Connections.Add(conn);
        }

        var hallway = Make($"Floor {floor} Hallway", new[] { "hallway" });

        int unitCount = rng.Next(4, 9);
        for (int i = 1; i <= unitCount; i++)
        {
            var bedroom = Make($"Apt {i} Bedroom", new[] { "bedroom", "private" });
            var kitchen = Make($"Apt {i} Kitchen", new[] { "kitchen", "food" });
            var living = Make($"Apt {i} Living Room", new[] { "living", "social" });
            var bathroom = Make($"Apt {i} Bathroom", new[] { "restroom" });

            Connect(hallway, living);
            Connect(living, bedroom);
            Connect(living, kitchen);
            Connect(living, bathroom);
        }

        floorPlaceholder.IsGenerated = true;

        return new SublocationGraph(subs, conns);
    }
}
