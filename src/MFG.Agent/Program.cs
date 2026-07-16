using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using MFG.Agent.Data;
using MFG.Agent.Services;
using MFG.Common.Communication;
using MFG.Common.Contracts;

var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
await MfgDbContext.InitializeAsync();
var sunshine = await SunshineInstallationService.RefreshAsync();
var sunshineRuntime = await SunshineInstallationService.EnsureRunningAsync(sunshine);

Console.WriteLine($"MFG.Agent em execução. Pipe: {AgentPipe.Name}");
Console.WriteLine(sunshine.IsInstalled
    ? $"Sunshine encontrado: {sunshine.ExecutablePath}"
    : "Sunshine não foi encontrado. O status foi registrado no banco local.");
Console.WriteLine(sunshineRuntime.Message);

while (true)
{
    await using var pipe = new NamedPipeServerStream(AgentPipe.Name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
    await pipe.WaitForConnectionAsync();

    using var reader = new StreamReader(pipe, Encoding.UTF8, leaveOpen: true);
    using var writer = new StreamWriter(pipe, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };

    var commandJson = await reader.ReadLineAsync();
    var command = commandJson is null ? null : JsonSerializer.Deserialize<AgentCommand>(commandJson, jsonOptions);
    var response = command switch
    {
        { Name: "launch-notepad" } => LaunchNotepad(),
        { Name: "register-game", GameId: not null, ExecutablePath: not null } => await RegisterGameAsync(command),
        { Name: "launch-game", GameId: not null } => await LaunchGameAsync(command.GameId),
        { Name: "get-sunshine-status" } => GetSunshineStatus(await SunshineInstallationService.RefreshAsync()),
        { Name: "install-sunshine" } => await InstallSunshineAsync(),
        null => new AgentResponse(false, "Comando inválido."),
        _ => new AgentResponse(false, $"Comando desconhecido: {command.Name}.")
    };

    await writer.WriteLineAsync(JsonSerializer.Serialize(response, jsonOptions));
}

static AgentResponse GetSunshineStatus(SunshineInstallation installation)
{
    var runtime = SunshineInstallationService.GetRuntimeStatus(installation);
    return new AgentResponse(
    true,
    runtime.Message,
    new SunshineStatus(
        installation.IsInstalled,
        installation.ExecutablePath,
        installation.LastCheckedAt,
        runtime.IsRunning,
        runtime.IsReady,
        runtime.Message,
        runtime.WebUiUrl));
}

static async Task<AgentResponse> InstallSunshineAsync()
{
    var result = await SunshineInstallerService.InstallAsync();
    return new AgentResponse(
        result.Success,
        result.Message,
        result.Installation is null ? null : CreateSunshineStatus(result.Installation));
}

static SunshineStatus CreateSunshineStatus(SunshineInstallation installation)
{
    var runtime = SunshineInstallationService.GetRuntimeStatus(installation);
    return new SunshineStatus(installation.IsInstalled, installation.ExecutablePath, installation.LastCheckedAt, runtime.IsRunning, runtime.IsReady, runtime.Message, runtime.WebUiUrl);
}

static async Task<AgentResponse> RegisterGameAsync(AgentCommand command)
{
    var gameId = command.GameId!.Trim();
    var executablePath = command.ExecutablePath!.Trim();

    if (string.IsNullOrWhiteSpace(gameId) || string.IsNullOrWhiteSpace(executablePath))
        return new AgentResponse(false, "Informe o ID e o caminho do executável.");

    using var database = MfgDbContext.Create();
    var game = await database.Games.FindAsync(gameId);
    var now = DateTimeOffset.UtcNow;

    if (game is null)
    {
        game = new GameEntry { Id = gameId, CreatedAt = now };
        database.Games.Add(game);
    }

    game.ExecutablePath = executablePath;
    game.Arguments = string.IsNullOrWhiteSpace(command.Arguments) ? null : command.Arguments.Trim();
    game.UpdatedAt = now;
    await database.SaveChangesAsync();

    Console.WriteLine($"Jogo registrado: {gameId} -> {executablePath}");
    var sunshine = await SunshineInstallationService.RefreshAsync();
    var sunshineApplication = await SunshineApplicationService.UpsertGameAsync(sunshine, gameId);
    Console.WriteLine(sunshineApplication.Message);
    return new AgentResponse(
        sunshineApplication.Success,
        sunshineApplication.Success
            ? $"Jogo '{gameId}' salvo e registrado no Sunshine."
            : sunshineApplication.Message);
}

static async Task<AgentResponse> LaunchGameAsync(string gameId)
{
    using var database = MfgDbContext.Create();
    var game = await database.Games.FindAsync(gameId.Trim());

    if (game is null)
        return new AgentResponse(false, $"Nenhum jogo foi encontrado para o ID '{gameId}'.");

    if (!File.Exists(game.ExecutablePath))
        return new AgentResponse(false, $"O executável configurado não foi encontrado: {game.ExecutablePath}");

    try
    {
        var startInfo = new ProcessStartInfo(game.ExecutablePath)
        {
            Arguments = game.Arguments ?? string.Empty,
            WorkingDirectory = Path.GetDirectoryName(game.ExecutablePath) ?? Environment.CurrentDirectory,
            UseShellExecute = true
        };
        Process.Start(startInfo);
        Console.WriteLine($"Jogo iniciado: {game.Id}");
        return new AgentResponse(true, $"Jogo '{game.Id}' iniciado.");
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine(exception);
        return new AgentResponse(false, $"Não foi possível iniciar o jogo: {exception.Message}");
    }
}

static AgentResponse LaunchNotepad()
{
    try
    {
        Process.Start(new ProcessStartInfo("notepad.exe") { UseShellExecute = true });
        Console.WriteLine("Bloco de Notas iniciado.");
        return new AgentResponse(true, "Bloco de Notas iniciado pelo Agent.");
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine(exception);
        return new AgentResponse(false, $"Não foi possível iniciar o Bloco de Notas: {exception.Message}");
    }
}
