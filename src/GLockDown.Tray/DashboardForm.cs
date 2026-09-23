using System.Drawing.Drawing2D;
using GLockDown.Core;

namespace GLockDown.Tray;

internal sealed class DashboardForm : Form
{
    private static readonly Color Background = Color.FromArgb(13, 19, 29);
    private static readonly Color Surface = Color.FromArgb(22, 31, 45);
    private static readonly Color Ink = Color.FromArgb(239, 244, 250);
    private static readonly Color Muted = Color.FromArgb(157, 173, 192);
    private static readonly Color Mint = Color.FromArgb(108, 232, 196);
    private static readonly Color Coral = Color.FromArgb(255, 139, 145);
    private static readonly Color Amber = Color.FromArgb(255, 205, 127);
    private static readonly TimeZoneInfo BangkokTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");

    private readonly List<Font> _fonts = [];
    private readonly ToolTip _tips = new();
    private readonly Label _service;
    private readonly Label _subtitle;
    private readonly BudgetCard _valorant;
    private readonly BudgetCard _shared;
    private readonly Label _reset;
    private readonly Label _resetDetail;
    private readonly Label _active;
    private readonly Label _reason;
    private bool _allowClose;

    public DashboardForm()
    {
        Text = "G-LOCK-DOWN | Time Left";
        AutoScaleDimensions = new SizeF(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(828, 668);
        BackColor = Background;
        ForeColor = Ink;
        Font = MakeFont(10);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        Icon = SystemIcons.Shield;
        DoubleBuffered = true;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, Padding = new Padding(24), ColumnCount = 1, RowCount = 6,
            BackColor = Background
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        foreach (var height in new[] { 42, 78, 222, 112, 118, 48 })
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
        Controls.Add(layout);

        var header = Row(50, 50);
        header.Controls.Add(Label("G /  G-LOCK-DOWN", 11, Mint, true), 0, 0);
        _service = Label("CONNECTING", 9, Muted, true);
        _service.TextAlign = ContentAlignment.MiddleRight;
        header.Controls.Add(_service, 1, 0);
        layout.Controls.Add(header, 0, 0);

        var intro = Column(44, 24);
        intro.Controls.Add(Label("Make time for what matters.", 25, Ink, true));
        _subtitle = Label("Your daily gaming allowance, at a glance.", 10, Muted);
        intro.Controls.Add(_subtitle);
        layout.Controls.Add(intro, 0, 1);

        var cards = Row(50, 50);
        _shared = CreateBudgetCard("SHARED ALLOWANCE", "Valorant + Roblox + Minecraft + Steam", Mint);
        _valorant = CreateBudgetCard("VALORANT", "Also uses your shared allowance", Coral);
        _shared.Container.Margin = new Padding(0, 0, 8, 14);
        _valorant.Container.Margin = new Padding(8, 0, 0, 14);
        cards.Controls.Add(_shared.Container, 0, 0);
        cards.Controls.Add(_valorant.Container, 1, 0);
        layout.Controls.Add(cards, 0, 2);

        var resetCard = new RoundedPanel { Dock = DockStyle.Fill, BackColor = Surface,
            Padding = new Padding(22, 12, 22, 12), Margin = new Padding(0, 0, 0, 14) };
        var resetRow = Row(60, 40);
        var resetCopy = Column(30, 30);
        resetCopy.Controls.Add(Label("A fresh start in", 14, Ink, true));
        _resetDetail = Label("Waiting for the daily reset schedule", 9, Muted);
        resetCopy.Controls.Add(_resetDetail);
        resetRow.Controls.Add(resetCopy, 0, 0);
        _reset = Label("--:--:--", 29, Mint, true, "Consolas");
        _reset.TextAlign = ContentAlignment.MiddleRight;
        resetRow.Controls.Add(_reset, 1, 0);
        resetCard.Controls.Add(resetRow);
        layout.Controls.Add(resetCard, 0, 3);

        var activityCard = new RoundedPanel { Dock = DockStyle.Fill, BackColor = Surface,
            Padding = new Padding(22, 10, 22, 10), Margin = new Padding(0, 0, 0, 10) };
        var activityCopy = Column(23, 30, 25);
        activityCopy.Controls.Add(Label("RIGHT NOW", 9, Muted, true));
        _active = Label("Checking activity...", 12, Ink, true);
        _reason = Label("Waiting for the background service.", 9, Muted);
        activityCopy.Controls.Add(_active);
        activityCopy.Controls.Add(_reason);
        activityCard.Controls.Add(activityCopy);
        layout.Controls.Add(activityCard, 0, 4);

        var footer = Row(77, 23);
        footer.Controls.Add(Label("Closing this window keeps protection running.", 9, Muted));
        var pin = new CheckBox
        {
            Text = "Keep on top", Appearance = Appearance.Button,
            Dock = DockStyle.Fill, FlatStyle = FlatStyle.Flat, TextAlign = ContentAlignment.MiddleCenter,
            BackColor = Surface, ForeColor = Muted, Cursor = Cursors.Hand,
            Margin = new Padding(14, 6, 0, 6), AccessibleName = "Keep dashboard on top"
        };
        pin.FlatAppearance.BorderColor = Color.FromArgb(48, 64, 83);
        pin.FlatAppearance.CheckedBackColor = Color.FromArgb(33, 72, 67);
        pin.CheckedChanged += (_, _) => { TopMost = pin.Checked; pin.ForeColor = pin.Checked ? Mint : Muted; };
        footer.Controls.Add(pin, 1, 0);
        layout.Controls.Add(footer, 0, 5);

        FormClosing += (_, eventArgs) =>
        {
            if (_allowClose) return;
            eventArgs.Cancel = true;
            Hide();
        };
    }

    public void UpdateStatus(RuntimeStatus? status, string? error)
    {
        if (status is null)
        {
            _service.Text = "●  SERVICE UNAVAILABLE";
            _service.ForeColor = Coral;
            _subtitle.Text = "Waiting for a connection to your background service.";
            SetUnknown(_shared);
            SetUnknown(_valorant);
            _reset.Text = "--:--:--";
            _resetDetail.Text = "Reset schedule unavailable";
            _active.Text = "Activity unavailable";
            _reason.Text = error ?? "Waiting for the first status update.";
            _reason.ForeColor = Coral;
            _tips.SetToolTip(_reason, _reason.Text);
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var stale = now - status.UpdatedUtc > TimeSpan.FromSeconds(5);
        _service.Text = !status.Healthy ? "●  ATTENTION NEEDED" : stale ? "●  CONNECTION STALE"
            : status.DryRun ? "●  TEST MODE" : "●  PROTECTION ACTIVE";
        _service.ForeColor = !status.Healthy ? Coral : stale || status.DryRun ? Amber : Mint;
        _subtitle.Text = stale ? $"Last received {status.UpdatedUtc.ToLocalTime():h:mm:ss tt}. Times below may be out of date."
            : status.DryRun ? "Test mode is on. Usage is counted; limits are not enforced."
            : "Your daily gaming allowance, at a glance.";

        var unavailable = !status.Healthy;
        var fullLockDown = status.FullLockDownUntilUtc > now;
        var sharedBlocked = status.SharedBlocked || unavailable;
        var valorantBlocked = status.ValorantBlocked || sharedBlocked;
        SetBudget(_shared, status.SharedUsedSeconds, status.SharedRemainingSeconds,
            sharedBlocked ? 0 : status.SharedRemainingSeconds, sharedBlocked,
            unavailable ? "Access withheld" : fullLockDown ? "Full Lock-Down active" : sharedBlocked ? "Daily limit reached" : "remaining across all tracked games");
        SetBudget(_valorant, status.ValorantUsedSeconds, status.ValorantRemainingSeconds,
            valorantBlocked ? 0 : Math.Min(status.ValorantRemainingSeconds, status.SharedRemainingSeconds),
            valorantBlocked, unavailable ? "Access withheld" : fullLockDown ? "Full Lock-Down active" : valorantBlocked ? "Daily limit reached"
                : status.SharedRemainingSeconds < status.ValorantRemainingSeconds
                    ? "playable time • shared limit applies" : "playable time remaining");

        var legacy = status.NextResetUtc == default;
        var nextResetUtc = legacy && status.Day != default
            ? new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(status.Day.AddDays(1).ToDateTime(TimeOnly.MinValue), BangkokTimeZone))
            : status.NextResetUtc;
        if (nextResetUtc == default)
        {
            _reset.Text = "--:--:--";
            _resetDetail.Text = "Reset schedule unavailable";
        }
        else
        {
            var resetLocal = TimeZoneInfo.ConvertTime(nextResetUtc, BangkokTimeZone);
            _reset.Text = Clock((nextResetUtc - now).TotalSeconds);
            _resetDetail.Text = now >= nextResetUtc ? "Waiting for the service to confirm reset"
                : $"{resetLocal:ddd, MMM d • HH:mm} Bangkok" + (legacy ? " (estimated)" : "");
        }

        _active.Text = stale ? "Activity cannot be confirmed"
            : status.ActiveApplications.Length == 0 ? "No games counting right now"
            : "Counting: " + string.Join(", ", status.ActiveApplications);
        _reason.Text = status.BlockReason ?? (stale ? "Check that the background service is running."
            : "Shared time counts once, even when multiple games are open.");
        if (fullLockDown && !stale && status.Healthy)
            _reason.Text = $"Full Lock-Down: {Clock((status.FullLockDownUntilUtc!.Value - now).TotalSeconds)} remaining";
        _reason.ForeColor = status.BlockReason is not null ? Coral : Muted;
        _tips.SetToolTip(_active, _active.Text);
        _tips.SetToolTip(_reason, _reason.Text);
    }

    public void ClosePermanently()
    {
        _allowClose = true;
        Close();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing) return;
        _tips.Dispose();
        foreach (var font in _fonts) font.Dispose();
    }

