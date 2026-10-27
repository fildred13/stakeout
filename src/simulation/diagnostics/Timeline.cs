using System;
using System.Collections.Generic;
using System.Linq;
using Stakeout.Simulation.History;

namespace Stakeout.Simulation.Diagnostics;

/// <summary>Plain-text reconstruction of what someone did, straight from History (debugging and case replay).</summary>
public static class Timeline
{
    public static List<(DateTime Time, string Text)> ForPerson(World world, int personId, DateTime from, DateTime to)
    {
        var state = world.State;
        var lines = new List<(DateTime, string)>();
        foreach (var p in world.History.PresenceOf(personId, from, to))
        {
            if (p.End == p.Start) continue;
            var loc = state.Locations[p.LocationId];
            string room = p.SubLocationId.HasValue ? $"{loc.Name} / {state.SubLocations[p.SubLocationId.Value].Name}" : loc.Name;
            string end = p.End.HasValue ? p.End.Value.ToString("HH:mm") : "...";
            lines.Add((p.Start, $"{p.Start:ddd HH:mm}-{end}  {p.Activity,-9} {state.PlaceName(p.AddressId)} ({room})"));
        }
        foreach (var t in world.History.TripsOf(personId).Where(t => t.Overlaps(from, to)))
        {
            string how = t.VehicleId.HasValue
                ? $"{(t.LeaderId == personId ? "drives" : "rides in")} the {state.Vehicles[t.VehicleId.Value].Description}"
                : "walks";
            var others = t.PersonIds.Where(id => id != personId).Select(id => state.People[id].FirstName).ToList();
            string with = others.Count > 0 ? $" with {string.Join(", ", others)}" : "";
            lines.Add((t.Depart, $"{t.Depart:ddd HH:mm}-{t.Arrive:HH:mm}  {how}{with} to {state.PlaceName(t.ToAddressId)}"));
        }
        foreach (var c in world.History.Calls.Where(c => (c.CallerId == personId || c.AnsweredById == personId) && c.Time >= from && c.Time < to))
        {
            string other = c.CallerId == personId
                ? $"calls {state.People[c.IntendedId].FullName}"
                : $"answers a call from {state.People[c.CallerId].FullName}";
            lines.Add((c.Time, $"{c.Time:ddd HH:mm}        {other} [{c.Outcome}] {c.Transcript}"));
        }
        lines.Sort((a, b) => a.Item1.CompareTo(b.Item1));
        return lines;
    }

    public static string Format(IEnumerable<(DateTime Time, string Text)> lines) =>
        string.Join("\n", lines.Select(l => l.Text));
}
