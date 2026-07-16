using System.Diagnostics;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;

namespace MFG.Agent.Services;

public static class SunshineInstallerService
{
    private const string ReleasesUrl = "https://api.github.com/repos/LizardByte/Sunshine/releases?per_page=20";

    public static async Task<SunshineInstallationResult> InstallAsync()
    {
        if (!OperatingSystem.IsWindows())
            return new SunshineInstallationResult(false, "A instalação guiada do Sunshine está disponível apenas no Windows.");

        try
        {
            using var http = new HttpClient();
            http.DefaultRequestHeaders.UserAgent.ParseAdd("MoonlightForwarderGenerator/1.0");
            http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");

            var releases = await http.GetFromJsonAsync<List<GitHubRelease>>(ReleasesUrl);
            var release = releases?.FirstOrDefault(item => !item.Draft && !item.Prerelease);
            if (release is null)
                return new SunshineInstallationResult(false, "Nenhuma release estável do Sunshine foi encontrada no GitHub.");

            var asset = SelectInstaller(release.Assets);
            if (asset is null)
                return new SunshineInstallationResult(false, "A release estável atual não possui um instalador Windows compatível com esta arquitetura.");

            var downloadDirectory = Path.Combine(Path.GetTempPath(), "MoonlightForwarderGenerator", "Sunshine", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(downloadDirectory);
            var installerPath = Path.Combine(downloadDirectory, asset.Name);

            try
            {
                await using (var source = await http.GetStreamAsync(asset.BrowserDownloadUrl))
                await using (var destination = File.Create(installerPath))
                    await source.CopyToAsync(destination);

                var installer = Process.Start(new ProcessStartInfo(installerPath) { UseShellExecute = true });
                if (installer is null)
                    return new SunshineInstallationResult(false, "Não foi possível iniciar o instalador do Sunshine.");

                await installer.WaitForExitAsync();
                if (installer.ExitCode != 0)
                    return new SunshineInstallationResult(false, $"A instalação foi cancelada ou terminou com o código {installer.ExitCode}.");
            }
            finally
            {
                if (Directory.Exists(downloadDirectory))
                    Directory.Delete(downloadDirectory, true);
            }

            var installation = await SunshineInstallationService.RefreshAsync();
            if (!installation.IsInstalled)
                return new SunshineInstallationResult(false, "O instalador terminou, mas o Sunshine não foi localizado. Verifique a instalação e tente novamente.", installation);

            var runtime = await SunshineInstallationService.EnsureRunningAsync(installation);
            return runtime.IsReady
                ? new SunshineInstallationResult(true, "Sunshine instalado e pronto para conexão local.", installation)
                : new SunshineInstallationResult(false, $"Sunshine foi instalado, mas não ficou pronto: {runtime.Message}", installation);
        }
        catch (HttpRequestException exception)
        {
            return new SunshineInstallationResult(false, $"Não foi possível consultar ou baixar a release oficial do Sunshine: {exception.Message}");
        }
        catch (UnauthorizedAccessException exception)
        {
            return new SunshineInstallationResult(false, $"A instalação foi cancelada ou bloqueada pelo Windows: {exception.Message}");
        }
        catch (Exception exception)
        {
            return new SunshineInstallationResult(false, $"Não foi possível instalar o Sunshine: {exception.Message}");
        }
    }

    private static GitHubReleaseAsset? SelectInstaller(IEnumerable<GitHubReleaseAsset> assets)
    {
        var architecture = RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "ARM64" : "AMD64";
        var candidates = assets.Where(asset =>
            asset.Name.StartsWith("Sunshine-Windows-", StringComparison.OrdinalIgnoreCase)
            && asset.Name.Contains(architecture, StringComparison.OrdinalIgnoreCase)
            && asset.Name.Contains("-installer.", StringComparison.OrdinalIgnoreCase));

        return candidates.FirstOrDefault(asset => asset.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            ?? candidates.FirstOrDefault(asset => asset.Name.EndsWith(".msi", StringComparison.OrdinalIgnoreCase));
    }

    private sealed record GitHubRelease(
        [property: JsonPropertyName("draft")] bool Draft,
        [property: JsonPropertyName("prerelease")] bool Prerelease,
        [property: JsonPropertyName("assets")] List<GitHubReleaseAsset> Assets);

    private sealed record GitHubReleaseAsset(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("browser_download_url")] Uri BrowserDownloadUrl);
}

public sealed record SunshineInstallationResult(bool Success, string Message, Data.SunshineInstallation? Installation = null);
