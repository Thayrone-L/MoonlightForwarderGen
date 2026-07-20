using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.Win32;

namespace MFG.App.Services;

public sealed class SunshineManager
{
    private const int StreamingPort = 47989;
    private const int WebUiPort = 47990;
    private const string WindowsServiceName = "SunshineService";
    private const string ReleasesUrl = "https://api.github.com/repos/LizardByte/Sunshine/releases?per_page=20";

    public SunshineHealth Check()
    {
        var executablePath = FindExecutablePath();
        if (executablePath is null)
            return new SunshineHealth(false, false, false, false, null, "Precisamos instalar o Sunshine antes de preparar seus jogos.");

        var running = IsRunning(executablePath);
        var ready = running && HasRequiredPorts();
        var configured = File.Exists(Path.Combine(GetConfigDirectory(executablePath), "apps.json"));

        return ready
            ? new SunshineHealth(true, true, true, configured, executablePath, "Tudo pronto para preparar jogos.")
            : running
                ? new SunshineHealth(true, true, false, configured, executablePath, "O Sunshine está iniciando ou precisa de atenção.")
                : new SunshineHealth(true, false, false, configured, executablePath, "O Sunshine está instalado, mas ainda não está em execução.");
    }

    public async Task<SunshineHealth> PrepareAsync()
    {
        var health = Check();
        if (!health.IsInstalled)
        {
            var installation = await InstallAsync();
            if (!installation.Success)
                throw new InvalidOperationException(installation.Message);
            health = Check();
        }

        if (health.ExecutablePath is null)
            throw new InvalidOperationException("O Sunshine foi instalado, mas não foi localizado.");

        await EnsureConfigurationAsync(health.ExecutablePath);
        await EnsureRunningAsync(health.ExecutablePath);
        return Check();
    }

