namespace GLockDown.Core;

public sealed class UsageState
{
    public DateOnly Day { get; set; }
    public double ValorantSeconds { get; set; }
    public double SharedSeconds { get; set; }
    public DateTimeOffset? LastObservedUtc { get; set; }
}

public readonly record struct UsageStatus(
    DateOnly Day,
    TimeSpan ValorantUsed,
    TimeSpan SharedUsed,
    TimeSpan ValorantRemaining,
    TimeSpan SharedRemaining,
    bool ValorantBlocked,
    bool SharedBlocked);
