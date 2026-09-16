namespace GLockDown.Core;

public sealed class UsageLedger
{
    private readonly LimiterSettings _settings;
    private readonly TimeZoneInfo _timeZone;

    public UsageLedger(LimiterSettings settings, UsageState state)
    {
        _settings = settings;
        _timeZone = TimeZoneInfo.FindSystemTimeZoneById(settings.TimeZoneId);
        State = state;
    }

    public UsageState State { get; }

    public UsageStatus Record(
        DateTimeOffset observedUtc,
        TimeSpan monotonicElapsed,
        bool valorantActive,
        bool sharedActivityActive)
    {
        if (monotonicElapsed < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(monotonicElapsed));

        var localDay = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(observedUtc, _timeZone).DateTime);
        EnsureInitialized(localDay);

        if (localDay > State.Day)
        {
            var oldDayElapsed = TimeUntilNextLocalMidnight(State.LastObservedUtc ?? observedUtc, State.Day);
            var beforeReset = oldDayElapsed < TimeSpan.Zero
                ? TimeSpan.Zero
                : Min(oldDayElapsed, monotonicElapsed);
            Add(beforeReset, valorantActive, sharedActivityActive);
            Reset(localDay);
            Add(monotonicElapsed - beforeReset, valorantActive, sharedActivityActive);
        }
        else
        {
            // A backward wall-clock change never grants a fresh allowance.
            Add(monotonicElapsed, valorantActive, sharedActivityActive);
        }

        State.LastObservedUtc = observedUtc;
        return GetStatus();
    }

    public UsageStatus GetStatus()
    {
        var valorantUsed = TimeSpan.FromSeconds(State.ValorantSeconds);
        var sharedUsed = TimeSpan.FromSeconds(State.SharedSeconds);
        return new UsageStatus(
            State.Day,
            valorantUsed,
            sharedUsed,
            Max(TimeSpan.Zero, _settings.ValorantLimit - valorantUsed),
            Max(TimeSpan.Zero, _settings.SharedLimit - sharedUsed),
            valorantUsed >= _settings.ValorantLimit,
            sharedUsed >= _settings.SharedLimit);
    }

    public DateTimeOffset GetNextResetUtc()
    {
        var localMidnight = State.Day.AddDays(1).ToDateTime(TimeOnly.MinValue);
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(localMidnight, _timeZone));
    }

    private void EnsureInitialized(DateOnly localDay)
    {
        if (State.Day == default)
            Reset(localDay);
    }

    private void Reset(DateOnly day)
    {
        State.Day = day;
        State.ValorantSeconds = 0;
        State.SharedSeconds = 0;
    }

    private void Add(TimeSpan elapsed, bool valorantActive, bool sharedActivityActive)
    {
        if (valorantActive)
            State.ValorantSeconds += elapsed.TotalSeconds;
        if (valorantActive || sharedActivityActive)
            State.SharedSeconds += elapsed.TotalSeconds;
    }

    private TimeSpan TimeUntilNextLocalMidnight(DateTimeOffset fromUtc, DateOnly day)
    {
        var localMidnight = day.AddDays(1).ToDateTime(TimeOnly.MinValue);
        var midnightUtc = TimeZoneInfo.ConvertTimeToUtc(localMidnight, _timeZone);
        return midnightUtc - fromUtc.UtcDateTime;
    }

    private static TimeSpan Min(TimeSpan left, TimeSpan right) => left <= right ? left : right;
    private static TimeSpan Max(TimeSpan left, TimeSpan right) => left >= right ? left : right;
}
