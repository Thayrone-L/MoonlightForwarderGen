using MFG.Common.Library;

namespace MFG.App.Services;

public sealed record InstalledGameDiscovery(
    GameSource Source,
    string SourceId,
    string Name,
    string InstallDirectory,
    string? ExecutablePath);

public sealed record GameDiscoveryResult(int Found, int Added, int Updated, int MetadataUpdated);

public sealed record GameDiscoveryProgress(
    int DirectoriesVisited,
    int ExecutablesChecked,
    int CandidatesFound,
    string CurrentLocation);
