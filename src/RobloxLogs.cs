using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace RobloxDiscordStatus;

/// <summary>
/// Follows one Roblox log file. Each time we check it, we only read the lines
/// Roblox added since last time, so this stays quick even when a log gets huge.
/// The text gets thrown away right after we look at it. We only hang on to the
/// few facts we care about (which place you're in, when you joined, and so on).
/// </summary>
public abstract class LogFile
{
    public string Path { get; }

    // The place you're in (or have open in Studio), or null if there isn't one
    public string? PlaceId { get; protected set; }

    // When you joined / opened it, used for the timer on your status
    public DateTime? Since { get; protected set; }

    long _bytesRead;                       // how far into the file we've read
    byte[] _leftover = Array.Empty<byte>(); // half a line Roblox hasn't finished writing yet

    const int ChunkSize = 64 * 1024;
    const int MaxLeftover = 64 * 1024;     // if a "line" gets this long without ending, give up on it

    protected LogFile(string path)
    {
        Path = path;
    }

    /// <summary>Reads anything new in the file.</summary>
    public void Check()
    {
        long size;
        try { size = new FileInfo(Path).Length; }
        catch (IOException) { return; }

        if (size < _bytesRead)
        {
            // The file got smaller, which means it was rewritten, so start over from the top
            _bytesRead = 0;
            _leftover = Array.Empty<byte>();
            Reset();
        }

        if (size == _bytesRead)
            return; // nothing new

        try
        {
            // FileShare.ReadWrite matters here, Roblox still has the file open while we read it
            using var stream = new FileStream(Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            stream.Seek(_bytesRead, SeekOrigin.Begin);

            var buffer = new byte[ChunkSize];
            int count;
            while ((count = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                _bytesRead += count;

                // Glue the leftover half-line from last time onto the front of this chunk
                var data = new byte[_leftover.Length + count];
                _leftover.CopyTo(data, 0);
                Array.Copy(buffer, 0, data, _leftover.Length, count);

                // Handle every complete line, and keep whatever comes after the last newline for next time
                int lineStart = 0;
                for (int i = 0; i < data.Length; i++)
                {
                    if (data[i] != (byte)'\n')
                        continue;
                    HandleLine(Encoding.UTF8.GetString(data, lineStart, i - lineStart));
                    lineStart = i + 1;
                }

                _leftover = data[lineStart..];
                if (_leftover.Length > MaxLeftover)
                    _leftover = Array.Empty<byte>();
            }
        }
        catch (IOException)
        {
            // Roblox might be in the middle of deleting or rotating the file, we'll try again next time
        }
    }

    /// <summary>Forget everything we know (the file was closed or rewritten).</summary>
    protected abstract void Reset();

    /// <summary>Look at one line of the log and update what we know.</summary>
    protected abstract void HandleLine(string line);

    /// <summary>
    /// Every log line starts with a timestamp like 2026-11-08T02:00:00.000Z.
    /// We grab it so the timer on your status starts from the real moment you joined.
    /// </summary>
    protected static DateTime LineTime(string line)
    {
        var match = Regex.Match(line, @"^(\d{4}-\d{2}-\d{2}T[\d:.]+Z)");
        if (match.Success && DateTime.TryParse(match.Groups[1].Value, CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var time))
            return time;
        return DateTime.UtcNow;
    }
}

/// <summary>A log from the Roblox game client (the "Player").</summary>
public class GameLog : LogFile
{
    // The server you're in. We use it for the Join button.
    public string? JobId { get; private set; }

    // Looks like: ! Joining game 'JOB-ID' place 1234567 at 1.2.3.4
    static readonly Regex JoinLine = new(@"Joining game '([0-9a-fA-F-]+)' place (\d+)");

    // These two only show up when you actually leave a game. We purposely don't
    // use lines like "Client:Disconnect", because Roblox writes those for the OLD
    // server every time you teleport, right AFTER the new "Joining game" line.
    static readonly Regex[] LeaveLines =
    {
        new(@"leaveUGCGameInternal"),
        new(@"SessionTransitionFSM\] Tearing down"),
    };

    public GameLog(string path) : base(path) { }

    protected override void Reset()
    {
        PlaceId = null;
        Since = null;
        JobId = null;
    }

    protected override void HandleLine(string line)
    {
        var join = JoinLine.Match(line);
        if (join.Success)
        {
            JobId = join.Groups[1].Value;
            PlaceId = join.Groups[2].Value;
            Since = LineTime(line);
            return;
        }

        foreach (var leave in LeaveLines)
        {
            if (leave.IsMatch(line))
            {
                Reset();
                return;
            }
        }
    }
}

/// <summary>A log from Roblox Studio.</summary>
public class StudioLog : LogFile
{
    public bool Playtesting { get; private set; }
    public bool Scripting { get; private set; }   // true when a script tab is in front
    public bool TeamCreate { get; private set; }

    // All of these come straight from real Studio logs:
    // a place was opened:   [FLog::CloseDataModel] Setting place ID 1234567
    static readonly Regex PlaceLine = new(@"\[FLog::CloseDataModel\] Setting place ID (\d+)");
    // what Studio is doing: Setting StudioGameStateType to StudioGameStateType_Edit
    //                       (Edit = editing, PlayClient = playtesting, Null = the place got closed)
    static readonly Regex ModeLine = new(@"Setting StudioGameStateType to StudioGameStateType_(\w+)");
    // which tab is in front: [FLog::RobloxDocManager] Setting current doc to type 2
    //                       (0 = the 3D view, 2 = a script)
    static readonly Regex TabLine = new(@"\[FLog::RobloxDocManager\] Setting current doc to type (-?\d+)");
    // the place joined Team Create: [FLog::TeamCreateJoinPayload] Joining game <id> place 1234567
    static readonly Regex TeamCreateLine = new(@"\[FLog::TeamCreateJoinPayload\] Joining game \S+ place (\d+)");

    public StudioLog(string path) : base(path) { }

    /// <summary>What you're up to, written as the words that go before the game's name.</summary>
    public string Activity
    {
        get
        {
            if (Playtesting) return "Testing";
            if (Scripting) return "Scripting in";
            return "Building in";
        }
    }

    protected override void Reset()
    {
        PlaceId = null;
        Since = null;
        Playtesting = false;
        Scripting = false;
        TeamCreate = false;
    }

    protected override void HandleLine(string line)
    {
        var place = PlaceLine.Match(line);
        if (place.Success)
        {
            // This line shows up again whenever a playtest starts, so only
            // restart the timer if it's actually a different place
            if (place.Groups[1].Value != PlaceId)
            {
                PlaceId = place.Groups[1].Value;
                Since = LineTime(line);
                TeamCreate = false; // the Team Create line (if there is one) comes right after this
            }
            return;
        }

        var teamCreate = TeamCreateLine.Match(line);
        if (teamCreate.Success)
        {
            if (teamCreate.Groups[1].Value == PlaceId)
                TeamCreate = true;
            return;
        }

        var mode = ModeLine.Match(line);
        if (mode.Success)
        {
            if (mode.Groups[1].Value == "Null")
                Reset(); // the place got closed
            else
                Playtesting = mode.Groups[1].Value.StartsWith("Play");
            return;
        }

        var tab = TabLine.Match(line);
        if (tab.Success)
        {
            if (tab.Groups[1].Value == "2") Scripting = true;
            else if (tab.Groups[1].Value == "0") Scripting = false;
        }
    }
}

/// <summary>
/// Keeps an eye on the newest few logs of one kind. Roblox writes to more than one
/// log at the same time (and every Studio window has its own), so if we only looked
/// at the single newest file, your status would flicker back and forth between them.
/// </summary>
public class LogWatcher
{
    // Logs nobody has written to in this long are from old sessions. Skipping them
    // also saves us from reading through a giant old Studio log every time we start.
    static readonly TimeSpan MaxLogAge = TimeSpan.FromHours(6);

    readonly string _kind;                  // "Player" or "Studio"
    readonly int _howMany;
    readonly Func<string, LogFile> _open;
    readonly Dictionary<string, LogFile> _files = new();

    public LogWatcher(string kind, int howMany, Func<string, LogFile> open)
    {
        _kind = kind;
        _howMany = howMany;
        _open = open;
    }

    /// <summary>Returns the log that shows something open (the newest one wins), or null.</summary>
    public LogFile? Check()
    {
        var newest = NewestLogs(_kind, _howMany);

        // Stop tracking files that aren't among the newest anymore
        foreach (var path in _files.Keys.ToList())
            if (!newest.Contains(path))
                _files.Remove(path);

        // Start tracking new ones, and read whatever got added to each
        foreach (var path in newest)
        {
            if (!_files.ContainsKey(path))
                _files[path] = _open(path);
            _files[path].Check();
        }

        return _files.Values
            .Where(log => log.PlaceId != null)
            .OrderByDescending(log => log.Since)
            .FirstOrDefault();
    }

    public static string LogFolder =>
        System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Roblox", "logs");

    /// <summary>
    /// The newest logs of one kind ("Player", "Studio", or null for any), newest first.
    /// Crash handler logs get skipped since they never have anything we want.
    /// </summary>
    public static List<string> NewestLogs(string? kind, int howMany)
    {
        if (!Directory.Exists(LogFolder))
            return new List<string>();

        var pattern = kind == null ? "*.log" : $"*_{kind}_*.log";
        var cutoff = DateTime.Now - MaxLogAge;

        return new DirectoryInfo(LogFolder)
            .GetFiles(pattern)
            .Where(file => !file.Name.Contains("CrashHandler") && file.LastWriteTime >= cutoff)
            .OrderByDescending(file => file.LastWriteTime)
            .Take(howMany)
            .Select(file => file.FullName)
            .ToList();
    }
}
