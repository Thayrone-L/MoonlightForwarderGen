using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;
using MFG.Common.Localization;

namespace MFG.App.Services;

public sealed class LanguageSettingsStore
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MoonlightForwarderGenerator", "language.json");

    public string? Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return null;
            var settings = JsonSerializer.Deserialize<LanguageSettings>(File.ReadAllText(FilePath));
            return AppLanguage.IsSupported(settings?.CultureName) ? AppLanguage.Normalize(settings!.CultureName) : null;
        }
        catch (Exception exception)
        {
            AppLog.Error("Nao foi possivel ler o idioma configurado.", exception);
            return null;
        }
    }

    public async Task SaveAsync(string cultureName)
    {
        var normalized = AppLanguage.Normalize(cultureName);
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        var temporaryPath = $"{FilePath}.{Guid.NewGuid():N}.tmp";
        await File.WriteAllTextAsync(temporaryPath, JsonSerializer.Serialize(new LanguageSettings(normalized)));
        File.Move(temporaryPath, FilePath, true);
    }

    public static void Apply(string cultureName)
    {
        var normalized = AppLanguage.Normalize(cultureName);
        var culture = CultureInfo.GetCultureInfo(normalized);
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;

        var dictionaries = Application.Current.Resources.MergedDictionaries;
        var current = dictionaries.FirstOrDefault(item =>
            item.Source?.OriginalString.Contains("Localization/Strings.", StringComparison.OrdinalIgnoreCase) == true);
        if (current is not null) dictionaries.Remove(current);
        dictionaries.Add(new ResourceDictionary
        {
            Source = new Uri($"Localization/Strings.{normalized}.xaml", UriKind.Relative)
        });
    }

    private sealed record LanguageSettings(string CultureName);
}

public static class LocalizedText
{
    public static string Get(string key) =>
        Application.Current.TryFindResource(key) as string ?? key;

    public static string Format(string key, params object[] arguments) =>
        string.Format(CultureInfo.CurrentCulture, Get(key), arguments);
}
