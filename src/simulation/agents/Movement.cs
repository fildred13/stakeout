using System;
using System.Collections.Generic;
using System.Linq;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.History;
using Stakeout.Simulation.Space;

namespace Stakeout.Simulation.Agents;

/// <summary>
/// The only code that moves people and vehicles. Every change of whereabouts is written to History here,
/// so the truth cannot drift from what happened.
/// </summary>
public class Movement
{
    private readonly World _world;
    private SimulationState State => _world.State;

    public Movement(World world)
    {
        _world = world;
    }

    /// <summary>Puts an agent who is at an address into a room, doing something.</summary>
    public void Settle(Agent agent, int locationId, int? subLocationId, ActivityKind activity, string label = null)
    {
        if (agent.Trip != null) throw new InvalidOperationException($"{agent} is travelling");
        var now = _world.Now;
        if (agent.OpenPresence != null && agent.LocationId == locationId && agent.SubLocationId == subLocationId
            && agent.Activity == activity)
        {
            agent.ActivityLabel = label;
            return;
        }
        if (agent.OpenPresence != null) _world.History.ClosePresence(agent.OpenPresence, now);
        int addressId = agent.AddressId ?? State.Locations[locationId].AddressId;
        agent.AddressId = addressId;
        _world.SetLocation(agent, locationId);
        agent.SubLocationId = subLocationId;
        agent.Activity = activity;
        agent.ActivityLabel = label;
        agent.OpenPresence = _world.History.OpenPresence(new PresenceFact
        {
            PersonId = agent.Id,
            AddressId = addressId,
            LocationId = locationId,
            SubLocationId = subLocationId,
            Start = now,
            Activity = activity
        });
    }

    public void SetActivity(Agent agent, ActivityKind activity, string label = null) =>
        Settle(agent, agent.LocationId.Value, agent.SubLocationId, activity, label);

    /// <summary>The lobby people cross to get in and out, for buildings that have one.</summary>
    public Location LobbyOf(int addressId)
    {
        foreach (var id in State.Addresses[addressId].LocationIds)
        {
            var loc = State.Locations[id];
            if (loc.Name == "Lobby") return loc;
        }
        return null;
    }

    /// <summary>Where a newcomer stands before going further in: lobby, front yard, parking lot, entrance.</summary>
    public Location ArrivalLocation(int addressId)
    {
        var locations = State.GetLocationsForAddress(addressId);
        return LobbyOf(addressId)
               ?? locations.FirstOrDefault(l => l.HasTag("exterior") && l.HasTag("publicly_accessible"))
               ?? locations.FirstOrDefault(l => l.HasTag("entrance"))
               ?? locations.First();
    }

    /// <summary>
    /// Starts a trip for a group standing at the same address. With a vehicle, the first member drives and
    /// everyone rides inside it. Calls <paramref name="onArrive"/> when the group reaches the destination,
    /// with every member at the destination's arrival location.
    /// </summary>
    public TripFact StartTrip(IReadOnlyList<Agent> group, int? vehicleId, int toAddressId, Action<TripFact> onArrive)
    {
        var leader = group[0];
        if (leader.Trip != null || !leader.AddressId.HasValue)
            throw new InvalidOperationException($"{leader} cannot leave: not at an address");
        int fromAddressId = leader.AddressId.Value;
        foreach (var member in group)
            if (member.Trip != null || member.AddressId != fromAddressId)
                throw new InvalidOperationException($"{member} is not with {leader} at {fromAddressId}");

        var now = _world.Now;
        var depart = now;

        // Crossing the lobby on the way out takes a minute.
        var lobby = LobbyOf(fromAddressId);
        if (lobby != null && group.Any(m => m.LocationId != lobby.Id))
        {
            depart = now.AddMinutes(1);
            foreach (var member in group)
            {
                if (member.OpenPresence != null) _world.History.ClosePresence(member.OpenPresence, now);
                member.OpenPresence = null;
                _world.History.AddClosedPresence(new PresenceFact
                {
                    PersonId = member.Id, AddressId = fromAddressId, LocationId = lobby.Id,
                    Start = now, End = depart, Activity = ActivityKind.Travel
                });
            }
        }
        else
        {
            foreach (var member in group)
            {
                if (member.OpenPresence != null) _world.History.ClosePresence(member.OpenPresence, now);
                member.OpenPresence = null;
            }
        }

        VehicleRuntime vehicle = null;
        if (vehicleId.HasValue)
        {
            vehicle = _world.Vehicles[vehicleId.Value];
            if (vehicle.ParkedAddressId != fromAddressId)
                throw new InvalidOperationException($"Vehicle {vehicleId} is not at {fromAddressId}");
            _world.History.CloseParked(vehicle.OpenPark, depart);
            vehicle.OpenPark = null;
            vehicle.ParkedAddressId = null;
        }

        var mode = vehicleId.HasValue ? TravelMode.Drive : TravelMode.Walk;
        var trip = new TripFact
        {
            Id = _world.NextFactId(),
            VehicleId = vehicleId,
            LeaderId = leader.Id,
            PersonIds = group.Select(m => m.Id).ToArray(),
            Route = _world.Router.Between(fromAddressId, toAddressId, mode),
            Depart = depart,
            FromAddressId = fromAddressId,
            ToAddressId = toAddressId
        };
        _world.History.AddTrip(trip);
        if (vehicle != null) vehicle.Trip = trip;

        foreach (var member in group)
        {
            member.Trip = trip;
            member.RidingVehicleId = vehicleId;
            member.AddressId = null;
            _world.SetLocation(member, null);
            member.SubLocationId = null;
            member.Activity = ActivityKind.Travel;
            member.ActivityLabel = $"to {State.PlaceName(toAddressId)}";
        }

        var members = group.ToArray();
        _world.Scheduler.At(trip.Arrive, () => Arrive(trip, members, vehicle, onArrive));
        return trip;
    }

