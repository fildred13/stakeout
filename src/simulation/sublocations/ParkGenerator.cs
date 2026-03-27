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

        Sublocation Make(string name, string[] tags, int floor)
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
        var parkingLot = Make("Parking Lot", new[] { "parking" }, 0);
        var mainEntrance = Make("Main Entrance", new[] { "entrance" }, 0);
        var sideGate = Make("Side Gate", new[] { "covert_entry" }, 0);
        var joggingPath = Make("Jogging Path", new[] { "outdoor" }, 0);
        var picnicArea = Make("Picnic Area", new[] { "food", "social" }, 0);
        var playground = Make("Playground", new[] { "outdoor", "social" }, 0);
        var woodedArea = Make("Wooded Area", new[] { "outdoor", "covert_entry" }, 0);
        var shoreLine = Make("Shore/Beach", new[] { "outdoor" }, 0);
        var restroomBuilding = Make("Restroom Building", new[] { "restroom" }, 0);

        Connect(road, parkingLot);
        Connect(parkingLot, mainEntrance);
        Connect(road, sideGate, ConnectionType.Gate);
        Connect(sideGate, joggingPath);
        Connect(mainEntrance, joggingPath);
        Connect(joggingPath, picnicArea, ConnectionType.OpenPassage);
        Connect(joggingPath, playground, ConnectionType.OpenPassage);
        Connect(joggingPath, woodedArea, ConnectionType.OpenPassage);
        Connect(joggingPath, shoreLine, ConnectionType.OpenPassage);
        Connect(picnicArea, playground);
        Connect(picnicArea, woodedArea);
        Connect(picnicArea, shoreLine);
        Connect(playground, woodedArea);
        Connect(playground, shoreLine);
        Connect(woodedArea, shoreLine);
        Connect(picnicArea, restroomBuilding, ConnectionType.Door);

        return new SublocationGraph(subs, conns);
    }
}
