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
                Type = type,
                IsBidirectional = true
            };
            conns.Add(conn);
            address.Connections.Add(conn);
        }

        // Ground floor layout
        var road = Make("Road", new[] { "road" }, 0);
        var lobby = Make("Lobby", new[] { "entrance", "public" }, 0);
        var securityRoom = Make("Security Room", new[] { "security" }, 0);
        var elevator = Make("Elevator", new[] { "elevator" }, 0);
        var stairwell = Make("Stairwell", new[] { "stairwell" }, 0);

        Connect(road, lobby, ConnectionType.Door);
        Connect(lobby, securityRoom, ConnectionType.Door);
        Connect(lobby, elevator, ConnectionType.Elevator);
        Connect(lobby, stairwell, ConnectionType.Stairs);

        // Upper floors
        int floorCount = rng.Next(1, 6);
        Sublocation prevElevator = elevator;
        Sublocation prevStairwell = stairwell;

        for (int floor = 1; floor <= floorCount; floor++)
        {
            var floorElevator = Make($"Floor {floor} Elevator", new[] { "elevator" }, floor);
            var floorStairwell = Make($"Floor {floor} Stairwell", new[] { "stairwell" }, floor);
            var reception = Make($"Floor {floor} Reception", new[] { "public" }, floor);
            var cubicleArea = Make($"Floor {floor} Cubicle Area", new[] { "work_area" }, floor);
            var managerOffice = Make($"Floor {floor} Manager Office", new[] { "work_area", "private" }, floor);
            var breakRoom = Make($"Floor {floor} Break Room", new[] { "food", "social" }, floor);
            var restroom = Make($"Floor {floor} Restroom", new[] { "restroom" }, floor);

            Connect(prevElevator, floorElevator, ConnectionType.Elevator);
            Connect(prevStairwell, floorStairwell, ConnectionType.Stairs);
            Connect(floorElevator, reception);
            Connect(reception, cubicleArea);
            Connect(cubicleArea, managerOffice, ConnectionType.Door);
            Connect(cubicleArea, breakRoom);
            Connect(reception, restroom, ConnectionType.Door);

            prevElevator = floorElevator;
            prevStairwell = floorStairwell;
        }

        return new SublocationGraph(subs, conns);
    }
}
