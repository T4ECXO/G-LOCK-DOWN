namespace GLockDown.Core;

public sealed class LimiterSettings
{
    public int ValorantLimitMinutes { get; init; } = 180;
    public int SharedLimitMinutes { get; init; } = 240;
    public int PollIntervalMilliseconds { get; init; } = 1000;
    public string TimeZoneId { get; init; } = "SE Asia Standard Time";
    public List<string> ValorantProcessNames { get; init; } =
        ["VALORANT-Win64-Shipping", "VALORANT"];
    public List<string> RobloxProcessNames { get; init; } = ["RobloxPlayerBeta"];
    public List<string> MinecraftProcessNames { get; init; } = ["Minecraft.Windows"];
    public List<string> MinecraftJavaRuntimePaths { get; init; } = [];
    public List<string> SteamClientProcessNames { get; init; } = ["steam"];
    public List<string> AdditionalSharedProcessNames { get; init; } = [];
    public List<string> IgnoredProcessNames { get; init; } = ["wallpaper32", "wallpaper64"];
    public List<string> SteamLibraryPaths { get; init; } = [];
    public TimeSpan ValorantLimit => TimeSpan.FromMinutes(ValorantLimitMinutes);
    public TimeSpan SharedLimit => TimeSpan.FromMinutes(SharedLimitMinutes);

    public void Validate()
    {
        if (ValorantLimitMinutes <= 0 || SharedLimitMinutes <= 0)
            throw new InvalidOperationException("Usage limits must be positive.");
        if (ValorantLimitMinutes > SharedLimitMinutes)
            throw new InvalidOperationException("The Valorant limit cannot exceed the shared limit.");
        if (PollIntervalMilliseconds is < 250 or > 60_000)
            throw new InvalidOperationException("PollIntervalMilliseconds must be between 250 and 60000.");
        _ = TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);
        foreach (var path in MinecraftJavaRuntimePaths)
            if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
                throw new InvalidOperationException("MinecraftJavaRuntimePaths must contain absolute executable paths.");
    }
}
