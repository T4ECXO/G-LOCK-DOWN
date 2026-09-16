using System.Diagnostics;
using System.Text.Json;
using GLockDown.Core;

namespace GLockDown.Worker;

internal sealed class WorkerApplication
{
    public async Task<int> RunAsync(CommandLine options, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(options.DataDirectory);
        var settingsPath = Path.Combine(options.DataDirectory, "settings.json");
        var statePath = Path.Combine(options.DataDirectory, "usage.json");
        var statusPath = Path.Combine(options.DataDirectory, "Public", "status.json");
        var settings = LoadOrCreateSettings(settingsPath);
        settings.Validate();

        var store = new JsonStateStore(statePath);
        var stateCorrupt = false;
        string? healthFailure = null;
        UsageState state;
        try
        {
            state = store.Load();
        }
        catch (InvalidDataException exception)
        {
            stateCorrupt = true;
            healthFailure = exception.Message;
            state = new UsageState();
            Console.Error.WriteLine(exception.Message);
        }

        var ledger = new UsageLedger(settings, state);
        var detector = new ProcessDetector(settings);
        var warnings = new WarningTracker();
        var publisher = new StatusPublisher(statusPath);
        var stopwatch = Stopwatch.StartNew();
        var previousElapsed = stopwatch.Elapsed;
        long notificationSequence = 0;

        Console.WriteLine(options.DryRun
            ? "G-LOCK-DOWN is monitoring in dry-run mode; no processes will be closed."
            : "G-LOCK-DOWN enforcement is active.");

        do
        {
            var nowElapsed = stopwatch.Elapsed;
            var elapsed = nowElapsed - previousElapsed;
            previousElapsed = nowElapsed;
            var snapshot = detector.Scan();
            var status = ledger.Record(
                DateTimeOffset.UtcNow,
                elapsed,
                snapshot.ValorantActive,
                snapshot.SharedActive);

            if (!stateCorrupt)
                store.Save(state);

            string? notification = null;
            foreach (var warning in warnings.GetNewWarnings(status))
            {
                notification = warning;
                notificationSequence++;
                Console.WriteLine($"WARNING: {warning}");
            }

            string? blockReason = null;
            if (stateCorrupt)
            {
                blockReason = "Usage records are damaged; access is withheld until administrator repair.";
                Terminate(snapshot.AllSharedRestricted, blockReason, options.DryRun);
            }
            else if (status.SharedBlocked)
            {
                blockReason = "Shared daily allowance exhausted.";
                Terminate(snapshot.AllSharedRestricted, blockReason, options.DryRun);
            }
            else if (status.ValorantBlocked)
            {
                blockReason = "Valorant daily allowance exhausted.";
                Terminate(snapshot.Valorant, blockReason, options.DryRun);
            }

            publisher.Publish(new RuntimeStatus
            {
                UpdatedUtc = DateTimeOffset.UtcNow,
                Day = status.Day,
                NextResetUtc = ledger.GetNextResetUtc(),
                ValorantUsedSeconds = status.ValorantUsed.TotalSeconds,
                ValorantRemainingSeconds = status.ValorantRemaining.TotalSeconds,
                SharedUsedSeconds = status.SharedUsed.TotalSeconds,
                SharedRemainingSeconds = status.SharedRemaining.TotalSeconds,
                ValorantBlocked = stateCorrupt || status.ValorantBlocked,
                SharedBlocked = stateCorrupt || status.SharedBlocked,
                Healthy = !stateCorrupt,
                DryRun = options.DryRun,
                ActiveApplications = snapshot.SharedProcesses
                    .Select(process => process.Name)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Order(StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
                BlockReason = blockReason ?? healthFailure,
                NotificationSequence = notificationSequence,
                Notification = notification
            });

            Console.WriteLine(
                $"{DateTime.Now:T} | Valorant {Format(status.ValorantUsed)}/{settings.ValorantLimitMinutes}m | " +
                $"Shared {Format(status.SharedUsed)}/{settings.SharedLimitMinutes}m | " +
                $"Active: {Describe(snapshot)}");

            if (!options.Once)
            {
                try
                {
                    await Task.Delay(settings.PollIntervalMilliseconds, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
            }
        } while (!options.Once && !cancellationToken.IsCancellationRequested);

        return stateCorrupt ? 2 : 0;
    }

    private static LimiterSettings LoadOrCreateSettings(string path)
    {
        var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
        if (File.Exists(path))
        {
            return JsonSerializer.Deserialize<LimiterSettings>(File.ReadAllText(path), jsonOptions)
                ?? throw new InvalidDataException("settings.json was empty.");
        }

        var settings = new LimiterSettings();
        File.WriteAllText(path, JsonSerializer.Serialize(settings, jsonOptions));
        Console.WriteLine($"Created default settings at {path}");
        return settings;
    }

    private static void Terminate(IEnumerable<DetectedProcess> processes, string reason, bool dryRun)
    {
        foreach (var target in processes.DistinctBy(process => process.Id))
        {
            Console.WriteLine($"BLOCK: {target.Name} (PID {target.Id}): {reason}");
            if (dryRun)
                continue;

            try
            {
                using var process = Process.GetProcessById(target.Id);
                process.Kill(entireProcessTree: true);
            }
            catch (Exception exception) when (
                exception is InvalidOperationException or ArgumentException or System.ComponentModel.Win32Exception)
            {
                Console.Error.WriteLine($"Could not terminate PID {target.Id}: {exception.Message}");
            }
        }
    }

    private static string Format(TimeSpan value) => $"{value.TotalMinutes:0.0}m";

    private static string Describe(ActivitySnapshot snapshot)
    {
        var names = snapshot.SharedProcesses
            .Select(process => process.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase);
        return names.Any() ? string.Join(", ", names) : "none";
    }
}
