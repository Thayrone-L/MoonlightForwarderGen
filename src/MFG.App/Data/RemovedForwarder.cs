namespace MFG.App.Data;

public sealed class RemovedForwarder
{
    public string TitleId { get; set; } = null!;
    public string Name { get; set; } = null!;
    public DateTimeOffset RemovedAt { get; set; }
}
