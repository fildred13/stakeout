using System;
using System.Collections.Generic;
using Stakeout.Simulation.Entities;

namespace Stakeout.Simulation.Sublocations;

public class DinerGenerator : ISublocationGenerator
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
            conn.Fingerprints ??= new FingerprintSurface();
            conn.FromSublocationId = from.Id;
            conn.ToSublocationId = to.Id;
            conns.Add(conn);
            address.Connections.Add(conn);
        }

        var road = Make("Road", new[] { "road" }, 0);
        var diningArea = Make("Dining Area", new[] { "service_area", "social" }, 0);
        var counter = Make("Counter", new[] { "service_area" }, 0);
        var kitchen = Make("Kitchen", new[] { "work_area", "food" }, 0);
        var storage = Make("Storage", new[] { "storage" }, 0);
        var managerOffice = Make("Manager Office", new[] { "work_area", "private" }, 0);
        var restrooms = Make("Restrooms", new[] { "restroom" }, 0);

        Connect(road, diningArea, new SublocationConnection
        {
            Name = "Front Door",
            Tags = new[] { "entrance" },
            Type = ConnectionType.Door,
            Lockable = new LockableProperty(),
            Breakable = new BreakableProperty()
        });
        Connect(diningArea, counter);
        Connect(road, kitchen, new SublocationConnection
        {
            Name = "Back Door",
            Tags = new[] { "staff_entry" },
            Type = ConnectionType.Door,
            Lockable = new LockableProperty(),
            Breakable = new BreakableProperty()
        });
        Connect(kitchen, storage);
        Connect(kitchen, managerOffice, new SublocationConnection { Type = ConnectionType.Door });
        Connect(diningArea, restrooms, new SublocationConnection { Type = ConnectionType.Door });

        return new SublocationGraph(subs, conns);
    }
}
