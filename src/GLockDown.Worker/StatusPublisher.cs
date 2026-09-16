using System.Text.Json;
using GLockDown.Core;

namespace GLockDown.Worker;

internal sealed class StatusPublisher(string path)
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public void Publish(RuntimeStatus status)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path))!;
        Directory.CreateDirectory(directory);
        var temporaryPath = path + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(status, Options));
        File.Move(temporaryPath, path, overwrite: true);
    }
}
