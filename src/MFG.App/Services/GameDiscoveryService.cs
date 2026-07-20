using System.Net.Http;
using System.IO;
using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using MFG.App.Data;
using MFG.Common.Library;

namespace MFG.App.Services;

public sealed class GameDiscoveryService
{
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(15) };
    private static readonly string MetadataDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MoonlightForwarderGenerator",
        "metadata",
        "covers");

    public async Task<GameDiscoveryResult> RefreshAsync(
        bool includeLocalExecutableScan = false,
        IProgress<GameDiscoveryProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var games = DiscoverSteamGames().Concat(DiscoverEpicGames()).ToList();
        if (includeLocalExecutableScan)
            games.AddRange(await DiscoverLocalGamesAsync(progress, cancellationToken));
        games = games
            .GroupBy(game => string.IsNullOrWhiteSpace(game.ExecutablePath)
                ? $"{game.Source}:{game.SourceId}"
                : game.ExecutablePath, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();

        var added = 0;
        var updated = 0;
        var metadataUpdated = 0;
        await using var database = MfgDbContext.Create();
        var scannedSources = includeLocalExecutableScan
            ? new[] { GameSource.Steam, GameSource.EpicGames, GameSource.Emulator, GameSource.LocalScan }
            : new[] { GameSource.Steam, GameSource.EpicGames };
        var foundKeys = games.Select(game => $"{game.Source}:{game.SourceId}").ToHashSet(StringComparer.OrdinalIgnoreCase);
        var staleGames = await database.DiscoveredGames
            .Where(game => scannedSources.Contains(game.Source))
            .ToListAsync(cancellationToken);
        database.DiscoveredGames.RemoveRange(staleGames.Where(game => !foundKeys.Contains($"{game.Source}:{game.SourceId}")));
        foreach (var discovered in games)
        {
            if (!string.IsNullOrWhiteSpace(discovered.ExecutablePath))
            {
                var duplicates = await database.DiscoveredGames
                    .Where(item => item.ExecutablePath == discovered.ExecutablePath &&
                                   (item.Source != discovered.Source || item.SourceId != discovered.SourceId))
                    .ToListAsync(cancellationToken);
                database.DiscoveredGames.RemoveRange(duplicates);
            }

            var game = await database.DiscoveredGames.FirstOrDefaultAsync(item =>
                item.Source == discovered.Source && item.SourceId == discovered.SourceId, cancellationToken);
            if (game is null)
            {
                game = new DiscoveredGame
                {
                    Id = Guid.NewGuid(),
                    Source = discovered.Source,
                    SourceId = discovered.SourceId,
                    DiscoveredAt = DateTimeOffset.UtcNow
                };
                database.DiscoveredGames.Add(game);
                added++;
            }
            else
            {
                updated++;
            }

            game.Name = discovered.Name;
            game.InstallDirectory = discovered.InstallDirectory;
            game.ExecutablePath = discovered.ExecutablePath;
            game.CoverPath ??= FindLocalArtwork(discovered.InstallDirectory, discovered.ExecutablePath);

            if (ShouldUpdateMetadata(game))
            {
                if (await UpdateGameMetadataAsync(game, cancellationToken))
                    metadataUpdated++;
            }
        }
        await database.SaveChangesAsync(cancellationToken);
        return new GameDiscoveryResult(games.Count, added, updated, metadataUpdated);
    }

    public async Task<IReadOnlyList<DiscoveredGame>> GetInstalledGamesAsync()
    {
        await using var database = MfgDbContext.Create();
        var games = await database.DiscoveredGames.OrderBy(game => game.Name).ToListAsync();
        return games
            .GroupBy(game => string.IsNullOrWhiteSpace(game.ExecutablePath)
                ? $"{game.Source}:{game.SourceId}"
                : game.ExecutablePath, StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderBy(game => GetSourcePriority(game.Source))
                .First())
            .OrderBy(game => game.Name)
            .ToList();
    }

    private static int GetSourcePriority(GameSource source) => source switch
    {
        GameSource.Steam => 0,
        GameSource.EpicGames => 1,
        GameSource.Emulator => 2,
        _ => 3
    };

    private static IEnumerable<InstalledGameDiscovery> DiscoverSteamGames()
    {
        foreach (var steamRoot in GetSteamRoots())
        {
            var steamApps = Path.Combine(steamRoot, "steamapps");
            if (!Directory.Exists(steamApps))
                continue;

            foreach (var manifest in Directory.EnumerateFiles(steamApps, "appmanifest_*.acf"))
            {
                var fields = ReadVdfFields(manifest);
                if (!fields.TryGetValue("appid", out var appId) || !fields.TryGetValue("name", out var name) ||
                    !fields.TryGetValue("installdir", out var installName))
                    continue;
                var installDirectory = Path.Combine(steamApps, "common", installName);
                if (!Directory.Exists(installDirectory))
                    continue;
                yield return new InstalledGameDiscovery(GameSource.Steam, appId, name, installDirectory, FindLikelyExecutable(installDirectory, installName, name));
            }
        }
    }

    private static IEnumerable<InstalledGameDiscovery> DiscoverEpicGames()
    {
        var games = new List<InstalledGameDiscovery>();
        var manifestsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Epic",
            "EpicGamesLauncher",
            "Data",
            "Manifests");
        if (!Directory.Exists(manifestsDirectory))
            return games;

        foreach (var manifestPath in Directory.EnumerateFiles(manifestsDirectory, "*.item"))
        {
            try
            {
                using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
                var root = document.RootElement;
                var name = GetString(root, "DisplayName");
                var installDirectory = GetString(root, "InstallLocation");
                var sourceId = GetString(root, "CatalogItemId") ?? GetString(root, "AppName");
                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(installDirectory) || string.IsNullOrWhiteSpace(sourceId) || !Directory.Exists(installDirectory))
                    continue;
                var launchExecutable = GetString(root, "LaunchExecutable");
                var executable = string.IsNullOrWhiteSpace(launchExecutable) ? null : Path.Combine(installDirectory, launchExecutable);
                games.Add(new InstalledGameDiscovery(GameSource.EpicGames, sourceId, name, installDirectory, File.Exists(executable) ? executable : null));
            }
            catch (JsonException)
            {
                // Ignore malformed manifests and continue with the remaining installed games.
            }
        }
        return games;
    }

    private static Task<IReadOnlyList<InstalledGameDiscovery>> DiscoverLocalGamesAsync(
        IProgress<GameDiscoveryProgress>? progress,
        CancellationToken cancellationToken) =>
        Task.Run(() => DiscoverLocalGames(progress, cancellationToken), cancellationToken);

    private static IReadOnlyList<InstalledGameDiscovery> DiscoverLocalGames(IProgress<GameDiscoveryProgress>? progress, CancellationToken cancellationToken)
    {
        var context = new LocalScanContext(progress);
        var candidates = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var drive in DriveInfo.GetDrives().Where(drive => drive.DriveType == DriveType.Fixed && drive.IsReady))
        {
            foreach (var executablePath in EnumerateExecutables(drive.RootDirectory.FullName, context, cancellationToken))
            {
                if (IsKnownLauncherGameDirectory(Path.GetDirectoryName(executablePath) ?? string.Empty) && !IsKnownGameExecutableName(executablePath))
                    continue;
                if (!IsLikelyGameExecutable(executablePath))
                    continue;

                var gameRoot = GetGameRoot(executablePath);
                if (!candidates.TryGetValue(gameRoot, out var executables))
                {
                    executables = new List<string>();
                    candidates.Add(gameRoot, executables);
                }
                executables.Add(executablePath);
            }
        }

        var games = new List<InstalledGameDiscovery>();
        foreach (var candidate in candidates)
        {
            var executablePath = candidate.Value
                .OrderByDescending(path => GetLocalExecutableScore(candidate.Key, path))
                .ThenByDescending(path => new FileInfo(path).Length)
                .FirstOrDefault();
            if (executablePath is null || GetLocalExecutableScore(candidate.Key, executablePath) < 0)
                continue;

            context.CandidatesFound++;
            context.Report(candidate.Key, true);
            games.Add(new InstalledGameDiscovery(
                IsKnownEmulatorExecutable(executablePath) ? GameSource.Emulator : GameSource.LocalScan,
                executablePath,
                GetExecutableDisplayName(executablePath),
                candidate.Key,
                executablePath));
        }
        return games;
    }

    private static IEnumerable<string> EnumerateExecutables(string root, LocalScanContext context, CancellationToken cancellationToken)
    {
        var directories = new Stack<string>();
        directories.Push(root);
        while (directories.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var directory = directories.Pop();
            if (ShouldSkipDirectory(directory))
                continue;
            context.DirectoriesVisited++;
            context.Report(directory, false);

            foreach (var file in SafeEnumerate(directory, "*.exe", SearchTarget.Files))
            {
                context.ExecutablesChecked++;
                context.Report(directory, false);
                yield return file;
            }

            foreach (var child in SafeEnumerate(directory, "*", SearchTarget.Directories))
                directories.Push(child);
        }
    }

    private static IReadOnlyList<string> SafeEnumerate(string directory, string searchPattern, SearchTarget target)
    {
        try
        {
            var options = new EnumerationOptions
            {
                IgnoreInaccessible = true,
                RecurseSubdirectories = false,
                AttributesToSkip = FileAttributes.ReparsePoint
            };
            return target == SearchTarget.Files
                ? Directory.EnumerateFiles(directory, searchPattern, options).ToList()
                : Directory.EnumerateDirectories(directory, searchPattern, options).ToList();
        }
        catch (UnauthorizedAccessException)
        {
            return Array.Empty<string>();
        }
        catch (IOException)
        {
            return Array.Empty<string>();
        }
    }

    private enum SearchTarget { Files, Directories }

    private static bool ShouldSkipDirectory(string path)
    {
        var name = Path.GetFileName(path);
        return name.Equals("Windows", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("WindowsApps", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("$Recycle.Bin", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("System Volume Information", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("node_modules", StringComparison.OrdinalIgnoreCase) ||
            name.Equals(".git", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("Temp", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("\\AppData\\", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("\\ProgramData\\", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("\\NuGet\\", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLikelyGameExecutable(string path)
    {
        try
        {
            if (IsGameSupportExecutable(path))
                return false;
            var info = FileVersionInfo.GetVersionInfo(path);
            var identity = $"{Path.GetFileNameWithoutExtension(path)} {info.ProductName} {info.FileDescription} {info.CompanyName}";
            if (KnownNonGameTerms.Any(term => identity.Contains(term, StringComparison.OrdinalIgnoreCase)))
                return false;
            if (IsKnownGameTitle(identity))
                return true;
            var directory = Path.GetDirectoryName(path) ?? string.Empty;
            var knownGameLocation = new[] { "\\games\\", "\\jogos\\", "\\roms\\", "\\emulators\\", "gog galaxy\\games", "ubisoft game launcher\\games" }
                .Any(marker => directory.Contains(marker, StringComparison.OrdinalIgnoreCase));
            if (knownGameLocation)
                return true;

            return KnownGamePublishers.Any(publisher => identity.Contains(publisher, StringComparison.OrdinalIgnoreCase));
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
    }

    private static bool IsKnownLauncherGameDirectory(string directory) =>
        directory.Contains("steamapps", StringComparison.OrdinalIgnoreCase) ||
        directory.Contains("epic games", StringComparison.OrdinalIgnoreCase);

    private static bool IsKnownGameExecutableName(string executablePath) =>
        IsKnownGameTitle($"{Path.GetFileNameWithoutExtension(executablePath)} {Path.GetDirectoryName(executablePath)}");

    private static bool IsKnownGameTitle(string value)
    {
        var normalizedValue = NormalizeName(value);
        return KnownGameTitles.Any(title => normalizedValue.Contains(NormalizeName(title), StringComparison.OrdinalIgnoreCase));
    }

    private static string GetGameRoot(string executablePath)
    {
        var directory = Path.GetDirectoryName(executablePath)!;
        var parts = directory.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        for (var index = 0; index < parts.Length - 1; index++)
        {
            if (!parts[index].Equals("Games", StringComparison.OrdinalIgnoreCase) &&
                !parts[index].Equals("Jogos", StringComparison.OrdinalIgnoreCase) &&
                !parts[index].Equals("Roms", StringComparison.OrdinalIgnoreCase) &&
                !parts[index].Equals("Emulators", StringComparison.OrdinalIgnoreCase) &&
                !(parts[index].Equals("common", StringComparison.OrdinalIgnoreCase) && index > 0 && parts[index - 1].Equals("steamapps", StringComparison.OrdinalIgnoreCase)))
                continue;
            return string.Join(Path.DirectorySeparatorChar, parts.Take(index + 2));
        }
        return directory;
    }

    private static int GetLocalExecutableScore(string gameRoot, string executablePath)
    {
        var executableName = NormalizeName(Path.GetFileNameWithoutExtension(executablePath));
        if (GenericExecutableTerms.Any(term => executableName.Contains(term, StringComparison.OrdinalIgnoreCase)))
            return -10_000;

        var rootName = NormalizeName(Path.GetFileName(gameRoot));
        var score = 0;
        if (IsKnownEmulatorExecutable(executablePath))
            score += 2000;
        if (executableName == rootName)
            score += 1000;
        else if (executableName.Contains(rootName, StringComparison.OrdinalIgnoreCase) || rootName.Contains(executableName, StringComparison.OrdinalIgnoreCase))
            score += 500;
        if (executablePath.Contains("Binaries", StringComparison.OrdinalIgnoreCase) || executablePath.Contains("Win64", StringComparison.OrdinalIgnoreCase))
            score += 100;
        return score;
    }

    private static string? FindLocalArtwork(string directory, string? executablePath)
    {
        if (!Directory.Exists(directory))
            return null;
        try
        {
            var executableName = executablePath is null ? string.Empty : Path.GetFileNameWithoutExtension(executablePath);
            var imageExtensions = new[] { ".png", ".jpg", ".jpeg", ".webp", ".ico" };
            return Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly)
                .Where(path => imageExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
                .OrderByDescending(path =>
                    Path.GetFileNameWithoutExtension(path).Contains(executableName, StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(path =>
                    Path.GetFileName(path).Contains("cover", StringComparison.OrdinalIgnoreCase) ||
                    Path.GetFileName(path).Contains("icon", StringComparison.OrdinalIgnoreCase) ||
                    Path.GetFileName(path).Contains("poster", StringComparison.OrdinalIgnoreCase))
                .FirstOrDefault();
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string GetExecutableDisplayName(string path)
    {
        var info = FileVersionInfo.GetVersionInfo(path);
        return !string.IsNullOrWhiteSpace(info.ProductName)
            ? info.ProductName
            : !string.IsNullOrWhiteSpace(info.FileDescription)
                ? info.FileDescription
                : Path.GetFileNameWithoutExtension(path);
    }

    private static readonly string[] KnownNonGameTerms =
    {
        "amd", "radeon", "nvidia", "geforce", "intel", "excel", "office", "outlook", "word",
        "onedrive", "teams", "chrome", "edge", "firefox", "adobe", "photoshop", "visual studio",
        "driver", "service", "updater", "update", "installer", "setup", "uninstall", "diagnostic",
        "git", "7-zip", "7zip", "winrar", "vscode", "visual studio code", "msbuild", "devenv",
        "steam client", "steam service", "epic games launcher", "gog galaxy", "ubisoft connect",
        "uplay", "ea app", "origin", "battle.net", "rockstar games launcher", "riot client",
        "discord", "obs studio", "playnite", "unity hub", "unreal engine"
    };

    private static readonly string[] GenericExecutableTerms =
    {
        "error", "browser", "webview", "crash", "report", "helper", "tool", "server", "service",
        "client", "launcher", "setup", "install", "uninstall", "updater", "update", "redistributable"
    };

    private static readonly string[] KnownGamePublishers =
    {
        "nintendo", "playstation", "sony interactive", "xbox game studios", "microsoft studios",
        "valve", "electronic arts", "ea games", "ubisoft", "rockstar", "take-two", "bethesda",
        "activision", "blizzard", "bandai namco", "capcom", "sega", "konami", "square enix",
        "epic games", "cd projekt", "riot games", "mojang", "bungie", "warner bros", "wb games",
        "devolver", "paradox interactive", "supergiant games", "larian studios", "gearbox"
    };

    private static readonly string[] KnownGameTitles =
    {
        "Hollow Knight", "Hollow Knight Silksong", "Silksong", "Celeste", "Hades", "Dead Cells",
        "Stardew Valley", "Terraria", "Minecraft", "Undertale", "Cuphead", "Baldur's Gate",
        "Elden Ring", "Dark Souls", "Cyberpunk", "The Witcher", "Grand Theft Auto", "Red Dead Redemption",
        "Forza", "Halo", "Doom", "Skyrim", "Fallout", "Portal", "Half-Life", "Resident Evil",
        "Monster Hunter", "Final Fantasy", "Persona", "Yakuza", "Sonic", "Mario", "Zelda", "Metroid"
    };

    private static readonly string[] KnownEmulators =
    {
        "retroarch", "dolphin", "cemu", "ryujinx", "yuzu", "pcsx2", "rpcs3", "ppsspp",
        "duckstation", "melon ds", "melonds", "mgba", "snes9x", "mesen", "project64", "citra",
        "xenia", "vita3k", "bizhawk", "azahar"
    };

    private static bool IsKnownEmulatorExecutable(string executablePath)
    {
        var identity = $"{Path.GetFileNameWithoutExtension(executablePath)} {Path.GetDirectoryName(executablePath)}";
        return KnownEmulators.Any(emulator => identity.Contains(emulator, StringComparison.OrdinalIgnoreCase));
    }

    private sealed class LocalScanContext(IProgress<GameDiscoveryProgress>? progress)
    {
        private int _lastReportedExecutables;
        public int DirectoriesVisited { get; set; }
        public int ExecutablesChecked { get; set; }
        public int CandidatesFound { get; set; }

        public void Report(string location, bool force)
        {
            if (!force && ExecutablesChecked - _lastReportedExecutables < 100 && DirectoriesVisited % 50 != 0)
                return;
            _lastReportedExecutables = ExecutablesChecked;
            progress?.Report(new GameDiscoveryProgress(DirectoriesVisited, ExecutablesChecked, CandidatesFound, location));
        }
    }

    private static IEnumerable<string> GetSteamRoots()
    {
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
        var registryPath = key?.GetValue("SteamPath") as string;
        if (!string.IsNullOrWhiteSpace(registryPath) && Directory.Exists(registryPath))
            roots.Add(registryPath);
        var defaultPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam");
        if (Directory.Exists(defaultPath))
            roots.Add(defaultPath);

        foreach (var root in roots.ToList())
        {
            var libraryFile = Path.Combine(root, "steamapps", "libraryfolders.vdf");
            if (!File.Exists(libraryFile))
                continue;
            var contents = File.ReadAllText(libraryFile);
            foreach (Match match in Regex.Matches(contents, "\\\"path\\\"\\s+\\\"(?<path>(?:\\\\\\\\|[^\\\"])*)\\\""))
            {
                var libraryPath = match.Groups["path"].Value.Replace("\\\\", "\\");
                if (Directory.Exists(libraryPath))
                    roots.Add(libraryPath);
            }
        }

        foreach (var root in roots)
            yield return root;
    }

    private static Dictionary<string, string> ReadVdfFields(string path)
    {
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in Regex.Matches(File.ReadAllText(path), "\\\"(?<key>appid|name|installdir)\\\"\\s+\\\"(?<value>[^\\\"]*)\\\"", RegexOptions.IgnoreCase))
            fields[match.Groups["key"].Value] = match.Groups["value"].Value;
        return fields;
    }

    private static string? FindLikelyExecutable(string installDirectory, string installName, string displayName)
    {
        try
        {
            var normalizedInstallName = NormalizeName(installName);
            var normalizedDisplayName = NormalizeName(displayName);
            return Directory.EnumerateFiles(installDirectory, "*.exe", SearchOption.TopDirectoryOnly)
                .Concat(Directory.EnumerateFiles(installDirectory, "*.exe", SearchOption.AllDirectories))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Where(path => !IsGameSupportExecutable(path))
                .OrderByDescending(path => GetExecutableScore(path, normalizedInstallName, normalizedDisplayName))
                .ThenByDescending(path => new FileInfo(path).Length)
                .FirstOrDefault();
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static bool IsGameSupportExecutable(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        return new[]
        {
            "unins", "uninstall", "redist", "redistributable", "setup", "dxsetup", "vcredist",
            "crashreport", "crashhandler", "unitycrash", "easyanticheat", "battleye", "eac",
            "vc_redist", "installscript", "helper", "launcher", "client", "bootstrapper"
        }.Any(excluded => name.Contains(excluded, StringComparison.OrdinalIgnoreCase));
    }

    private static int GetExecutableScore(string path, string normalizedInstallName, string normalizedDisplayName)
    {
        var executableName = NormalizeName(Path.GetFileNameWithoutExtension(path));
        var score = 0;
        if (executableName == normalizedInstallName || executableName == normalizedDisplayName)
            score += 1000;
        if (executableName.Contains(normalizedInstallName, StringComparison.OrdinalIgnoreCase) ||
            executableName.Contains(normalizedDisplayName, StringComparison.OrdinalIgnoreCase))
            score += 500;
        if (path.Contains("Binaries", StringComparison.OrdinalIgnoreCase) || path.Contains("Win64", StringComparison.OrdinalIgnoreCase))
            score += 100;
        if (Path.GetFileName(path).Contains("launcher", StringComparison.OrdinalIgnoreCase))
            score -= 50;
        return score;
    }

    private static string NormalizeName(string value) => Regex.Replace(value, "[^a-z0-9]", string.Empty, RegexOptions.IgnoreCase);

    private static bool ShouldUpdateMetadata(DiscoveredGame game) =>
        game.MetadataUpdatedAt is null || game.MetadataUpdatedAt < DateTimeOffset.UtcNow.AddDays(-30);

    private static async Task<bool> UpdateGameMetadataAsync(DiscoveredGame game, CancellationToken cancellationToken)
    {
        var appId = game.Source == GameSource.Steam
            ? game.SourceId
            : await FindSteamApplicationIdAsync(game.Name, cancellationToken);
        if (string.IsNullOrWhiteSpace(appId))
        {
            // Evita repetir uma pesquisa sem resultado a cada abertura do Companion.
            game.MetadataUpdatedAt = DateTimeOffset.UtcNow;
            return false;
        }

        return await UpdateSteamMetadataAsync(game, appId, cancellationToken);
    }

    private static async Task<bool> UpdateSteamMetadataAsync(DiscoveredGame game, string appId, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await HttpClient.GetAsync($"https://store.steampowered.com/api/appdetails?appids={Uri.EscapeDataString(appId)}&l=portuguese", cancellationToken);
            if (!response.IsSuccessStatusCode)
                return false;
            using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));
            if (!document.RootElement.TryGetProperty(appId, out var entry) ||
                !entry.TryGetProperty("success", out var success) || !success.GetBoolean() ||
                !entry.TryGetProperty("data", out var data))
                return false;

            game.Name = GetString(data, "name") ?? game.Name;
            game.Description = GetString(data, "short_description");
            game.Developer = GetFirstArrayValue(data, "developers");
            game.Publisher = GetFirstArrayValue(data, "publishers");
            var headerImage = GetString(data, "header_image");
            var libraryCover = $"https://cdn.cloudflare.steamstatic.com/steam/apps/{Uri.EscapeDataString(appId)}/library_600x900_2x.jpg";
            game.CoverPath = await DownloadCoverAsync(appId, libraryCover, cancellationToken)
                ?? (!string.IsNullOrWhiteSpace(headerImage)
                    ? await DownloadCoverAsync(appId, headerImage, cancellationToken)
                    : null)
                ?? game.CoverPath;
            game.MetadataUpdatedAt = DateTimeOffset.UtcNow;
            return true;
        }
        catch (HttpRequestException)
        {
            return false;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static async Task<string?> FindSteamApplicationIdAsync(string gameName, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await HttpClient.GetAsync(
                $"https://store.steampowered.com/api/storesearch/?term={Uri.EscapeDataString(gameName)}&cc=US&l=english", cancellationToken);
            if (!response.IsSuccessStatusCode)
                return null;
            using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));
            if (!document.RootElement.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
                return null;

            var normalizedName = NormalizeName(gameName);
            var match = items.EnumerateArray()
                .Where(item => item.TryGetProperty("type", out var type) && type.GetString() == "app")
                .Select(item => new { Item = item, Name = GetString(item, "name") ?? string.Empty })
                .FirstOrDefault(candidate => NormalizeName(candidate.Name) == normalizedName)
                ?? items.EnumerateArray()
                    .Where(item => item.TryGetProperty("type", out var type) && type.GetString() == "app")
                    .Select(item => new { Item = item, Name = GetString(item, "name") ?? string.Empty })
                    .FirstOrDefault(candidate =>
                    {
                        var normalizedCandidate = NormalizeName(candidate.Name);
                        return normalizedName.Length >= 6 &&
                               (normalizedCandidate.Contains(normalizedName, StringComparison.Ordinal) ||
                                normalizedName.Contains(normalizedCandidate, StringComparison.Ordinal));
                    });

            return match is not null && match.Item.TryGetProperty("id", out var id) && id.TryGetInt32(out var value)
                ? value.ToString(System.Globalization.CultureInfo.InvariantCulture)
                : null;
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static async Task<string?> DownloadCoverAsync(string sourceId, string url, CancellationToken cancellationToken)
    {
        try
        {
            Directory.CreateDirectory(MetadataDirectory);
            var coverPath = Path.Combine(MetadataDirectory, $"steam-{sourceId}.jpg");
            if (!File.Exists(coverPath))
            {
                var data = await HttpClient.GetByteArrayAsync(url, cancellationToken);
                await File.WriteAllBytesAsync(coverPath, data, cancellationToken);
            }
            return coverPath;
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    private static string? GetString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static string? GetFirstArrayValue(JsonElement element, string property) =>
        element.TryGetProperty(property, out var values) && values.ValueKind == JsonValueKind.Array
            ? values.EnumerateArray().FirstOrDefault(value => value.ValueKind == JsonValueKind.String).GetString()
            : null;
}
