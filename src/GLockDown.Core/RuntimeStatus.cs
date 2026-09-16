namespace GLockDown.Core;

public sealed class RuntimeStatus
{
    public DateTimeOffset UpdatedUtc { get; init; }
    public DateOnly Day { get; init; }
    public double ValorantUsedSeconds { get; init; }
    public double ValorantRemainingSeconds { get; init; }
    public double SharedUsedSeconds { get; init; }
    public double SharedRemainingSeconds { get; init; }
    public bool ValorantBlocked { get; init; }
    public bool SharedBlocked { get; init; }
    public bool Healthy { get; init; } = true;
    public bool DryRun { get; init; }
    public string[] ActiveApplications { get; init; } = [];
    public string? BlockReason { get; init; }
    public long NotificationSequence { get; init; }
    public string? Notification { get; init; }
}
