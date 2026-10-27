using System;
using System.Collections.Generic;
using System.Linq;
using Stakeout.Simulation.Core;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.History;

namespace Stakeout.Simulation.Observers;

public enum SightingKind { Person, Vehicle, Noise }

/// <summary>Something a witness remembers. True fields (PersonId, VehicleId, TrueTime) are for judging, not display.</summary>
public class Sighting
{
    public SightingKind Kind { get; init; }
    public int WitnessId { get; init; }
    public DateTime TrueTime { get; init; }
    public string When { get; init; }
    public int? PersonId { get; init; }
    public bool KnownByName { get; init; }
    public PersonDescription Description { get; init; }
    public int? VehicleId { get; init; }
    public string VehicleText { get; init; }
    public NoiseKind? Noise { get; init; }
    public int AddressId { get; init; }
    public string Where { get; init; }
    public string Text { get; init; }
}

/// <summary>
/// What a person could have noticed, computed from History when they are asked: people they were in the same
/// room or lobby with, people knocking on a door down the hall, cars parked outside, noises through the walls.
/// Each memory is a keyed coin flip (asking twice gives the same answer), fades with time, and asleep people
/// notice nothing but loud noises.
/// </summary>
public class WitnessMemory
{
    private readonly World _world;
    private SimulationState State => _world.State;

    public WitnessMemory(World world)
    {
        _world = world;
    }

    private double Fade(DateTime when, DateTime askedAt) => Math.Exp(-Math.Max(0, (askedAt - when).TotalDays) / 14.0);

    private bool Roll(double p, int witness, long a, long b) => Rng.Chance(p, _world.Seed, Purpose.Witness, witness, a, b);

    public bool KnowsByName(int witnessId, int personId)
    {
        if (witnessId == personId) return true;
        var w = State.People[witnessId];
        var p = State.People[personId];
        if (State.GetRelationship(witnessId, personId) != null) return true;
        if (w.HouseholdId == p.HouseholdId) return true;
        if (w.BusinessId.HasValue && w.BusinessId == p.BusinessId) return true;
        if (w.HomeAddressId == p.HomeAddressId) return Rng.Chance(0.45, _world.Seed, Purpose.Witness, Math.Min(witnessId, personId), Math.Max(witnessId, personId));
        // Regulars are known by the staff where they drink and eat.
        if (w.BusinessId.HasValue)
        {
            var biz = State.Businesses[w.BusinessId.Value];
            if (p.FavoriteBarIds.Take(1).Contains(biz.AddressId) || p.FavoriteDinerIds.Take(1).Contains(biz.AddressId))
                return Rng.Chance(0.5, _world.Seed, Purpose.Witness, witnessId, personId);
        }
        return false;
    }

    private static int Floor(Location l) => l.Floor ?? 0;

