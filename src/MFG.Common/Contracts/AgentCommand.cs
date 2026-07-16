namespace MFG.Common.Contracts;

public sealed record AgentCommand(
    string Name,
    string? GameId = null,
    string? ExecutablePath = null,
    string? Arguments = null);