    private BudgetCard CreateBudgetCard(string title, string description, Color accent)
    {
        var container = new RoundedPanel { Dock = DockStyle.Fill, BackColor = Surface, Padding = new Padding(22, 14, 22, 14) };
        var content = Column(23, 23, 58, 24, 18, 27);
        content.Controls.Add(Label(title, 10, accent, true));
        content.Controls.Add(Label(description, 9, Muted));
        var time = Label("--:--:--", 33, Ink, true, "Consolas");
        var caption = Label("Waiting for status", 9, Muted);
        var meter = new BudgetMeter { Dock = DockStyle.Fill, Margin = new Padding(0, 6, 0, 6), Accent = accent,
            AccessibleName = title + " daily usage" };
        var used = Label("Usage unavailable", 9, Muted);
        content.Controls.Add(time);
        content.Controls.Add(caption);
        content.Controls.Add(meter);
        content.Controls.Add(used);
        container.Controls.Add(content);
        return new BudgetCard(container, time, caption, used, meter);
    }

    private static void SetUnknown(BudgetCard card)
    {
        card.Time.Text = "--:--:--";
        card.Time.ForeColor = Muted;
        card.Caption.Text = "Waiting for status";
        card.Used.Text = "Usage unavailable";
        card.Meter.Fraction = 0;
    }

