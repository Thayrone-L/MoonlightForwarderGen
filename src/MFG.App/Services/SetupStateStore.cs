using System.Text.Json;
using System.IO;

namespace MFG.App.Services;

public sealed class SetupStateStore
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MoonlightForwarderGenerator", "setup.json");

    public bool IsCompleted()
    {
        try
        {
            return File.Exists(FilePath) &&
                JsonSerializer.Deserialize<SetupState>(File.ReadAllText(FilePath))?.Completed == true;
        }
        catch (Exception exception)
        {
            AppLog.Error("Nao foi possivel ler o estado da configuracao inicial.", exception);
            return false;
        }
    }

    public async Task MarkCompletedAsync()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        var temporaryPath = FilePath + ".tmp";
        await File.WriteAllTextAsync(temporaryPath, JsonSerializer.Serialize(new SetupState(true)));
        File.Move(temporaryPath, FilePath, true);
    }

    private sealed record SetupState(bool Completed);
}
