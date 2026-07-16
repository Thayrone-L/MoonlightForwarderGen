using System.Windows;
using MFG.Common.Communication;
using MFG.Common.Contracts;

namespace MFG.App;

public partial class MainWindow : Window
{
    private readonly AgentClient _agentClient = new();

    public MainWindow()
    {
        InitializeComponent();
        Loaded += async (_, _) => await RefreshSunshineStatusAsync();
    }

    private async Task RefreshSunshineStatusAsync()
    {
        try
        {
            var response = await _agentClient.SendAsync(new AgentCommand("get-sunshine-status"));
            var sunshine = response.Sunshine;
            SunshineStatusText.Text = sunshine?.IsReady == true
                ? $"Sunshine está pronto para conexão local: {sunshine.ExecutablePath}"
                : sunshine?.IsInstalled == true
                ? $"{sunshine.RuntimeMessage} Configure-o em {sunshine.WebUiUrl ?? "a interface web local"}."
                : "Sunshine não foi encontrado. Instale-o para preparar o streaming de jogos.";
            InstallSunshineButton.Visibility = sunshine?.IsInstalled == true
                ? Visibility.Collapsed
                : Visibility.Visible;
        }
        catch (Exception)
        {
            SunshineStatusText.Text = "Não foi possível consultar o Sunshine. Inicie o MFG.Agent e tente novamente.";
            InstallSunshineButton.Visibility = Visibility.Collapsed;
        }
    }

    private async void InstallSunshine_Click(object sender, RoutedEventArgs e)
    {
        var confirmation = MessageBox.Show(
            "O instalador oficial mais recente do Sunshine será baixado e aberto. Deseja continuar?",
            "Instalar Sunshine",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirmation != MessageBoxResult.Yes)
            return;

        try
        {
            InstallSunshineButton.IsEnabled = false;
            StatusText.Text = "Baixando e iniciando o instalador do Sunshine...";
            var response = await _agentClient.SendAsync(new AgentCommand("install-sunshine"));
            StatusText.Text = response.Message;
            await RefreshSunshineStatusAsync();
        }
        catch (Exception exception)
        {
            StatusText.Text = $"Não foi possível instalar o Sunshine: {exception.Message}";
        }
        finally
        {
            InstallSunshineButton.IsEnabled = true;
        }
    }

    private void BrowseExecutable_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Executáveis (*.exe)|*.exe|Todos os arquivos (*.*)|*.*"
        };

        if (dialog.ShowDialog() == true)
            ExecutablePathTextBox.Text = dialog.FileName;
    }

    private async void SaveGame_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            StatusText.Text = "Salvando jogo...";
            var response = await _agentClient.SendAsync(new AgentCommand(
                "register-game",
                RegisterGameIdTextBox.Text,
                ExecutablePathTextBox.Text));
            StatusText.Text = response.Message;
        }
        catch (TimeoutException)
        {
            StatusText.Text = "Não foi possível conectar ao Agent. Inicie o MFG.Agent e tente novamente.";
        }
        catch (Exception exception)
        {
            StatusText.Text = $"Erro de comunicação: {exception.Message}";
        }
    }

    private async void LaunchGame_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            StatusText.Text = "Solicitando inicialização do jogo...";
            var response = await _agentClient.SendAsync(new AgentCommand("launch-game", LaunchGameIdTextBox.Text));
            StatusText.Text = response.Message;
        }
        catch (TimeoutException)
        {
            StatusText.Text = "Não foi possível conectar ao Agent. Inicie o MFG.Agent e tente novamente.";
        }
        catch (Exception exception)
        {
            StatusText.Text = $"Erro de comunicação: {exception.Message}";
        }
    }
}