    /// <summary>Everything the witness can tell about an address during a time window.</summary>
    public List<Sighting> Recall(int witnessId, int addressId, DateTime from, DateTime to, DateTime askedAt)
    {
        var result = new List<Sighting>();
        var witness = State.People[witnessId];
        var address = State.Addresses[addressId];
        var history = _world.History;
        bool observant = witness.Has(Traits.Observant);

        var myPresence = history.PresenceOf(witnessId, from.AddHours(-1), to.AddHours(1)).ToList();
        PresenceFact AwakeAt(DateTime t) => myPresence.FirstOrDefault(p => p.Start <= t && p.EndOr(askedAt) > t);

        bool NearEnough(PresenceFact p)
        {
            if (p == null) return false;
            if (p.AddressId == addressId) return true;
            var other = State.Addresses[p.AddressId];
            return other.Type == AddressType.SuburbanHome && address.Type == AddressType.SuburbanHome
                   && Math.Abs(other.GridX - address.GridX) + Math.Abs(other.GridY - address.GridY) <= 2;
        }

        // 1. People in the same room or lobby as the witness.
        foreach (var mine in myPresence.Where(p => p.AddressId == addressId && p.Activity != ActivityKind.Sleep))
        {
            var start = mine.Start > from ? mine.Start : from;
            var end = mine.EndOr(askedAt) < to ? mine.EndOr(askedAt) : to;
            if (end <= start) continue;
            foreach (var other in history.PresenceAt(addressId, start, end))
            {
                if (other.PersonId == witnessId || other.LocationId != mine.LocationId) continue;
                if (other.Activity is ActivityKind.Dead) continue;
                var overlapStart = other.Start > start ? other.Start : start;
                var overlapEnd = other.EndOr(askedAt) < end ? other.EndOr(askedAt) : end;
                double minutes = (overlapEnd - overlapStart).TotalMinutes;
                if (minutes <= 0) continue;
                bool known = KnowsByName(witnessId, other.PersonId);
                bool publicSpace = State.Locations[other.LocationId].HasTag("publicly_accessible") || State.Locations[other.LocationId].Name == "Lobby";
                double p = (known ? 0.85 : publicSpace ? 0.4 : 0.75) + (observant ? 0.15 : 0);
                p *= Math.Min(1.0, 0.45 + minutes / 4.0) * Fade(overlapStart, askedAt);
                if (!Roll(p, witnessId, other.PersonId, overlapStart.Ticks / TimeSpan.TicksPerMinute)) continue;
                result.Add(PersonSighting(witness, other.PersonId, known, overlapStart, addressId,
                    State.Locations[other.LocationId].Name, quality: Math.Min(1, minutes / 10.0) * (observant ? 1 : 0.8), askedAt));
            }
        }

        // 2. Someone at a neighbour's door, glimpsed through a peephole or a window.
        foreach (var visit in history.PresenceAt(addressId, from, to))
        {
            var loc = State.Locations[visit.LocationId];
            if (!loc.HasTag("home") || visit.Start < from || visit.Start > to) continue;
            var visitor = State.People[visit.PersonId];
            if (visitor.HomeLocationId == loc.Id || visitor.Id == witnessId) continue;
            var mine = AwakeAt(visit.Start);
            if (mine == null || mine.Activity == ActivityKind.Sleep || !NearEnough(mine)) continue;
            var myLoc = State.Locations[mine.LocationId];
            if (myLoc.Id == loc.Id) continue;
            bool sameFloor = mine.AddressId == addressId && Floor(myLoc) == Floor(loc) && address.Type == AddressType.ApartmentBuilding;
            bool house = address.Type == AddressType.SuburbanHome;
            if (!sameFloor && !house) continue;
            double p = (sameFloor ? 0.18 : 0.1) + (observant ? 0.2 : 0);
            if (Describe.IsDark(visit.Start)) p *= 0.6;
            p *= Fade(visit.Start, askedAt);
            if (!Roll(p, witnessId, visit.PersonId, visit.Start.Ticks / TimeSpan.TicksPerMinute + 7)) continue;
            bool known = KnowsByName(witnessId, visit.PersonId);
            string where = sameFloor ? $"at the door of {loc.Name}" : $"going up to the door of {State.AddressLine(addressId)}";
            result.Add(PersonSighting(witness, visit.PersonId, known, visit.Start, addressId, where, quality: 0.35, askedAt));
        }

        // 3. Cars that came and went outside.
        var residentsHere = State.People.Values.Where(p => p.HomeAddressId == addressId).Select(p => p.VehicleId).ToHashSet();
        foreach (var parked in history.ParkedAt(addressId, from, to))
        {
            if (residentsHere.Contains(parked.VehicleId)) continue;
            var vehicle = State.Vehicles[parked.VehicleId];
            if (vehicle.OwnerPersonId == witnessId) continue;
            foreach (var (moment, verb) in new[] { (parked.Start, "pull up"), (parked.End ?? DateTime.MaxValue, "drive off") })
            {
                if (moment < from || moment > to) continue;
                var mine = AwakeAt(moment);
                if (mine == null || mine.Activity == ActivityKind.Sleep || !NearEnough(mine)) continue;
                if (!State.Locations[mine.LocationId].HasTag("home")) continue;
                double p = (0.12 + (observant ? 0.18 : 0)) * Fade(moment, askedAt);
                if ((parked.End ?? askedAt) - parked.Start > TimeSpan.FromMinutes(30)) p += 0.05;
                if (!Roll(p, witnessId, parked.VehicleId, moment.Ticks / TimeSpan.TicksPerMinute)) continue;
                var rng = Rng.For(_world.Seed, Purpose.Witness, witnessId, parked.VehicleId, moment.Ticks);
                bool knownCar = KnowsByName(witnessId, vehicle.OwnerPersonId) && rng.NextDouble() < 0.6;
                string car = knownCar
                    ? $"{State.People[vehicle.OwnerPersonId].FullName}'s {vehicle.Color} {vehicle.Make}"
                    : Describe.Vehicle(vehicle, moment, rng, observant);
                string when = Describe.Approximate(moment, rng, 15);
                result.Add(new Sighting
                {
                    Kind = SightingKind.Vehicle, WitnessId = witnessId, TrueTime = moment, When = when,
                    VehicleId = vehicle.Id, VehicleText = car, AddressId = addressId, Where = "outside",
                    PersonId = knownCar ? vehicle.OwnerPersonId : null, KnownByName = knownCar,
                    Text = $"I saw {car} {verb} outside, {when}."
                });
            }
        }

        // 4. Noises through walls and windows; loud ones wake sleepers.
        foreach (var noise in history.Noises.Where(n => n.AddressId == addressId && n.Time >= from && n.Time <= to))
        {
            if (noise.SourcePersonId == witnessId) continue;
            var mine = AwakeAt(noise.Time);
            if (mine == null || !NearEnough(mine) || mine.LocationId == noise.LocationId) continue;
            bool asleep = mine.Activity == ActivityKind.Sleep;
            bool sameBuilding = mine.AddressId == addressId;
            int floors = sameBuilding ? Math.Abs(Floor(State.Locations[mine.LocationId]) - Floor(State.Locations[noise.LocationId])) : 0;
            if (sameBuilding && floors > 1) continue;
            double p = noise.Kind switch
            {
                NoiseKind.Gunshot => asleep ? 0.6 : 0.95,
                NoiseKind.Scream => asleep ? 0.2 : 0.6,
                NoiseKind.Argument => asleep ? 0.03 : floors == 0 ? 0.45 : 0.15,
                _ => asleep ? 0.2 : 0.5
            };
            if (!sameBuilding) p *= noise.Kind == NoiseKind.Gunshot ? 0.9 : 0.4;
            p *= Fade(noise.Time, askedAt);
            if (!Roll(p, witnessId, (long)noise.Kind, noise.Time.Ticks / TimeSpan.TicksPerMinute)) continue;
            var rng = Rng.For(_world.Seed, Purpose.Witness, witnessId, (long)noise.Kind, noise.Time.Ticks);
            string when = Describe.Approximate(noise.Time, rng, asleep ? 25 : 10);
            string what = noise.Kind switch
            {
                NoiseKind.Gunshot => asleep ? "a bang woke me up. I thought it was a car backfiring" : "a loud bang, like a gunshot",
                NoiseKind.Scream => "a scream, then nothing",
                NoiseKind.Argument => "raised voices, a man and a woman arguing".Replace("a man and a woman", Voices(noise)),
                NoiseKind.BreakingGlass => "glass breaking",
                _ => "a crash, like something being forced"
            };
            result.Add(new Sighting
            {
                Kind = SightingKind.Noise, WitnessId = witnessId, TrueTime = noise.Time, When = when, Noise = noise.Kind,
                AddressId = addressId, Where = sameBuilding ? "through the wall" : "from next door",
                Text = $"I heard {what}, {when}."
            });
        }

        return result.OrderBy(s => s.TrueTime).ToList();
    }

