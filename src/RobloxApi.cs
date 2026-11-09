using System.Text;
using System.Text.Json.Nodes;

namespace RobloxDiscordStatus;

/// <summary>Everything we show about a game.</summary>
public record GameInfo(string Name, string? IconUrl, long? UniverseId, string? Creator);

/// <summary>
/// Looks things up on Roblox's public website APIs. None of these need you to be
/// logged in. Anything that fails just returns null, so a hiccup on Roblox's end
/// never crashes the app, your status just shows a bit less for a moment.
/// </summary>
public static class RobloxApi
{
    static readonly HttpClient Http = CreateHttpClient();

    static HttpClient CreateHttpClient()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("RobloxDiscordStatus/1.0");
        return http;
    }

    static async Task<JsonNode?> GetJson(string url)
    {
        try
        {
            return JsonNode.Parse(await Http.GetStringAsync(url));
        }
        catch (Exception e)
        {
            Log.Info($"Roblox lookup failed ({url}): {e.Message}");
            return null;
        }
    }

    // ---------------------------------------------------------------- games

    // We remember games we've already looked up, so we're not asking Roblox
    // the same thing over and over. Capped so it can't grow forever.
    static readonly Dictionary<string, GameInfo> GameCache = new();
    const int GameCacheLimit = 50;

    public static async Task<GameInfo> GetGame(string placeId)
    {
        // Studio files that were never published don't have a real place id
        if (placeId == "0")
            return new GameInfo("an unpublished place", null, null, null);

        if (GameCache.TryGetValue(placeId, out var cached))
            return cached;

        // Place id -> universe id. A "universe" is the whole game, a place is one level inside it.
        var universe = await GetJson($"https://apis.roblox.com/universes/v1/places/{placeId}/universe");
        long? universeId = universe?["universeId"]?.GetValue<long>();
        if (universeId == null)
            return new GameInfo("a Roblox game", null, null, null);

        // The usual games API only says "[TITLE UNAVAILABLE]" for private games
        // (like ones you're still building), but this "develop" one still gives the real name
        var details = await GetJson($"https://develop.roblox.com/v1/universes/multiget?ids={universeId}");
        var game = details?["data"]?[0];
        string name = game?["name"]?.GetValue<string>() ?? "a Roblox game";
        string? creator = game?["creatorName"]?.GetValue<string>();

        var icon = await GetImageUrl(
            $"https://thumbnails.roblox.com/v1/games/icons?universeIds={universeId}&size=512x512&format=Png&isCircular=false");

        var info = new GameInfo(name, icon, universeId, creator);

        if (GameCache.Count >= GameCacheLimit)
            GameCache.Clear(); // simplest way to keep it small, it just refills as you play
        GameCache[placeId] = info;
        return info;
    }

    // The player count changes all the time, so we keep it separate and refresh it every few minutes
    public static readonly TimeSpan PlayerCountRefresh = TimeSpan.FromMinutes(5);
    static long? _countUniverse;
    static int _count;
    static DateTime _countCheckedAt;

    public static async Task<int> GetPlayerCount(long? universeId)
    {
        if (universeId == null)
            return 0;
        if (universeId == _countUniverse && DateTime.UtcNow - _countCheckedAt < PlayerCountRefresh)
            return _count;

        var data = await GetJson($"https://games.roblox.com/v1/games?universeIds={universeId}");
        _count = data?["data"]?[0]?["playing"]?.GetValue<int>() ?? 0;
        _countUniverse = universeId;
        _countCheckedAt = DateTime.UtcNow;
        return _count;
    }

    // ---------------------------------------------------------------- users

    static readonly Dictionary<string, long?> UserIdCache = new();

    public static async Task<long?> GetUserId(string username)
    {
        if (UserIdCache.TryGetValue(username, out var cached))
            return cached;

        // This one has to be a POST with the name in the body, Roblox doesn't take it in the URL
        try
        {
            var request = new JsonObject
            {
                ["usernames"] = new JsonArray(username),
                ["excludeBannedUsers"] = false,
            };
            var body = new StringContent(request.ToJsonString(), Encoding.UTF8, "application/json");
            var response = await Http.PostAsync("https://users.roblox.com/v1/usernames/users", body);
            var json = JsonNode.Parse(await response.Content.ReadAsStringAsync());
            long? id = json?["data"]?[0]?["id"]?.GetValue<long>();
            UserIdCache[username] = id;
            return id;
        }
        catch (Exception e)
        {
            Log.Info($"Couldn't look up the user id for {username}: {e.Message}");
            return null;
        }
    }

    public static async Task<string?> GetUsername(string userId)
    {
        var user = await GetJson($"https://users.roblox.com/v1/users/{userId}");
        return user?["name"]?.GetValue<string>();
    }

    /// <summary>A picture of your avatar: the full body, or just the head.</summary>
    public static async Task<string?> GetAvatarUrl(string? username, bool headOnly)
    {
        if (string.IsNullOrEmpty(username))
            return null;
        var userId = await GetUserId(username);
        if (userId == null)
            return null;

        var url = headOnly
            ? $"https://thumbnails.roblox.com/v1/users/avatar-headshot?userIds={userId}&size=150x150&format=Png&isCircular=false"
            : $"https://thumbnails.roblox.com/v1/users/avatar?userIds={userId}&size=720x720&format=Png&isCircular=false";
        return await GetImageUrl(url);
    }

    /// <summary>All the thumbnail APIs answer in the same shape, so this reads any of them.</summary>
    static async Task<string?> GetImageUrl(string url)
    {
        var data = await GetJson(url);
        var image = data?["data"]?[0];
        if (image?["state"]?.GetValue<string>() != "Completed")
            return null; // Roblox is still making the picture, or it's being moderated
        return image?["imageUrl"]?.GetValue<string>();
    }
}
