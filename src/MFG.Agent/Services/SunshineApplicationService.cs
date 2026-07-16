using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using MFG.Agent.Data;

namespace MFG.Agent.Services;

public static class SunshineApplicationService
{
    public static async Task<SunshineApplicationSyncResult> UpsertGameAsync(SunshineInstallation installation, string gameId)
    {
        if (!installation.IsInstalled || string.IsNullOrWhiteSpace(installation.ExecutablePath))
            return new SunshineApplicationSyncResult(false, "O jogo foi salvo, mas o Sunshine não está instalado para receber a aplicação.");

        if (gameId.Contains('"'))
            return new SunshineApplicationSyncResult(false, "O jogo foi salvo, mas IDs com aspas não podem ser usados pelo launcher do Sunshine.");

        var launcherPath = Path.Combine(AppContext.BaseDirectory, "MFG.Launcher.exe");
        if (!File.Exists(launcherPath))
            return new SunshineApplicationSyncResult(false, "O jogo foi salvo, mas o MFG.Launcher não foi encontrado na pasta do Agent.");

        try
        {
            var appsPath = ResolveAppsPath(installation.ExecutablePath);
            var root = await LoadAppsAsync(appsPath);
            var apps = root["apps"]!.AsArray();
            var appName = $"MFG - {gameId}";
            var app = apps
                .OfType<JsonObject>()
                .FirstOrDefault(item => string.Equals(item["name"]?.GetValue<string>(), appName, StringComparison.Ordinal));

            if (app is null)
            {
                app = new JsonObject();
                apps.Add(app);
            }

            app["name"] = appName;
            app["cmd"] = $"\"{launcherPath}\" launch-game \"{gameId}\"";
            app["working-dir"] = Path.GetDirectoryName(launcherPath);

            Directory.CreateDirectory(Path.GetDirectoryName(appsPath)!);
            var temporaryPath = $"{appsPath}.{Guid.NewGuid():N}.tmp";
            await File.WriteAllTextAsync(temporaryPath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporaryPath, appsPath, true);

            return new SunshineApplicationSyncResult(true, $"Aplicação '{appName}' criada ou atualizada no Sunshine.");
        }
        catch (UnauthorizedAccessException)
        {
            return new SunshineApplicationSyncResult(false, "O jogo foi salvo, mas não há permissão para atualizar o apps.json do Sunshine.");
        }
        catch (Exception exception)
        {
            return new SunshineApplicationSyncResult(false, $"O jogo foi salvo, mas não foi possível atualizar o apps.json do Sunshine: {exception.Message}");
        }
    }

    private static async Task<JsonObject> LoadAppsAsync(string appsPath)
    {
        if (!File.Exists(appsPath))
            return new JsonObject { ["apps"] = new JsonArray() };

        var root = JsonNode.Parse(await File.ReadAllTextAsync(appsPath)) as JsonObject
            ?? throw new InvalidOperationException("O apps.json do Sunshine não contém um objeto JSON válido.");

        if (root["apps"] is null)
            root["apps"] = new JsonArray();
        if (root["apps"] is not JsonArray)
            throw new InvalidOperationException("O campo 'apps' do apps.json do Sunshine não é uma lista válida.");

        return root;
    }

    private static string ResolveAppsPath(string sunshineExecutablePath)
    {
        var configDirectory = Path.Combine(Path.GetDirectoryName(sunshineExecutablePath)!, "config");
        var configPath = Path.Combine(configDirectory, "sunshine.conf");
        if (File.Exists(configPath))
        {
            var fileApps = File.ReadLines(configPath)
                .Select(line => Regex.Match(line, @"^\s*file_apps\s*=\s*(?<path>[^#;]+)"))
                .FirstOrDefault(match => match.Success);

            if (fileApps?.Success == true)
            {
                var configuredPath = fileApps.Groups["path"].Value.Trim().Trim('"');
                if (!string.IsNullOrWhiteSpace(configuredPath))
                    return Path.IsPathRooted(configuredPath)
                        ? configuredPath
                        : Path.Combine(configDirectory, configuredPath);
            }
        }

        return Path.Combine(configDirectory, "apps.json");
    }
}

public sealed record SunshineApplicationSyncResult(bool Success, string Message);
