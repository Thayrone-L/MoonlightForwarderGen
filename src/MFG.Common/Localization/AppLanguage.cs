namespace MFG.Common.Localization;

public static class AppLanguage
{
    public const string Portuguese = "pt-BR";
    public const string English = "en";

    public static IReadOnlyList<string> Supported { get; } = [Portuguese, English];

    public static bool IsSupported(string? cultureName) =>
        Supported.Contains(cultureName, StringComparer.OrdinalIgnoreCase);

    public static string Normalize(string? cultureName) =>
        IsSupported(cultureName)
            ? Supported.First(item => string.Equals(item, cultureName, StringComparison.OrdinalIgnoreCase))
            : Portuguese;
}
