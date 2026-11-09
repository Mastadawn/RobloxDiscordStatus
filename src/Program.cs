using System.Windows.Forms;

namespace RobloxDiscordStatus;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Only one copy should ever run, or you'd get two tray icons fighting over your status.
        // If one's already running, poke it so it opens its window, and then quietly quit.
        using var oneCopyOnly = new Mutex(true, "RobloxDiscordStatus.OneCopyOnly", out bool weAreTheOnlyCopy);
        using var showSignal = new EventWaitHandle(false, EventResetMode.AutoReset, "RobloxDiscordStatus.ShowWindow");
        if (!weAreTheOnlyCopy)
        {
            showSignal.Set();
            return;
        }

        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2); // keeps the window sharp on high-DPI screens
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        // Started by Windows at login? Then stay tucked away in the tray. Opened by hand? Show the window.
        bool startHidden = args.Contains("--tray");
        Application.Run(new TrayApp(startHidden, showSignal));
    }
}
