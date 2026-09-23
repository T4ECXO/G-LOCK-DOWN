namespace GLockDown.Worker;

internal sealed record CommandLine(string DataDirectory, bool DryRun, bool Once, int? FullLockDownMinutes)
{
    public static CommandLine Parse(string[] args, bool serviceMode)
    {
        var defaultRoot = serviceMode
            ? Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData)
            : Environment.CurrentDirectory;
        var dataDirectory = Path.Combine(defaultRoot, serviceMode ? "G-Lock-Down" : "data");
        var dryRun = false;
        var once = false;
        int? fullLockDownMinutes = null;

        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--data-dir" when index + 1 < args.Length:
                    dataDirectory = Path.GetFullPath(args[++index]);
                    break;
                case "--dry-run":
                    dryRun = true;
                    break;
                case "--once":
                    once = true;
                    break;
                case "--full-lock-down" when index + 1 < args.Length:
                    if (!int.TryParse(args[++index], out var minutes) || minutes is < 1 or > 525600)
                        throw new ArgumentException("--full-lock-down requires a whole number of minutes between 1 and 525600.");
                    fullLockDownMinutes = minutes;
                    break;
                default:
                    throw new ArgumentException($"Unknown or incomplete argument: {args[index]}");
            }
        }

        if (serviceMode && (dryRun || once))
            throw new ArgumentException("--dry-run and --once cannot be used with --service.");
        if (fullLockDownMinutes.HasValue && (serviceMode || dryRun || once))
            throw new ArgumentException("--full-lock-down is a standalone command and cannot be combined with --service, --dry-run, or --once.");
        return new CommandLine(dataDirectory, dryRun, once, fullLockDownMinutes);
    }
}
