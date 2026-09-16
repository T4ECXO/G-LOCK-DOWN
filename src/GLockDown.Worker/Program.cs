using GLockDown.Worker;

var serviceMode = args.Contains("--service", StringComparer.OrdinalIgnoreCase);
var applicationArgs = args.Where(argument =>
    !argument.Equals("--service", StringComparison.OrdinalIgnoreCase)).ToArray();
var options = CommandLine.Parse(applicationArgs, serviceMode);
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
