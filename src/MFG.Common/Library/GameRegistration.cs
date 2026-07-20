namespace MFG.Common.Library;

public sealed record GameRegistration(
    Guid Id,
    string Name,
    string ExecutablePath,
    string SunshineApplicationName,
    string? MoonlightFavoriteId,
    string NspTitleId,
    string? NspPath,
    GameStatus Status);
