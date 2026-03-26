using System;
using System.Diagnostics;
using Stakeout.Simulation;
using Stakeout.Simulation.Sublocations;
using Stakeout.Simulation.Entities;
using Stakeout.Simulation.Scheduling;

namespace Stakeout.Benchmarks;

class Program
{
    static void Main(string[] args)
    {
        SublocationGeneratorRegistry.RegisterAll();
        var npcCounts = new[] { 50, 200, 500, 1000 };
        var simHours = 24;

        Console.WriteLine("Sublocation Simulation Benchmark");
        Console.WriteLine($"Simulating {simHours} in-game hours");
        Console.WriteLine(new string('-', 60));
        Console.WriteLine($"{"NPCs",-8} {"Avg ms/tick",-14} {"Max ms/tick",-14} {"Ticks/sec",-12} {"Memory MB",-10}");
        Console.WriteLine(new string('-', 60));

        foreach (var count in npcCounts)
        {
            RunBenchmark(count, simHours);
        }
    }

    static void RunBenchmark(int npcCount, int simHours)
    {
        var state = new SimulationState();
        var mapConfig = new MapConfig();
        var locationGenerator = new LocationGenerator(mapConfig);
        var personGenerator = new PersonGenerator(locationGenerator, mapConfig);
        var behavior = new PersonBehavior(mapConfig);

        locationGenerator.GenerateCityScaffolding(state);

        for (int i = 0; i < npcCount; i++)
        {
            personGenerator.GeneratePerson(state);
        }

        // Simulate
        var tickCount = 0;
        var totalMs = 0.0;
        var maxMs = 0.0;
        var sw = new Stopwatch();
        var tickDelta = 1.0; // 1 second per tick
        var totalSeconds = simHours * 3600;

        var memBefore = GC.GetTotalMemory(true);

        for (int s = 0; s < totalSeconds; s++)
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

        var memAfter = GC.GetTotalMemory(false);
        var memMb = (memAfter - memBefore) / (1024.0 * 1024.0);
        var avgMs = totalMs / tickCount;
        var ticksPerSec = 1000.0 / avgMs;

        Console.WriteLine($"{npcCount,-8} {avgMs,-14:F4} {maxMs,-14:F4} {ticksPerSec,-12:F0} {memMb,-10:F1}");
    }
}
