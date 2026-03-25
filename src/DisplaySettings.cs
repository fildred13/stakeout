using Godot;

namespace Stakeout;

public static class DisplaySettings
{
    private const string SettingsPath = "user://settings.cfg";
    private const int BaseViewportHeight = 720;

    public static readonly Vector2I[] SupportedResolutions =
    [
        new(1280, 720),
        new(1920, 1080),
        new(2560, 1080),
        new(2560, 1440),
        new(3440, 1440),
        new(3840, 2160),
    ];

    public static Vector2I CurrentResolution { get; private set; } = new(1920, 1080);
    public static bool IsFullscreen { get; private set; } = false;

    /// <summary>
    /// Load settings from disk. Call once at startup (e.g. in GameManager._Ready).
    /// </summary>
    public static void Load()
    {
        var config = new ConfigFile();
        if (config.Load(SettingsPath) != Error.Ok)
        {
            // No settings file yet — apply defaults
            Apply();
            return;
        }

        var width = (int)config.GetValue("display", "resolution_width", 1920);
        var height = (int)config.GetValue("display", "resolution_height", 1080);
        var fullscreen = (bool)config.GetValue("display", "fullscreen", false);

        CurrentResolution = new Vector2I(width, height);
        IsFullscreen = fullscreen;
        Apply();
    }

    /// <summary>
    /// Save current settings to disk. Call when the user confirms "Keep".
    /// </summary>
    public static void Save()
    {
        var config = new ConfigFile();
        config.SetValue("display", "resolution_width", CurrentResolution.X);
        config.SetValue("display", "resolution_height", CurrentResolution.Y);
        config.SetValue("display", "fullscreen", IsFullscreen);
        config.Save(SettingsPath);
    }

    /// <summary>
    /// Change resolution and apply immediately. Does NOT save to disk.
    /// </summary>
    public static void SetResolution(Vector2I resolution)
    {
        CurrentResolution = resolution;
        Apply();
    }

    /// <summary>
    /// Toggle fullscreen and apply immediately. Does NOT save to disk.
    /// </summary>
    public static void SetFullscreen(bool fullscreen)
    {
        IsFullscreen = fullscreen;
        Apply();
    }

    /// <summary>
    /// Apply current settings to the window and viewport.
    /// </summary>
    private static void Apply()
    {
        var window = ((SceneTree)Engine.GetMainLoop()).Root.GetWindow();

        if (IsFullscreen)
        {
            window.Mode = Window.ModeEnum.Fullscreen;
        }
        else
        {
            window.Mode = Window.ModeEnum.Windowed;
            window.Size = CurrentResolution;
            // Center the window on the screen
            var screenSize = DisplayServer.ScreenGetSize();
            window.Position = (screenSize - CurrentResolution) / 2;
        }
    }

    /// <summary>
    /// Get a display string for a resolution (e.g. "1920 x 1080").
    /// </summary>
    public static string ResolutionToString(Vector2I resolution)
    {
        return $"{resolution.X} x {resolution.Y}";
    }

    /// <summary>
    /// Find the index of the current resolution in SupportedResolutions.
    /// Returns 1 (1920x1080) if not found.
    /// </summary>
    public static int GetCurrentResolutionIndex()
    {
        for (int i = 0; i < SupportedResolutions.Length; i++)
        {
            if (SupportedResolutions[i] == CurrentResolution)
                return i;
        }
        return 1; // default to 1920x1080
    }
}