    private void Arrive(TripFact trip, Agent[] members, VehicleRuntime vehicle, Action<TripFact> onArrive)
    {
        var now = _world.Now;
        if (vehicle != null)
        {
            vehicle.Trip = null;
            vehicle.ParkedAddressId = trip.ToAddressId;
            vehicle.OpenPark = _world.History.OpenParked(new ParkedFact
            {
                VehicleId = vehicle.Vehicle.Id, AddressId = trip.ToAddressId, Start = now
            });
        }

        var arrival = ArrivalLocation(trip.ToAddressId);
        foreach (var member in members)
        {
            member.Trip = null;
            member.RidingVehicleId = null;
            member.AddressId = trip.ToAddressId;
            _world.SetLocation(member, arrival.Id);
            member.SubLocationId = null;
            member.Activity = ActivityKind.Waiting;
            member.ActivityLabel = "just arrived";
        }
        onArrive?.Invoke(trip);
        // Anyone the callback did not place stays where they arrived.
        foreach (var member in members)
            if (member.Trip == null && member.OpenPresence == null && member.AddressId == trip.ToAddressId)
                Settle(member, arrival.Id, null, ActivityKind.Waiting, "standing outside");
    }

    /// <summary>
    /// Moves an agent already at an address into one of its rooms. Going past a lobby takes a minute,
    /// spent in the lobby; <paramref name="then"/> runs once the agent is in the room.
    /// </summary>
    public void GoInside(Agent agent, int locationId, int? subLocationId, ActivityKind activity, string label,
        Action then = null)
    {
        var lobby = LobbyOf(agent.AddressId.Value);
        bool fromOutside = agent.OpenPresence == null || agent.Activity == ActivityKind.Waiting;
        if (lobby != null && fromOutside && locationId != lobby.Id)
        {
            Settle(agent, lobby.Id, null, ActivityKind.Travel, "crossing the lobby");
            _world.Scheduler.After(TimeSpan.FromMinutes(1), () =>
            {
                if (agent.Trip != null || agent.LocationId != lobby.Id || !agent.Person.IsAlive) return;
                Settle(agent, locationId, subLocationId, activity, label);
                then?.Invoke();
            });
            return;
        }
        Settle(agent, locationId, subLocationId, activity, label);
        then?.Invoke();
    }

    /// <summary>Parks a vehicle that is not moving at an address (world setup).</summary>
    public void Park(VehicleRuntime vehicle, int addressId)
    {
        if (vehicle.OpenPark != null) _world.History.CloseParked(vehicle.OpenPark, _world.Now);
        vehicle.ParkedAddressId = addressId;
        vehicle.OpenPark = _world.History.OpenParked(new ParkedFact
        {
            VehicleId = vehicle.Vehicle.Id, AddressId = addressId, Start = _world.Now
        });
    }

    /// <summary>The vehicle the agent can drive from where they stand, if any.</summary>
    public int? AvailableVehicle(Agent agent)
    {
        if (!agent.Person.VehicleId.HasValue || !agent.AddressId.HasValue) return null;
        var v = _world.Vehicles[agent.Person.VehicleId.Value];
        return v.ParkedAddressId == agent.AddressId ? v.Vehicle.Id : null;
    }

    /// <summary>Estimated door-to-door time, including lobbies.</summary>
    public TimeSpan TravelEstimate(int fromAddressId, int toAddressId, TravelMode mode)
    {
        if (fromAddressId == toAddressId) return TimeSpan.Zero;
        var seconds = _world.Router.Between(fromAddressId, toAddressId, mode).DurationSeconds;
        if (LobbyOf(fromAddressId) != null) seconds += 60;
        if (LobbyOf(toAddressId) != null) seconds += 60;
        return TimeSpan.FromSeconds(seconds);
    }

    public TravelMode ModeFor(Agent agent) => AvailableVehicle(agent).HasValue ? TravelMode.Drive : TravelMode.Walk;
}
