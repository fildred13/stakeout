using System;
using System.Collections.Generic;
using System.Linq;
using Stakeout.Simulation.Agents;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.History;

namespace Stakeout.Simulation.Diagnostics;

/// <summary>
/// Checks a world's history against the laws of the real world. Every violation is a bug in the simulation.
/// </summary>
public class InvariantChecker
{
    private readonly World _world;
    public List<string> Violations { get; } = new();
    public int MaxViolations { get; set; } = 50;

    public InvariantChecker(World world)
    {
        _world = world;
    }

    private void Fail(string message)
    {
        if (Violations.Count < MaxViolations) Violations.Add(message);
    }

    public List<string> CheckAll()
    {
        CheckPersonTimelines();
        CheckVehicleTimelines();
        CheckCalls();
        CheckDinerStaffing();
        CheckJointTrips();
        return Violations;
    }

    private string Name(int personId) => $"{_world.State.People[personId].FullName}#{personId}";

    /// <summary>Each person is in one place at a time, and moves only by trips that start where they were.</summary>
    public void CheckPersonTimelines()
    {
        var now = _world.Now;
        foreach (var person in _world.State.People.Values.OrderBy(p => p.Id))
        {
            var items = new List<(DateTime Start, DateTime End, int FromAddress, int ToAddress, string What)>();
            foreach (var p in _world.History.PresenceOf(person.Id))
                items.Add((p.Start, p.EndOr(now), p.AddressId, p.AddressId, $"in {p.LocationId} ({p.Activity})"));
            foreach (var t in _world.History.TripsOf(person.Id))
                items.Add((t.Depart, t.Arrive, t.FromAddressId, t.ToAddressId, $"trip {t.Id}"));
            items.Sort((a, b) => a.Start != b.Start ? a.Start.CompareTo(b.Start) : a.End.CompareTo(b.End));

            for (int i = 1; i < items.Count; i++)
            {
                var prev = items[i - 1];
                var cur = items[i];
                if (cur.Start < prev.End)
                    Fail($"{Name(person.Id)} is in two places: {prev.What} {prev.Start:ddd HH:mm:ss}-{prev.End:HH:mm:ss} and {cur.What} from {cur.Start:ddd HH:mm:ss}");
                if (cur.FromAddress != prev.ToAddress)
                    Fail($"{Name(person.Id)} teleported from address {prev.ToAddress} to {cur.FromAddress} at {cur.Start:ddd HH:mm:ss} ({prev.What} -> {cur.What})");
                if (cur.Start > prev.End.AddSeconds(1) && !_world.Agents[person.Id].ExternallyControlled)
                    Fail($"{Name(person.Id)} is nowhere between {prev.End:ddd HH:mm:ss} and {cur.Start:HH:mm:ss}");
            }
        }
    }

    /// <summary>A car is parked in one place or driving; it leaves from where it was parked.</summary>
    public void CheckVehicleTimelines()
    {
        var now = _world.Now;
        var tripsByVehicle = _world.History.Trips.Where(t => t.VehicleId.HasValue).GroupBy(t => t.VehicleId.Value)
            .ToDictionary(g => g.Key, g => g.ToList());
        foreach (var vehicle in _world.State.Vehicles.Values.OrderBy(v => v.Id))
        {
            var items = new List<(DateTime Start, DateTime End, int From, int To, string What)>();
            foreach (var p in _world.History.ParkedOf(vehicle.Id))
                items.Add((p.Start, p.End ?? now, p.AddressId, p.AddressId, "parked"));
            if (tripsByVehicle.TryGetValue(vehicle.Id, out var trips))
                foreach (var t in trips)
                {
                    items.Add((t.Depart, t.Arrive, t.FromAddressId, t.ToAddressId, $"trip {t.Id}"));
                    var driver = _world.State.People[t.LeaderId];
                    if (t.PersonIds[0] != t.LeaderId) Fail($"Trip {t.Id}: leader is not the first occupant");
                    _ = driver;
                }
            items.Sort((a, b) => a.Start != b.Start ? a.Start.CompareTo(b.Start) : a.End.CompareTo(b.End));
            for (int i = 1; i < items.Count; i++)
            {
                if (items[i].Start < items[i - 1].End)
                    Fail($"Vehicle {vehicle.Id} overlaps: {items[i - 1].What} and {items[i].What} at {items[i].Start:ddd HH:mm}");
                if (items[i].From != items[i - 1].To)
                    Fail($"Vehicle {vehicle.Id} teleported to {items[i].From} at {items[i].Start:ddd HH:mm}");
            }
        }
    }

