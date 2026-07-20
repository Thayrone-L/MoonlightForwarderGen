using System.IO;
using System.Text;

namespace MFG.App.Services;

public static class AppLog
{
    private static readonly object Sync = new();
    private static readonly string DirectoryPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MoonlightForwarderGenerator",
        "logs");

    public static string LogDirectory => DirectoryPath;

    public static void Initialize()
    {
        Directory.CreateDirectory(DirectoryPath);
        foreach (var file in Directory.EnumerateFiles(DirectoryPath, "mfg-*.log"))
        {
            if (File.GetLastWriteTimeUtc(file) < DateTime.UtcNow.AddDays(-30))
                File.Delete(file);
        }
        Info("Aplicativo iniciado.");
    }

    public static void Info(string message) => Write("INFO", message, null);
    public static void Warning(string message) => Write("WARN", message, null);
    public static void Warning(string message, Exception exception) => Write("WARN", message, exception);
    public static void Error(string message, Exception exception) => Write("ERROR", message, exception);

    private static void Write(string level, string message, Exception? exception)
    {
        try
        {
            Directory.CreateDirectory(DirectoryPath);
            var path = Path.Combine(DirectoryPath, $"mfg-{DateTime.UtcNow:yyyy-MM-dd}.log");
            var text = new StringBuilder()
                .Append(DateTimeOffset.Now.ToString("O"))
                .Append(" [").Append(level).Append("] ")
                .Append(message);
            if (exception is not null)
                text.AppendLine().Append(exception);
            text.AppendLine();
            lock (Sync)
                File.AppendAllText(path, text.ToString(), Encoding.UTF8);
        }
        catch
        {
            // Logging must never interrupt the Companion.
        }
    }
}
