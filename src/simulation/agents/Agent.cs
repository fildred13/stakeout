using System;
using System.Collections.Generic;
using Stakeout.Simulation.Core;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.History;

namespace Stakeout.Simulation.Agents;

/// <summary>
/// The runtime state of one person. A person is always in exactly one of: a room of an address
/// (AddressId/LocationId set, Trip null) or a trip (walking, or riding in a vehicle).
/// </summary>
public class Agent
{
    public Person Person { get; }
    public int Id => Person.Id;

    // Whereabouts
    public int? AddressId { get; internal set; }
    public int? LocationId { get; internal set; }
    public int? SubLocationId { get; internal set; }
    public TripFact Trip { get; internal set; }
    public int? RidingVehicleId { get; internal set; }
    public ActivityKind Activity { get; internal set; }
    public string ActivityLabel { get; internal set; }
    internal PresenceFact OpenPresence;

    // Planning
    public List<Appointment> Plan { get; internal set; } = new();
    public int PlanIndex { get; internal set; }
    internal EventHandle Pending;
    public JointActivity HeldBy { get; internal set; }
    public List<JointActivity> Commitments { get; } = new();
    /// <summary>Extra appointments a scenario wants in the next plan (e.g. dump a weapon on the way home).</summary>
    public List<Appointment> Injected { get; } = new();
    /// <summary>When set, the planner is bypassed (the player, the dead).</summary>
    public bool ExternallyControlled { get; set; }
    /// <summary>Set when the agenda changed while travelling; the agent replans on arrival.</summary>
    internal bool ReplanOnArrival;

    public Agent(Person person)
    {
        Person = person;
    }

    public bool InTransit => Trip != null;
    public bool Asleep => Activity == ActivityKind.Sleep;
    public bool IsAt(int addressId) => Trip == null && AddressId == addressId;
    public Appointment CurrentAppointment => PlanIndex < Plan.Count ? Plan[PlanIndex] : null;

    public override string ToString() => $"{Person.FullName} ({Activity} {ActivityLabel})";
}

/// <summary>Where in an address a person settles for an appointment.</summary>
public enum Spot { Entrance, HomeLiving, HomeBedroom, HomeKitchen, Workplace, Customer, Outdoors, GuestOf, Room }

/// <summary>One entry of a person's agenda: be at a place from ArriveBy until LeaveAt doing Kind.</summary>
public class Appointment
{
    public int AddressId { get; init; }
    public Spot Spot { get; init; }
    public DateTime ArriveBy { get; init; }
    public DateTime LeaveAt { get; init; }
    public ActivityKind Kind { get; init; }
    public string Label { get; init; }
    public JointActivity Joint { get; init; }
    /// <summary>For Spot.Room: the exact location.</summary>
    public int? LocationId { get; init; }
    public int? SubLocationId { get; init; }
    /// <summary>Runs once when the person has settled at the place.</summary>
    public Action<Agent> OnArrive { get; init; }
    /// <summary>Optional: appointments the person can skip if they are late.</summary>
    public bool Optional { get; init; }

    public override string ToString() => $"{ArriveBy:ddd HH:mm}-{LeaveAt:HH:mm} {Kind} {Label}";
}

/// <summary>Runtime state of a vehicle: parked at an address, or moving in a trip.</summary>
public class VehicleRuntime
{
    public Vehicle Vehicle { get; }
    public int? ParkedAddressId { get; internal set; }
    public TripFact Trip { get; internal set; }
    internal ParkedFact OpenPark;

    public VehicleRuntime(Vehicle vehicle)
    {
        Vehicle = vehicle;
    }
}