    private static async Task<SunshineInstallResult> InstallAsync()
    {
        try
        {
            using var http = new HttpClient();
            http.DefaultRequestHeaders.UserAgent.ParseAdd("MoonlightForwarderGenerator/1.0");
            http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");

            var releases = await http.GetFromJsonAsync<List<GitHubRelease>>(ReleasesUrl);
            var release = releases?.FirstOrDefault(item => !item.Draft && !item.Prerelease);
            var asset = release is null ? null : SelectWindowsInstaller(release.Assets);
            if (asset is null)
                return new SunshineInstallResult(false, "Não encontramos um instalador compatível do Sunshine.");

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
                    return new SunshineInstallResult(false, "Não foi possível abrir o instalador do Sunshine.");

                await installer.WaitForExitAsync();
                return installer.ExitCode == 0
                    ? new SunshineInstallResult(true, "Instalação concluída.")
                    : new SunshineInstallResult(false, "A instalação foi cancelada ou não foi concluída.");
            }
            finally
            {
                if (Directory.Exists(downloadDirectory))
                    Directory.Delete(downloadDirectory, true);
            }
        }
        catch (HttpRequestException exception)
        {
            AppLog.Error("Falha ao baixar o instalador do Sunshine.", exception);
            return new SunshineInstallResult(false, "Não foi possível baixar o Sunshine. Verifique sua conexão e tente novamente.");
        }
        catch (Exception exception)
        {
            AppLog.Error("Falha inesperada ao preparar a instalacao do Sunshine.", exception);
            return new SunshineInstallResult(false, $"Não foi possível preparar o Sunshine: {exception.Message}");
        }
    }

    private static async Task EnsureConfigurationAsync(string executablePath)
    {
        var configDirectory = GetConfigDirectory(executablePath);
        Directory.CreateDirectory(configDirectory);
        var appsPath = Path.Combine(configDirectory, "apps.json");
        if (!File.Exists(appsPath))
            await File.WriteAllTextAsync(appsPath, new JsonObject { ["apps"] = new JsonArray() }
                .ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    private static async Task EnsureRunningAsync(string executablePath)
    {
        if (!IsRunning(executablePath))
        {
            if (IsWindowsServiceInstalled())
            {
                await RunServiceCommandElevatedAsync("start");
            }
            else
            {
            var process = Process.Start(new ProcessStartInfo(executablePath)
            {
                WorkingDirectory = Path.GetDirectoryName(executablePath) ?? Environment.CurrentDirectory,
                UseShellExecute = true,
                Verb = "runas"
            });
            if (process is null)
                throw new InvalidOperationException("Não foi possível iniciar o Sunshine.");
            }
        }

        for (var attempt = 0; attempt < 20; attempt++)
        {
            if (IsRunning(executablePath) && HasRequiredPorts())
                return;
            await Task.Delay(500);
        }
    }

    public async Task RestartElevatedAsync(Func<Task>? beforeStart = null)
    {
        var executablePath = FindExecutablePath();
        if (executablePath is null)
            return;

        if (IsWindowsServiceInstalled())
        {
            await RestartWindowsServiceAsync(executablePath, beforeStart);
            return;
        }

        var processName = Path.GetFileNameWithoutExtension(executablePath);
        foreach (var process in Process.GetProcessesByName(processName))
        {
            try
            {
                process.Kill(true);
                await process.WaitForExitAsync();
            }
            catch (System.ComponentModel.Win32Exception exception) when (exception.NativeErrorCode == 5)
            {
                // Sunshine foi iniciado elevado. Um Companion sem elevação não pode
                // encerrar sua árvore diretamente, então delegamos apenas esse
                // encerramento ao Windows com a mesma confirmação UAC usada para iniciar.
                await StopProcessElevatedAsync(process.Id);
            }
            finally
            {
                process.Dispose();
            }
        }

        if (beforeStart is not null)
            await beforeStart();

        var started = Process.Start(new ProcessStartInfo(executablePath)
        {
            WorkingDirectory = Path.GetDirectoryName(executablePath) ?? Environment.CurrentDirectory,
            UseShellExecute = true,
            Verb = "runas"
        });
        if (started is null)
            throw new InvalidOperationException("Não foi possível iniciar o Sunshine como administrador.");

        for (var attempt = 0; attempt < 20; attempt++)
        {
            if (IsRunning(executablePath) && HasRequiredPorts())
                return;
            await Task.Delay(500);
        }
        throw new InvalidOperationException("O Sunshine não ficou pronto após reiniciar como administrador.");
    }

    private static async Task RestartWindowsServiceAsync(string executablePath, Func<Task>? beforeStart)
    {
        if (IsRunning(executablePath))
        {
            await RunServiceCommandElevatedAsync("stop");
            for (var attempt = 0; attempt < 20 && IsRunning(executablePath); attempt++)
                await Task.Delay(500);
        }

        if (beforeStart is not null)
            await beforeStart();

        await RunServiceCommandElevatedAsync("start");
        for (var attempt = 0; attempt < 20; attempt++)
        {
            if (IsRunning(executablePath) && HasRequiredPorts())
                return;
            await Task.Delay(500);
        }

        throw new InvalidOperationException("O serviço do Sunshine não ficou pronto após reiniciar.");
    }

    private static bool IsWindowsServiceInstalled()
    {
        try
        {
            using var query = Process.Start(new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "sc.exe"), $"query {WindowsServiceName}")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            if (query is null)
                return false;
            query.WaitForExit(3000);
            return query.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static async Task RunServiceCommandElevatedAsync(string command)
    {
        var process = Process.Start(new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "sc.exe"), $"{command} {WindowsServiceName}")
        {
            UseShellExecute = true,
            Verb = "runas",
            WindowStyle = ProcessWindowStyle.Hidden
        });
        if (process is null)
            throw new InvalidOperationException("Não foi possível controlar o serviço do Sunshine.");

        await process.WaitForExitAsync();
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"O Windows não conseguiu executar '{command}' no serviço do Sunshine.");
    }

    private static async Task StopProcessElevatedAsync(int processId)
    {
        var stop = Process.Start(new ProcessStartInfo("taskkill.exe", $"/PID {processId} /T /F")
        {
            UseShellExecute = true,
            Verb = "runas",
            CreateNoWindow = true
        });
        if (stop is null)
            throw new InvalidOperationException("Não foi possível solicitar ao Windows o encerramento do Sunshine.");

        await stop.WaitForExitAsync();
        if (stop.ExitCode != 0 && Process.GetProcesses().Any(process => process.Id == processId))
            throw new InvalidOperationException("O Windows não conseguiu encerrar o Sunshine elevado.");
    }

    public static string GetConfigDirectory(string executablePath) =>
        Path.Combine(Path.GetDirectoryName(executablePath)!, "config");

    private static GitHubReleaseAsset? SelectWindowsInstaller(IEnumerable<GitHubReleaseAsset> assets)
    {
        var architecture = RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "ARM64" : "AMD64";
        var candidates = assets.Where(asset =>
            asset.Name.StartsWith("Sunshine-Windows-", StringComparison.OrdinalIgnoreCase)
            && asset.Name.Contains(architecture, StringComparison.OrdinalIgnoreCase)
            && asset.Name.Contains("-installer.", StringComparison.OrdinalIgnoreCase));

        return candidates.FirstOrDefault(asset => asset.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            ?? candidates.FirstOrDefault(asset => asset.Name.EndsWith(".msi", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsRunning(string executablePath)
    {
        var processName = Path.GetFileNameWithoutExtension(executablePath);
        return Process.GetProcessesByName(processName).Any(process =>
        {
            try
            {
                return string.Equals(process.MainModule?.FileName, executablePath, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return true;
            }
            finally
            {
                process.Dispose();
            }
        });
    }

    private static bool HasRequiredPorts()
    {
        var ports = IPGlobalProperties.GetIPGlobalProperties()
            .GetActiveTcpListeners()
            .Select(endpoint => endpoint.Port)
            .ToHashSet();
        return ports.Contains(StreamingPort) && ports.Contains(WebUiPort);
    }

    private static string? FindExecutablePath()
    {
        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Sunshine", "sunshine.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Sunshine", "sunshine.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Sunshine", "sunshine.exe")
        };

        return candidates.FirstOrDefault(File.Exists) ?? FindInRegistry();
    }

    private static string? FindInRegistry()
    {
        foreach (var uninstallKey in new[]
                 {
                     @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                     @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
                 })
        {
            using var key = Registry.LocalMachine.OpenSubKey(uninstallKey);
            foreach (var name in key?.GetSubKeyNames() ?? [])
            {
                using var subKey = key?.OpenSubKey(name);
                if (subKey?.GetValue("DisplayName") is not string displayName
                    || !displayName.Contains("Sunshine", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (subKey.GetValue("InstallLocation") is string location)
                {
                    var executable = Path.Combine(location, "sunshine.exe");
                    if (File.Exists(executable))
                        return executable;
                }
            }
        }

        return null;
    }

    private sealed record GitHubRelease(
        [property: JsonPropertyName("draft")] bool Draft,
        [property: JsonPropertyName("prerelease")] bool Prerelease,
        [property: JsonPropertyName("assets")] List<GitHubReleaseAsset> Assets);

    private sealed record GitHubReleaseAsset(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("browser_download_url")] Uri BrowserDownloadUrl);

    private sealed record SunshineInstallResult(bool Success, string Message);
}

public sealed record SunshineHealth(
    bool IsInstalled,
    bool IsRunning,
    bool IsReady,
    bool IsConfigured,
    string? ExecutablePath,
    string Message);
