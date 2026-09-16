using GLockDown.Core;

var tests = new (string Name, Action Run)[]
{
    ("Valorant consumes both budgets", ValorantConsumesBoth),
    ("Simultaneous apps consume shared time once", SharedOverlapCountsOnce),
    ("Valorant is constrained by prior shared use", PriorSharedUseConstrainsValorant),
    ("Midnight divides a session", MidnightDividesSession),
    ("Backward clock changes do not reset", BackwardClockDoesNotReset),
    ("Warning thresholds fire once", WarningsFireOnce)
};

var failed = 0;
foreach (var test in tests)
{
    try
    {
        test.Run();
        Console.WriteLine($"PASS {test.Name}");
    }
    catch (Exception exception)
    {
        failed++;
        Console.Error.WriteLine($"FAIL {test.Name}: {exception.Message}");
    }
}

Console.WriteLine($"{tests.Length - failed}/{tests.Length} tests passed.");
return failed == 0 ? 0 : 1;

static LimiterSettings Settings() => new()
{
    ValorantLimitMinutes = 180,
    SharedLimitMinutes = 240,
    TimeZoneId = "SE Asia Standard Time"
};

static void ValorantConsumesBoth()
{
    var ledger = NewLedger();
    var status = ledger.Record(At(2026, 9, 16, 10), TimeSpan.FromHours(1), true, false);
    Equal(60, status.ValorantUsed.TotalMinutes);
    Equal(60, status.SharedUsed.TotalMinutes);
}

static void SharedOverlapCountsOnce()
{
    var ledger = NewLedger();
    var status = ledger.Record(At(2026, 9, 16, 10), TimeSpan.FromMinutes(30), false, true);
    Equal(30, status.SharedUsed.TotalMinutes);
}

static void PriorSharedUseConstrainsValorant()
{
    var ledger = NewLedger();
    ledger.Record(At(2026, 9, 16, 10), TimeSpan.FromHours(2.5), false, true);
    var status = ledger.Record(At(2026, 9, 16, 11), TimeSpan.FromHours(1.5), true, false);
    True(status.SharedBlocked);
    Equal(90, status.ValorantUsed.TotalMinutes);
}

static void MidnightDividesSession()
{
    var state = new UsageState
    {
        Day = new DateOnly(2026, 9, 16),
        LastObservedUtc = At(2026, 9, 16, 23, 59, 30)
    };
    var ledger = new UsageLedger(Settings(), state);
    var status = ledger.Record(At(2026, 9, 17, 0, 0, 30), TimeSpan.FromMinutes(1), true, false);
    Equal(new DateOnly(2026, 9, 17), status.Day);
    Equal(0.5, status.ValorantUsed.TotalMinutes);
    Equal(0.5, status.SharedUsed.TotalMinutes);
}

static void BackwardClockDoesNotReset()
{
    var state = new UsageState { Day = new DateOnly(2026, 9, 16) };
    var ledger = new UsageLedger(Settings(), state);
    var status = ledger.Record(At(2026, 9, 15, 10), TimeSpan.FromMinutes(10), false, true);
    Equal(new DateOnly(2026, 9, 16), status.Day);
    Equal(10, status.SharedUsed.TotalMinutes);
}

static void WarningsFireOnce()
{
    var tracker = new WarningTracker();
    var tenMinutes = new UsageStatus(
        new DateOnly(2026, 9, 16), TimeSpan.Zero, TimeSpan.Zero,
        TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(10), false, false);
    Equal(2, tracker.GetNewWarnings(tenMinutes).Count);
    Equal(0, tracker.GetNewWarnings(tenMinutes).Count);
}

static UsageLedger NewLedger() => new(Settings(), new UsageState());

static DateTimeOffset At(int year, int month, int day, int hour, int minute = 0, int second = 0) =>
    new DateTimeOffset(year, month, day, hour, minute, second, TimeSpan.FromHours(7)).ToUniversalTime();

static void Equal<T>(T expected, T actual) where T : notnull
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new Exception($"Expected {expected}, got {actual}.");
}

static void True(bool value)
{
    if (!value)
        throw new Exception("Expected true, got false.");
}
