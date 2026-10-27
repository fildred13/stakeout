using System;
using System.Collections.Generic;
using System.Linq;
using Stakeout.Simulation.Core;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Fixtures;
using Stakeout.Simulation.History;

namespace Stakeout.Simulation.Observers;

/// <summary>One moment on a security tape.</summary>
public class CameraRecord
{
    public DateTime Time { get; init; }
    public string Text { get; init; }
    public int? VehicleId { get; init; }
    public int[] PersonIds { get; init; } = Array.Empty<int>();
    public bool Night { get; init; }
    public string PlateSeen { get; init; }
    public PersonDescription[] Descriptions { get; init; } = Array.Empty<PersonDescription>();
}

/// <summary>
/// Security cameras record what passed in front of them; tapes are reused after three days. Street cameras
/// see every trip whose route crosses their cell: by day make, colour and often part of the plate, at night
/// only the shape and how many people are inside. Lobby cameras show everyone who came through, in black and white.
/// </summary>
public class Cameras
{
    public static readonly TimeSpan Retention = TimeSpan.FromHours(72);
    private readonly World _world;

    public Cameras(World world)
    {
        _world = world;
    }

    public List<CameraRecord> Footage(Fixture camera, DateTime from, DateTime to, DateTime viewedAt)
    {
        var start = from < viewedAt - Retention ? viewedAt - Retention : from;
        var end = to > viewedAt ? viewedAt : to;
        var records = new List<CameraRecord>();
        if (end <= start) return records;

        if (camera.WatchedCell.HasValue)
        {
            int cell = camera.WatchedCell.Value;
            foreach (var trip in _world.History.TripsBetween(start, end))
            {
                var at = trip.TimeAt(cell);
                if (at == null || at < start || at > end) continue;
                records.Add(StreetRecord(camera, trip, at.Value));
            }
        }
        if (camera.WatchedLocationId.HasValue)
        {
            var seen = new HashSet<(int, long)>();
            foreach (var p in _world.History.PresenceAt(_world.State.Locations[camera.WatchedLocationId.Value].AddressId, start, end))
            {
                if (p.LocationId != camera.WatchedLocationId || p.Start < start || p.Start > end) continue;
                if (!seen.Add((p.PersonId, p.Start.Ticks / TimeSpan.TicksPerMinute))) continue;
                var rng = Rng.For(_world.Seed, Purpose.Camera, camera.Id, p.PersonId, p.Start.Ticks);
                var d = Describe.Person(_world.State.People[p.PersonId], rng, 0.55);
                // Black and white: hair is only light or dark.
                if (d.Hair is HairColor.Blond or HairColor.Gray) d.Hair = HairColor.Blond;
                else if (d.Hair is HairColor.Red or HairColor.Brown) d.Hair = HairColor.Black;
                string hair = d.Hair == HairColor.Blond ? "light hair" : d.Hair == HairColor.Black ? "dark hair" : d.Hair == HairColor.Bald ? "bald" : "";
                var body = new PersonDescription { Sex = d.Sex, AgeDecade = d.AgeDecade, Height = d.Height, Build = d.Build, Glasses = d.Glasses, Mustache = d.Mustache, Beard = d.Beard };
                string text = body.Text + (hair.Length > 0 ? $", {hair}" : "");
                records.Add(new CameraRecord
                {
                    Time = p.Start, PersonIds = new[] { p.PersonId }, Night = false, Descriptions = new[] { d },
                    Text = $"{p.Start:ddd HH:mm} - {text} crosses the lobby"
                });
            }
        }
        return records.OrderBy(r => r.Time).ToList();
    }

    private CameraRecord StreetRecord(Fixture camera, TripFact trip, DateTime at)
    {
        var rng = Rng.For(_world.Seed, Purpose.Camera, camera.Id, trip.Id);
        bool night = Describe.IsDark(at);
        int people = trip.PersonIds.Length;
        string count = people switch { 1 => "one person", 2 => "two people", _ => $"{people} people" };
        if (trip.VehicleId.HasValue)
        {
            var v = _world.State.Vehicles[trip.VehicleId.Value];
            if (night)
            {
                string figures = people == 1 ? "one figure inside" : people == 2 ? "two figures inside" : $"{people} figures inside";
                return new CameraRecord
                {
                    Time = at, VehicleId = v.Id, PersonIds = trip.PersonIds, Night = true,
                    Text = $"{at:ddd HH:mm} - a {v.NightDescription}, headlights on, {figures}"
                };
            }
            string plate = rng.NextDouble() < 0.55 ? v.Plate[..(rng.NextDouble() < 0.5 ? 3 : 5)] : null;
            string plateText = plate == null ? "plate unreadable" : plate.Length >= 7 ? $"plate {plate}" : $"plate starts {plate}";
            return new CameraRecord
            {
                Time = at, VehicleId = v.Id, PersonIds = trip.PersonIds, Night = false, PlateSeen = plate,
                Text = $"{at:ddd HH:mm} - a {v.Color} {v.Make} {v.Model}, {plateText}, {count} inside"
            };
        }
        if (trip.Taxi)
            return new CameraRecord
            {
                Time = at, PersonIds = trip.PersonIds, Night = night,
                Text = night ? $"{at:ddd HH:mm} - a taxi, roof light off, {count} in the back" : $"{at:ddd HH:mm} - a yellow cab with {count} in the back"
            };
        var descs = trip.PersonIds.Select(id => Describe.Person(_world.State.People[id], rng, night ? 0.15 : 0.5)).ToArray();
        string who = night ? $"{count} on foot" : string.Join(" and ", descs.Select(d => d.Text)) + " on foot";
        return new CameraRecord
        {
            Time = at, PersonIds = trip.PersonIds, Night = night, Descriptions = descs, Text = $"{at:ddd HH:mm} - {who}"
        };
    }
}
