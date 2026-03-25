using System;
using System.Collections.Generic;
using Stakeout.Simulation.Entities;

namespace Stakeout.Simulation.Sublocations;

public class DinerGenerator : ISublocationGenerator
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

        var road = Make("Road", new[] { "road" }, 0);
        var frontDoor = Make("Front Door", new[] { "entrance" }, 0);
        var diningArea = Make("Dining Area", new[] { "service_area", "social" }, 0);
        var counter = Make("Counter", new[] { "service_area" }, 0);
        var backDoor = Make("Back Door", new[] { "staff_entry" }, 0);
        var kitchen = Make("Kitchen", new[] { "work_area", "food" }, 0);
        var storage = Make("Storage", new[] { "storage" }, 0);
        var managerOffice = Make("Manager Office", new[] { "work_area", "private" }, 0);
        var restrooms = Make("Restrooms", new[] { "restroom" }, 0);

        Connect(road, frontDoor, ConnectionType.Door);
        Connect(frontDoor, diningArea);
        Connect(diningArea, counter);
        Connect(road, backDoor, ConnectionType.Door);
        Connect(backDoor, kitchen);
        Connect(kitchen, storage);
        Connect(kitchen, managerOffice, ConnectionType.Door);
        Connect(diningArea, restrooms, ConnectionType.Door);

        return new SublocationGraph(subs, conns);
    }
}
