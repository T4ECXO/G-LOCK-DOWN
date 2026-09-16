namespace GLockDown.Core;

public sealed class WarningTracker
{
    private static readonly TimeSpan[] Thresholds =
        [TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(1)];

    private DateOnly _day;
    private readonly HashSet<string> _sent = [];

    public IReadOnlyList<string> GetNewWarnings(UsageStatus status)
    {
        if (_day != status.Day)
        {
            _day = status.Day;
            _sent.Clear();
        }

        var result = new List<string>();
        AddWarnings("Valorant", status.ValorantRemaining, status.ValorantBlocked, result);
        AddWarnings("Shared", status.SharedRemaining, status.SharedBlocked, result);
        return result;
    }

    private void AddWarnings(string budget, TimeSpan remaining, bool blocked, List<string> result)
    {
        if (blocked)
            return;

        foreach (var threshold in Thresholds)
        {
            var key = $"{budget}:{threshold.TotalMinutes}";
            if (remaining <= threshold && _sent.Add(key))
                result.Add($"{budget} allowance: {threshold.TotalMinutes:0} minute(s) remaining.");
        }
    }
}
