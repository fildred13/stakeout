using System;
using System.Collections.Generic;
using System.Linq;
using Stakeout.Simulation.Core;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.History;

namespace Stakeout.Simulation.Agents;

/// <summary>
/// Builds an agent's agenda from now until their next wake-up: fixed blocks (commitments, work shift, sleep)
/// and, between them, meals out, drinks, walks or time at home. Choices are keyed by person and day, so
/// replanning on the same day makes the same choices when nothing else changed.
/// </summary>
public class Planner
{
    private readonly World _world;

    public Planner(World world)
    {
        _world = world;
    }

    public static (DateTime Start, DateTime End) NextSleep(Person person, DateTime now)
    {
        for (int d = -1; d <= 1; d++)
        {
            var day = now.Date.AddDays(d);
            var start = day + person.SleepTime;
            var end = person.SleepTime < person.WakeTime ? day + person.WakeTime : day.AddDays(1) + person.WakeTime;
            if (end > now) return (start, end);
        }
        var tomorrow = now.Date.AddDays(1);
        return (tomorrow + person.SleepTime, tomorrow + person.SleepTime + TimeSpan.FromHours(8));
    }

    public static IEnumerable<(DateTime Start, DateTime End)> Shifts(Position position, DateTime from, DateTime to)
    {
        if (position == null) yield break;
        for (var day = from.Date.AddDays(-1); day <= to.Date; day = day.AddDays(1))
        {
            if (!position.WorkDays.Contains(day.DayOfWeek)) continue;
            var start = day + position.ShiftStart;
            var end = position.ShiftEnd > position.ShiftStart ? day + position.ShiftEnd : day.AddDays(1) + position.ShiftEnd;
            if (end > from && start < to) yield return (start, end);
        }
    }

    public static bool IsOpen(Business business, DateTime time)
    {
        foreach (var dayOffset in new[] { 0, -1 })
        {
            var day = time.Date.AddDays(dayOffset);
            var hours = business.Hours.FirstOrDefault(h => h.Day == day.DayOfWeek);
            if (hours?.OpenTime == null || hours.CloseTime == null) continue;
            var open = day + hours.OpenTime.Value;
            var close = hours.CloseTime.Value <= hours.OpenTime.Value ? day.AddDays(1) + hours.CloseTime.Value : day + hours.CloseTime.Value;
            if (time >= open && time < close) return true;
        }
        return false;
    }

    public List<Appointment> PlanFrom(Agent agent, DateTime now)
    {
        var person = agent.Person;
        var plan = new List<Appointment>();
        if (person.IsPlayer || !person.IsAlive) return plan;

        int home = person.HomeAddressId;
        var (sleepStart, wake) = NextSleep(person, now);
        var rng = Rng.For(_world.Seed, Purpose.DayPlan, person.Id, wake.Ticks / TimeSpan.TicksPerDay);

        var blocks = new List<Appointment>();
        foreach (var c in agent.Commitments)
            if (!c.Finished && c.Start < wake.AddHours(4)) blocks.Add(c.BlockFor(agent));

        bool Conflicts(DateTime s, DateTime e) => blocks.Any(b => b.ArriveBy < e && b.LeaveAt > s);

        var position = _world.State.PositionOf(person);
        if (position != null)
        {
            var business = _world.State.Businesses[person.BusinessId.Value];
            foreach (var (s, e) in Shifts(position, now, sleepStart > now ? sleepStart : wake))
            {
                if (e <= now || Conflicts(s, e)) continue;
                var jitter = TimeSpan.FromMinutes(rng.Next(0, 11));
                blocks.Add(new Appointment
                {
                    AddressId = business.AddressId, Spot = Spot.Workplace, Kind = ActivityKind.Work,
                    ArriveBy = s - jitter > now ? s - jitter : now, LeaveAt = e, Label = $"work at {business.Name}"
                });
            }
        }

        // Sleep fills the night, around any commitment that falls in it.
        var sleepFrom = sleepStart > now ? sleepStart : now;
        foreach (var (s, e) in Subtract(sleepFrom, wake, blocks))
        {
            if (e - s < TimeSpan.FromMinutes(45)) continue;
            blocks.Add(new Appointment
            {
                AddressId = home, Spot = Spot.HomeBedroom, Kind = ActivityKind.Sleep,
                ArriveBy = s, LeaveAt = e, Label = "asleep"
            });
        }

        foreach (var extra in agent.Injected) blocks.Add(extra);
        agent.Injected.Clear();

        blocks.Sort((a, b) => a.ArriveBy.CompareTo(b.ArriveBy));

        var cursorTime = now;
        int cursorPlace = agent.AddressId ?? agent.Trip?.ToAddressId ?? home;
        var mode = _world.Movement.ModeFor(agent);
        var ctx = new DayContext();
        foreach (var block in blocks)
        {
            if (block.LeaveAt <= cursorTime && block.Joint == null) continue;
            FillGap(plan, agent, ref cursorPlace, cursorTime, block.ArriveBy, block.AddressId, mode, rng, ctx);
            plan.Add(block);
            if (block.LeaveAt > cursorTime) cursorTime = block.LeaveAt;
            cursorPlace = block.Joint?.EndAddressFor(agent) ?? block.AddressId;
        }
        if (plan.Count == 0 || plan[^1].LeaveAt < wake)
        {
            plan.Add(new Appointment
            {
                AddressId = home, Spot = Spot.HomeLiving, Kind = ActivityKind.Home,
                ArriveBy = cursorTime, LeaveAt = wake, Label = "at home"
            });
        }
        return plan;
    }

