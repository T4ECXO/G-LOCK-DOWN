namespace GLockDown.Worker;

internal sealed record CommandLine(string DataDirectory, bool DryRun, bool Once)
{
    public static CommandLine Parse(string[] args, bool serviceMode)
    {
        var defaultRoot = serviceMode
            ? Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData)
            : Environment.CurrentDirectory;
        var dataDirectory = Path.Combine(defaultRoot, serviceMode ? "G-Lock-Down" : "data");
        var dryRun = false;
        var once = false;

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
                default:
                    throw new ArgumentException($"Unknown or incomplete argument: {args[index]}");
            }
        }

        if (serviceMode && (dryRun || once))
            throw new ArgumentException("--dry-run and --once cannot be used with --service.");
        return new CommandLine(dataDirectory, dryRun, once);
    }
}
