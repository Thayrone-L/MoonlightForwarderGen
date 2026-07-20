using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Cryptography;

namespace MFG.App.Services;

public sealed class SwitchCompanionDeliveryService
{
    private const string FolderName = "MFG-Updater";
    private const string FileName = "MFG-Updater.nro";
    private readonly MoonlightSwitchManager _switchManager = new();

    public async Task<SwitchCompanionDeliveryResult> EnsureInstalledAsync(CancellationToken cancellationToken = default)
    {
        var sdCard = _switchManager.DetectSdCard();
        if (!sdCard.IsDetected || sdCard.RootPath is null)
            return new SwitchCompanionDeliveryResult(false, false, null, "Cartão SD não conectado; complemento do Switch será copiado quando ele for detectado.");

        var sourcePath = Path.Combine(AppContext.BaseDirectory, "switch", FolderName, FileName);
        if (!File.Exists(sourcePath))
        {
            AppLog.Error("O NRO do complemento não foi encontrado junto ao Companion.", new FileNotFoundException(FileName, sourcePath));
            return new SwitchCompanionDeliveryResult(true, false, null, "O arquivo do complemento do Switch não está disponível nesta instalação do Companion.");
        }

        var destinationDirectory = Path.Combine(sdCard.RootPath, "switch", FolderName);
        var destinationPath = Path.Combine(destinationDirectory, FileName);
        if (FilesMatch(sourcePath, destinationPath))
            return new SwitchCompanionDeliveryResult(true, false, destinationPath, "Complemento do Switch já está atualizado.");

        Directory.CreateDirectory(destinationDirectory);
        var temporaryPath = destinationPath + ".mfg.tmp";
        try
        {
            await using var source = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, FileOptions.SequentialScan);
            await using var destination = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write,
                FileShare.ReadWrite | FileShare.Delete, 64 * 1024, FileOptions.SequentialScan);
            await source.CopyToAsync(destination, cancellationToken);
            await destination.FlushAsync(cancellationToken);
            File.Move(temporaryPath, destinationPath, true);
            AppLog.Info($"Complemento do Switch copiado para: {destinationPath}.");
            return new SwitchCompanionDeliveryResult(true, true, destinationPath, "Complemento do Switch copiado para o cartão SD.");
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    private static bool FilesMatch(string sourcePath, string destinationPath)
    {
        if (!File.Exists(destinationPath) || new FileInfo(sourcePath).Length != new FileInfo(destinationPath).Length)
            return false;
        using var source = File.OpenRead(sourcePath);
        using var destination = File.OpenRead(destinationPath);
        return CryptographicOperations.FixedTimeEquals(SHA256.HashData(source), SHA256.HashData(destination));
    }
}

public sealed record SwitchCompanionDeliveryResult(bool SdDetected, bool Copied, string? DestinationPath, string Message);
