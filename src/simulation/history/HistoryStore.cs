using System;
using System.Collections.Generic;
using System.Linq;

namespace Stakeout.Simulation.History;

/// <summary>
/// The ground truth: an append-only record of who and what was where, and what happened.
/// Facts are appended in time order by the world; observers only ever read from here.
/// </summary>
public class HistoryStore
{
    private readonly Dictionary<int, List<PresenceFact>> _presenceByPerson = new();
    private readonly Dictionary<int, List<PresenceFact>> _presenceByAddress = new();
    private readonly Dictionary<int, List<ParkedFact>> _parkedByAddress = new();
    private readonly Dictionary<int, List<ParkedFact>> _parkedByVehicle = new();
    private readonly Dictionary<int, List<TripFact>> _tripsByPerson = new();
    private readonly Dictionary<int, List<TouchFact>> _touchesBySurface = new();
    private readonly Dictionary<int, List<WipeFact>> _wipesBySurface = new();
    private readonly Dictionary<int, List<CallFact>> _callsByPhone = new();

    public List<TripFact> Trips { get; } = new();
    public List<CallFact> Calls { get; } = new();
    public List<NoiseFact> Noises { get; } = new();
    public List<DeathFact> Deaths { get; } = new();
    public List<ForcedEntryFact> ForcedEntries { get; } = new();
    public List<ItemMovedFact> ItemMoves { get; } = new();
    public List<PurchaseFact> Purchases { get; } = new();
    public long PresenceCount { get; private set; }
    public long TouchCount { get; private set; }

    /// <summary>Live feed for stakeouts and the UI.</summary>
    public event Action<PresenceFact> PresenceStarted;
    public event Action<PresenceFact> PresenceEnded;
    public event Action<TripFact> TripStarted;
    public event Action<ParkedFact> ParkStarted;
    public event Action<ParkedFact> ParkEnded;
    public event Action<CallFact> CallPlaced;

    public PresenceFact OpenPresence(PresenceFact fact)
    {
        Add(_presenceByPerson, fact.PersonId, fact);
        Add(_presenceByAddress, fact.AddressId, fact);
        PresenceCount++;
        PresenceStarted?.Invoke(fact);
        return fact;
    }

    public void ClosePresence(PresenceFact fact, DateTime end)
    {
        if (fact.End.HasValue) return;
        fact.End = end;
        PresenceEnded?.Invoke(fact);
    }

    /// <summary>Records an already-finished interval (e.g. a minute crossing a lobby).</summary>
    public void AddClosedPresence(PresenceFact fact)
    {
        Add(_presenceByPerson, fact.PersonId, fact);
        Add(_presenceByAddress, fact.AddressId, fact);
        PresenceCount++;
        PresenceStarted?.Invoke(fact);
        PresenceEnded?.Invoke(fact);
    }

    public ParkedFact OpenParked(ParkedFact fact)
    {
        Add(_parkedByAddress, fact.AddressId, fact);
        Add(_parkedByVehicle, fact.VehicleId, fact);
        ParkStarted?.Invoke(fact);
        return fact;
    }

    public void CloseParked(ParkedFact fact, DateTime end)
    {
        if (fact.End.HasValue) return;
        fact.End = end;
        ParkEnded?.Invoke(fact);
    }

    public void AddTrip(TripFact trip)
    {
        Trips.Add(trip);
        foreach (var id in trip.PersonIds) Add(_tripsByPerson, id, trip);
        TripStarted?.Invoke(trip);
    }

    public void AddTouch(TouchFact touch)
    {
        Add(_touchesBySurface, touch.SurfaceId, touch);
        TouchCount++;
    }

    public void AddWipe(WipeFact wipe) => Add(_wipesBySurface, wipe.SurfaceId, wipe);

    public void AddCall(CallFact call)
    {
        Calls.Add(call);
        Add(_callsByPhone, call.FromPhoneId, call);
        if (call.ToPhoneId != call.FromPhoneId) Add(_callsByPhone, call.ToPhoneId, call);
        CallPlaced?.Invoke(call);
    }

