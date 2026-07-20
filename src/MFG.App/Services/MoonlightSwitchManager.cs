using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using MFG.Common.Library;

namespace MFG.App.Services;

public sealed class MoonlightSwitchManager
{
    private static readonly string[] MoonlightNroFileNames = ["Moonlight-Switch.nro", "Moonlight.nro"];

    public SwitchSdCardStatus DetectSdCard()
    {
        foreach (var drive in DriveInfo.GetDrives()
                     .Where(item => item.DriveType == DriveType.Removable && item.IsReady))
        {
            var switchDirectory = Path.Combine(drive.RootDirectory.FullName, "switch");
            if (!Directory.Exists(switchDirectory))
                continue;

            var settingsPath = Path.Combine(switchDirectory, "Moonlight-Switch", "settings.json");
            return new SwitchSdCardStatus(
                true,
                drive.RootDirectory.FullName,
                settingsPath,
                File.Exists(settingsPath)
                    ? "Cartao SD do Switch conectado. Os atalhos serao copiados automaticamente."
                    : "Cartao SD do Switch conectado. Os atalhos podem ser copiados para a pasta switch.");
        }

        return new SwitchSdCardStatus(
            false,
            null,
            null,
            "Nenhum cartao SD do Switch foi encontrado. Os atalhos serao mantidos neste computador.");
    }

    public MoonlightFavoritesStatus ReadFavorites()
    {
        var sdCard = DetectSdCard();
        if (!sdCard.IsDetected || string.IsNullOrWhiteSpace(sdCard.SettingsPath))
            return new MoonlightFavoritesStatus(HealthCheckState.Unavailable, new HashSet<string>(), "Conecte o cartao SD do Switch para verificar os favoritos do Moonlight.");
        if (!File.Exists(sdCard.SettingsPath))
            return new MoonlightFavoritesStatus(HealthCheckState.Unavailable, new HashSet<string>(), "O Moonlight-Switch ainda nao foi configurado no cartao SD.");

        try
        {
            using var stream = new FileStream(sdCard.SettingsPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var document = JsonDocument.Parse(stream);
            if (!document.RootElement.TryGetProperty("hosts", out var hosts) || hosts.ValueKind != JsonValueKind.Array)
                return new MoonlightFavoritesStatus(HealthCheckState.Invalid, new HashSet<string>(), "O arquivo de configuracao do Moonlight-Switch nao possui hosts validos.");

            var favoriteIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var host in hosts.EnumerateArray())
            {
                if (!host.TryGetProperty("favorites", out var favorites) || favorites.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (var favorite in favorites.EnumerateArray())
                {
                    if (!favorite.TryGetProperty("id", out var id))
                        continue;
                    if (id.ValueKind == JsonValueKind.Number && id.TryGetInt64(out var numericId))
                        favoriteIds.Add(numericId.ToString());
                    else if (id.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(id.GetString()))
                        favoriteIds.Add(id.GetString()!);
                }
            }

            return new MoonlightFavoritesStatus(HealthCheckState.Ready, favoriteIds, "Favoritos do Moonlight-Switch verificados.");
        }
        catch (JsonException exception)
        {
            AppLog.Warning("O settings.json do Moonlight-Switch esta invalido.", exception);
            return new MoonlightFavoritesStatus(HealthCheckState.Invalid, new HashSet<string>(), "O arquivo settings.json do Moonlight-Switch esta invalido.");
        }
        catch (IOException exception)
        {
            AppLog.Warning("Nao foi possivel ler o settings.json do Moonlight-Switch.", exception);
            return new MoonlightFavoritesStatus(HealthCheckState.Unavailable, new HashSet<string>(), "Nao foi possivel ler o settings.json do Moonlight-Switch.");
        }
    }

    public string GetMoonlightNroPath()
    {
        var sdCard = DetectSdCard();
        if (sdCard.IsDetected && sdCard.RootPath is not null)
        {
            foreach (var fileName in MoonlightNroFileNames)
            {
                var localPath = Path.Combine(sdCard.RootPath, "switch", "Moonlight-Switch", fileName);
                if (File.Exists(localPath))
                    return $"/switch/Moonlight-Switch/{fileName}";
            }
        }

        // Nome utilizado pelas versoes atuais do Moonlight-Switch.
        return "/switch/Moonlight-Switch/Moonlight-Switch.nro";
    }

    public MoonlightPairedConnection GetPairedConnection()
    {
        var sdCard = DetectSdCard();
        if (!sdCard.IsDetected || sdCard.RootPath is null || sdCard.SettingsPath is null || !File.Exists(sdCard.SettingsPath))
            throw new InvalidOperationException("Conecte o cartão SD com o Moonlight-Switch já pareado para preparar o atalho.");

        using var document = JsonDocument.Parse(File.ReadAllText(sdCard.SettingsPath));
        var host = document.RootElement.TryGetProperty("hosts", out var hosts) && hosts.ValueKind == JsonValueKind.Array
            ? hosts.EnumerateArray().FirstOrDefault(item => item.TryGetProperty("address", out var address) && !string.IsNullOrWhiteSpace(address.GetString()))
            : default;
        var address = host.ValueKind == JsonValueKind.Undefined ? null : host.GetProperty("address").GetString();
        if (string.IsNullOrWhiteSpace(address))
            throw new InvalidOperationException("Não encontramos um computador pareado no Moonlight-Switch.");

        var keyDirectory = Path.Combine(sdCard.RootPath, "switch", "Moonlight-Switch", "key");
        var certificatePath = Path.Combine(keyDirectory, "client.pem");
        var privateKeyPath = Path.Combine(keyDirectory, "key.pem");
        if (!File.Exists(certificatePath) || !File.Exists(privateKeyPath))
            throw new InvalidOperationException("As credenciais do Moonlight-Switch não foram encontradas no cartão SD.");
        return new MoonlightPairedConnection(address, certificatePath, privateKeyPath);
    }

    public void DisableDiagnostics()
    {
        var sdCard = DetectSdCard();
        if (!sdCard.IsDetected || string.IsNullOrWhiteSpace(sdCard.SettingsPath) || !File.Exists(sdCard.SettingsPath))
            return;
        try
        {
            var root = JsonNode.Parse(File.ReadAllText(sdCard.SettingsPath))?.AsObject();
            if (root is null)
                return;
            var settings = root["settings"] as JsonObject;
            if (settings is null || settings["write_log"]?.GetValue<bool>() != true)
                return;
            settings["write_log"] = false;
            var temporaryPath = sdCard.SettingsPath + ".mfg.tmp";
            File.WriteAllText(temporaryPath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporaryPath, sdCard.SettingsPath, true);
            AppLog.Info("Visualizacao de depuracao do Moonlight-Switch foi desabilitada.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            AppLog.Error("Nao foi possivel desabilitar a visualizacao de depuracao do Moonlight-Switch.", exception);
        }
    }
}

public sealed record SwitchSdCardStatus(
    bool IsDetected,
    string? RootPath,
    string? SettingsPath,
    string Message);

public sealed record MoonlightFavoritesStatus(
    HealthCheckState State,
    IReadOnlySet<string> FavoriteIds,
    string Message);

public sealed record MoonlightPairedConnection(string Address, string ClientCertificatePath, string PrivateKeyPath);
