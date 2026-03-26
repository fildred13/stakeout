using System;
using System.Collections.Generic;
using Stakeout.Simulation.Entities;

namespace Stakeout.Simulation.Sublocations;

public class SuburbanHomeGenerator : ISublocationGenerator
{
    public SublocationGraph Generate(int addressId, SimulationState state, Random rng)
    {
        var subs = new Dictionary<int, Sublocation>();
        var conns = new List<SublocationConnection>();

        Sublocation Make(string name, string[] tags, int floor)
        {
            var sub = new Sublocation
            {
                Id = state.GenerateEntityId(),
                AddressId = addressId,
                Name = name,
                Tags = tags,
                Floor = floor
            };
            subs[sub.Id] = sub;
            state.Sublocations[sub.Id] = sub;
            return sub;
        }

        void Connect(Sublocation from, Sublocation to, ConnectionType type = ConnectionType.OpenPassage)
        {
            var conn = new SublocationConnection
            {
                FromSublocationId = from.Id,
                ToSublocationId = to.Id,
                Type = type,
                IsBidirectional = true
            };
            conns.Add(conn);
            state.SublocationConnections.Add(conn);
        }

        // Ground floor layout
        var road = Make("Road", new[] { "road" }, 0);
        var frontYard = Make("Front Yard", new[] { "yard", "front" }, 0);
        var frontDoor = Make("Front Door", new[] { "entrance" }, 0);
        var hallway = Make("Ground Floor Hallway", new[] { "hallway" }, 0);
        var kitchen = Make("Kitchen", new[] { "kitchen", "food" }, 0);
        var livingRoom = Make("Living Room", new[] { "living", "social" }, 0);
        var groundBathroom = Make("Ground Floor Bathroom", new[] { "restroom" }, 0);
        var backDoor = Make("Back Door", new[] { "covert_entry", "staff_entry" }, 0);
        var backyard = Make("Backyard", new[] { "yard", "back" }, 0);
        var groundWindow = Make("Ground Floor Window", new[] { "covert_entry" }, 0);
        var stairs = Make("Stairs", new[] { "stairs" }, 0);

        // Upstairs layout
        var upstairsHallway = Make("Upstairs Hallway", new[] { "hallway" }, 1);
        var upstairsBathroom = Make("Upstairs Bathroom", new[] { "restroom" }, 1);

        // Connect ground floor
        Connect(road, frontYard);
        Connect(frontYard, frontDoor, ConnectionType.Door);
        Connect(frontDoor, hallway);
        Connect(hallway, kitchen);
        Connect(hallway, livingRoom);
        Connect(hallway, groundBathroom, ConnectionType.Door);
        Connect(hallway, backDoor, ConnectionType.Door);
        Connect(backDoor, backyard);
        Connect(backyard, road);
        Connect(frontYard, groundWindow, ConnectionType.Window);
        Connect(groundWindow, livingRoom);
        Connect(hallway, stairs);

        // Connect stairs to upstairs
        Connect(stairs, upstairsHallway, ConnectionType.Stairs);
        Connect(upstairsHallway, upstairsBathroom, ConnectionType.Door);

        // Bedrooms (2-3)
        int bedroomCount = rng.Next(2, 4);
        for (int i = 1; i <= bedroomCount; i++)
        {
            var bedroom = Make($"Bedroom {i}", new[] { "bedroom", "private" }, 1);
            Connect(upstairsHallway, bedroom, ConnectionType.Door);
        }

        return new SublocationGraph(subs, conns);
    }
}
