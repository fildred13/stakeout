using System;
using Stakeout.Simulation;
using Stakeout.Simulation.Generation;

namespace Stakeout.Investigation;

/// <summary>
/// A new game: a populated city, a few days of real history, a murder planned for the last night of it, and
/// the detective waking up in their office the next morning.
/// </summary>
public class GameSession
{
    public const int PreRunDays = 5;

    public int Seed { get; private init; }
    public World World { get; private init; }
    public Detective Detective { get; private init; }

    public static GameSession NewGame(int seed, Action<string> progress = null)
    {
        progress?.Invoke("Laying out the city");
        var world = WorldBuilder.Build(seed);
        var player = PlayerSetup.AddPlayer(world);
        world.Start();
        var firstMorning = world.Now.Date.AddDays(PreRunDays).AddHours(6).AddMinutes(30);
        world.Crimes.ScheduleMurder(firstMorning.Date.AddDays(-1));
        for (var day = world.Now.Date.AddDays(1); day < firstMorning; day = day.AddDays(1))
        {
            progress?.Invoke($"Living through {day.AddDays(-1):dddd}");
            world.AdvanceTo(day);
        }
        world.AdvanceTo(firstMorning);
        return new GameSession { Seed = seed, World = world, Detective = new Detective(world, player) };
    }
}
