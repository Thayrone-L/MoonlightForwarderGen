namespace MFG.Common.Nsp;

public sealed record NspGenerationRequest(
    Guid GameId,
    string TitleId,
    string DisplayName,
    string Publisher,
    string HostIp,
    string MoonlightFavoriteId,
    string MoonlightAppName,
    string MoonlightNroPath,
    string TemplatePath,
    string IconPath,
    string OutputPath);