    // ---- queries ----

    public IReadOnlyList<PresenceFact> PresenceOf(int personId) =>
        _presenceByPerson.TryGetValue(personId, out var list) ? list : Array.Empty<PresenceFact>();

    public IEnumerable<PresenceFact> PresenceOf(int personId, DateTime from, DateTime to) =>
        Window(PresenceOf(personId), from, to);

    public IReadOnlyList<PresenceFact> PresenceAt(int addressId) =>
        _presenceByAddress.TryGetValue(addressId, out var list) ? list : Array.Empty<PresenceFact>();

    public IEnumerable<PresenceFact> PresenceAt(int addressId, DateTime from, DateTime to) =>
        Window(PresenceAt(addressId), from, to);

    public IReadOnlyList<ParkedFact> ParkedAt(int addressId) =>
        _parkedByAddress.TryGetValue(addressId, out var list) ? list : Array.Empty<ParkedFact>();

    public IEnumerable<ParkedFact> ParkedAt(int addressId, DateTime from, DateTime to) =>
        ParkedAt(addressId).Where(p => p.Overlaps(from, to));

    public IReadOnlyList<ParkedFact> ParkedOf(int vehicleId) =>
        _parkedByVehicle.TryGetValue(vehicleId, out var list) ? list : Array.Empty<ParkedFact>();

    public IReadOnlyList<TripFact> TripsOf(int personId) =>
        _tripsByPerson.TryGetValue(personId, out var list) ? list : Array.Empty<TripFact>();

    public IEnumerable<TripFact> TripsBetween(DateTime from, DateTime to)
    {
        // Trips are appended by departure; none lasts more than a few hours.
        int i = LowerBound(Trips, from.AddHours(-3), t => t.Depart);
        for (; i < Trips.Count && Trips[i].Depart < to; i++)
            if (Trips[i].Overlaps(from, to)) yield return Trips[i];
    }

    public IReadOnlyList<TouchFact> TouchesOf(int surfaceId) =>
        _touchesBySurface.TryGetValue(surfaceId, out var list) ? list : Array.Empty<TouchFact>();

    public IReadOnlyList<WipeFact> WipesOf(int surfaceId) =>
        _wipesBySurface.TryGetValue(surfaceId, out var list) ? list : Array.Empty<WipeFact>();

    public IReadOnlyList<CallFact> CallsOf(int phoneId) =>
        _callsByPhone.TryGetValue(phoneId, out var list) ? list : Array.Empty<CallFact>();

    /// <summary>The presence interval covering <paramref name="time"/> for a person, if they were indoors.</summary>
    public PresenceFact PresenceOfAt(int personId, DateTime time)
    {
        var list = PresenceOf(personId);
        for (int i = list.Count - 1; i >= 0; i--)
        {
            var p = list[i];
            if (p.Start <= time && (p.End ?? DateTime.MaxValue) > time) return p;
            if (p.End.HasValue && p.End.Value <= time && p.Start < time.AddDays(-2)) break;
        }
        return null;
    }

    private static IEnumerable<PresenceFact> Window(IReadOnlyList<PresenceFact> list, DateTime from, DateTime to)
    {
        // Lists are in start order; intervals are at most a couple of days long, except the dead.
        for (int i = list.Count - 1; i >= 0; i--)
        {
            var p = list[i];
            if (p.Start >= to) continue;
            if (p.Overlaps(from, to)) yield return p;
            else if (p.Start < from.AddDays(-3)) yield break;
        }
    }

    private static int LowerBound<T>(List<T> list, DateTime value, Func<T, DateTime> key)
    {
        int lo = 0, hi = list.Count;
        while (lo < hi)
        {
            int mid = (lo + hi) / 2;
            if (key(list[mid]) < value) lo = mid + 1; else hi = mid;
        }
        return lo;
    }

    private static void Add<T>(Dictionary<int, List<T>> index, int key, T value)
    {
        if (!index.TryGetValue(key, out var list))
        {
            list = new List<T>();
            index[key] = list;
        }
        list.Add(value);
    }
}
