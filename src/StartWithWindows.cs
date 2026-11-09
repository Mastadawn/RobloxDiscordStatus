using Microsoft.Win32;

namespace RobloxDiscordStatus;

/// <summary>
/// Turns "start when Windows starts" on or off. Windows runs anything listed under
/// this registry key when you log in. It's per-user, so no admin rights are needed,
/// and you can also see/turn it off in Task Manager's Startup tab.
/// </summary>
public static class StartWithWindows
{
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string ValueName = "RobloxDiscordStatus";

    // --tray tells the app to start hidden in the tray instead of popping the window open
    static string Command => $"\"{Environment.ProcessPath}\" --tray";

    public static bool IsOn
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(ValueName) is string;
        }
    }

    public static void TurnOn()
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        key.SetValue(ValueName, Command);
    }

    public static void TurnOff()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        key?.DeleteValue(ValueName, throwOnMissingValue: false);
    }

    /// <summary>
    /// If you move the .exe after turning this on, the old path would stop working,
    /// so every time the app starts we point the entry at wherever the .exe is now.
    /// </summary>
    public static void FixPathIfMoved()
    {
        if (IsOn)
            TurnOn();
    }
}
