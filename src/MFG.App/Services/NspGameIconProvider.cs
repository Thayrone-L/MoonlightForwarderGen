using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace MFG.App.Services;

public sealed class NspGameIconProvider
{
    private static readonly string OutputDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MoonlightForwarderGenerator", "metadata", "nsp-icons");

    private readonly DefaultNspIconProvider _fallback = new();

    public async Task<string> GetPathAsync(string? coverPath, string gameName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(coverPath) || !File.Exists(coverPath))
            return _fallback.GetPath();

        try
        {
            Directory.CreateDirectory(OutputDirectory);
            var fileName = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes($"{coverPath}|{new FileInfo(coverPath).LastWriteTimeUtc.Ticks}"))).Substring(0, 16);
            var outputPath = Path.Combine(OutputDirectory, $"{fileName}.jpg");
            if (File.Exists(outputPath))
                return outputPath;

            // As artes das lojas podem ser JPEG progressivo, CMYK ou conter perfis de
            // cor que o Switch não aceita para icon_AmericanEnglish.dat. Sempre
            // recodificamos para um JPEG RGB simples antes de montar o NSP.
            await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var input = new FileStream(coverPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                var decoder = BitmapDecoder.Create(input, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                var encoder = new JpegBitmapEncoder { QualityLevel = 92 };
                encoder.Frames.Add(BitmapFrame.Create(decoder.Frames[0]));
                using var output = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);
                encoder.Save(output);
            }, cancellationToken);
            AppLog.Info($"Capa do NSP normalizada para JPEG: {gameName}.");
            return outputPath;
        }
        catch (Exception exception)
        {
            AppLog.Error($"Nao foi possivel converter a capa de '{gameName}' para o NSP; sera usado o icone MFG.", exception);
            return _fallback.GetPath();
        }
    }
}
