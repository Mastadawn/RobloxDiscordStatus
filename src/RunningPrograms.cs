using System.Diagnostics;

namespace RobloxDiscordStatus;

/// <summary>
/// Checks which Roblox programs are open right now. We need this because the logs
/// alone can't tell "Studio is sitting on the start page" apart from "Studio got
/// closed an hour ago", and a crash never writes a "left the game" line either.
/// </summary>
public static class RunningPrograms
{
    // The real program names, taken from the Roblox Versions folder
    const string RobloxPlayer = "RobloxPlayerBeta";
    const string RobloxStudio = "RobloxStudioBeta";
    // AI agents talk to Studio through this. Each agent that's hooked up starts its own copy.
    const string StudioMcp = "StudioMCP";

    // Asking Windows for the process list is cheap, but there's no point doing it every loop
    static readonly TimeSpan CheckEvery = TimeSpan.FromSeconds(5);
    static readonly Dictionary<string, (int Count, DateTime CheckedAt)> Counts = new();

    public static bool RobloxOpen => CountRunning(RobloxPlayer) > 0;
    public static bool StudioOpen => CountRunning(RobloxStudio) > 0;

    /// <summary>
    /// How many AI agents are hooked up to Studio. Heads up: this means "connected",
    /// not "doing something right now". Studio doesn't let us see that part.
    /// </summary>
    public static int AgentsConnected => CountRunning(StudioMcp);

    static int CountRunning(string name)
    {
        if (Counts.TryGetValue(name, out var last) && DateTime.UtcNow - last.CheckedAt < CheckEvery)
            return last.Count;

        // GetProcessesByName hands back objects that hold onto Windows handles,
        // so we dispose them right away instead of leaking a few every check
        var processes = Process.GetProcessesByName(name);
        int count = processes.Length;
        foreach (var process in processes)
            process.Dispose();

        if (Counts.TryGetValue(name, out var before) && before.Count != count)
            Log.Info($"{name}.exe running: {count}");

        Counts[name] = (count, DateTime.UtcNow);
        return count;
    }
}
