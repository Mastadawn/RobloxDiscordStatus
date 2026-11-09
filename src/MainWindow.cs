using System.Drawing;
using System.Windows.Forms;

namespace RobloxDiscordStatus;

/// <summary>
/// The window you get when you open the app from the tray. The top previews your
/// Discord status the way other people see it, the middle has your settings, and the
/// bottom shows what the app has been doing lately.
///
/// Closing it only hides it. The app keeps running in the tray until you pick Quit.
/// </summary>
public class MainWindow : Form
{
    const int Edge = 20; // space around the edges of the window
    const int ContentWidth = 400;

    readonly StatusService _service;
    Settings _settings;

    readonly StatusCard _card = new();
    readonly Label _connectionDot = new();
    readonly Label _connectionText = new();

    readonly TextBox _username = new();
    readonly TextBox _appId = new();
    readonly ToggleSwitch _showButtons = new();
    readonly ToggleSwitch _showPlayerCount = new();
    readonly ToggleSwitch _startWithWindows = new();
    readonly Label _savedNote = new();

    readonly ListBox _activity = new();

    public bool ReallyClosing { get; set; } // set when you pick Quit, so we don't just hide

    public MainWindow(StatusService service, Settings settings)
    {
        _service = service;
        _settings = settings;

        Text = "Roblox Discord Status";
        Icon = TrayApp.AppIcon;
        BackColor = Theme.Background;
        ForeColor = Theme.Text;
        Font = Theme.Body;
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(Edge * 2 + ContentWidth, 696);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;

        BuildHeader();
        BuildStatus();
        BuildSettings();
        BuildActivity();

        FillInSettings();
        _card.Display(_service.Status);
        ShowConnection();
        foreach (var line in Log.Recent())
            _activity.Items.Add(line);
        ScrollActivityToBottom();

        // These fire from the background, so each handler hops back onto the window's own thread
        _service.StatusChanged += OnStatusChanged;
        _service.DiscordStateChanged += OnDiscordStateChanged;
        Log.NewLine += OnNewLogLine;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Theme.UseDarkTitleBar(this); // so the title bar isn't a white strip on top of a dark window
    }

    // ---------------------------------------------------------------- building the window

    void BuildHeader()
    {
        var logo = new PictureBox
        {
            Image = TrayApp.AppIcon.ToBitmap(),
            SizeMode = PictureBoxSizeMode.Zoom,
            Location = new Point(Edge, 16),
            Size = new Size(28, 28),
        };
        var title = new Label
        {
            Text = "Roblox Discord Status",
            Font = Theme.Title,
            Location = new Point(Edge + 38, 17),
            AutoSize = true,
        };
        Controls.Add(logo);
        Controls.Add(title);
    }

    void BuildStatus()
    {
        _card.Location = new Point(Edge, 56);
        _card.Size = new Size(ContentWidth, 116);
        Controls.Add(_card);

        // A little colored dot and a line saying whether we're connected to Discord
        _connectionDot.Text = "●";
        _connectionDot.Location = new Point(Edge, 178);
        _connectionDot.AutoSize = true;
        _connectionText.Location = new Point(Edge + 16, 180);
        _connectionText.AutoSize = true;
        _connectionText.Font = Theme.Small;
        _connectionText.ForeColor = Theme.Muted;
        Controls.Add(_connectionDot);
        Controls.Add(_connectionText);
    }

    void BuildSettings()
    {
        AddSectionHeader("SETTINGS", 214);

        AddLabel("Roblox username", 236);
        AddTextBox(_username, 256);
        AddHint("Leave it empty to find it automatically.", 291);

        AddLabel("Discord App ID", 316);
        AddTextBox(_appId, 336);
        AddHint("The name of this Discord app shows at the top of your status.", 371);

        AddToggle(_showButtons, "Show Join and View Game buttons", 400);
        AddToggle(_showPlayerCount, "Show how many people are playing", 430);
        AddToggle(_startWithWindows, "Start when Windows starts", 460);

        var save = new Button
        {
            Text = "Save",
            Location = new Point(Edge, 500),
            Size = new Size(96, 32),
            FlatStyle = FlatStyle.Flat,
            BackColor = Theme.Accent,
            ForeColor = Color.White,
            Font = Theme.Bold,
            Cursor = Cursors.Hand,
        };
        save.FlatAppearance.BorderSize = 0;
        save.FlatAppearance.MouseOverBackColor = Theme.AccentHover;
        save.Click += (_, _) => SaveSettings();
        Controls.Add(save);
        AcceptButton = save; // pressing Enter in a text box saves too

        _savedNote.Location = new Point(Edge + 108, 508);
        _savedNote.AutoSize = true;
        _savedNote.ForeColor = Theme.Green;
        _savedNote.Font = Theme.Small;
        Controls.Add(_savedNote);
    }

    void BuildActivity()
    {
        AddSectionHeader("RECENT ACTIVITY", 548);

        var box = new RoundedPanel
        {
            BackColor = Theme.Card,
            Location = new Point(Edge, 568),
            Size = new Size(ContentWidth, 92),
            Padding = new Padding(10, 8, 6, 6),
        };
        _activity.Dock = DockStyle.Fill;
        _activity.BorderStyle = BorderStyle.None;
        _activity.BackColor = Theme.Card;
        _activity.ForeColor = Theme.Muted;
        _activity.Font = Theme.Small;
        _activity.HandleCreated += (_, _) => Theme.UseDarkScrollbars(_activity);
        box.Controls.Add(_activity);
        Controls.Add(box);

        AddHint("Closing this window keeps the app running in your tray.", 670);
    }

    // Small helpers so the building code above reads like a list of what's on screen

