using System;
using System.Collections.Generic;
using Stakeout.Simulation.Entities;

namespace Stakeout.Simulation.Sublocations;

public class DiveBarGenerator : ISublocationGenerator
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
        var barArea = Make("Bar Area", new[] { "service_area", "social" }, 0);
        var alley = Make("Alley", new[] { "covert_entry" }, 0);
        var backHallway = Make("Back Hallway", new[] { "hallway" }, 0);
        var storage = Make("Storage", new[] { "storage" }, 0);
        var managerOffice = Make("Manager Office", new[] { "work_area", "private" }, 0);
        var restrooms = Make("Restrooms", new[] { "restroom" }, 0);

        Connect(road, barArea, new SublocationConnection
        {
            Name = "Front Door",
            Tags = new[] { "entrance" },
            Type = ConnectionType.Door,
            Lockable = new LockableProperty { Mechanism = LockMechanism.Key },
            Breakable = new BreakableProperty()
        });
        Connect(road, backHallway, new SublocationConnection
        {
            Name = "Back Door",
            Tags = new[] { "staff_entry" },
            Type = ConnectionType.Door,
            Lockable = new LockableProperty { Mechanism = LockMechanism.Key },
            Breakable = new BreakableProperty()
        });
        Connect(road, alley);
        Connect(alley, backHallway);
        Connect(backHallway, storage);
        Connect(backHallway, managerOffice, new SublocationConnection { Type = ConnectionType.Door });
        Connect(barArea, restrooms, new SublocationConnection { Type = ConnectionType.Door });

        return new SublocationGraph(subs, conns);
    }
}
