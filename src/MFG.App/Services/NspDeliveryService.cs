using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using MFG.App.Data;
using MFG.Common.Library;

namespace MFG.App.Services;

public sealed class NspDeliveryService
{
    private static readonly string LocalForwarderDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MoonlightForwarderGenerator",
        "forwarders");

    private readonly MoonlightSwitchManager _switchManager = new();

    public string GetLocalForwarderDirectory()
    {
        Directory.CreateDirectory(LocalForwarderDirectory);
        return LocalForwarderDirectory;
    }

    public async Task<NspDeliveryResult> DeliverAsync(string localNspPath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(localNspPath))
            throw new FileNotFoundException("O atalho NSP gerado nao foi encontrado.", localNspPath);

        var sdCard = _switchManager.DetectSdCard();
        if (!sdCard.IsDetected || sdCard.RootPath is null)
        {
            AppLog.Info("NSP mantido apenas na biblioteca local; cartao SD nao detectado.");
            return new NspDeliveryResult(
                localNspPath,
                null,
                false,
                "Atalho salvo na biblioteca local. Conecte o cartao SD para copia-lo automaticamente.");
        }

        var destinationDirectory = GetSdForwarderDirectory(sdCard.RootPath);
        Directory.CreateDirectory(destinationDirectory);
        var destinationPath = Path.Combine(destinationDirectory, Path.GetFileName(localNspPath));
        await CopyAndVerifyAsync(localNspPath, destinationPath, cancellationToken);
        AppLog.Info($"NSP copiado para o cartao SD: {Path.GetFileName(destinationPath)}.");

        return new NspDeliveryResult(
            localNspPath,
            destinationPath,
            true,
            "Atalho copiado para o cartao SD do Switch.");
    }

    public async Task<SdLibrarySyncResult> SyncLibraryAsync(IReadOnlyList<LibraryGame> games, IReadOnlyList<RemovedForwarder> removedForwarders, CancellationToken cancellationToken = default)
    {
        var sdCard = _switchManager.DetectSdCard();
        if (!sdCard.IsDetected || sdCard.RootPath is null)
            return new SdLibrarySyncResult(false, 0, 0, "Conecte o cartão SD para sincronizar os atalhos.");

        var destinationDirectory = GetSdForwarderDirectory(sdCard.RootPath);
        Directory.CreateDirectory(destinationDirectory);
        var coverDirectory = Path.Combine(destinationDirectory, "covers");
        Directory.CreateDirectory(coverDirectory);
        var copied = 0;
        var activeGames = games.Where(game => !string.IsNullOrWhiteSpace(game.NspPath) && File.Exists(game.NspPath)).ToList();
        var expectedFiles = new HashSet<string>(activeGames.Select(game => Path.GetFileName(game.NspPath!)), StringComparer.OrdinalIgnoreCase);
        foreach (var game in activeGames)
        {
            var localNspPath = game.NspPath!;
            var destinationPath = Path.Combine(destinationDirectory, Path.GetFileName(localNspPath));
            if (!FilesMatch(localNspPath, destinationPath))
            {
                await CopyAndVerifyAsync(localNspPath, destinationPath, cancellationToken);
                copied++;
            }

            if (!string.IsNullOrWhiteSpace(game.CoverPath) && File.Exists(game.CoverPath))
            {
                var coverDestination = Path.Combine(coverDirectory, $"{game.NspTitleId}.jpg");
                if (!FilesMatch(game.CoverPath, coverDestination))
                    await CopyAndVerifyAsync(game.CoverPath, coverDestination, cancellationToken);
            }
        }

        foreach (var orphan in Directory.EnumerateFiles(destinationDirectory, "MFG - *.nsp", SearchOption.TopDirectoryOnly)
                     .Where(path => !expectedFiles.Contains(Path.GetFileName(path))))
            File.Delete(orphan);

        var expectedCovers = activeGames
            .Where(game => !string.IsNullOrWhiteSpace(game.CoverPath) && File.Exists(game.CoverPath))
            .Select(game => $"{game.NspTitleId}.jpg")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var orphanCover in Directory.EnumerateFiles(coverDirectory, "*.jpg", SearchOption.TopDirectoryOnly)
                     .Where(path => !expectedCovers.Contains(Path.GetFileName(path))))
            File.Delete(orphanCover);

        var manifest = new SdLibraryManifest(2, DateTimeOffset.UtcNow, activeGames.Select(game => new SdLibraryManifestGame(
            game.NspTitleId,
            game.Name,
            Path.GetFileName(game.NspPath!),
            ComputeSha256(game.NspPath!),
            !string.IsNullOrWhiteSpace(game.CoverPath) && File.Exists(game.CoverPath)
                ? $"covers/{game.NspTitleId}.jpg"
                : null)).ToList(), removedForwarders.Select(item => item.TitleId).ToList());
        await WriteManifestAtomicallyAsync(Path.Combine(destinationDirectory, "manifest.json"),
            JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase }), cancellationToken);
        return new SdLibrarySyncResult(true, activeGames.Count, copied, copied == 0 ? "Atalhos no SD já estão atualizados." : "Atalhos sincronizados com o cartão SD.");
    }

    public Task RemoveAsync(string? localNspPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(localNspPath))
            return Task.CompletedTask;

        try
        {
            var fullPath = Path.GetFullPath(localNspPath);
            var managedDirectory = Path.GetFullPath(GetLocalForwarderDirectory());
            if (!fullPath.StartsWith(managedDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                return Task.CompletedTask;

            if (File.Exists(fullPath))
                File.Delete(fullPath);

            var sdCard = _switchManager.DetectSdCard();
            if (sdCard.IsDetected && sdCard.RootPath is not null)
            {
                var sdPath = Path.Combine(GetSdForwarderDirectory(sdCard.RootPath), Path.GetFileName(fullPath));
                if (File.Exists(sdPath))
                    File.Delete(sdPath);
            }
            AppLog.Info($"Atalho anterior removido: {Path.GetFileName(fullPath)}.");
        }
        catch (IOException exception)
        {
            // Um arquivo antigo bloqueado nao deve impedir a nova geracao, que possui nome unico.
            AppLog.Error("Nao foi possivel remover um atalho anterior; um novo sera gerado.", exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            AppLog.Error("Nao foi possivel remover um atalho anterior por falta de permissao; um novo sera gerado.", exception);
        }

        return Task.CompletedTask;
    }

    public async Task RemovePreviousVersionsAsync(string gameName, string? knownNspPath, CancellationToken cancellationToken = default)
    {
        var directory = GetLocalForwarderDirectory();
        var stableName = NspGenerationService.GetFileName(gameName);
        var legacyPrefix = NspGenerationService.SanitizeFileName(gameName) + " [";
        var files = Directory.EnumerateFiles(directory, "*.nsp", SearchOption.TopDirectoryOnly)
            .Where(path => string.Equals(Path.GetFileName(path), stableName, StringComparison.OrdinalIgnoreCase) ||
                           Path.GetFileName(path).StartsWith(legacyPrefix, StringComparison.OrdinalIgnoreCase))
            .Append(knownNspPath)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var path in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await RemoveAsync(path, cancellationToken);
        }
    }

    private static async Task CopyAtomicallyAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken)
    {
        if (string.Equals(Path.GetFullPath(sourcePath), Path.GetFullPath(destinationPath), StringComparison.OrdinalIgnoreCase))
            return;

        var temporaryPath = $"{destinationPath}.{Guid.NewGuid():N}.tmp";
        try
        {
            // O Explorer e o antivirus podem inspecionar o arquivo enquanto o MFG o entrega.
            // Permitimos leitores paralelos; somente esta instancia grava o arquivo temporario.
            await using var source = new FileStream(sourcePath, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete, 64 * 1024, FileOptions.SequentialScan);
            await using var destination = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write,
                FileShare.ReadWrite | FileShare.Delete, 64 * 1024, FileOptions.SequentialScan);
            await source.CopyToAsync(destination, cancellationToken);
            await destination.FlushAsync(cancellationToken);
            destination.Flush(flushToDisk: true);
            File.Move(temporaryPath, destinationPath, true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    public HealthCheckItem CheckSdForwarder(string? localNspPath)
    {
        var sdCard = _switchManager.DetectSdCard();
        if (!sdCard.IsDetected || sdCard.RootPath is null)
            return new HealthCheckItem(HealthCheckState.Unavailable, "Cartão SD não conectado.");
        if (string.IsNullOrWhiteSpace(localNspPath) || !File.Exists(localNspPath))
            return new HealthCheckItem(HealthCheckState.Missing, "NSP local pendente.");
        var destination = Path.Combine(GetSdForwarderDirectory(sdCard.RootPath), Path.GetFileName(localNspPath));
        if (!File.Exists(destination))
            return new HealthCheckItem(HealthCheckState.Missing, "Pendente de cópia para SD:/switch/MFG.");
        return FilesMatch(localNspPath, destination)
            ? new HealthCheckItem(HealthCheckState.Ready, "NSP sincronizado em SD:/switch/MFG.")
            : new HealthCheckItem(HealthCheckState.Invalid, "NSP do SD está desatualizado.");
    }

    private static string GetSdForwarderDirectory(string rootPath) => Path.Combine(rootPath, "switch", "MFG");

    private static bool FilesMatch(string source, string destination) => File.Exists(destination)
        && new FileInfo(source).Length == new FileInfo(destination).Length
        && string.Equals(ComputeSha256(source), ComputeSha256(destination), StringComparison.OrdinalIgnoreCase);

    private static string ComputeSha256(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static async Task CopyAndVerifyAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken)
    {
        IOException? lastError = null;
        var expectedHash = ComputeSha256(sourcePath);
        var expectedLength = new FileInfo(sourcePath).Length;
        for (var attempt = 0; attempt < 10; attempt++)
        {
            try
            {
                await CopyAtomicallyAsync(sourcePath, destinationPath, cancellationToken);
                if (!File.Exists(destinationPath) ||
                    new FileInfo(destinationPath).Length != expectedLength ||
                    !string.Equals(ComputeSha256(destinationPath), expectedHash, StringComparison.OrdinalIgnoreCase))
                    throw new IOException("O arquivo copiado para o cartão SD não corresponde ao NSP original.");
                return;
            }
            catch (IOException exception) when (attempt < 9)
            {
                lastError = exception;
                // Dispositivos removiveis e scanners podem manter um handle por alguns instantes.
                await Task.Delay(TimeSpan.FromMilliseconds(250 * (attempt + 1)), cancellationToken);
            }
        }

        throw new IOException("Não foi possível concluir a cópia para o cartão após algumas tentativas. Remova e conecte o cartão novamente e tente outra vez. Os detalhes foram registrados no log.", lastError);
    }
    private static async Task WriteManifestAtomicallyAsync(string path, string contents, CancellationToken cancellationToken)
    {
        var temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            await using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write,
                             FileShare.Read, 16 * 1024, FileOptions.WriteThrough))
            await using (var writer = new StreamWriter(stream, new System.Text.UTF8Encoding(false)))
            {
                await writer.WriteAsync(contents.AsMemory(), cancellationToken);
                await writer.FlushAsync(cancellationToken);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, path, true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }
}

public sealed record NspDeliveryResult(
    string LocalPath,
    string? SdCardPath,
    bool CopiedToSdCard,
    string Message);

public sealed record SdLibrarySyncResult(bool SdDetected, int Total, int Copied, string Message);
internal sealed record SdLibraryManifest(int SchemaVersion, DateTimeOffset UpdatedAt, IReadOnlyList<SdLibraryManifestGame> Games, IReadOnlyList<string> RemovedTitleIds);
internal sealed record SdLibraryManifestGame(string TitleId, string Name, string NspFileName, string Sha256, string? CoverFileName);
