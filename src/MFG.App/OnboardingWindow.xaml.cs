using System.IO;
using System.Windows;
using MFG.App.Services;
using MFG.Common.Setup;

namespace MFG.App;

public partial class OnboardingWindow : Window
{
    private readonly SecureKeyStorage _keyStorage = new();
    private readonly SunshineManager _sunshine = new();
    private readonly MoonlightSwitchManager _switch = new();
    private readonly SetupStateStore _setupState = new();
    private readonly OnboardingFlow _flow;

    public OnboardingWindow()
    {
        InitializeComponent();
        var saved = _setupState.Load();
        var switchReady = IsSwitchReady(_switch.DetectSdCard());
        _flow = new OnboardingFlow(_sunshine.Check().IsReady, _keyStorage.IsConfigured, switchReady, saved.Step);
        RefreshStep();
    }

    private void RefreshStep()
    {
        var sdCard = _switch.DetectSdCard();
        _flow.UpdateReadiness(_sunshine.Check().IsReady, _keyStorage.IsConfigured, IsSwitchReady(sdCard));
        ComputerStep.Visibility = _flow.Step == OnboardingStep.Computer ? Visibility.Visible : Visibility.Collapsed;
        KeyStep.Visibility = _flow.Step == OnboardingStep.Key ? Visibility.Visible : Visibility.Collapsed;
        PairingStep.Visibility = _flow.Step == OnboardingStep.Switch ? Visibility.Visible : Visibility.Collapsed;
        FinishStep.Visibility = _flow.Step == OnboardingStep.Finish ? Visibility.Visible : Visibility.Collapsed;
        StepTitleText.Text = _flow.Step switch { OnboardingStep.Computer => "Preparar computador", OnboardingStep.Key => "Arquivo de chave", OnboardingStep.Switch => "Conectar Switch", _ => "Tudo pronto" };
        StepIndicatorText.Text = $"Passo {(int)_flow.Step + 1} de 4";
        BackButton.Visibility = _flow.CanGoBack ? Visibility.Visible : Visibility.Collapsed;
        LaterButton.Visibility = _flow.Step == OnboardingStep.Finish ? Visibility.Collapsed : Visibility.Visible;
        NextButton.Content = _flow.Step == OnboardingStep.Finish ? "Concluir" : "Continuar";
        NextButton.IsEnabled = _flow.CanContinue;
        KeyStatusText.Text = _keyStorage.IsConfigured ? "Arquivo guardado com segurança neste computador." : "Selecione o arquivo para continuar.";
        SwitchStatusText.Text = sdCard.IsDetected && !IsSwitchReady(sdCard)
            ? "Cartão SD encontrado, mas o Moonlight-Switch ainda não foi configurado e pareado. Conclua as etapas no console e tente novamente."
            : sdCard.Message;
    }

    private async void PrepareComputer_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            PrepareComputerButton.IsEnabled = false;
            ComputerStatusText.Text = "Preparando o Sunshine...";
            var health = await _sunshine.PrepareAsync();
            ComputerStatusText.Text = health.IsReady ? "Computador pronto." : health.Message;
        }
        catch (Exception exception)
        {
            AppLog.Error("Falha na configuracao rapida do Sunshine.", exception);
            ComputerStatusText.Text = exception.Message;
        }
        finally
        {
            PrepareComputerButton.IsEnabled = true;
            RefreshStep();
        }
    }

    private async void SelectKey_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "Arquivos de chave (*.keys)|*.keys|Todos os arquivos (*.*)|*.*" };
        if (dialog.ShowDialog() != true) return;
        try { KeyStatusText.Text = "Guardando arquivo com segurança..."; await _keyStorage.ImportAsync(dialog.FileName); }
        catch (Exception exception)
        {
            AppLog.Error("Falha ao guardar a chave do gerador.", exception);
            KeyStatusText.Text = $"Não foi possível guardar o arquivo: {exception.Message}";
        }
        finally { RefreshStep(); }
    }

    private void CheckSwitch_Click(object sender, RoutedEventArgs e) => RefreshStep();

    private async void Next_Click(object sender, RoutedEventArgs e)
    {
        if (_flow.Step == OnboardingStep.Finish) { await _setupState.MarkCompletedAsync(); DialogResult = true; return; }
        if (_flow.MoveNext()) await _setupState.SaveProgressAsync(_flow.Step);
        RefreshStep();
    }

    private async void Back_Click(object sender, RoutedEventArgs e)
    {
        if (_flow.MoveBack()) await _setupState.SaveProgressAsync(_flow.Step);
        RefreshStep();
    }

    private async void Later_Click(object sender, RoutedEventArgs e)
    {
        await _setupState.SaveProgressAsync(_flow.Step);
        DialogResult = false;
    }

    private static bool IsSwitchReady(SwitchSdCardStatus status) =>
        status.IsDetected && !string.IsNullOrWhiteSpace(status.SettingsPath) && File.Exists(status.SettingsPath);
}
