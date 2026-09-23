using System.Text.Json;

namespace GLockDown.Core;

public sealed class FullLockDownStore(string path)
{
    public DateTimeOffset? Load()
    {
        if (!File.Exists(path)) return null;
        try
        {
            var record = JsonSerializer.Deserialize<LockDownRecord>(File.ReadAllText(path));
            if (record is null || record.UntilUtc == default)
                throw new InvalidDataException("Full Lock-Down deadline is missing.");
            return record.UntilUtc;
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            throw new InvalidDataException("Full Lock-Down record cannot be read; administrator repair is required.", exception);
        }
    }

    public DateTimeOffset Start(DateTimeOffset now, int minutes)
    {
        if (minutes is < 1 or > 525600)
            throw new ArgumentOutOfRangeException(nameof(minutes), "Duration must be between 1 and 525600 minutes.");
        var directory = Path.GetDirectoryName(Path.GetFullPath(path))!;
        Directory.CreateDirectory(directory);
        // Serialize simultaneous commands and never shorten an existing lock-down.
        using var commandLock = new FileStream(path + ".lock", FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
        var requested = now.AddMinutes(minutes);
        var existing = Load();
        var until = existing > requested ? existing.Value : requested;
        var temporaryPath = path + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(new LockDownRecord(until)));
        File.Move(temporaryPath, path, overwrite: true);
        return until;
    }

    private sealed record LockDownRecord(DateTimeOffset UntilUtc);
}