    /// <summary>An answered phone was answered by someone awake at that phone.</summary>
    public void CheckCalls()
    {
        foreach (var call in _world.History.Calls)
        {
            if (call.Outcome == CallOutcome.Emergency) continue;
            var phone = _world.State.Fixtures[call.ToPhoneId];
            int location = _world.Phones.PhoneLocation(phone);
            if (call.Outcome == CallOutcome.Answered)
            {
                var p = PresenceAround(call.AnsweredById.Value, call.Time, location);
                if (p == null)
                    Fail($"Call {call.Id} answered by {Name(call.AnsweredById.Value)} who was not at the phone");
                else if (p.Activity == ActivityKind.Sleep)
                    Fail($"Call {call.Id} answered by {Name(call.AnsweredById.Value)} in their sleep");
            }
            var from = _world.State.Fixtures[call.FromPhoneId];
            if (PresenceAround(call.CallerId, call.Time, _world.Phones.PhoneLocation(from)) == null)
                Fail($"Call {call.Id}: caller {Name(call.CallerId)} was not at the phone they called from");
        }
    }

    /// <summary>Presence at an instant, counting one that ends exactly then (events at the same instant).</summary>
    private PresenceFact PresenceAround(int personId, DateTime time, int locationId)
    {
        var p = _world.History.PresenceOfAt(personId, time);
        if (p?.LocationId == locationId) return p;
        p = _world.History.PresenceOfAt(personId, time.AddTicks(-1));
        return p?.LocationId == locationId ? p : null;
    }

    /// <summary>A diner that is open has at least one member of staff on site.</summary>
    public void CheckDinerStaffing()
    {
        var start = _world.History.Trips.Count > 0 ? _world.History.Trips[0].Depart.Date.AddDays(1) : _world.Now;
        foreach (var business in _world.State.Businesses.Values.Where(b => b.Type == BusinessType.Diner).OrderBy(b => b.Id))
        {
            var staff = business.Positions.Where(p => p.AssignedPersonId.HasValue).Select(p => p.AssignedPersonId.Value).ToHashSet();
            for (var t = start.AddHours(6); t < _world.Now; t = t.AddHours(5))
            {
                bool someone = _world.History.PresenceAt(business.AddressId, t, t.AddSeconds(1)).Any(p => staff.Contains(p.PersonId));
                if (!someone) Fail($"{business.Name} is open at {t:ddd HH:mm} with no staff on site");
            }
        }
    }

    /// <summary>People on a date ride to the venue in the same car.</summary>
    public void CheckJointTrips()
    {
        foreach (var date in _world.Joints.OfType<DateActivity>().Where(d => d.Finished && !d.Aborted))
        {
            var trips = _world.History.TripsOf(date.DriverId)
                .Where(t => t.Depart >= date.Start.AddMinutes(-1) && t.ToAddressId == date.VenueAddressId).ToList();
            if (trips.Count == 0)
            {
                Fail($"Date {date.Id}: the driver never went to the venue");
                continue;
            }
            if (!trips[0].PersonIds.Contains(date.PassengerId))
                Fail($"Date {date.Id}: {Name(date.PassengerId)} did not ride with {Name(date.DriverId)}");
        }
    }
}
