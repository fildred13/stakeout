using System;
using System.Collections.Generic;
using Stakeout.Simulation.Entities;

namespace Stakeout.Simulation.Sublocations;

public class ParkGenerator : ISublocationGenerator
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
        var parkingLot = Make("Parking Lot", new[] { "parking" }, 0);
        var joggingPath = Make("Jogging Path", new[] { "outdoor" }, 0);
        var picnicArea = Make("Picnic Area", new[] { "food", "social" }, 0);
        var playground = Make("Playground", new[] { "outdoor", "social" }, 0);
        var woodedArea = Make("Wooded Area", new[] { "outdoor", "covert_entry" }, 0);
        var shoreLine = Make("Shore/Beach", new[] { "outdoor" }, 0);
        var restroomBuilding = Make("Restroom Building", new[] { "restroom" }, 0);

        Connect(road, parkingLot);
        Connect(parkingLot, joggingPath, new SublocationConnection
        {
            Name = "Main Entrance",
            Tags = new[] { "entrance" },
            Type = ConnectionType.Gate
        });
        Connect(road, joggingPath, new SublocationConnection
        {
            Name = "Side Gate",
            Tags = new[] { "covert_entry" },
            Type = ConnectionType.Gate
        });
        Connect(joggingPath, picnicArea);
        Connect(joggingPath, playground);
        Connect(joggingPath, woodedArea);
        Connect(joggingPath, shoreLine);
        Connect(picnicArea, playground);
        Connect(picnicArea, woodedArea);
        Connect(picnicArea, shoreLine);
        Connect(playground, woodedArea);
        Connect(playground, shoreLine);
        Connect(woodedArea, shoreLine);
        Connect(picnicArea, restroomBuilding, new SublocationConnection { Type = ConnectionType.Door });

        return new SublocationGraph(subs, conns);
    }
}
