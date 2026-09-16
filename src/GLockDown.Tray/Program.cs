using GLockDown.Tray;

var defaultStatusPath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
    "G-Lock-Down",
    "Public",
    "status.json");
var statusPath = args.Length switch
{
    0 => defaultStatusPath,
    2 when args[0].Equals("--status-file", StringComparison.OrdinalIgnoreCase) => Path.GetFullPath(args[1]),
    _ => throw new ArgumentException("Usage: GLockDown.Tray [--status-file <path>]")
};

ApplicationConfiguration.Initialize();
Application.Run(new TrayApplicationContext(statusPath));
