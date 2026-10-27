using System;
using System.Collections.Generic;
using System.Linq;
using Stakeout.Simulation.Fixtures;
using Stakeout.Simulation.History;
using Stakeout.Simulation.Observers;

namespace Stakeout.Investigation;

/// <summary>One thing seen during a stakeout.</summary>
public record StakeoutEntry(DateTime Time, string Text, int? PersonId, int? VehicleId, TripFact Departure);

public partial class Detective
{
    private int? _stakeoutAddress;
    private DateTime _stakeoutStart;
    public List<StakeoutEntry> StakeoutLog { get; } = new();
    public bool OnStakeout => _stakeoutAddress.HasValue;

    /// <summary>
    /// Sits outside an address and watches. While time runs, everyone and every car that comes or goes is
    /// logged; people the detective has met are recognised, cars by plate in daylight.
    /// </summary>
    public bool StartStakeout()
    {
        if (Travelling || !AddressId.HasValue) return false;
        StopStakeout();
        EndConversation();
        World.Movement.Settle(Agent, ArrivalLocation(AddressId.Value).Id, null, ActivityKind.Stakeout, "on a stakeout");
        _stakeoutAddress = AddressId;
        _stakeoutStart = Now;
        StakeoutLog.Clear();
        World.History.TripStarted += OnTripStarted;
        World.History.ParkStarted += OnParked;
        World.History.PresenceStarted += OnPresenceStarted;
        Say($"You settle in across the street from {State.PlaceName(AddressId.Value)}.");
        return true;
    }

    public Clue StopStakeout()
    {
        if (!_stakeoutAddress.HasValue) return null;
        int address = _stakeoutAddress.Value;
        _stakeoutAddress = null;
        World.History.TripStarted -= OnTripStarted;
        World.History.ParkStarted -= OnParked;
        World.History.PresenceStarted -= OnPresenceStarted;
        if (StakeoutLog.Count == 0) return null;
        var clue = AddClue(new Clue
        {
            Id = NextClueId(), Kind = ClueKind.Stakeout,
            Title = $"Stakeout at {State.PlaceName(address)}, {_stakeoutStart:ddd HH:mm}-{Now:HH:mm}",
            Text = string.Join("\n", StakeoutLog.Select(e => $"{e.Time:HH:mm} {e.Text}")), FoundAt = Now, Source = "Stakeout",
            TimeFrom = _stakeoutStart, TimeTo = Now
        });
        foreach (var e in StakeoutLog.Where(e => e.PersonId.HasValue && People.TryGetValue(e.PersonId.Value, out var k) && k.Met))
            if (!clue.NamedPersonIds.Contains(e.PersonId.Value)) clue.NamedPersonIds.Add(e.PersonId.Value);
        return clue;
    }

    private string Who(int personId) =>
        personId == Agent.Id ? "you" : People.TryGetValue(personId, out var k) && k.Met ? State.People[personId].FullName : Looks(personId);

    private string Car(int vehicleId, DateTime t)
    {
        var v = State.Vehicles[vehicleId];
        return Describe.IsDark(t) ? $"a {v.NightDescription}" : $"a {v.Color} {v.Make} {v.Model}, plate {v.Plate}";
    }

    private void OnTripStarted(TripFact trip)
    {
        if (trip.FromAddressId != _stakeoutAddress || trip.PersonIds.Contains(Agent.Id)) return;
        string who = string.Join(" and ", trip.PersonIds.Select(Who));
        bool one = trip.PersonIds.Length == 1;
        string how = trip.VehicleId.HasValue
            ? $"drive{(one ? "s" : "")} off in {Car(trip.VehicleId.Value, trip.Depart)}"
            : trip.Taxi ? $"get{(one ? "s" : "")} into a cab" : $"walk{(one ? "s" : "")} off";
        AddEntry($"{Cap(who)} {how}.", trip.LeaderId, trip.VehicleId, trip);
    }

