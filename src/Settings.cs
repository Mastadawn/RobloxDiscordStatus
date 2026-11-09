using System.Text.Json;

namespace RobloxDiscordStatus;

/// <summary>
/// Your settings. You change them from the Settings part of the window, and they get
/// saved to config.json in %APPDATA%\RobloxDiscordStatus so they stick around even if
/// you move the .exe somewhere else.
/// </summary>
public class Settings
{
    // This is the Application ID from https://discord.com/developers/applications.
    // Whatever you name that app is the name that shows up at the top of your status.
    public string DiscordAppId { get; set; } = "1522629582887588061";

    // Your Roblox username. Leave it empty and we'll try to figure it out from your logs.
    public string RobloxUsername { get; set; } = "";

    // How often (in seconds) we look for new lines in the Roblox logs.
    public int CheckEverySeconds { get; set; } = 3;

    // Show Join / View Game buttons on your status while you're in a game.
    public bool ShowButtons { get; set; } = true;

    // Show how many people are playing the game you're in.
    public bool ShowPlayerCount { get; set; } = true;

    public static string Folder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RobloxDiscordStatus");

    static string FilePath => Path.Combine(Folder, "config.json");

    static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    /// <summary>True the very first time the app runs (no config file yet).</summary>
    public static bool IsFirstRun => !File.Exists(FilePath);

    public static Settings Load()
    {
        if (!File.Exists(FilePath))
            return new Settings();

        try
        {
            return JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? new Settings();
        }
        catch (Exception e)
        {
            // A broken config file shouldn't stop the whole app, so fall back to the defaults
            Log.Info($"Couldn't read your settings ({e.Message}), using the defaults instead");
            return new Settings();
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(Folder);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonOptions));
    }

    /// <summary>A separate copy, so the window can edit one without touching the one in use.</summary>
    public Settings Copy() => (Settings)MemberwiseClone();
}
