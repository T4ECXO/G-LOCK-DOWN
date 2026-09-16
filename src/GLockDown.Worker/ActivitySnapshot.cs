namespace GLockDown.Worker;

internal sealed record ActivitySnapshot(
    IReadOnlyList<DetectedProcess> Valorant,
    IReadOnlyList<DetectedProcess> SharedProcesses,
    IReadOnlyList<DetectedProcess> AllSharedRestricted)
{
    public bool ValorantActive => Valorant.Count > 0;
    public bool SharedActive => SharedProcesses.Count > 0;
}

internal sealed record DetectedProcess(int Id, string Name, string? ExecutablePath);
