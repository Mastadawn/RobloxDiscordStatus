using DiscordRPC;
using Button = DiscordRPC.Button; // Windows Forms has a Button too, this one is the Discord kind

namespace RobloxDiscordStatus;

/// <summary>
/// Turns "what you're doing" into what Discord actually shows.
/// Discord gives us two short lines of text, a big picture, a small picture
/// in the corner of the big one, a timer, and up to two buttons.
/// The text you see when hovering a picture is a handy place to put extra stuff.
/// </summary>
public static class StatusBuilder
{
    /// <summary>
    /// You're in a game:
    ///   Jailbreak
    ///   @YourName · 5,986 playing
    ///   (game icon, your avatar in the corner, Join + View Game buttons)
    /// </summary>
    public static async Task<RichPresence> InGame(GameLog game, string? username, Settings settings)
    {
        var info = await RobloxApi.GetGame(game.PlaceId!);

        // Build the second line out of whichever bits we have, joined with dots
        var bits = new List<string>();
        if (username != null)
            bits.Add($"@{username}");
        if (settings.ShowPlayerCount)
        {
            int playing = await RobloxApi.GetPlayerCount(info.UniverseId);
            if (playing > 0)
                bits.Add($"{playing:N0} playing");
        }
        var secondLine = bits.Count > 0 ? string.Join(" · ", bits) : "In game";

        var presence = new RichPresence
        {
            // StatusDisplay.Details makes your status bar say "Playing Jailbreak"
            // instead of "Playing <whatever your Discord app is called>"
            StatusDisplay = StatusDisplayType.Details,
            Details = Fit(info.Name),
            State = Fit(secondLine),
            Timestamps = new Timestamps(game.Since ?? DateTime.UtcNow),
            Assets = new Assets
            {
                LargeImageKey = info.IconUrl,
                LargeImageText = Fit(info.Name),
            },
        };

        // Your avatar's head as the little circle on the game icon
        var head = await RobloxApi.GetAvatarUrl(username, headOnly: true);
        if (head != null)
        {
            presence.Assets.SmallImageKey = head;
            presence.Assets.SmallImageText = username;
        }

        if (settings.ShowButtons)
        {
            // Discord only shows your buttons to other people, never to you, so don't
            // panic when you can't see them on your own profile.
            // The Join link asks Roblox for your exact server, but Roblox sometimes
            // ignores that part and drops people into a different server of the same game.
            var buttons = new List<Button>();
            if (game.JobId != null)
            {
                buttons.Add(new Button
                {
                    Label = "Join",
                    Url = $"https://www.roblox.com/games/start?placeId={game.PlaceId}&gameInstanceId={game.JobId}",
                });
            }
            buttons.Add(new Button
            {
                Label = "View Game",
                Url = $"https://www.roblox.com/games/{game.PlaceId}",
            });
            presence.Buttons = buttons.ToArray();
        }

        return presence;
    }

    /// <summary>
    /// You've got a place open in Studio:
    ///   Roblox Studio · 2 Agents connected
    ///   Scripting in My Game
    ///   (hover the game icon: "My Game by My Group", hover your avatar: "Team Create")
    /// </summary>
    public static async Task<RichPresence> InStudio(StudioLog studio, int agents, string? username)
    {
        var info = await RobloxApi.GetGame(studio.PlaceId!);

        var hover = info.Creator != null ? $"{info.Name} by {info.Creator}" : info.Name;

        var presence = new RichPresence
        {
            StatusDisplay = StatusDisplayType.Details,
            Details = StudioTitle(agents),
            State = Fit($"{studio.Activity} {info.Name}"),
            Timestamps = new Timestamps(studio.Since ?? DateTime.UtcNow),
            Assets = new Assets
            {
                LargeImageKey = info.IconUrl,
                LargeImageText = Fit(hover),
            },
        };

        var head = await RobloxApi.GetAvatarUrl(username, headOnly: true);
        if (head != null)
        {
            presence.Assets.SmallImageKey = head;
            presence.Assets.SmallImageText = studio.TeamCreate ? "Team Create" : username;
        }

        return presence;
    }

    /// <summary>
    /// Studio's open but you haven't opened a place:
    ///   Roblox Studio · 1 Agent connected
    ///   No game open
    /// </summary>
    public static async Task<RichPresence> StudioStartPage(int agents, string? username)
    {
        var presence = new RichPresence
        {
            StatusDisplay = StatusDisplayType.Details,
            Details = StudioTitle(agents),
            State = "No game open",
        };
        await AddFullAvatar(presence, username);
        return presence;
    }

    /// <summary>
    /// Roblox is open but you're not in a game:
    ///   Roblox
    ///   @YourName · No game open
    /// </summary>
    public static async Task<RichPresence> RobloxHomePage(string? username)
    {
        var presence = new RichPresence
        {
            StatusDisplay = StatusDisplayType.Details,
            Details = "Roblox",
            State = username != null ? $"@{username} · No game open" : "No game open",
        };
        await AddFullAvatar(presence, username);
        return presence;
    }

    // ---------------------------------------------------------------- helpers

    // "Roblox Studio", plus "· 2 Agents connected" when there are any
    static string StudioTitle(int agents)
    {
        if (agents == 0)
            return "Roblox Studio";
        return $"Roblox Studio · {agents} Agent{(agents == 1 ? "" : "s")} connected";
    }

    // Your whole avatar as the big picture, for the "No game open" statuses
    static async Task AddFullAvatar(RichPresence presence, string? username)
    {
        var avatar = await RobloxApi.GetAvatarUrl(username, headOnly: false);
        if (avatar != null)
            presence.Assets = new Assets { LargeImageKey = avatar, LargeImageText = username };
    }

    // Discord won't take more than 128 characters on a line, so trim anything longer
    static string Fit(string text) => text.Length <= 128 ? text : text[..128];
}