    private void OnParked(ParkedFact parked)
    {
        if (parked.AddressId != _stakeoutAddress || parked.VehicleId == Me.VehicleId) return;
        AddEntry($"{Cap(Car(parked.VehicleId, parked.Start))} pulls up.", null, parked.VehicleId, null);
    }

    private void OnPresenceStarted(PresenceFact p)
    {
        if (p.AddressId != _stakeoutAddress || p.PersonId == Agent.Id) return;
        // From the street you only see people at the way in, and only when they come from outside.
        if (p.LocationId != ArrivalLocation(p.AddressId).Id) return;
        var prior = World.History.PresenceOf(p.PersonId);
        bool fromOutside = prior.Count < 2 || prior[^2].AddressId != p.AddressId || prior[^2].End < p.Start;
        if (!fromOutside) return;
        AddEntry($"{Cap(Who(p.PersonId))} goes in.", p.PersonId, null, null);
    }

    private void AddEntry(string text, int? personId, int? vehicleId, TripFact trip)
    {
        StakeoutLog.Add(new StakeoutEntry(Now, text, personId, vehicleId, trip));
        Message?.Invoke($"STAKEOUT {Now:HH:mm}: {text}");
    }

    private static string Cap(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s[1..];

    /// <summary>Follows someone who just drove off from the stakeout, staying a block behind.</summary>
    public TripFact Follow(StakeoutEntry entry, bool wait = true)
    {
        if (entry.Departure == null || !AddressId.HasValue) return null;
        var target = entry.Departure;
        StopStakeout();
        var trip = TravelTo(target.ToAddressId, wait: false);
        Say("You pull out after them, keeping your distance.");
        if (wait && trip != null) World.AdvanceTo(trip.Arrive);
        return trip;
    }

    // ---- cameras ----

    public IEnumerable<Fixture> CamerasHere() =>
        AddressId.HasValue
            ? State.GetLocationsForAddress(AddressId.Value).SelectMany(l => State.AllFixturesIn(l.Id)).Where(f => f.Type == FixtureType.Camera)
            : Enumerable.Empty<Fixture>();

    /// <summary>Watches a building's security tape: ask at the front desk while it's open, or let yourself into the security room.</summary>
    public Clue ViewFootage(int cameraId, DateTime from, DateTime to)
    {
        var camera = State.Fixtures[cameraId];
        var loc = State.Locations[camera.LocationId.Value];
        var business = State.BusinessAt(loc.AddressId);
        bool open = business != null ? Simulation.Agents.Planner.IsOpen(business, Now) : Now.Hour >= 8 && Now.Hour < 20;
        bool inSecurityRoom = LocationId.HasValue && State.Locations[LocationId.Value].HasTag("security");
        if (AddressId != loc.AddressId)
        {
            Say("You need to be there.");
            return null;
        }
        if (!open && !inSecurityRoom)
        {
            Say("Nobody at the desk. The tapes are in the security room.");
            return null;
        }
        if (!inSecurityRoom)
        {
            Say("The doorman runs the tape back for you, for a ten-dollar bill.");
            Cash -= 10;
        }
        var records = Cameras.Footage(camera, from, to, Now);
        Spend(TimeSpan.FromMinutes(Math.Clamp(records.Count / 3 + 15, 15, 60)));
        string where = State.PlaceName(loc.AddressId);
        if (Now - Cameras.Retention > from) Say("Anything older than three days has been taped over.");
        return AddClue(new Clue
        {
            Id = NextClueId(), Kind = ClueKind.Camera, Title = $"{camera.Name} at {where}, {from:ddd HH:mm}-{to:ddd HH:mm}",
            Text = records.Count == 0 ? "Nothing on the tape for that time." : string.Join("\n", records.Select(r => r.Text)),
            FoundAt = Now, Source = where, Footage = records, CameraAddressId = loc.AddressId, TimeFrom = from, TimeTo = to
        });
    }
}
