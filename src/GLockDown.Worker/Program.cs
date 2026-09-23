using GLockDown.Worker;

var serviceMode = args.Contains("--service", StringComparer.OrdinalIgnoreCase);
var applicationArgs = args.Where(argument =>
    !argument.Equals("--service", StringComparison.OrdinalIgnoreCase)).ToArray();
var options = CommandLine.Parse(applicationArgs, serviceMode);
if (options.FullLockDownMinutes is int minutes)
{
    try
    {
        var store = new GLockDown.Core.FullLockDownStore(Path.Combine(options.DataDirectory, "full-lock-down.json"));
        var until = store.Start(DateTimeOffset.UtcNow, minutes);
        Console.WriteLine($"Full Lock-Down requested until {until.ToLocalTime():yyyy-MM-dd HH:mm:ss zzz}. All tracked games and Steam will be blocked by the running worker on its next scan.");
        Console.WriteLine("The worker must be running with the same data directory and enforcement enabled.");
        return 0;
    }
    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
    {
        Console.Error.WriteLine($"Could not start Full Lock-Down: {exception.Message}");
        return 1;
    }
}
var application = new WorkerApplication();

if (serviceMode)
    return NativeServiceHost.Run(token => application.RunAsync(options, token));

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellation.Cancel();
};
return await application.RunAsync(options, cancellation.Token);