    private class DayContext
    {
        public bool AteOut;
        public bool Drank;
        public bool Walked;
    }

    private static IEnumerable<(DateTime, DateTime)> Subtract(DateTime start, DateTime end, List<Appointment> blocks)
    {
        var cursor = start;
        foreach (var b in blocks.OrderBy(b => b.ArriveBy))
        {
            if (b.LeaveAt <= cursor || b.ArriveBy >= end) continue;
            if (b.ArriveBy > cursor) yield return (cursor, b.ArriveBy - TimeSpan.FromMinutes(20));
            cursor = b.LeaveAt + TimeSpan.FromMinutes(30);
        }
        if (cursor < end) yield return (cursor, end);
    }

    private TimeSpan Travel(int from, int to, Space.TravelMode mode) => _world.Movement.TravelEstimate(from, to, mode);

    private void FillGap(List<Appointment> plan, Agent agent, ref int place, DateTime from, DateTime nextArriveBy,
        int nextAddress, Space.TravelMode mode, Random rng, DayContext ctx)
    {
        var person = agent.Person;
        int home = person.HomeAddressId;
        var t = from;

        for (int outing = 0; outing < 2; outing++)
        {
            var option = ChooseOuting(person, place, t, nextArriveBy, nextAddress, mode, rng, ctx);
            if (option == null) break;
            var (address, spot, kind, arrive, leave, label) = option.Value;

            // Idle time before the outing is spent at home if that is worth the trip.
            var depart = arrive - Travel(place, address, mode);
            if (depart > t + TimeSpan.FromMinutes(30))
            {
                if (place != home)
                {
                    var atHome = t + Travel(place, home, mode);
                    var leaveHome = arrive - Travel(home, address, mode);
                    if (leaveHome - atHome >= TimeSpan.FromMinutes(30))
                    {
                        plan.Add(HomeAppointment(home, atHome, leaveHome));
                        place = home;
                    }
                }
                else
                {
                    plan.Add(HomeAppointment(home, t, depart));
                }
            }
            plan.Add(new Appointment
            {
                AddressId = address, Spot = spot, Kind = kind, ArriveBy = arrive, LeaveAt = leave, Label = label,
                Optional = true
            });
            place = address;
            t = leave;
        }

        // The rest of the gap at home, if there is time to get there (and back): nobody idles where they were.
        if (place == home)
        {
            var leaveHome = nextAddress == home ? nextArriveBy : nextArriveBy - Travel(home, nextAddress, mode);
            if (leaveHome > t) plan.Add(HomeAppointment(home, t, leaveHome));
        }
        else if (nextAddress != place)
        {
            var atHome = t + Travel(place, home, mode);
            var leaveHome = nextAddress == home ? nextArriveBy : nextArriveBy - Travel(home, nextAddress, mode);
            if (leaveHome - atHome >= TimeSpan.FromMinutes(20))
            {
                plan.Add(HomeAppointment(home, atHome, leaveHome));
                place = home;
            }
        }
    }

