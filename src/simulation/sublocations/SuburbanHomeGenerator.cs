using System;
using System.Collections.Generic;
using Stakeout.Simulation.Entities;

namespace Stakeout.Simulation.Sublocations;

public class SuburbanHomeGenerator : ISublocationGenerator
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

        // Ground floor layout
        var road = Make("Road", new[] { "road" }, 0);
        var frontYard = Make("Front Yard", new[] { "yard", "front" }, 0);
        var hallway = Make("Ground Floor Hallway", new[] { "hallway" }, 0);
        var kitchen = Make("Kitchen", new[] { "kitchen", "food" }, 0);
        var livingRoom = Make("Living Room", new[] { "living", "social" }, 0);
        var groundBathroom = Make("Ground Floor Bathroom", new[] { "restroom" }, 0);
        var backyard = Make("Backyard", new[] { "yard", "back" }, 0);

        // Upstairs layout
        var upstairsHallway = Make("Upstairs Hallway", new[] { "hallway" }, 1);
        var upstairsBathroom = Make("Upstairs Bathroom", new[] { "restroom" }, 1);

        // Connect ground floor
        Connect(road, frontYard);
        Connect(frontYard, hallway, new SublocationConnection
        {
            Name = "Front Door",
            Tags = new[] { "entrance" },
            Type = ConnectionType.Door,
            Lockable = new LockableProperty(),
            Breakable = new BreakableProperty()
        });
        Connect(hallway, kitchen);
        Connect(hallway, livingRoom);
        Connect(hallway, groundBathroom);
        Connect(hallway, backyard, new SublocationConnection
        {
            Name = "Back Door",
            Tags = new[] { "covert_entry", "staff_entry" },
            Type = ConnectionType.Door,
            Lockable = new LockableProperty(),
            Breakable = new BreakableProperty()
        });
        Connect(backyard, road);
        Connect(frontYard, livingRoom, new SublocationConnection
        {
            Name = "Ground Floor Window",
            Tags = new[] { "covert_entry" },
            Type = ConnectionType.Window,
            Lockable = new LockableProperty(),
            Transparent = new TransparentProperty { CanSeeThrough = true },
            Breakable = new BreakableProperty()
        });

        // Connect stairs to upstairs
        Connect(hallway, upstairsHallway, new SublocationConnection
        {
            Name = "Stairs",
            Type = ConnectionType.Stairs
        });
        Connect(upstairsHallway, upstairsBathroom);

        // Bedrooms (2-3)
        int bedroomCount = rng.Next(2, 4);
        for (int i = 1; i <= bedroomCount; i++)
        {
            var bedroom = Make($"Bedroom {i}", new[] { "bedroom", "private" }, 1);
            Connect(upstairsHallway, bedroom);
        }

        return new SublocationGraph(subs, conns);
    }
}
