using System.Text.Json;

namespace GLockDown.Core;

public sealed class JsonStateStore(string path)
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public UsageState Load()
    {
        if (!File.Exists(path))
            return new UsageState();

        try
        {
            return JsonSerializer.Deserialize<UsageState>(File.ReadAllText(path), Options)
                ?? throw new InvalidDataException("The usage state was empty.");
        }
        catch (Exception exception) when (exception is JsonException or IOException)
        {
            throw new InvalidDataException(
                "Usage records could not be read. Access must remain blocked until an administrator repairs them.",
                exception);
        }
    }

    public void Save(UsageState state)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path))!;
        Directory.CreateDirectory(directory);
        var temporaryPath = path + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(state, Options));
        File.Move(temporaryPath, path, overwrite: true);
    }
}
