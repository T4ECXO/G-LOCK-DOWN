using System.ComponentModel;
using System.Runtime.InteropServices;

namespace GLockDown.Worker;

internal static partial class NativeServiceHost
{
    private const uint ServiceWin32OwnProcess = 0x10;
    private const uint ServiceStopped = 0x1;
    private const uint ServiceStartPending = 0x2;
    private const uint ServiceStopPending = 0x3;
    private const uint ServiceRunning = 0x4;
    private const uint ServiceAcceptStop = 0x1;
    private const uint ServiceAcceptShutdown = 0x4;
    private const uint ControlStop = 0x1;
    private const uint ControlShutdown = 0x5;
    private const int ErrorFailedServiceControllerConnect = 1063;
    private const string ServiceName = "GLockDown";

    private static readonly ServiceMainCallback MainCallback = ServiceMain;
    private static readonly ServiceControlCallback ControlCallback = ServiceControl;
    private static Func<CancellationToken, Task<int>>? _run;
    private static CancellationTokenSource? _cancellation;
    private static IntPtr _statusHandle;
    private static ServiceStatus _status;

    public static unsafe int Run(Func<CancellationToken, Task<int>> run)
    {
        _run = run;
        var serviceName = Marshal.StringToHGlobalUni(ServiceName);
        try
        {
            var table = stackalloc ServiceTableEntry[2];
            table[0] = new ServiceTableEntry
            {
                Name = serviceName,
                Procedure = Marshal.GetFunctionPointerForDelegate(MainCallback)
            };
            table[1] = default;

            if (!StartServiceCtrlDispatcher(table))
            {
                var error = Marshal.GetLastWin32Error();
                var message = error == ErrorFailedServiceControllerConnect
                    ? "Service mode must be started by the Windows Service Control Manager."
                    : new Win32Exception(error).Message;
                Console.Error.WriteLine(message);
                return error;
            }
            return 0;
        }
        finally
        {
            Marshal.FreeHGlobal(serviceName);
        }
    }

    private static void ServiceMain(uint argumentCount, IntPtr arguments)
    {
        _ = argumentCount;
        _ = arguments;
        _statusHandle = RegisterServiceCtrlHandlerEx(ServiceName, ControlCallback, IntPtr.Zero);
        if (_statusHandle == IntPtr.Zero)
            return;

        _cancellation = new CancellationTokenSource();
        Report(ServiceStartPending, 0, 10_000);
        Report(ServiceRunning, ServiceAcceptStop | ServiceAcceptShutdown, 0);

        uint exitCode = 0;
        try
        {
            exitCode = unchecked((uint)(_run?.Invoke(_cancellation.Token).GetAwaiter().GetResult() ?? 1));
        }
        catch
        {
            exitCode = 1;
        }
        finally
        {
            Report(ServiceStopped, 0, 0, exitCode);
            _cancellation.Dispose();
            _cancellation = null;
        }
    }

    private static uint ServiceControl(uint control, uint eventType, IntPtr eventData, IntPtr context)
    {
        _ = eventType;
        _ = eventData;
        _ = context;
        if (control is ControlStop or ControlShutdown)
        {
            Report(ServiceStopPending, 0, 10_000);
            _cancellation?.Cancel();
        }
        return 0;
    }

    private static void Report(uint state, uint acceptedControls, uint waitHint, uint exitCode = 0)
    {
        _status = new ServiceStatus
        {
            ServiceType = ServiceWin32OwnProcess,
            CurrentState = state,
            ControlsAccepted = acceptedControls,
            Win32ExitCode = exitCode,
            WaitHint = waitHint
        };
        _ = SetServiceStatus(_statusHandle, ref _status);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ServiceTableEntry
    {
        public IntPtr Name;
        public IntPtr Procedure;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ServiceStatus
    {
        public uint ServiceType;
        public uint CurrentState;
        public uint ControlsAccepted;
        public uint Win32ExitCode;
        public uint ServiceSpecificExitCode;
        public uint CheckPoint;
        public uint WaitHint;
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate void ServiceMainCallback(uint argumentCount, IntPtr arguments);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate uint ServiceControlCallback(uint control, uint eventType, IntPtr eventData, IntPtr context);

    [LibraryImport("advapi32.dll", EntryPoint = "StartServiceCtrlDispatcherW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static unsafe partial bool StartServiceCtrlDispatcher(ServiceTableEntry* serviceTable);

    [LibraryImport("advapi32.dll", EntryPoint = "RegisterServiceCtrlHandlerExW", SetLastError = true,
        StringMarshalling = StringMarshalling.Utf16)]
    private static partial IntPtr RegisterServiceCtrlHandlerEx(
        string serviceName,
        ServiceControlCallback callback,
        IntPtr context);

    [LibraryImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetServiceStatus(IntPtr statusHandle, ref ServiceStatus status);
}
