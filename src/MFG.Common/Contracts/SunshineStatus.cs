namespace MFG.Common.Contracts;

public sealed record SunshineStatus(
    bool IsInstalled,
    string? ExecutablePath,
    DateTimeOffset LastCheckedAt,
    bool IsRunning,
    bool IsReady,
    string RuntimeMessage,
    string? WebUiUrl);
