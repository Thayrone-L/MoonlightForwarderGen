using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Security.Principal;
using System.ComponentModel;
using MFG.App.Data;

namespace MFG.App.Services;

public sealed class SunshineApplicationManager
{
    public async Task SyncAsync(string sunshineExecutablePath, LibraryGame game)
    {
        if (!File.Exists(game.ExecutablePath))
            throw new FileNotFoundException("O executável do jogo não foi encontrado.", game.ExecutablePath);

        var appsPath = ResolveAppsPath(sunshineExecutablePath);
        JsonObject root;
        try
        {
            root = await LoadAsync(appsPath);
        }
        catch (UnauthorizedAccessException exception)
        {
            throw new SunshineConfigurationAccessException(appsPath, exception);
        }
        var apps = root["apps"]!.AsArray();
        var expectedCommand = $"\"{game.ExecutablePath}\"";
        var app = apps
            .OfType<JsonObject>()
            .FirstOrDefault(item => string.Equals(
                item["name"]?.GetValue<string>(),
                game.SunshineApplicationName,
                StringComparison.Ordinal));

        app ??= apps.OfType<JsonObject>().FirstOrDefault(item => string.Equals(
            item["cmd"]?.GetValue<string>(), expectedCommand, StringComparison.Ordinal));

        if (app is null)
        {
            app = new JsonObject();
            apps.Add(app);
        }

        // Mantem a entrada existente na mesma posicao para nao alterar o AppID.
        var duplicates = apps.OfType<JsonObject>()
            .Where(item => !ReferenceEquals(item, app) && string.Equals(
                item["name"]?.GetValue<string>(), game.SunshineApplicationName, StringComparison.Ordinal))
            .ToList();
        foreach (var duplicate in duplicates)
            apps.Remove(duplicate);

        app["name"] = game.SunshineApplicationName;
        app["cmd"] = expectedCommand;
        app["working-dir"] = Path.GetDirectoryName(game.ExecutablePath);
        // O MFG inicia cada jogo como uma sessão independente. Ao encerrar a sessão,
        // o Sunshine deve encerrar também o processo associado, em vez de reaproveitá-lo
        // para o próximo atalho aberto no Switch.
        app["auto-detach"] = false;
        app["wait-all"] = true;
        // Alguns jogos pedem UAC ao serem abertos pelo Explorer. Sem isto, o Sunshine
        // pode iniciar o processo e ele encerrar imediatamente sem mostrar a tela do UAC.
        app["elevated"] = true;
        var outputDirectory = Path.Combine(Path.GetDirectoryName(appsPath)!, "mfg-logs");
        Directory.CreateDirectory(outputDirectory);
        app["output"] = Path.Combine(outputDirectory, $"{SanitizeLogFileName(game.SunshineApplicationName)}.log");

        try
        {
            await SaveAsync(appsPath, root);
        }
        catch (UnauthorizedAccessException exception)
        {
            throw new SunshineConfigurationAccessException(appsPath, exception);
        }
    }

    public async Task RemoveAsync(string sunshineExecutablePath, string applicationName, string executablePath)
    {
        var appsPath = ResolveAppsPath(sunshineExecutablePath);
        try
        {
            var root = await LoadAsync(appsPath);
            var apps = root["apps"]!.AsArray();
            var expectedCommand = $"\"{executablePath}\"";
            var removed = apps.OfType<JsonObject>()
                .Where(item => string.Equals(item["name"]?.GetValue<string>(), applicationName, StringComparison.Ordinal) ||
                               string.Equals(item["cmd"]?.GetValue<string>(), expectedCommand, StringComparison.Ordinal))
                .ToList();
            foreach (var item in removed)
                apps.Remove(item);

            if (removed.Count == 0)
                return;

            await SaveAsync(appsPath, root);
            AppLog.Info($"{removed.Count} registro(s) anterior(es) removido(s) do Sunshine para {applicationName}.");
        }
        catch (UnauthorizedAccessException exception)
        {
            throw new SunshineConfigurationAccessException(appsPath, exception);
        }
    }

