using System.Text.Json;
using GLockDown.Core;

namespace GLockDown.Tray;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly string _statusPath;
    private readonly NotifyIcon _icon;
    private readonly DashboardForm _dashboard = new();
    private readonly System.Windows.Forms.Timer _timer;
    private long _lastNotificationSequence;
    private bool _hasReadStatus;
    private string? _lastBlockReason;

    public TrayApplicationContext(string statusPath)
    {
        _statusPath = statusPath;
        var menu = new ContextMenuStrip();
        menu.Items.Add("Open dashboard", null, (_, _) => ShowDashboard());
        menu.Items.Add("Exit tray", null, (_, _) => ExitThread());
        _icon = new NotifyIcon
        {
            Icon = SystemIcons.Shield,
            Text = "G-LOCK-DOWN",
            ContextMenuStrip = menu,
            Visible = true
        };
        _icon.DoubleClick += (_, _) => ShowDashboard();

        _timer = new System.Windows.Forms.Timer { Interval = 1000 };
        _timer.Tick += (_, _) => RefreshStatus();
        _timer.Start();
        ShowDashboard();
    }

    protected override void ExitThreadCore()
    {
        _timer.Stop();
        _timer.Dispose();
        _icon.Visible = false;
        _icon.Dispose();
        _dashboard.ClosePermanently();
        base.ExitThreadCore();
    }

    private void ShowDashboard()
    {
        RefreshStatus();
        _dashboard.Show();
        _dashboard.WindowState = FormWindowState.Normal;
        _dashboard.Activate();
    }

    private void RefreshStatus()
    {
        RuntimeStatus? status = null;
        string? error = null;
        try
        {
            if (File.Exists(_statusPath))
            {
                status = JsonSerializer.Deserialize<RuntimeStatus>(File.ReadAllText(_statusPath));
                if (status is null)
                    error = "status file was empty";
            }
            else
            {
                error = "status file not found";
            }
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            error = exception.Message;
        }

        _dashboard.UpdateStatus(status, error);
        if (status is null)
        {
            _icon.Text = "G-LOCK-DOWN: service unavailable";
            return;
        }

        _icon.Text = status.SharedBlocked
            ? "G-LOCK-DOWN: shared allowance blocked"
            : $"G-LOCK-DOWN: {TimeSpan.FromSeconds(status.SharedRemainingSeconds).TotalMinutes:0} shared minutes left";

        if (!_hasReadStatus)
        {
            _hasReadStatus = true;
            _lastNotificationSequence = status.NotificationSequence;
            _lastBlockReason = status.BlockReason;
            return;
        }

        if (status.NotificationSequence > _lastNotificationSequence && status.Notification is not null)
        {
            ShowNotification("Time limit warning", status.Notification, ToolTipIcon.Warning);
            _lastNotificationSequence = status.NotificationSequence;
        }

        if (status.BlockReason is not null && status.BlockReason != _lastBlockReason)
            ShowNotification("Application blocked", status.BlockReason, ToolTipIcon.Error);
        _lastBlockReason = status.BlockReason;
    }

    private void ShowNotification(string title, string message, ToolTipIcon icon)
    {
        _icon.BalloonTipTitle = title;
        _icon.BalloonTipText = message;
        _icon.BalloonTipIcon = icon;
        _icon.ShowBalloonTip(5000);
    }
}
