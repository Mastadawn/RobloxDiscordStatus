namespace RobloxDiscordStatus;

/// <summary>
/// Keeps a list of recent messages (with the time in front) so the window can show
/// what the app has been up to. There's no console anymore, so this is where they go.
/// </summary>
public static class Log
{
    const int KeepLast = 200; // older messages get dropped so this can't grow forever

    static readonly List<string> Lines = new();
    static readonly object Lock = new();

    // The window listens to this so new messages show up straight away
    public static event Action<string>? NewLine;

    public static void Info(string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] {message}";
        lock (Lock)
        {
            Lines.Add(line);
            if (Lines.Count > KeepLast)
                Lines.RemoveAt(0);
        }
        NewLine?.Invoke(line);
    }

    /// <summary>A copy of the recent messages, oldest first.</summary>
    public static List<string> Recent()
    {
        lock (Lock)
            return new List<string>(Lines);
    }
}
