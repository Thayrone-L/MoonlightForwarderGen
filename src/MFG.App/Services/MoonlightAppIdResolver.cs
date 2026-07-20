using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace MFG.App.Services;

public sealed class MoonlightAppIdResolver
{
    private readonly MoonlightSwitchManager _switch = new();

    public async Task<string> ResolveAsync(string applicationName, CancellationToken cancellationToken = default)
    {
        var connection = _switch.GetPairedConnection();
        Exception? lastError = null;
        for (var attempt = 1; attempt <= 12; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var appList = await GetAppListAsync(connection, cancellationToken);
                var document = XDocument.Parse(appList);
                var app = document.Descendants("App").FirstOrDefault(item =>
                    string.Equals(item.Element("AppTitle")?.Value, applicationName, StringComparison.Ordinal));
                var id = app?.Element("ID")?.Value?.Trim();
                if (!string.IsNullOrWhiteSpace(id) && int.TryParse(id, out _))
                {
                    AppLog.Info($"Identificador Moonlight obtido para {applicationName}: {id} (tentativa {attempt}).");
                    return id;
                }
            }
            catch (Exception exception) when (exception is InvalidOperationException or JsonException or System.Xml.XmlException)
            {
                lastError = exception;
            }

            if (attempt < 12)
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        }

        throw new InvalidOperationException(
            $"O Sunshine não retornou o jogo '{applicationName}' após sincronizar. Verifique o pareamento do Moonlight e tente novamente.",
            lastError);
    }

    private static async Task<string> GetAppListAsync(MoonlightPairedConnection connection, CancellationToken cancellationToken)
    {
        var toolDirectory = Path.Combine(AppContext.BaseDirectory, "nsp-generator");
        var scriptPath = Path.Combine(toolDirectory, "get-moonlight-applist.mjs");
        if (!File.Exists(scriptPath))
            throw new FileNotFoundException("O componente de conexão com o Moonlight não foi encontrado.", scriptPath);

        var temporaryDirectory = Path.Combine(Path.GetTempPath(), "MoonlightForwarderGenerator", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporaryDirectory);
        var requestPath = Path.Combine(temporaryDirectory, "moonlight-request.json");
        try
        {
            await File.WriteAllTextAsync(requestPath, JsonSerializer.Serialize(new
            {
                address = connection.Address,
                certificatePath = connection.ClientCertificatePath,
                privateKeyPath = connection.PrivateKeyPath
            }), cancellationToken);
            var startInfo = new ProcessStartInfo("node")
            {
                WorkingDirectory = toolDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add(scriptPath);
            startInfo.ArgumentList.Add(requestPath);
            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Não foi possível iniciar a conexão com o Moonlight.");
            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            var output = await outputTask;
            var error = await errorTask;
            if (process.ExitCode != 0)
                throw new InvalidOperationException($"Não foi possível consultar o Sunshine pelo Moonlight: {(string.IsNullOrWhiteSpace(error) ? output : error.Trim())}");
            return JsonDocument.Parse(output).RootElement.GetProperty("appList").GetString()
                ?? throw new InvalidOperationException("O Moonlight não retornou a lista de jogos.");
        }
        finally
        {
            if (Directory.Exists(temporaryDirectory))
                Directory.Delete(temporaryDirectory, true);
        }
    }
}
