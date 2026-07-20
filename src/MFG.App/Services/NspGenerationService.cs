using System.IO;
using MFG.Common.Library;
using MFG.Common.Nsp;

namespace MFG.App.Services;

public sealed class NspGenerationService
{
    public NspGenerationPlan CreatePlan(
        GameRegistration game,
        string hostIp,
        string moonlightNroPath,
        string templatePath,
        string iconPath,
        string outputDirectory,
        string publisher = "MFG")
    {
        if (!Directory.Exists(templatePath))
            throw new DirectoryNotFoundException("Os recursos do gerador NSP nao foram encontrados.");
        if (!File.Exists(iconPath))
            throw new FileNotFoundException("Escolha uma imagem JPG para o icone do jogo.", iconPath);
        if (!IsJpeg(iconPath))
            throw new InvalidOperationException("O ícone do atalho precisa estar no formato JPEG.");
        if (string.IsNullOrWhiteSpace(hostIp))
            throw new ArgumentException("Informe o endereco do computador.", nameof(hostIp));

        Directory.CreateDirectory(outputDirectory);
        var outputPath = Path.Combine(outputDirectory, GetFileName(game.Name));
        var request = new NspGenerationRequest(
            game.Id,
            game.NspTitleId,
            game.Name,
            publisher,
            hostIp.Trim(),
            game.MoonlightFavoriteId ?? throw new InvalidOperationException("Não foi possível obter o identificador do jogo no Sunshine."),
            EncodeMoonlightAppName(game.SunshineApplicationName),
            moonlightNroPath,
            templatePath,
            iconPath,
            outputPath);

        return new NspGenerationPlan(request, new[]
        {
            $"--ip={request.HostIp}",
            $"--appid={request.MoonlightFavoriteId}",
            $"--appname={request.MoonlightAppName}"
        });
    }

    public static string GetFileName(string gameName) => $"MFG - {SanitizeFileName(gameName)}.nsp";

    public static string SanitizeFileName(string value)
    {
        foreach (var invalidCharacter in Path.GetInvalidFileNameChars())
            value = value.Replace(invalidCharacter, '_');
        return string.IsNullOrWhiteSpace(value) ? "Jogo" : value;
    }

    private static bool IsJpeg(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        return stream.Length >= 3 && stream.ReadByte() == 0xFF && stream.ReadByte() == 0xD8 && stream.ReadByte() == 0xFF;
    }

    private static string EncodeMoonlightAppName(string name) => name.Replace(" ", "&#160;", StringComparison.Ordinal);
}
