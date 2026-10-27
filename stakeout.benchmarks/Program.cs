using System;
using System.Diagnostics;
using System.Linq;
using Stakeout.Simulation;
using Stakeout.Simulation.Generation;
using Stakeout.Simulation.History;

namespace Stakeout.Benchmarks;

/// <summary>
/// Measures world creation and simulation speed for a fully populated city.
/// Usage: dotnet run --project stakeout.benchmarks -c Release [seed] [days]
/// </summary>
public static class Program
{
    public static void Main(string[] args)
    {
        int seed = args.Length > 0 ? int.Parse(args[0]) : 42;
        int days = args.Length > 1 ? int.Parse(args[1]) : 7;

        var sw = Stopwatch.StartNew();
        var world = WorldBuilder.Build(seed);
        world.Start();
        var build = sw.Elapsed;
        Console.WriteLine($"Built seed {seed}: {world.State.People.Count} people, {world.State.Households.Count} households, " +
                          $"{world.State.Vehicles.Count} cars, {world.State.Businesses.Count} businesses, " +
                          $"{world.State.Locations.Count} locations in {build.TotalSeconds:F2}s");

        for (int d = 1; d <= days; d++)
        {
            sw.Restart();
            long events = world.Scheduler.EventsProcessed;
            world.AdvanceTo(world.Now.AddDays(1));
            Console.WriteLine($"Day {d} ({world.Now:ddd dd MMM}): {sw.Elapsed.TotalMilliseconds:F0} ms, " +
                              $"{world.Scheduler.EventsProcessed - events} events, trips {world.History.Trips.Count}, " +
                              $"presence {world.History.PresenceCount}, calls {world.History.Calls.Count}, " +
                              $"routes cached {world.Router.CacheSize}, " +
                              $"joint {world.Joints.Count(j => j.Finished && !j.Aborted)}/{world.Joints.Count}");
        }
        sw.Restart();
        var violations = new Stakeout.Simulation.Diagnostics.InvariantChecker(world).CheckAll();
        Console.WriteLine($"Invariants checked in {sw.Elapsed.TotalSeconds:F1}s: {violations.Count} violations");
        foreach (var v in violations.Take(25)) Console.WriteLine("  " + v);
        if (Environment.GetEnvironmentVariable("DEBUG_CALLS") == "1")
            foreach (var call in world.History.Calls.Where(c => violations.Any(v => v.StartsWith($"Call {c.Id}"))).Take(3))
            {
                Console.WriteLine($"Call {call.Id} at {call.Time:ddd HH:mm:ss} from phone {call.FromPhoneId} to {call.ToPhoneId} {call.Outcome}");
                foreach (var pid in new[] { call.CallerId, call.AnsweredById ?? call.CallerId })
                    foreach (var p in world.History.PresenceOf(pid, call.Time.AddMinutes(-30), call.Time.AddMinutes(30)))
                        Console.WriteLine($"   {world.State.People[pid].FirstName} {p.Start:HH:mm:ss}-{p.End:HH:mm:ss} loc {p.LocationId} sub {p.SubLocationId} {p.Activity}");
                Console.WriteLine($"   phone loc {world.Phones.PhoneLocation(world.State.Fixtures[call.FromPhoneId])}");
            }
        var date = world.Joints.OfType<Stakeout.Simulation.Agents.DateActivity>().FirstOrDefault(j => j.Finished && !j.Aborted);
        if (date != null)
        {
            Console.WriteLine($"--- A date (#{date.Id}) ---");
            foreach (var id in new[] { date.DriverId, date.PassengerId })
            {
                Console.WriteLine(world.State.People[id].FullName + ":");
                Console.WriteLine(Stakeout.Simulation.Diagnostics.Timeline.Format(
                    Stakeout.Simulation.Diagnostics.Timeline.ForPerson(world, id, date.Start.AddHours(-26), date.Start.AddHours(6))));
            }
        }
        var aborted = world.Joints.Count(j => j.Aborted);
        Console.WriteLine($"Joint activities: {world.Joints.Count}, aborted {aborted}");
        var mem = GC.GetTotalMemory(true) / (1024 * 1024);
        Console.WriteLine($"Managed memory: {mem} MB");
    }
}
