namespace MFG.Common.Contracts;

public sealed record AgentResponse(bool Success, string Message, SunshineStatus? Sunshine = null);
