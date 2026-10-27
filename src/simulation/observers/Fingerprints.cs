using System;
using System.Collections.Generic;
using System.Linq;
using Stakeout.Simulation.Core;
using Stakeout.Simulation.Entities;

namespace Stakeout.Simulation.Observers;

/// <summary>One latent print lifted from a surface. PersonId is the truth; the player only learns it by a match.</summary>
public class LiftedPrint
{
    public int SurfaceId { get; init; }
    public int PersonId { get; init; }
    public bool Partial { get; init; }
}

/// <summary>
/// Latent prints on a surface, derived from touch facts: recent bare-handed touches since the surface was last
/// wiped, freshest first, a few at most (later touches smudge earlier ones). People living in a home leave prints
/// all over it without that being recorded touch by touch.
/// </summary>
public class Fingerprints
{
    private readonly World _world;
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(21);

    public Fingerprints(World world)
    {
        _world = world;
    }

    public List<LiftedPrint> Lift(int surfaceId, DateTime at)
    {
        var history = _world.History;
        var lastWipe = history.WipesOf(surfaceId).Where(w => w.Time <= at).Select(w => (DateTime?)w.Time).LastOrDefault();
        var touches = history.TouchesOf(surfaceId)
            .Where(t => t.Time <= at && !t.Gloved && at - t.Time < Lifetime && (lastWipe == null || t.Time > lastWipe))
            .OrderByDescending(t => t.Time)
            .ToList();

        var seen = new List<(int Person, DateTime Time)>();
        foreach (var t in touches)
            if (seen.All(s => s.Person != t.PersonId)) seen.Add((t.PersonId, t.Time));

        // Residents' prints are everywhere in their own home, unless wiped since they last could have touched.
        foreach (var resident in ResidentsFor(surfaceId))
        {
            if (seen.Any(s => s.Person == resident.Id)) continue;
            var lastTouchPossible = resident.IsAlive ? at : DeathOf(resident.Id) ?? at;
            if (lastWipe != null && lastWipe >= lastTouchPossible) continue;
            seen.Add((resident.Id, lastTouchPossible.AddHours(-12)));
        }

        var prints = new List<LiftedPrint>();
        foreach (var (person, time) in seen.OrderByDescending(s => s.Time).Take(4))
        {
            double age = (at - time).TotalDays;
            double legible = 0.9 * Math.Exp(-age / 25.0) - prints.Count * 0.08;
            double roll = Rng.Unit(_world.Seed, Purpose.Prints, surfaceId, person);
            if (roll > legible) continue;
            prints.Add(new LiftedPrint { SurfaceId = surfaceId, PersonId = person, Partial = roll > legible * 0.7 });
        }
        return prints;
    }

    private DateTime? DeathOf(int personId) =>
        _world.History.Deaths.FirstOrDefault(d => d.VictimId == personId)?.Time;

    /// <summary>The people who live where a surface is (door handles, furniture and items in a home).</summary>
    private IEnumerable<Person> ResidentsFor(int surfaceId)
    {
        var state = _world.State;
        int? locationId = null;
        if (state.Fixtures.TryGetValue(surfaceId, out var fixture))
            locationId = fixture.LocationId ?? (fixture.SubLocationId.HasValue ? state.SubLocations[fixture.SubLocationId.Value].LocationId : null);
        else if (state.GetAccessPoint(surfaceId) != null)
            locationId = state.LocationOfAccessPoint(surfaceId);
        else if (state.Items.TryGetValue(surfaceId, out var item) && item.ItemType is not (ItemType.CigaretteButt))
        {
            // Things people own carry their owner's prints.
            if (item.OwnerPersonId.HasValue && item.ItemType != ItemType.Knife) return new[] { state.People[item.OwnerPersonId.Value] };
            if (item.ItemType == ItemType.Knife && item.Data.ContainsKey("fromBlock") && item.OwnerPersonId.HasValue)
                return new[] { state.People[item.OwnerPersonId.Value] };
            return Array.Empty<Person>();
        }
        if (locationId == null || !state.Locations[locationId.Value].HasTag("home")) return Array.Empty<Person>();
        return state.People.Values.Where(p => p.HomeLocationId == locationId.Value);
    }
}