    public bool ContainsApplication(string sunshineExecutablePath, string applicationName)
    {
        var appsPath = ResolveAppsPath(sunshineExecutablePath);
        if (!File.Exists(appsPath))
            return false;

        var root = JsonNode.Parse(File.ReadAllText(appsPath)) as JsonObject;
        return root?["apps"] is JsonArray apps
            && apps.OfType<JsonObject>().Any(item => string.Equals(
                item["name"]?.GetValue<string>(),
                applicationName,
                StringComparison.Ordinal));
    }

    private static async Task<JsonObject> LoadAsync(string appsPath)
    {
        if (!File.Exists(appsPath))
            return new JsonObject { ["apps"] = new JsonArray() };

        var root = JsonNode.Parse(await File.ReadAllTextAsync(appsPath)) as JsonObject
            ?? throw new InvalidOperationException("A configuração de aplicações do Sunshine não é válida.");
        if (root["apps"] is null)
            root["apps"] = new JsonArray();
        if (root["apps"] is not JsonArray)
            throw new InvalidOperationException("A lista de aplicações do Sunshine não é válida.");

        return root;
    }

    private static async Task SaveAsync(string appsPath, JsonObject root)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(appsPath)!);
        var temporaryPath = $"{appsPath}.{Guid.NewGuid():N}.tmp";
        try
        {
            await File.WriteAllTextAsync(temporaryPath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporaryPath, appsPath, true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    private static string SanitizeLogFileName(string value)
    {
        foreach (var invalidCharacter in Path.GetInvalidFileNameChars())
            value = value.Replace(invalidCharacter, '_');
        return string.IsNullOrWhiteSpace(value) ? "game" : value;
    }

    public async Task GrantCurrentUserModifyAccessAsync(string sunshineExecutablePath)
    {
        var appsPath = ResolveAppsPath(sunshineExecutablePath);
        var directory = Path.GetDirectoryName(appsPath)
            ?? throw new InvalidOperationException("Nao foi possivel localizar a pasta de configuracao do Sunshine.");
        var account = WindowsIdentity.GetCurrent().Name;
        var startInfo = new ProcessStartInfo("icacls.exe")
        {
            UseShellExecute = true,
            Verb = "runas"
        };
        startInfo.ArgumentList.Add(directory);
        startInfo.ArgumentList.Add("/grant");
        startInfo.ArgumentList.Add($"{account}:(OI)(CI)M");
        startInfo.ArgumentList.Add("/T");
        startInfo.ArgumentList.Add("/C");

        try
        {
            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Nao foi possivel solicitar permissao para a configuracao do Sunshine.");
            await process.WaitForExitAsync();
            if (process.ExitCode != 0)
                throw new InvalidOperationException("O Windows nao aplicou a permissao para a configuracao do Sunshine.");
        }
        catch (Win32Exception exception)
        {
            throw new InvalidOperationException("A permissao foi cancelada ou bloqueada pelo Windows.", exception);
        }
    }

    public static string ResolveAppsPath(string sunshineExecutablePath)
    {
        var configDirectory = SunshineManager.GetConfigDirectory(sunshineExecutablePath);
        var configPath = Path.Combine(configDirectory, "sunshine.conf");
        if (File.Exists(configPath))
        {
            var setting = File.ReadLines(configPath)
                .Select(line => Regex.Match(line, @"^\s*file_apps\s*=\s*(?<path>[^#;]+)"))
                .FirstOrDefault(match => match.Success);
            if (setting?.Success == true)
            {
                var configuredPath = setting.Groups["path"].Value.Trim().Trim('"');
                if (!string.IsNullOrWhiteSpace(configuredPath))
                    return Path.IsPathRooted(configuredPath)
                        ? configuredPath
                        : Path.Combine(configDirectory, configuredPath);
            }
        }

        return Path.Combine(configDirectory, "apps.json");
    }
}

public sealed class SunshineConfigurationAccessException(string appsPath, Exception innerException)
    : UnauthorizedAccessException("O Sunshine bloqueou a gravacao da lista de jogos.", innerException)
{
    public string AppsPath { get; } = appsPath;
}
