using System;
using System.Collections.Generic;
using System.Linq;
using Stakeout.Simulation.Core;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.History;
using Stakeout.Simulation.Space;

namespace Stakeout.Simulation.Agents;

/// <summary>
/// Evenings at home, people phone their dates and friends to arrange dinners and visits. Arrangements exist
/// only if someone actually agreed on the phone (live, or by calling back after hearing a message).
/// </summary>
public class SocialLife
{
    private readonly World _world;
    private readonly Dictionary<(int, int), DateTime> _lastArranged = new();

    public SocialLife(World world)
    {
        _world = world;
    }

    public void OnSettled(Agent agent, Appointment ap)
    {
        var now = _world.Now;
        if (ap.Kind != ActivityKind.Home || agent.Person.IsPlayer) return;
        if (now.Hour < 17 || now.Hour >= 22) return;
        if (ap.LeaveAt - now < TimeSpan.FromMinutes(40)) return;
        var rng = Rng.For(_world.Seed, Purpose.Social, agent.Id, now.Date.Ticks);
        var delay = TimeSpan.FromMinutes(rng.Next(5, 30));
        _world.Scheduler.After(delay, () => MaybeCall(agent, rng));
    }

    private bool AtHomePhone(Agent agent) =>
        agent.Person.IsAlive && agent.HeldBy == null && agent.Trip == null && !agent.Asleep
        && agent.AddressId == agent.Person.HomeAddressId && _world.Phones.PhoneAt(agent) != null;

    private void MaybeCall(Agent agent, Random rng)
    {
        if (!AtHomePhone(agent)) return;
        var person = agent.Person;
        var now = _world.Now;

        foreach (var partnerId in _world.State.RelatedPeople(person.Id, RelationshipType.Dating))
        {
            if (Recently(person.Id, partnerId, 3) || HasPlansWith(agent, partnerId)) continue;
            if (rng.NextDouble() < 0.55)
            {
                ProposeDate(agent, partnerId, rng);
                return;
            }
        }
        var friends = _world.State.RelatedPeople(person.Id, RelationshipType.Friend).ToList();
        if (friends.Count > 0 && rng.NextDouble() < 0.12)
        {
            var friendId = friends[rng.Next(friends.Count)];
            if (!Recently(person.Id, friendId, 6) && !HasPlansWith(agent, friendId)) ProposeVisit(agent, friendId, rng);
        }
    }

    private bool Recently(int a, int b, int days)
    {
        var key = a < b ? (a, b) : (b, a);
        return _lastArranged.TryGetValue(key, out var t) && (_world.Now - t).TotalDays < days;
    }

    private void Remember(int a, int b)
    {
        var key = a < b ? (a, b) : (b, a);
        _lastArranged[key] = _world.Now;
    }

    private static bool HasPlansWith(Agent agent, int otherId) =>
        agent.Commitments.Any(c => c.ParticipantIds.Contains(otherId));

    /// <summary>Is the person free (no shift, no commitment, awake) for the whole interval?</summary>
    public bool IsFree(Agent agent, DateTime from, DateTime to)
    {
        var person = agent.Person;
        if (!person.IsAlive || person.IsPlayer) return false;
        if (agent.Commitments.Any(c => c.Start < to.AddMinutes(30) && c.EstimatedEnd > from.AddMinutes(-30))) return false;
        var position = _world.State.PositionOf(person);
        if (Planner.Shifts(position, from.AddHours(-1), to.AddHours(1)).Any()) return false;
        var (sleepStart, _) = Planner.NextSleep(person, from);
        if (sleepStart < to.AddMinutes(30) && sleepStart > from.AddHours(-6)) return false;
        return true;
    }

    private DateTime? FindEvening(Agent a, Agent b, Random rng, int startHour, int startMinute, int minutes)
    {
        var first = _world.Now.Date.AddDays(rng.NextDouble() < 0.3 ? 1 : 2);
        for (int d = 0; d < 3; d++)
        {
            var start = first.AddDays(d).AddHours(startHour).AddMinutes(startMinute);
            var end = start.AddMinutes(minutes);
            if (IsFree(a, start, end) && IsFree(b, start, end)) return start;
        }
        return null;
    }

