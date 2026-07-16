namespace MFG.Agent.Data;

public sealed class GameEntry
{
    public string Id { get; set; } = null!;
    public string ExecutablePath { get; set; } = null!;
    public string? Arguments { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