    private static Appointment HomeAppointment(int home, DateTime from, DateTime to) => new()
    {
        AddressId = home, Spot = Spot.HomeLiving, Kind = ActivityKind.Home, ArriveBy = from, LeaveAt = to,
        Label = "at home"
    };

    private (int Address, Spot Spot, ActivityKind Kind, DateTime Arrive, DateTime Leave, string Label)?
        ChooseOuting(Person person, int place, DateTime t, DateTime nextArriveBy, int nextAddress,
            Space.TravelMode mode, Random rng, DayContext ctx)
    {
        bool weekend = t.DayOfWeek is DayOfWeek.Friday or DayOfWeek.Saturday;
        var candidates = new List<(double Weight, int Address, Spot Spot, ActivityKind Kind, int WindowStart,
            int WindowEnd, int MinDur, int MaxDur, string Verb)>();

        if (!ctx.AteOut && person.FavoriteDinerIds.Count > 0)
        {
            double p = 0.22 + (person.Has(Traits.Foodie) ? 0.35 : 0) - (person.Has(Traits.Homebody) ? 0.12 : 0);
            var diner = PickVenue(person.FavoriteDinerIds, rng);
            candidates.Add((p, diner, Spot.Customer, ActivityKind.Dine, 17 * 60, 20 * 60 + 30, 40, 75, "dinner"));
            candidates.Add((p * 0.4, diner, Spot.Customer, ActivityKind.Dine, 7 * 60, 9 * 60 + 30, 25, 45, "breakfast"));
            candidates.Add((p * 0.5, diner, Spot.Customer, ActivityKind.Dine, 12 * 60, 13 * 60 + 30, 30, 50, "lunch"));
        }
        if (!ctx.Drank && person.FavoriteBarIds.Count > 0)
        {
            double p = 0.05 + (person.Has(Traits.Drinker) ? 0.38 : 0) + (weekend ? 0.12 : 0)
                       - (person.Has(Traits.Homebody) ? 0.04 : 0);
            candidates.Add((p, PickVenue(person.FavoriteBarIds, rng), Spot.Customer, ActivityKind.Drink,
                19 * 60, 23 * 60, 80, 170, "drinks"));
        }
        if (!ctx.Walked && person.FavoriteParkId.HasValue)
        {
            double p = 0.03 + (person.Has(Traits.Runner) ? 0.4 : 0);
            candidates.Add((p, person.FavoriteParkId.Value, Spot.Outdoors, ActivityKind.Walk,
                7 * 60, 18 * 60, 35, 70, "a walk"));
        }

        foreach (var c in candidates.OrderBy(_ => rng.Next()))
        {
            if (rng.NextDouble() >= c.Weight) continue;
            var travelThere = Travel(place, c.Address, mode);
            var earliest = t + travelThere;
            var day = earliest.Date;
            var windowStart = day.AddMinutes(c.WindowStart);
            var windowEnd = day.AddMinutes(c.WindowEnd);
            var arrive = earliest > windowStart ? earliest : windowStart.AddMinutes(rng.Next(0, 45));
            if (arrive > windowEnd) continue;
            arrive = RoundUp(arrive);
            var leave = arrive.AddMinutes(rng.Next(c.MinDur, c.MaxDur + 1));
            if (leave + Travel(c.Address, nextAddress, mode) > nextArriveBy) continue;
            var biz = _world.State.BusinessAt(c.Address);
            if (biz != null && (!IsOpen(biz, arrive) || !IsOpen(biz, leave.AddMinutes(-1)))) continue;
            switch (c.Kind)
            {
                case ActivityKind.Dine: ctx.AteOut = true; break;
                case ActivityKind.Drink: ctx.Drank = true; break;
                case ActivityKind.Walk: ctx.Walked = true; break;
            }
            return (c.Address, c.Spot, c.Kind, arrive, leave, $"{c.Verb} at {_world.State.PlaceName(c.Address)}");
        }
        return null;
    }

    private static DateTime RoundUp(DateTime t)
    {
        var minutes = (int)Math.Ceiling(t.TimeOfDay.TotalMinutes / 5.0) * 5;
        return t.Date.AddMinutes(minutes);
    }

    private static int PickVenue(List<int> favorites, Random rng)
    {
        double r = rng.NextDouble();
        if (favorites.Count == 1 || r < 0.65) return favorites[0];
        if (favorites.Count == 2 || r < 0.9) return favorites[1];
        return favorites[rng.Next(favorites.Count)];
    }
}
