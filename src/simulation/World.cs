using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Stakeout.Simulation.Agents;
using Stakeout.Simulation.Core;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.History;
using Stakeout.Simulation.Space;

namespace Stakeout.Simulation;

/// <summary>
/// The running simulation: static world (State), clock (Scheduler), ground truth (History), road network,
/// and the runtime state of every person and vehicle. Time only moves through <see cref="AdvanceTo"/>.
/// </summary>
public class World
{
    public int Seed { get; }
    public int CityId { get; internal set; }
    public SimulationState State { get; }
    public Scheduler Scheduler { get; }
    public HistoryStore History { get; } = new();
    public RoadGraph Roads { get; internal set; }
    public Router Router { get; internal set; }
    public Dictionary<int, Agent> Agents { get; } = new();
    public Dictionary<int, VehicleRuntime> Vehicles { get; } = new();
    public List<JointActivity> Joints { get; } = new();

    public Movement Movement { get; }
    public Executor Executor { get; }
    public Planner Planner { get; }
    public Places Places { get; }
    public PhoneSystem Phones { get; }
    public SocialLife Social { get; }

    /// <summary>Hooks run when an agent settles into an appointment (scenarios, crime, police).</summary>
    public event Action<Agent, Appointment> Settled;

    private readonly Dictionary<int, HashSet<Agent>> _occupants = new();
    private int _nextFactId = 1;

    public World(int seed, SimulationState state, DateTime start)
    {
        Seed = seed;
        State = state;
        Scheduler = new Scheduler(start);
        Movement = new Movement(this);
        Executor = new Executor(this);
        Planner = new Planner(this);
        Places = new Places(this);
        Phones = new PhoneSystem(this);
        Social = new SocialLife(this);
    }

    public DateTime Now => Scheduler.Now;
    public int NextFactId() => _nextFactId++;

    public void AdvanceTo(DateTime time) => Scheduler.RunUntil(time);

    public Agent Agent(int personId) => Agents[personId];

    // ---- occupancy ----

    internal void SetLocation(Agent agent, int? locationId)
    {
        if (agent.LocationId == locationId) return;
        if (agent.LocationId.HasValue && _occupants.TryGetValue(agent.LocationId.Value, out var old)) old.Remove(agent);
        agent.LocationId = locationId;
        if (locationId.HasValue)
        {
            if (!_occupants.TryGetValue(locationId.Value, out var set)) _occupants[locationId.Value] = set = new HashSet<Agent>();
            set.Add(agent);
        }
    }

    /// <summary>People in a location right now, in a stable order.</summary>
    public IEnumerable<Agent> AgentsIn(int locationId) =>
        _occupants.TryGetValue(locationId, out var set) ? set.OrderBy(a => a.Id) : Enumerable.Empty<Agent>();

    public IEnumerable<Agent> AgentsAt(int addressId) =>
        State.Addresses[addressId].LocationIds.SelectMany(AgentsIn);

    internal void OnSettled(Agent agent, Appointment ap)
    {
        if (ap.Kind is ActivityKind.Home or ActivityKind.Sleep && agent.AddressId == agent.Person.HomeAddressId)
            Phones.CheckMachine(agent);
        Social.OnSettled(agent, ap);
        Settled?.Invoke(agent, ap);
    }

    // ---- positions for the city view ----

    public Vector2 PositionOf(Agent agent)
    {
        if (agent.Trip != null) return PositionOnTrip(agent.Trip);
        var address = State.Addresses[agent.AddressId ?? agent.Person.HomeAddressId];
        return address.Position + new Vector2(Address.CellSize / 2f, Address.CellSize / 2f);
    }

    public Vector2 PositionOnTrip(TripFact trip) =>
        trip.Route.PositionAt(Roads, (Now - trip.Depart).TotalSeconds);

    public Vector2 PositionOf(VehicleRuntime vehicle) =>
        vehicle.Trip != null ? PositionOnTrip(vehicle.Trip) : Roads.CellCenter(Roads.AccessCell(vehicle.ParkedAddressId.Value));

    /// <summary>Starts everyone's agenda. Call once after the world is built.</summary>
    public void Start()
    {
        foreach (var agent in Agents.Values.OrderBy(a => a.Id))
            if (!agent.ExternallyControlled) Executor.Replan(agent);
    }
}
