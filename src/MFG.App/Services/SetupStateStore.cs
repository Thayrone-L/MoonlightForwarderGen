using System.Text.Json;
using System.IO;
using MFG.Common.Setup;

namespace MFG.App.Services;

public sealed class SetupStateStore
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MoonlightForwarderGenerator", "setup.json");

    public bool IsCompleted() => Load().Completed;

    public SetupProgress Load()
    {
        try
        {
            if (!File.Exists(FilePath))
                return new SetupProgress(false, OnboardingStep.Computer);

            var state = JsonSerializer.Deserialize<SetupState>(File.ReadAllText(FilePath));
            return new SetupProgress(state?.Completed == true, state?.Step ?? OnboardingStep.Computer);
        }
        catch (Exception exception)
        {
            AppLog.Error("Nao foi possivel ler o estado da configuracao inicial.", exception);
            return new SetupProgress(false, OnboardingStep.Computer);
        }
    }

    public Task SaveProgressAsync(OnboardingStep step) => WriteAsync(new SetupState(false, step));

    public async Task MarkCompletedAsync()
    {
        await WriteAsync(new SetupState(true, OnboardingStep.Finish));
    }

    private static async Task WriteAsync(SetupState state)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        var temporaryPath = $"{FilePath}.{Guid.NewGuid():N}.tmp";
        await File.WriteAllTextAsync(temporaryPath, JsonSerializer.Serialize(state));
        File.Move(temporaryPath, FilePath, true);
    }

    private sealed record SetupState(bool Completed, OnboardingStep Step = OnboardingStep.Computer);
}

public sealed record SetupProgress(bool Completed, OnboardingStep Step);
