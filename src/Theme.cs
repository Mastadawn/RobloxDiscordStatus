using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace RobloxDiscordStatus;

/// <summary>
/// The colors and fonts for the window, all in one place so it's easy to restyle.
/// They're picked to sit nicely next to Discord's own dark theme.
/// </summary>
public static class Theme
{
    public static readonly Color Background = Color.FromArgb(49, 51, 56);
    public static readonly Color Card = Color.FromArgb(43, 45, 49);
    public static readonly Color Input = Color.FromArgb(30, 31, 34);
    public static readonly Color Text = Color.FromArgb(219, 222, 225);
    public static readonly Color Muted = Color.FromArgb(148, 155, 164);
    public static readonly Color Accent = Color.FromArgb(79, 70, 229);      // same purple as the icon
    public static readonly Color AccentHover = Color.FromArgb(99, 90, 245);
    public static readonly Color Green = Color.FromArgb(35, 165, 90);
    public static readonly Color Red = Color.FromArgb(242, 63, 67);
    public static readonly Color ToggleOff = Color.FromArgb(78, 80, 88);

    public static readonly Font Body = new("Segoe UI", 9.5f);
    public static readonly Font Bold = new("Segoe UI Semibold", 10f);
    public static readonly Font Title = new("Segoe UI Semibold", 13f);
    public static readonly Font Small = new("Segoe UI", 8.5f);
    public static readonly Font SectionHeader = new("Segoe UI Semibold", 8f);

    /// <summary>A rectangle with rounded corners, used for the card, inputs and switches.</summary>
    public static GraphicsPath Rounded(RectangleF box, float radius)
    {
        var path = new GraphicsPath();
        float d = radius * 2;
        path.AddArc(box.X, box.Y, d, d, 180, 90);
        path.AddArc(box.Right - d, box.Y, d, d, 270, 90);
        path.AddArc(box.Right - d, box.Bottom - d, d, d, 0, 90);
        path.AddArc(box.X, box.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    // Windows 10/11 can draw a window's title bar dark instead of white. It's a hidden
    // option, so we have to ask Windows for it directly. On older Windows it just does nothing.
    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);

    public static void UseDarkTitleBar(Form form)
    {
        try
        {
            int on = 1;
            DwmSetWindowAttribute(form.Handle, 20, ref on, sizeof(int)); // 20 = "use dark mode"
        }
        catch (Exception) { }
    }

    // Same idea for scrollbars: without this, a list gets a bright white scrollbar
    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    static extern int SetWindowTheme(IntPtr window, string appName, string? idList);

    public static void UseDarkScrollbars(Control control)
    {
        try { SetWindowTheme(control.Handle, "DarkMode_Explorer", null); }
        catch (Exception) { }
    }
}

/// <summary>
/// A panel with rounded corners. We put text boxes and the activity list inside one
/// so they get a soft rounded background instead of a hard square border.
/// </summary>
public class RoundedPanel : Panel
{
    public int Radius { get; set; } = 6;

    public RoundedPanel()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Parent?.BackColor ?? Theme.Background);
        using var path = Theme.Rounded(new RectangleF(0, 0, Width - 1, Height - 1), Radius);
        using var fill = new SolidBrush(BackColor);
        g.FillPath(fill, path);
    }
}

/// <summary>
/// An on/off switch like the ones in Discord's settings. Under the hood it's just a
/// checkbox, we only draw it differently.
/// </summary>
public class ToggleSwitch : CheckBox
{
    public ToggleSwitch()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        Size = new Size(40, 24);
        Cursor = Cursors.Hand;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Parent?.BackColor ?? Theme.Background);

        // The track: green when on, gray when off
        using (var track = Theme.Rounded(new RectangleF(0, 2, Width - 1, Height - 5), (Height - 5) / 2f))
        using (var fill = new SolidBrush(Checked ? Theme.Green : Theme.ToggleOff))
            g.FillPath(fill, track);

        // The knob slides to the right when it's on
        float knob = Height - 9;
        float x = Checked ? Width - knob - 4 : 3;
        using var white = new SolidBrush(Color.White);
        g.FillEllipse(white, x, 4, knob, knob);
    }
}
