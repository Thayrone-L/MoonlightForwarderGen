namespace MFG.Common.Nsp;

public sealed record NspGenerationPlan(
    NspGenerationRequest Request,
    IReadOnlyList<string> MoonlightArguments);
