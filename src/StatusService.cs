using DiscordRPC;

namespace RobloxDiscordStatus;

/// <summary>What's on your Discord status right now, so the window can show it.</summary>
public record CurrentStatus(string Situation, string? Details, string? State, string? ImageUrl, DateTime? StartedAt);

/// <summary>
/// This is the part that does the actual work. It runs in the background the whole
/// time the app is open (window or no window), watches Roblox, and keeps your Discord
/// status up to date.
/// </summary>
public class StatusService
{
    Settings _settings;
    bool _settingsChanged;
    readonly object _lock = new();

    public CurrentStatus Status { get; private set; } = new("Starting up...", null, null, null, null);
    public string DiscordState { get; private set; } = "Connecting to Discord...";

    // The window listens to these to update itself
    public event Action? StatusChanged;
    public event Action? DiscordStateChanged;

    public StatusService(Settings settings)
    {
        _settings = settings;
    }

    /// <summary>Called when you hit Save in the window. Takes effect on the next check.</summary>
    public void UseSettings(Settings settings)
    {
        lock (_lock)
        {
            _settings = settings;
            _settingsChanged = true;
        }
    }

    public async Task RunAsync(CancellationToken stop)
    {
        if (!Directory.Exists(LogWatcher.LogFolder))
            Log.Info($"Couldn't find the Roblox logs folder at {LogWatcher.LogFolder}. Is Roblox installed?");

        var settings = _settings;
        string? username = null;
        var lastUsernameTry = DateTime.MinValue;

        DiscordRpcClient? discord = null;
        string? discordAppId = null;

        // The 3 newest game logs, and the 2 newest Studio logs (one per open Studio window)
        var games = new LogWatcher("Player", 3, path => new GameLog(path));
        var studios = new LogWatcher("Studio", 2, path => new StudioLog(path));

        string? shown = null; // a short description of what Discord is showing right now

        while (!stop.IsCancellationRequested)
        {
            try
            {
                // Pick up new settings from the window, if there are any
                lock (_lock)
                {
                    if (_settingsChanged)
                    {
                        settings = _settings;
                        _settingsChanged = false;
                        username = null;            // the username might have changed
                        lastUsernameTry = DateTime.MinValue;
                        shown = null;               // redo the status with the new settings
                        Log.Info("Settings saved");
                    }
                }

                // (Re)connect to Discord if we haven't yet, or if the App ID changed
                if (discord == null || discordAppId != settings.DiscordAppId)
                {
                    discord?.Dispose();
                    discord = ConnectToDiscord(settings.DiscordAppId);
                    discordAppId = settings.DiscordAppId;
                    shown = null;
                }

                // Work out your username: from the settings, or dig it out of the logs once a minute
                if (username == null && !string.IsNullOrWhiteSpace(settings.RobloxUsername))
                {
                    username = settings.RobloxUsername.Trim();
                }
                else if (username == null && DateTime.UtcNow - lastUsernameTry > TimeSpan.FromMinutes(1))
                {
                    bool firstTry = lastUsernameTry == DateTime.MinValue;
                    lastUsernameTry = DateTime.UtcNow;
                    username = await UsernameFinder.Find();
                    if (username != null)
                    {
                        Log.Info($"Found your Roblox username: {username}");
                        shown = null;
                    }
                    else if (firstTry)
                    {
                        // Only say this once, not every minute
                        Log.Info("Couldn't find your Roblox username yet. You can type it into Settings.");
                    }
                }

                bool robloxOpen = RunningPrograms.RobloxOpen;
                bool studioOpen = RunningPrograms.StudioOpen;
                int agents = studioOpen ? RunningPrograms.AgentsConnected : 0;

                // Only trust the logs while the program is actually open. That way a crash
                // (which never writes a "left the game" line) can't leave an old game stuck on your status.
                var game = robloxOpen ? games.Check() as GameLog : null;
                var studio = studioOpen ? studios.Check() as StudioLog : null;

                // Work out what SHOULD be showing. The order matters:
                //   in a game > a place open in Studio > Studio start page > Roblox home page > nothing.
                // We describe it as a short string, and only bother Discord when that string changes.
                string wanted;
                string situation;
                if (game != null)
                {
                    // the number on the end ticks over every 5 minutes, so the player count gets refreshed
                    wanted = $"game {game.PlaceId} {game.JobId} {DateTime.UtcNow.Ticks / RobloxApi.PlayerCountRefresh.Ticks}";
                    situation = "In a game";
                }
                else if (studio != null)
                {
                    wanted = $"studio {studio.PlaceId} {studio.Activity} {studio.TeamCreate} {agents}";
                    situation = "In Roblox Studio";
                }
                else if (studioOpen)
                {
                    wanted = $"studio start page {agents}";
                    situation = "Roblox Studio is open";
                }
                else if (robloxOpen)
                {
                    wanted = "roblox home page";
                    situation = "Roblox is open";
                }
                else
                {
                    wanted = "nothing open";
                    situation = "Roblox isn't running";
                }

                if (wanted != shown)
                {
                    RichPresence? presence = null;
                    if (game != null)
                        presence = await StatusBuilder.InGame(game, username, settings);
                    else if (studio != null)
                        presence = await StatusBuilder.InStudio(studio, agents, username);
                    else if (studioOpen)
                        presence = await StatusBuilder.StudioStartPage(agents, username);
                    else if (robloxOpen)
                        presence = await StatusBuilder.RobloxHomePage(username);

                    if (presence != null)
                    {
                        discord.SetPresence(presence);
                        Log.Info($"Now showing: {presence.Details} | {presence.State}");
                    }
                    else
                    {
                        discord.ClearPresence();
                        Log.Info("Roblox and Studio are both closed, cleared your status");
                    }

                    shown = wanted;
                    Status = new CurrentStatus(situation, presence?.Details, presence?.State,
                        presence?.Assets?.LargeImageKey, presence?.Timestamps?.Start);
                    StatusChanged?.Invoke();
                }
            }
            catch (Exception e)
            {
                // Something unexpected went wrong. Note it down and keep going rather than crash.
                Log.Info($"Something went wrong: {e.Message}");
            }

            try { await Task.Delay(TimeSpan.FromSeconds(Math.Max(1, settings.CheckEverySeconds)), stop); }
            catch (TaskCanceledException) { }
        }

        // Closing the client also clears your status on Discord
        discord?.Dispose();
    }

    DiscordRpcClient ConnectToDiscord(string appId)
    {
        // The library keeps the connection alive by itself, and if Discord gets closed and
        // reopened, it reconnects and puts your status back on its own
        var client = new DiscordRpcClient(appId);
        client.OnReady += (_, e) => SetDiscordState($"Connected to Discord as {e.User.Username}");
        client.OnConnectionFailed += (_, _) => SetDiscordState("Can't reach Discord. Is the desktop app open?");
        client.Initialize();
        return client;
    }

    void SetDiscordState(string state)
    {
        if (state == DiscordState)
            return;
        DiscordState = state;
        Log.Info(state);
        DiscordStateChanged?.Invoke();
    }
}
