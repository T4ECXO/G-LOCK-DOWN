using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using GLockDown.Core;

namespace GLockDown.Worker;

internal sealed partial class ProcessDetector
{
    private readonly HashSet<string> _valorantNames;
    private readonly HashSet<string> _alwaysSharedNames;
    private readonly HashSet<string> _steamNames;
    private readonly HashSet<string> _minecraftJavaRuntimePaths;
    private readonly HashSet<string> _ignoredNames;
    private readonly string[] _steamRoots;

    public ProcessDetector(LimiterSettings settings)
    {
        _valorantNames = Set(settings.ValorantProcessNames);
        _alwaysSharedNames = Set(settings.RobloxProcessNames
            .Concat(settings.MinecraftProcessNames)
            .Concat(settings.AdditionalSharedProcessNames));
        _steamNames = Set(settings.SteamClientProcessNames);
        _minecraftJavaRuntimePaths = Set(settings.MinecraftJavaRuntimePaths.Select(Path.GetFullPath));
        _ignoredNames = Set(settings.IgnoredProcessNames);
        _steamRoots = DiscoverSteamRoots(settings.SteamLibraryPaths).ToArray();
    }

    public ActivitySnapshot Scan()
    {
        var foregroundPid = GetForegroundProcessId();
        var valorant = new List<DetectedProcess>();
        var sharedActive = new List<DetectedProcess>();
        var allRestricted = new List<DetectedProcess>();

        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                string name;
                try { name = process.ProcessName; }
                catch (InvalidOperationException) { continue; }

                if (_ignoredNames.Contains(name))
                    continue;

                var path = TryGetPath(process);
                var detected = new DetectedProcess(process.Id, name, path);
                if (_valorantNames.Contains(name))
                {
                    valorant.Add(detected);
                    sharedActive.Add(detected);
                    allRestricted.Add(detected);
                }
                else if (_alwaysSharedNames.Contains(name) || IsMinecraftJava(name, path) || IsSteamGame(path))
                {
                    sharedActive.Add(detected);
                    allRestricted.Add(detected);
                }
                else if (_steamNames.Contains(name))
                {
                    allRestricted.Add(detected);
                    if (process.Id == foregroundPid)
                        sharedActive.Add(detected);
                }
            }
        }

        return new ActivitySnapshot(valorant, sharedActive, allRestricted);
    }

    private bool IsSteamGame(string? executablePath)
    {
        if (executablePath is null)
            return false;
        return _steamRoots.Any(root =>
            executablePath.StartsWith(root, StringComparison.OrdinalIgnoreCase) &&
            executablePath.Contains($"{Path.DirectorySeparatorChar}steamapps{Path.DirectorySeparatorChar}common{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase));
    }

    private bool IsMinecraftJava(string name, string? executablePath)
    {
        if (executablePath is null ||
            !name.Equals("javaw", StringComparison.OrdinalIgnoreCase) &&
            !name.Equals("java", StringComparison.OrdinalIgnoreCase))
            return false;

        var path = Path.GetFullPath(executablePath);
        return _minecraftJavaRuntimePaths.Contains(path) ||
            path.Contains($"{Path.DirectorySeparatorChar}.minecraft{Path.DirectorySeparatorChar}runtime{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) ||
            path.Contains($"{Path.DirectorySeparatorChar}Minecraft Launcher{Path.DirectorySeparatorChar}runtime{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> DiscoverSteamRoots(IEnumerable<string> configured)
    {
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in configured)
            AddRoot(roots, path);

        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var defaultRoot = Path.Combine(programFilesX86, "Steam");
        AddRoot(roots, defaultRoot);

        var libraryFile = Path.Combine(defaultRoot, "steamapps", "libraryfolders.vdf");
        if (File.Exists(libraryFile))
        {
            foreach (Match match in SteamPathRegex().Matches(File.ReadAllText(libraryFile)))
                AddRoot(roots, match.Groups[1].Value.Replace("\\\\", "\\"));
        }
        return roots;
    }

    private static void AddRoot(HashSet<string> roots, string path)
    {
        if (!string.IsNullOrWhiteSpace(path))
            roots.Add(Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar);
    }

    private static HashSet<string> Set(IEnumerable<string> values) =>
        new(values, StringComparer.OrdinalIgnoreCase);

    private static string? TryGetPath(Process process)
    {
        try { return process.MainModule?.FileName; }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }

    private static int GetForegroundProcessId()
    {
        var window = GetForegroundWindow();
        if (window == IntPtr.Zero)
            return -1;
        _ = GetWindowThreadProcessId(window, out var processId);
        return unchecked((int)processId);
    }

    [LibraryImport("user32.dll")]
    private static partial IntPtr GetForegroundWindow();

    [LibraryImport("user32.dll")]
    private static partial uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    [GeneratedRegex("\\\"path\\\"\\s+\\\"([^\\\"]+)\\\"", RegexOptions.IgnoreCase)]
    private static partial Regex SteamPathRegex();
}