    void AddSectionHeader(string text, int y) => Controls.Add(new Label
    {
        Text = text,
        Font = Theme.SectionHeader,
        ForeColor = Theme.Muted,
        Location = new Point(Edge, y),
        AutoSize = true,
    });

    void AddLabel(string text, int y) => Controls.Add(new Label
    {
        Text = text,
        Location = new Point(Edge, y),
        AutoSize = true,
    });

    void AddHint(string text, int y) => Controls.Add(new Label
    {
        Text = text,
        Font = Theme.Small,
        ForeColor = Theme.Muted,
        Location = new Point(Edge, y),
        AutoSize = true,
    });

    void AddTextBox(TextBox textBox, int y)
    {
        // The text box sits inside a rounded dark panel, which gives it the padded look
        var box = new RoundedPanel
        {
            BackColor = Theme.Input,
            Location = new Point(Edge, y),
            Size = new Size(ContentWidth, 32),
            Padding = new Padding(10, 7, 10, 0),
            Cursor = Cursors.IBeam,
        };
        textBox.BorderStyle = BorderStyle.None;
        textBox.BackColor = Theme.Input;
        textBox.ForeColor = Theme.Text;
        textBox.Dock = DockStyle.Fill;
        box.Click += (_, _) => textBox.Focus(); // clicking the padding still puts the cursor in the box
        box.Controls.Add(textBox);
        Controls.Add(box);
    }

    void AddToggle(ToggleSwitch toggle, string text, int y)
    {
        var label = new Label { Text = text, Location = new Point(Edge, y + 3), AutoSize = true, Cursor = Cursors.Hand };
        label.Click += (_, _) => toggle.Checked = !toggle.Checked; // clicking the words flips the switch too
        toggle.Location = new Point(Edge + ContentWidth - toggle.Width, y);
        Controls.Add(label);
        Controls.Add(toggle);
    }

    // ---------------------------------------------------------------- settings

    void FillInSettings()
    {
        _username.Text = _settings.RobloxUsername;
        _appId.Text = _settings.DiscordAppId;
        _showButtons.Checked = _settings.ShowButtons;
        _showPlayerCount.Checked = _settings.ShowPlayerCount;
        _startWithWindows.Checked = StartWithWindows.IsOn;
    }

    void SaveSettings()
    {
        var appId = _appId.Text.Trim();
        if (appId.Length == 0 || !appId.All(char.IsDigit))
        {
            MessageBox.Show(this, "The Discord App ID should only be numbers. You can copy it from the " +
                                  "Discord Developer Portal, on your app's General Information page.",
                "Roblox Discord Status", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // Make a fresh copy so the background service never sees a half-edited one
        var updated = _settings.Copy();
        updated.RobloxUsername = _username.Text.Trim();
        updated.DiscordAppId = appId;
        updated.ShowButtons = _showButtons.Checked;
        updated.ShowPlayerCount = _showPlayerCount.Checked;

        try
        {
            updated.Save();

            if (_startWithWindows.Checked)
                StartWithWindows.TurnOn();
            else
                StartWithWindows.TurnOff();
        }
        catch (Exception e)
        {
            MessageBox.Show(this, $"Couldn't save your settings: {e.Message}", "Roblox Discord Status",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        _settings = updated;
        _service.UseSettings(updated);
        _savedNote.Text = $"Saved at {DateTime.Now:h:mm tt}";
    }

    /// <summary>Puts the cursor in the username box, for the tray's Settings option.</summary>
    public void ShowSettings()
    {
        BringToFront();
        _username.Focus();
        _username.SelectAll();
    }

    /// <summary>Keeps the Start with Windows switch in sync when it's changed from the tray menu.</summary>
    public void RefreshStartWithWindows() => _startWithWindows.Checked = StartWithWindows.IsOn;

    // ---------------------------------------------------------------- live updates

    void ShowConnection()
    {
        var state = _service.DiscordState;
        _connectionText.Text = state;
        _connectionDot.ForeColor = state.StartsWith("Connected") ? Theme.Green
                                 : state.StartsWith("Can't") ? Theme.Red
                                 : Theme.Muted;
    }

    void OnStatusChanged() => RunOnWindowThread(() => _card.Display(_service.Status));

    void OnDiscordStateChanged() => RunOnWindowThread(ShowConnection);

    void OnNewLogLine(string line) => RunOnWindowThread(() =>
    {
        _activity.Items.Add(line);
        while (_activity.Items.Count > 200)
            _activity.Items.RemoveAt(0);
        ScrollActivityToBottom();
    });

    void ScrollActivityToBottom()
    {
        if (_activity.Items.Count > 0)
            _activity.TopIndex = _activity.Items.Count - 1;
    }

    // Window controls can only be touched from the thread that made them, and our updates
    // come from the background, so this passes the work over to the right thread
    void RunOnWindowThread(Action action)
    {
        if (IsDisposed || !IsHandleCreated)
            return;
        try { BeginInvoke(action); }
        catch (InvalidOperationException) { } // the window was closing at that exact moment
    }

    // ---------------------------------------------------------------- closing

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        // Clicking the X just hides the window, the app keeps going in the tray
        if (e.CloseReason == CloseReason.UserClosing && !ReallyClosing)
        {
            e.Cancel = true;
            Hide();
            return;
        }
        base.OnFormClosing(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Stop listening, otherwise the service and log would keep this window alive in memory
            _service.StatusChanged -= OnStatusChanged;
            _service.DiscordStateChanged -= OnDiscordStateChanged;
            Log.NewLine -= OnNewLogLine;
        }
        base.Dispose(disposing);
    }
}
