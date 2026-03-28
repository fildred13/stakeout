using System;
using System.Diagnostics;
using Stakeout.Simulation;
using Stakeout.Simulation.City;
using Stakeout.Simulation.Sublocations;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Scheduling;

namespace Stakeout.Benchmarks;

class Program
{
    static void Main(string[] args)
    {
        SublocationGeneratorRegistry.RegisterAll();

        var dayProfileNpcCount = 1000;
        if (args.Length > 0 && int.TryParse(args[0], out var parsed) && parsed > 0)
        {
            dayProfileNpcCount = parsed;
        }

        // Mode 1: Frame Budget
        var npcCounts = new[] { 50, 200, 300, 500, 1000, 5000 };
        Console.WriteLine("Frame Budget (5 game-minutes, 1s tick delta)");
        Console.WriteLine(new string('-', 60));
        Console.WriteLine($"{"NPCs",-8} {"Avg ms/tick",-14} {"Max ms/tick",-14} {"Ticks/sec",-12} {"Memory MB",-10}");
        Console.WriteLine(new string('-', 60));

        foreach (var count in npcCounts)
        {
            RunFrameBudget(count);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        Console.WriteLine();

        // Mode 2: Day Profile
        RunDayProfile(dayProfileNpcCount, 500);
    }

    static (SimulationState state, PersonBehavior behavior) CreateSimulation(int npcCount, DateTime? clockStart = null)
    {
        var clock = new GameClock(clockStart);
        var state = new SimulationState(clock);
        var mapConfig = new MapConfig();
        var locationGenerator = new LocationGenerator(mapConfig);
        var personGenerator = new PersonGenerator(mapConfig);
        var behavior = new PersonBehavior(mapConfig);

        locationGenerator.GenerateCityScaffolding(state);

        // Generate city grid so PersonGenerator can pick addresses
        var cityGen = new CityGenerator(seed: 42);
        state.CityGrid = cityGen.Generate(state);

        Console.Error.Write($"  Generating {npcCount} NPCs...");
        Console.Error.Flush();
        for (int i = 0; i < npcCount; i++)
        {
            personGenerator.GeneratePerson(state);
        }
        Console.Error.WriteLine(" done");
        Console.Error.Flush();

        return (state, behavior);
    }

    static void RunFrameBudget(int npcCount)
    {
        var (state, behavior) = CreateSimulation(npcCount);

        var tickCount = 0;
        var totalMs = 0.0;
        var maxMs = 0.0;
        var sw = new Stopwatch();
        var tickDelta = 1.0;
        var totalTicks = 300; // 5 game-minutes

        var memBefore = GC.GetTotalMemory(true);
        Console.Error.Write($"  Ticking {npcCount} NPCs: ");
        Console.Error.Flush();

        for (int s = 0; s < totalTicks; s++)
        {
            sw.Restart();

            state.Clock.Tick(tickDelta);
            foreach (var person in state.People.Values)
            {
                if (!person.IsAlive) continue;
                behavior.Update(person, state);
            }

            sw.Stop();
            var ms = sw.Elapsed.TotalMilliseconds;
            totalMs += ms;
            if (ms > maxMs) maxMs = ms;
            tickCount++;
        }

        Console.Error.WriteLine("done");
        Console.Error.Flush();

        var memAfter = GC.GetTotalMemory(false);
        var memMb = (memAfter - memBefore) / (1024.0 * 1024.0);
        var avgMs = totalMs / tickCount;
        var ticksPerSec = avgMs > 0 ? 1000.0 / avgMs : 0;

        Console.WriteLine($"{npcCount,-8} {avgMs,-14:F4} {maxMs,-14:F4} {ticksPerSec,-12:F0} {memMb,-10:F1}");
        Console.Out.Flush();
    }

    static void RunDayProfile(int npcCount, int simHours)
    {
        var midnight = new DateTime(1980, 1, 1, 0, 0, 0);
        var (state, behavior) = CreateSimulation(npcCount, midnight);

        Console.WriteLine($"Day Profile ({npcCount} NPCs, {simHours} game-hours, 1s tick delta)");
        Console.WriteLine(new string('-', 60));
        Console.WriteLine($"{"Hour",-8} {"Avg ms/tick",-14} {"Max ms/tick",-14} {"Events",-12} {"Memory MB",-10}");
        Console.WriteLine(new string('-', 60));

        var sw = new Stopwatch();
        var tickDelta = 1.0;
        var ticksPerHour = 3600;

        var totalMs = 0.0;
        var totalMaxMs = 0.0;
        var totalTicks = 0;
        var totalEventsBefore = state.Journal.AllEvents.Count;

        for (int hour = 0; hour < simHours; hour++)
        {
            var hourMs = 0.0;
            var hourMaxMs = 0.0;
            var hourEventsBefore = state.Journal.AllEvents.Count;

            for (int s = 0; s < ticksPerHour; s++)
            {
                sw.Restart();

                state.Clock.Tick(tickDelta);
                foreach (var person in state.People.Values)
                {
                    if (!person.IsAlive) continue;
                    behavior.Update(person, state);
                }

                sw.Stop();
                var ms = sw.Elapsed.TotalMilliseconds;
                hourMs += ms;
                if (ms > hourMaxMs) hourMaxMs = ms;
            }

            totalMs += hourMs;
            if (hourMaxMs > totalMaxMs) totalMaxMs = hourMaxMs;
            totalTicks += ticksPerHour;

            var hourEvents = state.Journal.AllEvents.Count - hourEventsBefore;
            var memMb = GC.GetTotalMemory(false) / (1024.0 * 1024.0);
            var avgMs = hourMs / ticksPerHour;

            var label = $"{hour:D2}:00";
            Console.WriteLine($"{label,-8} {avgMs,-14:F4} {hourMaxMs,-14:F4} {hourEvents,-12} {memMb,-10:F1}");
        }

        var totalEvents = state.Journal.AllEvents.Count - totalEventsBefore;
        var totalAvgMs = totalMs / totalTicks;
        var totalMemMb = GC.GetTotalMemory(false) / (1024.0 * 1024.0);

        Console.WriteLine(new string('-', 60));
        Console.WriteLine($"{"Total",-8} {totalAvgMs,-14:F4} {totalMaxMs,-14:F4} {totalEvents,-12} {totalMemMb,-10:F1}");
    }
}