    private static void SetBudget(BudgetCard card, double used, double remaining, double playable, bool blocked, string caption)
    {
        card.Time.Text = Clock(playable);
        card.Time.ForeColor = blocked ? Coral : playable <= 900 ? Amber : Ink;
        card.Caption.Text = caption;
        card.Used.Text = $"{Duration(used)} used  /  {Duration(used + remaining)} allowance";
        card.Meter.Fraction = used + remaining > 0 ? used / (used + remaining) : 0;
    }

    private static string Clock(double seconds)
    {
        var span = TimeSpan.FromSeconds(Math.Ceiling(Math.Max(0, seconds)));
        return $"{(int)span.TotalHours:00}:{span.Minutes:00}:{span.Seconds:00}";
    }

    private static string Duration(double seconds)
    {
        var span = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return $"{(int)span.TotalHours}h {span.Minutes:00}m";
    }

    private Font MakeFont(float size, bool bold = false, string family = "Segoe UI")
    {
        var font = new Font(family, size, bold ? FontStyle.Bold : FontStyle.Regular);
        _fonts.Add(font);
        return font;
    }

    private Label Label(string text, float size, Color color, bool bold = false, string family = "Segoe UI") => new()
    {
        Text = text, Dock = DockStyle.Fill, AutoSize = false, AutoEllipsis = true,
        TextAlign = ContentAlignment.MiddleLeft, ForeColor = color,
        Font = MakeFont(size, bold, family), Margin = Padding.Empty, UseMnemonic = false
    };

    private static TableLayoutPanel Row(params float[] widths)
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = widths.Length, RowCount = 1, Margin = Padding.Empty };
        foreach (var width in widths) panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, width));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        return panel;
    }

    private static TableLayoutPanel Column(params int[] heights)
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = heights.Length, Margin = Padding.Empty };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        foreach (var height in heights) panel.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
        return panel;
    }

    private sealed record BudgetCard(RoundedPanel Container, Label Time, Label Caption, Label Used, BudgetMeter Meter);

    private sealed class RoundedPanel : Panel
    {
        public RoundedPanel() => DoubleBuffered = true;

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.Clear(Background);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var radius = 16f * DeviceDpi / 96;
            using var path = new GraphicsPath();
            var bounds = new RectangleF(0.5f, 0.5f, Width - 1, Height - 1);
            path.AddArc(bounds.Left, bounds.Top, radius, radius, 180, 90);
            path.AddArc(bounds.Right - radius, bounds.Top, radius, radius, 270, 90);
            path.AddArc(bounds.Right - radius, bounds.Bottom - radius, radius, radius, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - radius, radius, radius, 90, 90);
            path.CloseFigure();
            using var surface = new SolidBrush(BackColor);
            e.Graphics.FillPath(surface, path);
            using var border = new Pen(Color.FromArgb(44, 58, 76));
            e.Graphics.DrawPath(border, path);
        }
    }

    private sealed class BudgetMeter : Control
    {
        private double _fraction;
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public Color Accent { get; init; }
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public double Fraction
        {
            set { _fraction = Math.Clamp(value, 0, 1); AccessibleDescription = $"{_fraction:P0} used"; Invalidate(); }
        }

        public BudgetMeter() => DoubleBuffered = true;

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using var track = new SolidBrush(Color.FromArgb(49, 63, 81));
            using var fill = new SolidBrush(Accent);
            e.Graphics.FillRectangle(track, ClientRectangle);
            e.Graphics.FillRectangle(fill, 0, 0, (float)(Width * _fraction), Height);
        }
    }
}
