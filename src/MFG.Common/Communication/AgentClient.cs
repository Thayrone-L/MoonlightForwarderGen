using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using MFG.Common.Contracts;

namespace MFG.Common.Communication;

public sealed class AgentClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<AgentResponse> SendAsync(AgentCommand command, CancellationToken cancellationToken = default)
    {
        using var pipe = new NamedPipeClientStream(".", AgentPipe.Name, PipeDirection.InOut, PipeOptions.Asynchronous);
        await pipe.ConnectAsync(2_000, cancellationToken);

        using var reader = new StreamReader(pipe, Encoding.UTF8, leaveOpen: true);
        using var writer = new StreamWriter(pipe, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };

        await writer.WriteLineAsync(JsonSerializer.Serialize(command, JsonOptions));
        var responseJson = await reader.ReadLineAsync(cancellationToken);

        return responseJson is null
            ? new AgentResponse(false, "O Agent não retornou uma resposta válida.")
            : JsonSerializer.Deserialize<AgentResponse>(responseJson, JsonOptions)
              ?? new AgentResponse(false, "O Agent não retornou uma resposta válida.");
    }
}
