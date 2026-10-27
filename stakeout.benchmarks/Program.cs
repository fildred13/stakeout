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
        if (args.Length > 0 && args[0] == "bot")
        {
            RunBot(args.Skip(1).Select(int.Parse).ToArray());
            return;
        }
        int seed = args.Length > 0 ? int.Parse(args[0]) : 42;
        int days = args.Length > 1 ? int.Parse(args[1]) : 7;

        var sw = Stopwatch.StartNew();
        var world = WorldBuilder.Build(seed);
        world.Start();
        var build = sw.Elapsed;
        bool murder = Environment.GetEnvironmentVariable("MURDER") == "1";
        if (murder) world.Crimes.ScheduleMurder(world.Now.Date.AddDays(days - 2));
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
        foreach (var mc in world.Crimes.Cases)
        {
            Console.WriteLine($"--- Murder case {mc.Id}: {mc.Status} {mc.Approach} {mc.Weapon}, death {mc.DeathTime:ddd HH:mm}, " +
                              $"reported {mc.ReportedAt:ddd HH:mm} by {(mc.DiscovererId.HasValue ? world.State.People[mc.DiscovererId.Value].FullName : "-")}, " +
                              $"weapon {mc.WeaponFate}, wiped {mc.WipedPrints}, gloves {mc.Gloves}, accomplice {(mc.AccompliceId.HasValue ? world.State.People[mc.AccompliceId.Value].FullName : "-")} agreed {mc.AlibiAgreed}");
            Console.WriteLine(mc.MotiveSummary(world.State));
            var from = mc.PlannedFor.AddHours(-6);
            foreach (var id in new[] { mc.KillerId, mc.VictimId }.Concat(mc.DiscovererId.HasValue ? new[] { mc.DiscovererId.Value } : Array.Empty<int>()))
            {
                Console.WriteLine(world.State.People[id].FullName + ":");
                Console.WriteLine(Stakeout.Simulation.Diagnostics.Timeline.Format(
                    Stakeout.Simulation.Diagnostics.Timeline.ForPerson(world, id, from, mc.PlannedFor.AddHours(16))));
            }
        }
        if (murder) return;
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

    private static void RunBot(int[] seeds)
    {
        int right = 0, convicted = 0;
        foreach (var seed in seeds)
        {
            var sw = Stopwatch.StartNew();
            var session = Stakeout.Investigation.GameSession.NewGame(seed);
            var setup = sw.Elapsed;
            foreach (var mc in session.World.Crimes.Cases)
                Console.WriteLine($"  case {mc.Id}: {mc.Status} {mc.Approach} death {mc.DeathTime} weapon {mc.WeaponItemId} reported {mc.ReportedAt} planned {mc.PlannedFor}");
            var bot = new Stakeout.Investigation.AutoDetective(session.Detective);
            Stakeout.Investigation.Verdict verdict = null;
            try { verdict = bot.Run(); }
            catch (Exception e)
            {
                Console.WriteLine("  BOT CRASH: " + e.Message);
                foreach (var mc in session.World.Crimes.Cases)
                    Console.WriteLine($"  case {mc.Id}: {mc.Status} {mc.Approach} death {mc.DeathTime} weapon {mc.WeaponItemId} reported {mc.ReportedAt} discoverer {mc.DiscovererId} victim {mc.VictimId}");
                Console.WriteLine($"  detective case: {session.Detective.Case?.Truth.Id}");
            }
            var murder = session.World.Crimes.Cases.FirstOrDefault(c => c.Status == Stakeout.Simulation.Crime.CaseStatus.Reported);
            Console.WriteLine($"=== Seed {seed}: setup {setup.TotalSeconds:F1}s, bot {(sw.Elapsed - setup).TotalSeconds:F1}s, clues {session.Detective.Clues.Count}");
            if (murder != null)
            {
                Console.WriteLine($"  Truth: {session.World.State.People[murder.KillerId].FullName} ({murder.Motive.Kind}, {murder.Approach}, {murder.Weapon}, weapon {murder.WeaponFate}, wiped {murder.WipedPrints}, alibi {murder.AlibiAgreed})");
                foreach (var id in new[] { murder.WeaponItemId, murder.BloodyClothesItemId }.Where(i => i.HasValue))
                {
                    var it = session.World.State.Items[id.Value];
                    string where = it.HeldByPersonId.HasValue ? $"held by {session.World.State.People[it.HeldByPersonId.Value].FullName}"
                        : it.FixtureId.HasValue ? $"in {session.World.State.Fixtures[it.FixtureId.Value].Name} at {session.World.State.PlaceName(session.World.State.Locations[session.World.State.LocationOfFixture(session.World.State.Fixtures[it.FixtureId.Value])].AddressId)}"
                        : it.LocationId.HasValue ? "lying at " + session.World.State.Locations[it.LocationId.Value].Name : "nowhere";
                    Console.WriteLine($"    {it.Name}: {where}");
                }
            }
            foreach (var step in bot.Steps) Console.WriteLine("  " + step);
            if (Environment.GetEnvironmentVariable("CLUES") == "1")
                foreach (var c in session.Detective.Clues) Console.WriteLine($"   [{c.Kind}] {c.Title}: {c.Text.Replace('\n', '|')}");
            if (verdict != null)
            {
                foreach (var f in verdict.Assessment.Findings) Console.WriteLine($"    +{f.Points} {f.Pillar}: {f.Text}");
                foreach (var m in verdict.Assessment.Missing) Console.WriteLine($"    missing: {m}");
                if (verdict.RightPerson) right++;
                if (verdict.RightPerson && verdict.Convicted) convicted++;
            }
        }
        Console.WriteLine($"Right suspect {right}/{seeds.Length}, convicted {convicted}/{seeds.Length}");
    }
}