    private void ProposeDate(Agent caller, int partnerId, Random rng)
    {
        var partner = _world.Agents[partnerId];
        var person = caller.Person;
        // Whoever has a car drives; the caller if both do.
        Agent driver = person.VehicleId.HasValue ? caller : partner.Person.VehicleId.HasValue ? partner : caller;
        Agent passenger = driver == caller ? partner : caller;
        if (driver.Person.HomeAddressId == passenger.Person.HomeAddressId) return;
        var venues = driver.Person.FavoriteDinerIds;
        if (venues.Count == 0) return;
        int venue = venues[rng.Next(venues.Count)];
        int dinner = rng.Next(60, 100);
        var pickup = FindEvening(caller, partner, rng, 18, 45, dinner + 70);
        if (pickup == null) return;
        var when = Describe(pickup.Value);
        string place = _world.State.BusinessAt(venue)?.Name ?? "dinner";
        string who = driver == caller ? $"I'll pick you up at {pickup:h:mm}" : $"pick me up at {pickup:h:mm}";

        void Agree()
        {
            if (!IsFree(caller, pickup.Value, pickup.Value.AddMinutes(dinner + 70))) return;
            if (!IsFree(partner, pickup.Value, pickup.Value.AddMinutes(dinner + 70))) return;
            new DateActivity(_world, driver.Id, passenger.Id, venue, pickup.Value, dinner).Commit();
            Remember(caller.Id, partnerId);
        }

        _world.Phones.Call(caller, _world.State.PhoneOf(partner.Person), new CallPurpose
        {
            ForPersonId = partnerId,
            OnAnswered = callee =>
            {
                bool yes = IsFree(callee, pickup.Value, pickup.Value.AddMinutes(dinner + 70))
                           && rng.NextDouble() < 0.9;
                if (yes) Agree();
                return yes
                    ? $"{person.FirstName}: Dinner at {place} {when}? {Cap(who)}. {callee.Person.FirstName}: Sounds lovely, see you then."
                    : $"{person.FirstName}: Dinner at {place} {when}? {callee.Person.FirstName}: I can't that night, sorry. Another time.";
            },
            MachineMessage = $"Hi {partner.Person.FirstName}, it's {person.FirstName}. How about dinner at {place} {when}? {Cap(who)}. Call me back.",
            OnMessageHeard = listener => _world.Scheduler.After(TimeSpan.FromMinutes(rng.Next(3, 20)), () =>
                CallBack(listener, caller, $"Yes to {when}", Agree, pickup.Value, dinner + 70))
        });
    }

    private void ProposeVisit(Agent caller, int friendId, Random rng)
    {
        var friend = _world.Agents[friendId];
        var person = caller.Person;
        bool callerHosts = rng.NextDouble() < 0.5;
        var host = callerHosts ? caller : friend;
        var guest = callerHosts ? friend : caller;
        if (host.Person.HomeAddressId == guest.Person.HomeAddressId) return;
        int minutes = rng.Next(90, 170);
        var start = FindEvening(caller, friend, rng, 19, 30, minutes + 40);
        if (start == null) return;
        var when = Describe(start.Value);
        string plan = callerHosts ? $"come over {when}, around {start:h:mm}" : $"I'll come by {when}, around {start:h:mm}";

        void Agree()
        {
            if (!IsFree(caller, start.Value, start.Value.AddMinutes(minutes + 40))) return;
            if (!IsFree(friend, start.Value, start.Value.AddMinutes(minutes + 40))) return;
            new VisitActivity(_world, host.Id, guest.Id, start.Value, minutes).Commit();
            Remember(caller.Id, friendId);
        }

        _world.Phones.Call(caller, _world.State.PhoneOf(friend.Person), new CallPurpose
        {
            ForPersonId = friendId,
            OnAnswered = callee =>
            {
                bool yes = IsFree(callee, start.Value, start.Value.AddMinutes(minutes + 40)) && rng.NextDouble() < 0.85;
                if (yes) Agree();
                return yes
                    ? $"{person.FirstName}: {Cap(plan)}? {callee.Person.FirstName}: Sure, see you then."
                    : $"{person.FirstName}: {Cap(plan)}? {callee.Person.FirstName}: Can't make it, sorry.";
            },
            MachineMessage = $"Hey {friend.Person.FirstName}, it's {person.FirstName}. {Cap(plan)}? Give me a call.",
            OnMessageHeard = listener => _world.Scheduler.After(TimeSpan.FromMinutes(rng.Next(3, 25)), () =>
                CallBack(listener, caller, $"Yes to {when}", Agree, start.Value, minutes + 40))
        });
    }

    private void CallBack(Agent listener, Agent original, string answer, Action agree, DateTime start, int minutes)
    {
        if (!AtHomePhone(listener)) return;
        if (!IsFree(listener, start, start.AddMinutes(minutes))) return;
        _world.Phones.Call(listener, _world.State.PhoneOf(original.Person), new CallPurpose
        {
            ForPersonId = original.Id,
            OnAnswered = callee =>
            {
                agree();
                return $"{listener.Person.FirstName}: Got your message. {answer}. {callee.Person.FirstName}: Great.";
            },
            MachineMessage = $"It's {listener.Person.FirstName}, got your message. {answer}, see you then.",
            OnMessageHeard = _ => agree()
        });
    }

    private string Describe(DateTime t)
    {
        var days = (t.Date - _world.Now.Date).Days;
        return days switch
        {
            0 => "tonight",
            1 => "tomorrow night",
            _ => $"{t:dddd} night"
        };
    }

    private static string Cap(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s[1..];
}
