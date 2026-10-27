using System;
using Stakeout.Simulation.Space;

namespace Stakeout.Simulation.History;

/// <summary>What a person is doing during a presence interval. Observers use it (asleep people see nothing).</summary>
public enum ActivityKind
{
    Idle, Sleep, Home, Work, Dine, Drink, Walk, Visit, Host, Date, Waiting, Travel, Phone,
    Crime, Dead, Investigate, Search, Stakeout, Discover
}

/// <summary>A person was in one room (location, optional sublocation) of an address from Start to End.</summary>
public sealed class PresenceFact
{
    public int PersonId { get; init; }
    public int AddressId { get; init; }
    public int LocationId { get; init; }
    public int? SubLocationId { get; init; }
    public DateTime Start { get; init; }
    public DateTime? End { get; internal set; }
    public ActivityKind Activity { get; init; }

    public bool Overlaps(DateTime from, DateTime to) => Start < to && (End ?? DateTime.MaxValue) > from;
    public DateTime EndOr(DateTime now) => End ?? now;
}

/// <summary>A vehicle stood at an address (its access cell) from Start to End.</summary>
public sealed class ParkedFact
{
    public int VehicleId { get; init; }
    public int AddressId { get; init; }
    public DateTime Start { get; init; }
    public DateTime? End { get; internal set; }
    public bool Overlaps(DateTime from, DateTime to) => Start < to && (End ?? DateTime.MaxValue) > from;
}

/// <summary>
/// One movement along the roads: a vehicle with its driver and passengers, or people walking together.
/// Everyone in a trip is at the same place at the same time by construction.
/// </summary>
public sealed class TripFact
{
    public int Id { get; init; }
    public int? VehicleId { get; init; }
    public int LeaderId { get; init; }
    public int[] PersonIds { get; init; }
    public Route Route { get; init; }
    public DateTime Depart { get; init; }
    public DateTime Arrive => Depart.AddSeconds(Route.DurationSeconds);
    public int FromAddressId { get; init; }
    public int ToAddressId { get; init; }

    public bool Overlaps(DateTime from, DateTime to) => Depart < to && Arrive > from;
    public bool Taxi => Route.Mode == TravelMode.Taxi;

    /// <summary>When the trip passes <paramref name="cell"/>, or null.</summary>
    public DateTime? TimeAt(int cell)
    {
        int offset = Route.OffsetAt(cell);
        return offset < 0 ? null : Depart.AddSeconds(offset);
    }
}

public enum CallOutcome { Answered, AnsweringMachine, NoAnswer, Emergency }

/// <summary>A telephone call between two phone fixtures. The phone company keeps the record either way.</summary>
public sealed class CallFact
{
    public int Id { get; init; }
    public int CallerId { get; init; }
    public int FromPhoneId { get; init; }
    public int ToPhoneId { get; init; }
    public DateTime Time { get; init; }
    public int DurationSeconds { get; init; }
    public CallOutcome Outcome { get; init; }
    public int? AnsweredById { get; init; }
    /// <summary>Who the caller wanted to reach.</summary>
    public int IntendedId { get; init; }
    /// <summary>What was said, in the caller's words (wiretaps and tapes reveal it).</summary>
    public string Transcript { get; init; }
    public bool Heard { get; internal set; }
    public DateTime? ErasedAt { get; internal set; }
}

/// <summary>A person touched a surface (door handle, fixture, item). Fingerprints come from these.</summary>
public sealed class TouchFact
{
    public int PersonId { get; init; }
    public int SurfaceId { get; init; }
    public DateTime Time { get; init; }
    public bool Gloved { get; init; }
}

/// <summary>Someone wiped a surface clean of prints.</summary>
public sealed class WipeFact
{
    public int PersonId { get; init; }
    public int SurfaceId { get; init; }
    public DateTime Time { get; init; }
}

public enum NoiseKind { Argument, Scream, Gunshot, BreakingGlass, Crash }

public sealed class NoiseFact
{
    public int AddressId { get; init; }
    public int LocationId { get; init; }
    public DateTime Time { get; init; }
    public NoiseKind Kind { get; init; }
    public int SourcePersonId { get; init; }
}

public enum WeaponKind { Knife, BluntObject, Handgun }

public sealed class DeathFact
{
    public int VictimId { get; init; }
    public int KillerId { get; init; }
    public DateTime Time { get; init; }
    public int AddressId { get; init; }
    public int LocationId { get; init; }
    public int? SubLocationId { get; init; }
    public WeaponKind Weapon { get; init; }
    public int? WeaponItemId { get; init; }
}

public sealed class ForcedEntryFact
{
    public int PersonId { get; init; }
    public int AccessPointId { get; init; }
    public int LocationId { get; init; }
    public DateTime Time { get; init; }
    public bool Picked { get; init; }
}

/// <summary>An item changed hands or place. Holder is a person, a fixture, or a location.</summary>
public sealed class ItemMovedFact
{
    public int ItemId { get; init; }
    public int? ByPersonId { get; init; }
    public DateTime Time { get; init; }
    public string ToDescription { get; init; }
}

public sealed class PurchaseFact
{
    public int PersonId { get; init; }
    public int BusinessId { get; init; }
    public DateTime Time { get; init; }
    public int AmountCents { get; init; }
    public bool Cash { get; init; }
}
