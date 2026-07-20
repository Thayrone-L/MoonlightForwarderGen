namespace MFG.Common.Nsp;

public sealed record NspGenerationResult(
    string OutputPath,
    string TitleId,
    long SizeBytes);
