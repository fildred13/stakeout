using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Stakeout.Simulation;
using Stakeout.Simulation.Generation;

namespace Stakeout.Investigation;

/// <summary>One thing the player did, at a game time, with its arguments.</summary>
public record Command(long AtTicks, string Verb, long[] Args, string Text = null)
{
    public DateTime At => new(AtTicks);
}

/// <summary>
/// A game: a populated city, a few days of real history, a murder planned for the last night of it, and the
/// detective waking up in their office the next morning. Everything the player does goes through
/// <see cref="Do"/> and is logged, so a save file is just the seed plus the command log: loading rebuilds the
/// world and replays the commands, and the deterministic simulation lands in exactly the same state.
/// </summary>
public class GameSession
{
    public const int PreRunDays = 5;
    public const int SaveVersion = 1;

    public int Seed { get; private init; }
    public World World { get; private init; }
    public Detective Detective { get; private init; }
    public List<Command> Log { get; } = new();
    private bool _replaying;

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

    // ---- saving ----

    private record SaveFile(int Version, int Seed, long NowTicks, List<Command> Commands);

    public string Save() =>
        JsonSerializer.Serialize(new SaveFile(SaveVersion, Seed, World.Now.Ticks, Log), new JsonSerializerOptions { WriteIndented = false });

    public static GameSession Load(string json, Action<string> progress = null)
    {
        var file = JsonSerializer.Deserialize<SaveFile>(json);
        if (file.Version != SaveVersion) throw new InvalidOperationException("This save is from another version of the game.");
        var session = NewGame(file.Seed, progress);
        progress?.Invoke("Retracing your steps");
        session._replaying = true;
        foreach (var command in file.Commands)
        {
            if (command.At > session.World.Now) session.World.AdvanceTo(command.At);
            session.Execute(command);
            session.Log.Add(command);
        }
        session._replaying = false;
        if (file.NowTicks > session.World.Now.Ticks) session.World.AdvanceTo(new DateTime(file.NowTicks));
        return session;
    }

    // ---- commands ----

    /// <summary>Runs a player command at the current time and logs it. Returns the command's text result.</summary>
    public object Do(string verb, params long[] args) => Do(verb, null, args);

    public object Do(string verb, string text, params long[] args)
    {
        var command = new Command(World.Now.Ticks, verb, args, text);
        var result = Execute(command);
        if (!_replaying) Log.Add(command);
        return result;
    }

    /// <summary>Lets time pass (waiting, a stakeout, watching the car drive). Logged as the next command's time.</summary>
    public void AdvanceTo(DateTime time) => World.AdvanceTo(time);

    private object Execute(Command c)
    {
        var d = Detective;
        int A(int i) => (int)c.Args[i];
        switch (c.Verb)
        {
            case "travel": return d.TravelTo(A(0), wait: false);
            case "enter": return d.Enter(A(0));
            case "picklock": return d.PickLock(A(0), c.Args[1] != 0);
            case "leave": d.Leave(); return null;
            case "knock": return d.Knock(A(0));
            case "ask": return d.Ask(A(0), (Topic)A(1), c.Args.Length > 2 && c.Args[2] != 0 ? A(2) : null);
            case "look": return d.LookAround();
            case "body": return d.ExamineBody();
            case "search": return d.Search(A(0));
            case "dust": return d.DustForPrints(A(0));
            case "collect": return d.Collect(A(0));
            case "door": return d.ExamineDoor(A(0));
            case "phonebook": return d.PhoneBook(c.Text);
            case "listing": d.LearnListing(A(0)); return null;
            case "reverse": return d.ReverseLookup(c.Text);
            case "call": return d.CallNumber(c.Text);
            case "police": return d.AskPolice((PoliceRequest)A(0), c.Args.Length > 1 && c.Args[1] != 0 ? A(1) : null);
            case "buytap": return d.BuyWiretap();
            case "planttap": return d.PlantWiretap(A(0));
            case "stakeout": return d.StartStakeout();
            case "endstakeout": return d.StopStakeout();
            case "follow":
            {
                var entry = d.StakeoutLog.ElementAtOrDefault(A(0));
                return entry == null ? null : d.Follow(entry, wait: false);
            }
            case "footage": return d.ViewFootage(A(0), new DateTime(c.Args[1]), new DateTime(c.Args[2]));
            case "wait": d.Spend(TimeSpan.FromTicks(c.Args[0])); return null;
            case "submit": return CaseEvaluator.Submit(d, A(0), c.Args.Skip(1).Select(x => (int)x));
            default: throw new InvalidOperationException($"Unknown command {c.Verb}");
        }
    }
}
