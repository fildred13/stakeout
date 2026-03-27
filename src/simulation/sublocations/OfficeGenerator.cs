using System;
using System.Collections.Generic;
using Stakeout.Simulation.Entities;

namespace Stakeout.Simulation.Sublocations;

public class OfficeGenerator : ISublocationGenerator
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

        // Ground floor layout
        var road = Make("Road", new[] { "road" }, 0);
        var lobby = Make("Lobby", new[] { "entrance", "public" }, 0);
        var securityRoom = Make("Security Room", new[] { "security" }, 0);
        var elevator = Make("Elevator", new[] { "elevator" }, null);

        Connect(road, lobby, new SublocationConnection
        {
            Type = ConnectionType.Door,
            Name = "Front Door",
            Tags = new[] { "entrance" },
            Lockable = new LockableProperty { Mechanism = LockMechanism.Key },
            Breakable = new BreakableProperty()
        });
        Connect(lobby, securityRoom, new SublocationConnection { Type = ConnectionType.Door });
        Connect(lobby, elevator, new SublocationConnection
        {
            Type = ConnectionType.Door,
            Name = "Elevator Doors (Lobby)"
        });

        // Upper floors
        int floorCount = rng.Next(1, 6);
        Sublocation prevReception = lobby;

        for (int floor = 1; floor <= floorCount; floor++)
        {
            var reception = Make($"Floor {floor} Reception", new[] { "public" }, floor);
            var cubicleArea = Make($"Floor {floor} Cubicle Area", new[] { "work_area" }, floor);
            var managerOffice = Make($"Floor {floor} Manager Office", new[] { "work_area", "private" }, floor);
            var breakRoom = Make($"Floor {floor} Break Room", new[] { "food", "social" }, floor);
            var restroom = Make($"Floor {floor} Restroom", new[] { "restroom" }, floor);

            Connect(elevator, reception, new SublocationConnection
            {
                Type = ConnectionType.Door,
                Name = $"Elevator Doors (Floor {floor})"
            });
            Connect(prevReception, reception, new SublocationConnection
            {
                Type = ConnectionType.Stairs,
                Name = floor == 1 ? "Stairs (Lobby to Floor 1)" : $"Stairs (Floor {floor - 1} to {floor})"
            });
            Connect(reception, cubicleArea);
            Connect(cubicleArea, managerOffice, new SublocationConnection { Type = ConnectionType.Door });
            Connect(cubicleArea, breakRoom);
            Connect(reception, restroom, new SublocationConnection { Type = ConnectionType.Door });

            prevReception = reception;
        }

        return new SublocationGraph(subs, conns);
    }
}
