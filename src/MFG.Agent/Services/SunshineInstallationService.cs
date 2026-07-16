using Microsoft.Win32;
using MFG.Agent.Data;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Runtime.Versioning;

namespace MFG.Agent.Services;

public static class SunshineInstallationService
{
    private const int StreamingPort = 47989;
    private const int WebUiPort = 47990;

    public static async Task<SunshineInstallation> RefreshAsync()
    {
        var executablePath = FindExecutablePath();

        using var database = MfgDbContext.Create();
        var installation = await database.SunshineInstallations.FindAsync(1)
            ?? new SunshineInstallation { Id = 1 };

        if (database.Entry(installation).State == Microsoft.EntityFrameworkCore.EntityState.Detached)
            database.SunshineInstallations.Add(installation);

        installation.IsInstalled = executablePath is not null;
        installation.ExecutablePath = executablePath;
        installation.LastCheckedAt = DateTimeOffset.UtcNow;
        await database.SaveChangesAsync();

        return installation;
    }

    public static async Task<SunshineRuntimeStatus> EnsureRunningAsync(SunshineInstallation installation)
    {
        if (!installation.IsInstalled || string.IsNullOrWhiteSpace(installation.ExecutablePath))
            return new SunshineRuntimeStatus(false, false, "Sunshine não está instalado.", null);

        try
        {
            if (!IsRunning(installation.ExecutablePath))
            {
                var process = Process.Start(new ProcessStartInfo(installation.ExecutablePath)
                {
                    WorkingDirectory = Path.GetDirectoryName(installation.ExecutablePath) ?? Environment.CurrentDirectory,
                    UseShellExecute = true
                });

                if (process is null)
                    return new SunshineRuntimeStatus(false, false, "Não foi possível iniciar o Sunshine.", null);
            }

            for (var attempt = 0; attempt < 20; attempt++)
            {
                var running = IsRunning(installation.ExecutablePath);
                var ready = running && IsReady();
                if (ready)
                    return new SunshineRuntimeStatus(true, true, "Sunshine está em execução e pronto para conexão local.", $"https://localhost:{WebUiPort}");

                await Task.Delay(500);
            }

            var stillRunning = IsRunning(installation.ExecutablePath);
            return stillRunning
                ? new SunshineRuntimeStatus(true, false, "Sunshine iniciou, mas as portas de streaming ainda não estão prontas. Verifique a configuração na interface web.", $"https://localhost:{WebUiPort}")
                : new SunshineRuntimeStatus(false, false, "O processo do Sunshine encerrou durante a inicialização. Verifique os logs do Sunshine.", null);
        }
        catch (Exception exception)
        {
            return new SunshineRuntimeStatus(false, false, $"Não foi possível iniciar o Sunshine: {exception.Message}", null);
        }
    }

    public static SunshineRuntimeStatus GetRuntimeStatus(SunshineInstallation installation)
    {
        if (!installation.IsInstalled || string.IsNullOrWhiteSpace(installation.ExecutablePath))
            return new SunshineRuntimeStatus(false, false, "Sunshine não está instalado.", null);

        var running = IsRunning(installation.ExecutablePath);
        var ready = running && IsReady();
        return ready
            ? new SunshineRuntimeStatus(true, true, "Sunshine está em execução e pronto para conexão local.", $"https://localhost:{WebUiPort}")
            : running
                ? new SunshineRuntimeStatus(true, false, "Sunshine está em execução, mas as portas necessárias ainda não estão prontas.", $"https://localhost:{WebUiPort}")
                : new SunshineRuntimeStatus(false, false, "Sunshine está instalado, mas não está em execução.", null);
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

    private static bool IsReady()
    {
        var listeningPorts = IPGlobalProperties.GetIPGlobalProperties()
            .GetActiveTcpListeners()
            .Select(endpoint => endpoint.Port)
            .ToHashSet();

        return listeningPorts.Contains(StreamingPort) && listeningPorts.Contains(WebUiPort);
    }

    private static string? FindExecutablePath()
    {
        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Sunshine", "sunshine.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Sunshine", "sunshine.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Sunshine", "sunshine.exe")
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
                return candidate;
        }

        return OperatingSystem.IsWindows() ? FindExecutablePathInRegistry() : null;
    }

    [SupportedOSPlatform("windows")]
    private static string? FindExecutablePathInRegistry()
    {
        var uninstallKeys = new[]
        {
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
            @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
        };

        foreach (var uninstallKey in uninstallKeys)
        {
            using var key = Registry.LocalMachine.OpenSubKey(uninstallKey);
            if (key is null)
                continue;

            foreach (var subKeyName in key.GetSubKeyNames())
            {
                using var subKey = key.OpenSubKey(subKeyName);
                if (subKey is null)
                    continue;

                var displayName = subKey.GetValue("DisplayName") as string;
                if (displayName is null || !displayName.Contains("Sunshine", StringComparison.OrdinalIgnoreCase))
                    continue;

                var installLocation = subKey.GetValue("InstallLocation") as string;
                if (!string.IsNullOrWhiteSpace(installLocation))
                {
                    var executablePath = Path.Combine(installLocation, "sunshine.exe");
                    if (File.Exists(executablePath))
                        return executablePath;
                }

                var displayIcon = subKey.GetValue("DisplayIcon") as string;
                var displayIconPath = displayIcon?.Trim().Trim('"').Split(',')[0];
                if (!string.IsNullOrWhiteSpace(displayIconPath) && File.Exists(displayIconPath))
                    return displayIconPath;
            }
        }

        return null;
    }
}

public sealed record SunshineRuntimeStatus(bool IsRunning, bool IsReady, string Message, string? WebUiUrl);
