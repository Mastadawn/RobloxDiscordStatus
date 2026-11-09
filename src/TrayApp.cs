using System.Drawing;
using System.Windows.Forms;

namespace RobloxDiscordStatus;

/// <summary>
/// The tray icon, and the boss of the whole app. It starts the background service,
/// owns the window, and the app keeps running for as long as this is alive, even with
/// the window closed. Right-click the icon for the menu, or double-click to open the window.
/// </summary>
public class TrayApp : ApplicationContext
{
    readonly NotifyIcon _tray = new();
    readonly ToolStripMenuItem _startWithWindowsItem = new("Start with Windows");
    readonly Control _uiThread = new(); // a hidden helper for running things on the UI thread
    readonly StatusService _service;
    readonly CancellationTokenSource _stop = new();
    readonly Task _serviceTask;
    readonly RegisteredWaitHandle _showWait;
    MainWindow? _window;
    bool _toldAboutTray;

    /// <summary>The app's icon (the one baked into the .exe), with a plain fallback just in case.</summary>
    public static Icon AppIcon { get; } = LoadIcon();

    public TrayApp(bool startHidden, EventWaitHandle showSignal)
    {
        _uiThread.CreateControl();

        var settings = Settings.Load();
        if (Settings.IsFirstRun)
        {
            // First time running: save the defaults and turn on starting with Windows,
            // since that's the whole point. It can be switched off in Settings or the tray menu.
            settings.Save();
            TryToggleStartWithWindows(true);
            Log.Info("First run! Turned on Start with Windows. You can switch it off in Settings.");
        }
        else
        {
            StartWithWindows.FixPathIfMoved();
        }

        // The tray icon and its right-click menu
        var menu = new ContextMenuStrip();
        var open = new ToolStripMenuItem("Open", null, (_, _) => ShowWindow());
        open.Font = new Font(open.Font, FontStyle.Bold); // bold = what double-clicking does
        menu.Items.Add(open);
        menu.Items.Add(new ToolStripMenuItem("Settings", null, (_, _) => ShowWindow(goToSettings: true)));
        _startWithWindowsItem.Click += (_, _) => TryToggleStartWithWindows(!StartWithWindows.IsOn);
        menu.Items.Add(_startWithWindowsItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("Quit", null, (_, _) => Quit()));
        menu.Opening += (_, _) => _startWithWindowsItem.Checked = StartWithWindows.IsOn;

        _tray.Icon = AppIcon;
        _tray.Text = "Roblox Discord Status";
        _tray.ContextMenuStrip = menu;
        _tray.DoubleClick += (_, _) => ShowWindow();
        _tray.Visible = true;

        // Start the part that does the actual work, in the background
        _service = new StatusService(settings);
        _service.StatusChanged += () => RunOnUiThread(UpdateTrayTooltip);
        _serviceTask = Task.Run(() => _service.RunAsync(_stop.Token));

        // If you open the .exe again while it's already running, the new copy pokes this
        // signal and quits, and we pop our window open instead of running twice
        _showWait = ThreadPool.RegisterWaitForSingleObject(showSignal,
            (_, _) => RunOnUiThread(() => ShowWindow()), null, Timeout.Infinite, executeOnlyOnce: false);

        if (!startHidden)
            ShowWindow();
    }

    void ShowWindow(bool goToSettings = false)
    {
        // We make the window once and then just show/hide it
        _window ??= new MainWindow(_service, Settings.Load());

        _window.Show();
        if (_window.WindowState == FormWindowState.Minimized)
            _window.WindowState = FormWindowState.Normal;
        _window.Activate();

        if (goToSettings)
            _window.ShowSettings();

        // The first time you close the window, remind people where the app went
        _window.VisibleChanged -= OnWindowVisibleChanged;
        _window.VisibleChanged += OnWindowVisibleChanged;
    }

    void OnWindowVisibleChanged(object? sender, EventArgs e)
    {
        if (_window is { Visible: false } && !_toldAboutTray)
        {
            _toldAboutTray = true;
            _tray.ShowBalloonTip(3000, "Still running",
                "Roblox Discord Status is still going in your tray. Right-click the icon to quit.", ToolTipIcon.Info);
        }
    }

    void TryToggleStartWithWindows(bool turnOn)
    {
        try
        {
            if (turnOn) StartWithWindows.TurnOn();
            else StartWithWindows.TurnOff();
        }
        catch (Exception e)
        {
            Log.Info($"Couldn't change Start with Windows: {e.Message}");
        }
        _window?.RefreshStartWithWindows();
    }

    void UpdateTrayTooltip()
    {
        // Hovering the tray icon shows what's going on. Windows cuts tooltips off at 63 characters.
        var text = $"Roblox Discord Status\n{_service.Status.Situation}";
        _tray.Text = text.Length <= 63 ? text : text[..63];
    }

    void Quit()
    {
        // Stop the background service and give it a moment to clear your Discord status
        _stop.Cancel();
        _serviceTask.Wait(TimeSpan.FromSeconds(3));

        _showWait.Unregister(null);
        _tray.Visible = false; // otherwise a dead icon hangs around in the tray until you hover it
        _tray.Dispose();

        if (_window != null)
        {
            _window.ReallyClosing = true;
            _window.Close();
            _window.Dispose();
        }
        _uiThread.Dispose();

        ExitThread();
    }

    void RunOnUiThread(Action action)
    {
        if (_uiThread.IsDisposed)
            return;
        try { _uiThread.BeginInvoke(action); }
        catch (InvalidOperationException) { } // we're in the middle of quitting
    }

    static Icon LoadIcon()
    {
        try
        {
            if (Environment.ProcessPath != null)
                return Icon.ExtractAssociatedIcon(Environment.ProcessPath) ?? SystemIcons.Application;
        }
        catch (Exception) { }
        return SystemIcons.Application;
    }
}
