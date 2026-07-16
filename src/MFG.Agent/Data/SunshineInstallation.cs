namespace MFG.Agent.Data;

public sealed class SunshineInstallation
{
    public int Id { get; set; }
    public bool IsInstalled { get; set; }
    public string? ExecutablePath { get; set; }
    public DateTimeOffset LastCheckedAt { get; set; }
}
