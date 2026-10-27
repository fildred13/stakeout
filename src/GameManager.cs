using System;
using System.Threading.Tasks;
using Godot;
using Stakeout.Evidence;
using Stakeout.Investigation;

namespace Stakeout;

/// <summary>Autoload that owns the running game between scene changes, and builds new games off the main thread.</summary>
public partial class GameManager : Node
{
    public GameSession Session { get; private set; }
    public EvidenceBoard EvidenceBoard { get; private set; } = new();
    public bool IsGameActive => Session != null;
    /// <summary>The play screen while it is open (the UI test driver uses it).</summary>
    public GameShell Shell { get; set; }

    /// <summary>What the loading screen should do next: a seed for a new game, or save-file text.</summary>
    public int? PendingSeed { get; set; }
    public string PendingSave { get; set; }
    public string LoadStatus { get; private set; } = "";
    public Exception LoadError { get; private set; }

    public override void _Ready()
    {
        // Scripted UI test runs must stay a small, unfocused window: never apply the player's saved fullscreen.
        if (System.Array.Exists(OS.GetCmdlineUserArgs(), a => a.StartsWith("--ui-test="))) return;
        DisplaySettings.Load();
    }

    /// <summary>Builds the pending game on a worker thread. The world is plain C#, so this is safe.</summary>
    public Task BuildPendingAsync()
    {
        LoadError = null;
        int? seed = PendingSeed;
        string save = PendingSave;
        PendingSeed = null;
        PendingSave = null;
        return Task.Run(() =>
        {
            try
            {
                var session = save != null
                    ? GameSession.Load(save, s => LoadStatus = s)
                    : GameSession.NewGame(seed ?? 1, s => LoadStatus = s);
                Session = session;
                EvidenceBoard = new EvidenceBoard();
            }
            catch (Exception e)
            {
                LoadError = e;
            }
        });
    }

    public void EndGame()
    {
        Session = null;
        EvidenceBoard = new EvidenceBoard();
    }

    public const string SaveDir = "user://saves";

    public string SaveGame(string slot)
    {
        DirAccess.MakeDirRecursiveAbsolute(SaveDir);
        var path = $"{SaveDir}/{slot}.json";
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
        file.StoreString(Session.Save());
        return path;
    }
}
