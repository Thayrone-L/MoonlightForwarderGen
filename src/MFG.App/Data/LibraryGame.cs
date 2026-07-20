using MFG.Common.Library;

namespace MFG.App.Data;

public sealed class LibraryGame
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string ExecutablePath { get; set; } = null!;
    public string SunshineApplicationName { get; set; } = null!;
    public string? MoonlightFavoriteId { get; set; }
    public string NspTitleId { get; set; } = null!;
    public string? NspPath { get; set; }
    public GameStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
