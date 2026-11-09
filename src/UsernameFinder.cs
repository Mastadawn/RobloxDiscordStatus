using System.Text.RegularExpressions;

namespace RobloxDiscordStatus;

/// <summary>
/// Digs through your newest Roblox logs to figure out your username, so most people
/// never have to type it in. We look for your numeric user id first (more reliable)
/// and ask Roblox what name it belongs to. If that doesn't pan out, we fall back to a
/// username written straight into a log.
/// </summary>
public static class UsernameFinder
{
    // Different ways your user id or username shows up in the logs
    static readonly Regex[] UserIdPatterns =
    {
        new(@"""userId""\s*:\s*(\d+)"),
        new(@"userId[:\s]+(\d+)"),
        new(@"""UserId""\s*:\s*(\d+)"),
        new(@"AccountId[:\s]+(\d+)"),
        new(@"userid[:\s]+(\d+)"),
    };
    static readonly Regex[] UsernamePatterns =
    {
        new(@"""name""\s*:\s*""([A-Za-z0-9_]{3,20})""\s*,\s*""displayName"""),
        new(@"username[""\s:=]+([A-Za-z0-9_]{3,20})"),
    };

    public static async Task<string?> Find()
    {
        var triedIds = new HashSet<string>();
        string? fallback = null;

        foreach (var path in LogWatcher.NewestLogs(null, 6))
        {
            try
            {
                // Read line by line instead of loading the whole file, some of these get big
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var reader = new StreamReader(stream);
                string? line;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    foreach (var pattern in UserIdPatterns)
                    {
                        var match = pattern.Match(line);
                        if (!match.Success || !triedIds.Add(match.Groups[1].Value))
                            continue;

                        var name = await RobloxApi.GetUsername(match.Groups[1].Value);
                        if (name != null)
                            return name;
                    }

                    if (fallback == null)
                    {
                        foreach (var pattern in UsernamePatterns)
                        {
                            var match = pattern.Match(line);
                            if (match.Success)
                            {
                                fallback = match.Groups[1].Value;
                                break;
                            }
                        }
                    }
                }
            }
            catch (IOException)
            {
                // The file got deleted or locked while we were reading it, just move on to the next one
            }
        }

        return fallback;
    }
}
