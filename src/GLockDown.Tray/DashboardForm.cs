using GLockDown.Core;

namespace GLockDown.Tray;

internal sealed class DashboardForm : Form
{
    private readonly Label _service = CreateLabel(16, 18, 440, FontStyle.Bold);
    private readonly Label _valorant = CreateLabel(16, 55, 440);
    private readonly Label _shared = CreateLabel(16, 85, 440);
    private readonly Label _active = CreateLabel(16, 125, 440);
    private readonly Label _reason = CreateLabel(16, 165, 440);
    private bool _allowClose;

    public DashboardForm()
    {
        Text = "G-LOCK-DOWN";
        ClientSize = new Size(475, 235);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        Icon = SystemIcons.Shield;
        Controls.AddRange([_service, _valorant, _shared, _active, _reason]);
        FormClosing += (_, eventArgs) =>
        {
            if (_allowClose)
                return;
            eventArgs.Cancel = true;
            Hide();
        };
    }

    public void UpdateStatus(RuntimeStatus? status, string? error)
    {
        if (status is null)
        {
            _service.Text = $"Service unavailable: {error ?? "waiting for first status"}";
            _service.ForeColor = Color.DarkRed;
            _valorant.Text = "Valorant: unknown";
            _shared.Text = "Shared: unknown";
            _active.Text = "Currently counted: unknown";
            _reason.Text = "Protection status cannot be confirmed.";
            return;
        }

        var stale = DateTimeOffset.UtcNow - status.UpdatedUtc > TimeSpan.FromSeconds(5);
        _service.Text = stale
            ? $"Service status is stale (last update {status.UpdatedUtc.ToLocalTime():T})"
            : status.DryRun ? "Service: dry-run monitoring" : "Service: protection active";
        _service.ForeColor = stale || !status.Healthy ? Color.DarkRed : Color.DarkGreen;
        _valorant.Text = BudgetText("Valorant", status.ValorantUsedSeconds, status.ValorantRemainingSeconds, status.ValorantBlocked);
        _shared.Text = BudgetText("Shared", status.SharedUsedSeconds, status.SharedRemainingSeconds, status.SharedBlocked);
        _active.Text = "Currently counted: " +
            (status.ActiveApplications.Length == 0 ? "none" : string.Join(", ", status.ActiveApplications));
        _reason.Text = status.BlockReason is null ? "No application is currently blocked." : status.BlockReason;
    }

    public void ClosePermanently()
    {
        _allowClose = true;
        Close();
    }

    private static string BudgetText(string name, double usedSeconds, double remainingSeconds, bool blocked)
    {
        var suffix = blocked ? " — BLOCKED" : string.Empty;
        return $"{name}: {Format(usedSeconds)} used, {Format(remainingSeconds)} remaining{suffix}";
    }

    private static string Format(double seconds)
    {
        var span = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return $"{(int)span.TotalHours}h {span.Minutes}m";
    }

    private static Label CreateLabel(int x, int y, int width, FontStyle style = FontStyle.Regular) => new()
    {
        AutoSize = false,
        Location = new Point(x, y),
        Size = new Size(width, 45),
        Font = new Font("Segoe UI", 9F, style),
        AutoEllipsis = true
    };
}