    private string Voices(NoiseFact noise)
    {
        var victim = _world.History.Deaths.FirstOrDefault(d => d.AddressId == noise.AddressId && d.Time >= noise.Time);
        if (victim == null) return "people";
        var a = State.People[noise.SourcePersonId].Appearance.Sex;
        var b = State.People[victim.VictimId].Appearance.Sex;
        if (a == b) return a == Sex.Male ? "two men" : "two women";
        return "a man and a woman";
    }

    private static string Phrase(string where) =>
        where.StartsWith("at ") || where.StartsWith("going") ? where : $"in the {where.ToLowerInvariant()}";

    private Sighting PersonSighting(Person witness, int personId, bool known, DateTime when, int addressId, string where,
        double quality, DateTime askedAt)
    {
        var rng = Rng.For(_world.Seed, Purpose.Witness, witness.Id, personId, when.Ticks);
        var person = State.People[personId];
        var description = Describe.Person(person, rng, quality);
        string time = Describe.Approximate(when, rng, 10);
        string who = known ? person.FullName : description.Text;
        return new Sighting
        {
            Kind = SightingKind.Person, WitnessId = witness.Id, TrueTime = when, When = time, PersonId = personId,
            KnownByName = known, Description = description, AddressId = addressId, Where = where,
            Text = $"I saw {who} {Phrase(where)}, {time}."
        };
    }
}
