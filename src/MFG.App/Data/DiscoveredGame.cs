using MFG.Common.Library;

namespace MFG.App.Data;

public sealed class DiscoveredGame
{
    public Guid Id { get; set; }
    public GameSource Source { get; set; }
    public string SourceId { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string InstallDirectory { get; set; } = null!;
    public string? ExecutablePath { get; set; }
    public string? CoverPath { get; set; }
    public string? Description { get; set; }
    public string? Developer { get; set; }
    public string? Publisher { get; set; }
    public DateTimeOffset DiscoveredAt { get; set; }
    public DateTimeOffset? MetadataUpdatedAt { get; set; }
}
