using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace RobloxDiscordStatus;

/// <summary>
/// A little preview of your Discord status, drawn to look like the card people see
/// on your profile: the picture on the left, then the two lines and a running timer.
/// </summary>
public class StatusCard : Control
{
    CurrentStatus _status = new("Starting up...", null, null, null, null);
    Image? _picture;
    string? _pictureUrl;
    readonly System.Windows.Forms.Timer _clock = new() { Interval = 1000 };

    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };

    public StatusCard()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        Height = 124;

        // Redraw once a second so the timer ticks along like it does on Discord
        _clock.Tick += (_, _) => Invalidate();
        _clock.Start();
    }

    public void Display(CurrentStatus status)
    {
        _status = status;
        if (status.ImageUrl != _pictureUrl)
        {
            _pictureUrl = status.ImageUrl;
            _ = LoadPicture(status.ImageUrl);
        }
        Invalidate();
    }

    async Task LoadPicture(string? url)
    {
        Image? image = null;
        if (url != null)
        {
            try
            {
                var bytes = await Http.GetByteArrayAsync(url);
                image = Image.FromStream(new MemoryStream(bytes));
            }
            catch (Exception) { } // no picture is fine, we just draw an empty square
        }

        // Only use it if nothing newer was asked for while we were downloading
        if (url != _pictureUrl)
        {
            image?.Dispose();
            return;
        }
        _picture?.Dispose();
        _picture = image;
        if (!IsDisposed)
            Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        g.Clear(Parent?.BackColor ?? Theme.Background);

        // The card itself
        using (var card = Theme.Rounded(new RectangleF(0, 0, Width - 1, Height - 1), 10))
        using (var cardFill = new SolidBrush(Theme.Card))
            g.FillPath(cardFill, card);

        // Small header, like Discord's "PLAYING A GAME"
        using var muted = new SolidBrush(Theme.Muted);
        using var text = new SolidBrush(Theme.Text);
        string header = _status.Details == null ? "NOT SHOWING ANYTHING" : _status.Situation.ToUpperInvariant();
        g.DrawString(header, Theme.SectionHeader, muted, 16, 12);

        // The picture, with rounded corners
        var pictureBox = new RectangleF(16, 34, 76, 76);
        using (var clip = Theme.Rounded(pictureBox, 8))
        {
            if (_picture != null)
            {
                var old = g.Clip;
                g.SetClip(clip);
                g.DrawImage(_picture, pictureBox);
                g.Clip = old;
            }
            else
            {
                using var empty = new SolidBrush(Theme.Input);
                g.FillPath(empty, clip);
            }
        }

        // The lines of text, cut off with "..." if they're too long to fit
        float x = 106;
        var lineBox = new RectangleF(x, 0, Width - x - 14, 22);
        using var cutOff = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };

        if (_status.Details == null)
        {
            lineBox.Y = 52;
            g.DrawString(_status.Situation, Theme.Bold, text, lineBox, cutOff);
            lineBox.Y = 74;
            g.DrawString("Your Discord status is empty right now", Theme.Small, muted, lineBox, cutOff);
            return;
        }

        lineBox.Y = 38;
        g.DrawString(_status.Details, Theme.Bold, text, lineBox, cutOff);
        lineBox.Y = 60;
        g.DrawString(_status.State ?? "", Theme.Body, text, lineBox, cutOff);

        if (_status.StartedAt is DateTime started)
        {
            lineBox.Y = 82;
            using var green = new SolidBrush(Theme.Green);
            g.DrawString(Elapsed(started), Theme.Body, green, lineBox, cutOff);
        }
    }

    // Time since you started, written like Discord does it: 5:07 or 1:05:07
    static string Elapsed(DateTime startedUtc)
    {
        var time = DateTime.UtcNow - startedUtc;
        if (time < TimeSpan.Zero)
            time = TimeSpan.Zero;
        return time.TotalHours >= 1
            ? $"{(int)time.TotalHours}:{time.Minutes:00}:{time.Seconds:00} elapsed"
            : $"{time.Minutes}:{time.Seconds:00} elapsed";
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _clock.Dispose();
            _picture?.Dispose();
        }
        base.Dispose(disposing);
    }
}
