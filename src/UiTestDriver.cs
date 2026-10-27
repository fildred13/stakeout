using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Stakeout.Investigation;

namespace Stakeout;

/// <summary>
/// Scripted UI run for development: with "-- --ui-test=DIR [--seed=N]" on the command line it starts a new game,
/// plays the opening of the case through the real screens and saves screenshots to DIR, then quits.
/// Without the argument it does nothing.
/// </summary>
public partial class UiTestDriver : Node
{
    private string _dir;
    private int _shot;

    public override void _Ready()
    {
        var args = OS.GetCmdlineUserArgs();
        var test = args.FirstOrDefault(a => a.StartsWith("--ui-test="));
        if (test == null) return;
        _dir = test["--ui-test=".Length..];
        int seed = int.TryParse(args.FirstOrDefault(a => a.StartsWith("--seed="))?["--seed=".Length..], out var s) ? s : 5;
        DirAccess.MakeDirRecursiveAbsolute(_dir);
        Run(seed);
    }

    private async Task Frames(int n)
    {
        for (int i = 0; i < n; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private void Shot(string name)
    {
        var image = GetViewport().GetTexture().GetImage();
        var path = $"{_dir}/{++_shot:D2}_{name}.png";
        image.SavePng(path);
        GD.Print($"UI-TEST shot {path}");
    }

    private async void Run(int seed)
    {
        try
        {
            await Frames(20);
            Shot("menu");
            var game = GetNode<GameManager>("/root/GameManager");
            ((MainMenu)GetTree().CurrentScene).StartNewGame(seed);
            await Frames(10);
            Shot("loading");
            while (game.Shell == null) await Frames(5);
            var shell = game.Shell;
            await Frames(20);
            Shot("office");

            var session = shell.Session;
            var d = shell.D;
            while (d.Case == null)
            {
                session.AdvanceTo(session.World.Now.AddMinutes(20));
                await Frames(1);
            }
            shell.AfterAction();
            await Frames(10);
            Shot("scanner_alert");
            shell.CloseModal();

            shell.TravelTo(d.Case.SceneAddressId);
            await Frames(30);
            Shot("driving");
            while (d.Travelling) await Frames(5);
            await Frames(15);
            Shot("arrived_at_scene");

            session.Do("enter", d.Case.SceneLocationId);
            session.Do("look");
            session.Do("body");
            foreach (var f in d.FixturesHere().ToList()) session.Do("search", f.Id);
            shell.AfterAction();
            await Frames(10);
            Shot("in_the_scene");

            shell.ShowTab("Notebook");
            await Frames(10);
            Shot("notebook");

            // A neighbour.
            var neighbour = session.World.State.GetLocationsForAddress(d.Case.SceneAddressId)
                .Where(l => l.HasTag("home") && l.Id != d.Case.SceneLocationId)
                .FirstOrDefault(l => session.World.AgentsIn(l.Id).Any(a => a.Person.IsAlive && !a.Asleep));
            if (neighbour != null)
            {
                var answered = (List<int>)session.Do("knock", neighbour.Id);
                if (answered.Count > 0)
                {
                    shell.OpenInterview(answered[0]);
                    await Frames(5);
                    foreach (var topic in new[] { Topic.Introduce, Topic.Victim, Topic.SawAnything, Topic.Enemies })
                        session.Do("ask", answered[0], (long)topic, 0);
                    shell.CloseModal();
                    shell.OpenInterview(answered[0]);
                    await Frames(10);
                    Shot("interview");
                    shell.CloseModal();
                }
            }

            shell.ShowTab("People");
            await Frames(10);
            Shot("people");
            shell.ShowTab("Case");
            await Frames(10);
            Shot("case_file");
            shell.ShowTab("Map");
            await Frames(10);
            Shot("map");

            session.Do("travel", d.Me.HomeAddressId);
            while (d.Travelling) { session.AdvanceTo(session.World.Now.AddMinutes(2)); await Frames(1); }
            session.Do("enter", d.Me.HomeLocationId);
            shell.AfterAction();
            shell.OpenPhone();
            await Frames(10);
            Shot("phone");
            shell.CloseModal();

            // A stakeout on the office building for a while, then the lockpick screen on a neighbour's door.
            session.Do("leave");
            shell.StartStakeoutForTest();
            for (int i = 0; i < 60; i++) await Frames(1);
            Shot("stakeout");
            session.Do("endstakeout");
            shell.SetSpeed(0);
            var locked = d.RoomsHere().FirstOrDefault(l => d.CanEnter(l.Id, out _) == Access.Locked);
            if (locked != null)
            {
                shell.OpenLockpick(locked.Id);
                await Frames(20);
                Shot("lockpick");
                shell.CloseModal();
            }

            var bot = new AutoDetective(d);
            var verdict = bot.Run();
            shell.AfterAction();
            if (verdict != null) shell.ShowVerdict(verdict);
            await Frames(15);
            Shot("verdict");
            GD.Print("UI-TEST done");
        }
        catch (Exception e)
        {
            GD.PrintErr($"UI-TEST failed: {e}");
            Shot("failure");
        }
        GetTree().Quit();
    }
}
