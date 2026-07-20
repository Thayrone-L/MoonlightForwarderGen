using System.Windows;
using MFG.App.Services;

namespace MFG.App;

public partial class OnboardingWindow : Window
{
    private readonly SecureKeyStorage _keyStorage = new();
    private readonly SunshineManager _sunshine = new();
    private readonly MoonlightSwitchManager _switch = new();
    private readonly SetupStateStore _setupState = new();
    private int _step;
    private bool _computerReady;

    public OnboardingWindow()
    {
        InitializeComponent();
        _computerReady = _sunshine.Check().IsReady;
        RefreshStep();
    }

    private void RefreshStep()
    {
        ComputerStep.Visibility = _step == 0 ? Visibility.Visible : Visibility.Collapsed;
        KeyStep.Visibility = _step == 1 ? Visibility.Visible : Visibility.Collapsed;
        PairingStep.Visibility = _step == 2 ? Visibility.Visible : Visibility.Collapsed;
        FinishStep.Visibility = _step == 3 ? Visibility.Visible : Visibility.Collapsed;
        StepTitleText.Text = _step switch { 0 => "Preparar computador", 1 => "Arquivo de chave", 2 => "Conectar Switch", _ => "Tudo pronto" };
        StepIndicatorText.Text = $"Passo {_step + 1} de 4";
        BackButton.Visibility = _step == 0 ? Visibility.Collapsed : Visibility.Visible;
        NextButton.Content = _step == 3 ? "Concluir" : "Continuar";
        NextButton.IsEnabled = _step switch { 0 => _computerReady, 1 => _keyStorage.IsConfigured, _ => true };
        KeyStatusText.Text = _keyStorage.IsConfigured ? "Arquivo guardado com segurança neste computador." : "Selecione o arquivo para continuar.";
        SwitchStatusText.Text = _switch.DetectSdCard().Message;
    }

    private async void PrepareComputer_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            PrepareComputerButton.IsEnabled = false;
            ComputerStatusText.Text = "Preparando o Sunshine...";
            var health = await _sunshine.PrepareAsync();
            _computerReady = health.IsReady;
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
        if (_step == 3) { await _setupState.MarkCompletedAsync(); DialogResult = true; return; }
        _step++; RefreshStep();
    }

    private void Back_Click(object sender, RoutedEventArgs e) { _step--; RefreshStep(); }
}
