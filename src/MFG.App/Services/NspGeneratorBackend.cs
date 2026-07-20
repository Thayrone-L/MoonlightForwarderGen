using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using MFG.Common.Nsp;

namespace MFG.App.Services;

public sealed class NspGeneratorBackend
{
    private readonly SecureKeyStorage _keyStorage = new();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public async Task<NspGenerationResult> GenerateAsync(NspGenerationPlan plan, CancellationToken cancellationToken = default)
    {
        AppLog.Info($"Geracao de NSP iniciada para jogo {plan.Request.GameId}, Title ID {plan.Request.TitleId}.");
        var toolDirectory = Path.Combine(AppContext.BaseDirectory, "nsp-generator");
        var scriptPath = Path.Combine(toolDirectory, "generate-nsp.mjs");
        if (!File.Exists(scriptPath))
            throw new FileNotFoundException("O componente local de criacao de atalhos nao foi encontrado.", scriptPath);

        var key = await _keyStorage.ReadAsync();
        var temporaryDirectory = Path.Combine(Path.GetTempPath(), "MoonlightForwarderGenerator", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporaryDirectory);
        var keyPath = Path.Combine(temporaryDirectory, "prod.keys");
        var requestPath = Path.Combine(temporaryDirectory, "request.json");

        try
        {
            await File.WriteAllBytesAsync(keyPath, key, cancellationToken);
            var request = new GeneratorRequest(plan.Request, plan.MoonlightArguments, keyPath);
            await File.WriteAllTextAsync(requestPath, JsonSerializer.Serialize(request, JsonOptions), cancellationToken);

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
                ?? throw new InvalidOperationException("Nao foi possivel iniciar o gerador de atalhos.");
            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            var output = await outputTask;
            var error = await errorTask;
            if (process.ExitCode != 0)
                throw new InvalidOperationException(string.IsNullOrWhiteSpace(error) ? output : error.Trim());

            var response = JsonSerializer.Deserialize<GeneratorResponse>(output, JsonOptions)
                ?? throw new InvalidOperationException("O gerador nao retornou uma resposta valida.");
            if (!File.Exists(response.OutputPath))
                throw new InvalidOperationException("O atalho nao foi criado.");

            var result = new NspGenerationResult(response.OutputPath, response.TitleId, new FileInfo(response.OutputPath).Length);
            AppLog.Info($"Geracao de NSP concluida para Title ID {result.TitleId}, tamanho {result.SizeBytes} bytes.");
            return result;
        }
        catch (Exception exception)
        {
            AppLog.Error($"Falha ao gerar NSP para Title ID {plan.Request.TitleId}.", exception);
            throw;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
            if (Directory.Exists(temporaryDirectory))
                Directory.Delete(temporaryDirectory, true);
        }
    }

    private sealed record GeneratorRequest(NspGenerationRequest Request, IReadOnlyList<string> MoonlightArguments, string KeyPath);
    private sealed record GeneratorResponse(string OutputPath, string TitleId);
}
