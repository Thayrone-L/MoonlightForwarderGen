using System.Windows;
using MFG.App.Services;
using System.IO;
using System.Collections.ObjectModel;
using MFG.App.Data;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace MFG.App;

public partial class MainWindow : Window
{
    private readonly LibraryManager _libraryManager = new();
    private readonly SunshineManager _sunshineManager = new();
    private readonly MoonlightSwitchManager _moonlightSwitchManager = new();
    private readonly NspDeliveryService _nspDelivery = new();
    private readonly GameDiscoveryService _gameDiscovery = new();
    private readonly SetupStateStore _setupState = new();
    private readonly NspForwarderService _forwarder = new();
    private readonly LocalNetworkAddressProvider _networkAddress = new();
    private readonly NspGameIconProvider _nspGameIcon = new();
    private readonly SwitchCompanionDeliveryService _switchCompanionDelivery = new();
    private readonly ObservableCollection<DiscoveredGameGroup> _installedGames = new();
    private string? _selectedCoverPath;

    public MainWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => EnableDarkTitleBar();
        InstalledGamesList.ItemsSource = _installedGames;
        Loaded += async (_, _) =>
        {
            try
            {
                await _sunshineManager.RestartElevatedAsync(_libraryManager.ResetActiveSessionAsync);
                _moonlightSwitchManager.DisableDiagnostics();
                var companion = await _switchCompanionDelivery.EnsureInstalledAsync();
                var sync = await _libraryManager.SyncSdLibraryAsync();
                var copied = sync.Copied;
                if (copied > 0 || companion.Copied)
                    StatusText.Text = copied == 1
                        ? companion.Copied
                            ? "Um atalho e o complemento do Switch foram copiados para o cartão SD."
                            : "Um atalho foi copiado para o cartão SD do Switch."
                        : copied > 1
                            ? $"{copied} atalhos foram copiados para o cartão SD do Switch."
                            : companion.Message;
                await RefreshLibraryHealthAsync(copied == 0);
                await RefreshInstalledGamesAsync(false);
                if (!_setupState.IsCompleted())
                {
                    var onboarding = new OnboardingWindow { Owner = this };
                    onboarding.ShowDialog();
                }
            }
            catch (Exception exception)
            {
                AppLog.Error("Falha durante a preparação inicial do Companion.", exception);
                StatusText.Text = $"Nao foi possivel copiar os atalhos para o cartao SD: {exception.Message}";
            }
            finally
            {
                GamesLoadingOverlay.Visibility = Visibility.Collapsed;
                InstalledGamesList.IsEnabled = true;
                RefreshReadiness();
            }
        };
    }

    private void EnableDarkTitleBar()
    {
        var handle = new WindowInteropHelper(this).Handle;
        var enabled = 1;
        if (DwmSetWindowAttribute(handle, 20, ref enabled, sizeof(int)) != 0)
            DwmSetWindowAttribute(handle, 19, ref enabled, sizeof(int));
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        IntPtr windowHandle,
        int attribute,
        ref int attributeValue,
        int attributeSize);

    private void RefreshReadiness()
    {
        var health = _sunshineManager.Check();
        var switchSdCard = _moonlightSwitchManager.DetectSdCard();
        ReadinessText.Text = $"{health.Message} {switchSdCard.Message}";
    }

    private async void PrepareSunshine_Click(object sender, RoutedEventArgs e)
    {
        var health = _sunshineManager.Check();
        if (!health.IsInstalled)
        {
            var confirmation = MessageBox.Show(
                "O instalador oficial do Sunshine será baixado e aberto. Deseja continuar?",
                "Preparar Sunshine",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (confirmation != MessageBoxResult.Yes)
                return;
        }

        try
        {
            StatusText.Text = "Preparando seu computador...";
            var result = await _sunshineManager.PrepareAsync();
            StatusText.Text = result.IsReady
                ? "Seu computador está pronto para preparar jogos."
                : result.Message;
        }
        catch (Exception exception)
        {
            AppLog.Error("Falha ao preparar o Sunshine.", exception);
            StatusText.Text = exception.Message;
        }
        finally
        {
            RefreshReadiness();
        }
    }

    private void OpenSetup_Click(object sender, RoutedEventArgs e)
    {
        var onboarding = new OnboardingWindow { Owner = this };
        onboarding.ShowDialog();
        RefreshReadiness();
    }

    private void OpenSwitchPreview_Click(object sender, RoutedEventArgs e)
    {
        new SwitchPreviewWindow { Owner = this }.ShowDialog();
    }

    private void BrowseExecutable_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Executáveis (*.exe)|*.exe|Todos os arquivos (*.*)|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            ExecutablePathTextBox.Text = dialog.FileName;
            _selectedCoverPath = null;
            if (string.IsNullOrWhiteSpace(GameNameTextBox.Text))
                GameNameTextBox.Text = Path.GetFileNameWithoutExtension(dialog.FileName);
        }
    }

    private async void PrepareGame_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            HideShortcutCreatedState();
            StatusText.Text = "Preparando jogo...";
            var game = await _libraryManager.RegisterAsync(GameNameTextBox.Text, ExecutablePathTextBox.Text, _selectedCoverPath);
            if (game.Status != MFG.Common.Library.GameStatus.SunshineApplicationReady)
            {
                StatusText.Text = $"'{game.Name}' foi adicionado. Use “Preparar computador” para concluir.";
                return;
            }

            StatusText.Text = $"Criando o atalho de '{game.Name}'...";
            game = await _libraryManager.ResolveMoonlightApplicationIdAsync(game.Id);
            var iconPath = await _nspGameIcon.GetPathAsync(_selectedCoverPath, game.Name);
            var result = await _forwarder.GenerateAndDeliverAsync(
                game,
                _networkAddress.GetStreamingAddress(),
                _moonlightSwitchManager.GetMoonlightNroPath(),
                iconPath);
            await _libraryManager.SyncSdLibraryAsync();
            ShowShortcutCreatedState(game.Name, result.Delivery.CopiedToSdCard);
            StatusText.Text = result.Delivery.CopiedToSdCard
                ? $"'{game.Name}' está pronto. NSP copiado para: {result.Delivery.SdCardPath}"
                : $"'{game.Name}' está pronto. NSP salvo em: {result.Delivery.LocalPath}";
        }
        catch (Exception exception)
        {
            if (exception is SunshineConfigurationAccessException)
            {
                var confirmation = MessageBox.Show(
                    "O Windows bloqueou a atualizacao da lista de jogos do Sunshine. Deseja autorizar o MFG a conceder permissao de gravacao apenas na pasta de configuracao do Sunshine?",
                    "Permissao do Sunshine",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                if (confirmation == MessageBoxResult.Yes)
                {
                    try
                    {
                        StatusText.Text = "Solicitando permissao do Windows...";
                        await _libraryManager.GrantSunshineConfigurationAccessAsync();
                        var game = await _libraryManager.RegisterAsync(GameNameTextBox.Text, ExecutablePathTextBox.Text, _selectedCoverPath);
                        StatusText.Text = $"'{game.Name}' foi preparado para o Sunshine.";
                        return;
                    }
                    catch (Exception repairException)
                    {
                        AppLog.Error("Falha ao reparar permissao da configuracao do Sunshine.", repairException);
                        StatusText.Text = repairException.Message;
                        return;
                    }
                }
            }
            AppLog.Error("Falha ao preparar jogo.", exception);
            StatusText.Text = exception is IOException
                ? $"Não foi possível concluir a cópia agora. O MFG tentou automaticamente por alguns segundos; reconecte o cartão e tente novamente. Detalhes em: {AppLog.LogDirectory}"
                : exception.Message;
        }
    }

    private async void RemoveGame_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ExecutablePathTextBox.Text))
        {
            StatusText.Text = "Selecione um jogo para remover.";
            return;
        }
        if (MessageBox.Show($"Remover '{GameNameTextBox.Text}' do MFG? O NSP será apagado do computador e do cartão SD. Um futuro Instalador MFG também poderá desinstalar o atalho do Switch.",
                "Remover atalho", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;
        try
        {
            await _libraryManager.RemoveAsync(ExecutablePathTextBox.Text.Trim());
            StatusText.Text = $"'{GameNameTextBox.Text}' foi removido da biblioteca e do cartão SD.";
            GameNameTextBox.Clear();
            ExecutablePathTextBox.Clear();
            await RefreshLibraryHealthAsync(false);
        }
        catch (Exception exception)
        {
            AppLog.Error("Falha ao remover jogo do MFG.", exception);
            StatusText.Text = exception.Message;
        }
    }

    private async void RunHealthCheck_Click(object sender, RoutedEventArgs e)
    {
        await RefreshLibraryHealthAsync(true);
    }

    private async void RefreshInstalledGames_Click(object sender, RoutedEventArgs e)
    {
        await RefreshInstalledGamesAsync(true);
    }

    private async void UpdateSwitch_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            StatusText.Text = "Atualizando o complemento e a biblioteca do Switch...";
            var companion = await _switchCompanionDelivery.EnsureInstalledAsync();
            if (!companion.SdDetected)
            {
                StatusText.Text = companion.Message;
                RefreshReadiness();
                return;
            }

            var library = await _libraryManager.SyncSdLibraryAsync();
            StatusText.Text = companion.Copied
                ? $"Complemento atualizado em {companion.DestinationPath}. {library.Message} Feche e abra novamente o NRO no Switch."
                : $"{companion.Message} {library.Message}";
            RefreshReadiness();
        }
        catch (Exception exception)
        {
            AppLog.Error("Falha ao atualizar o complemento do Switch.", exception);
            StatusText.Text = $"Não foi possível atualizar o Switch: {exception.Message}";
        }
    }

    private void OpenLogs_Click(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(AppLog.LogDirectory);
        Process.Start(new ProcessStartInfo("explorer.exe", AppLog.LogDirectory) { UseShellExecute = true });
    }

    private async Task RefreshInstalledGamesAsync(bool showResult)
    {
        try
        {
            GamesLoadingOverlay.Visibility = Visibility.Visible;
            InstalledGamesList.IsEnabled = false;
            IProgress<GameDiscoveryProgress>? progress = null;
            if (showResult)
            {
                AppLog.Info("Busca completa de jogos solicitada.");
                StatusText.Text = "Procurando jogos instalados...";
                SearchProgress.Visibility = Visibility.Visible;
                progress = new Progress<GameDiscoveryProgress>(update =>
                {
                    StatusText.Text = $"Procurando jogos: {update.DirectoriesVisited} pastas, {update.ExecutablesChecked} executaveis verificados, {update.CandidatesFound} jogos candidatos.";
                });
            }
            var result = await _gameDiscovery.RefreshAsync(showResult, progress);
            AppLog.Info($"Busca de jogos concluida: {result.Found} encontrados, {result.Added} novos, {result.MetadataUpdated} metadados atualizados.");
            var games = await _gameDiscovery.GetInstalledGamesAsync();
            _installedGames.Clear();
            foreach (var group in games
                         .GroupBy(game => game.Name.Trim(), StringComparer.OrdinalIgnoreCase)
                         .OrderBy(group => group.Key)
                         .Select(group => new DiscoveredGameGroup(group.Key, group.ToList())))
                _installedGames.Add(group);
            if (_installedGames.Count > 0)
                InstalledGamesList.SelectedIndex = 0;

            // A descoberta pode encontrar um nome comercial e uma capa melhores
            // para atalhos já existentes; reflita isso no SD na mesma execução.
            await _libraryManager.SyncSdLibraryAsync();

            if (!showResult)
                return;

            var names = string.Join(", ", _installedGames.Take(5).Select(game => game.Name));
            StatusText.Text = result.Found == 0
                ? "Nenhum jogo Steam ou Epic instalado foi encontrado."
                : $"Encontramos {_installedGames.Count} jogos instalados ({result.Added} novos, {result.MetadataUpdated} com metadados atualizados). {names}";
        }
        catch (Exception exception)
        {
            AppLog.Error("Falha durante a busca de jogos.", exception);
            if (showResult)
                StatusText.Text = $"Nao foi possivel procurar jogos instalados: {exception.Message}";
        }
        finally
        {
            GamesLoadingOverlay.Visibility = Visibility.Collapsed;
            InstalledGamesList.IsEnabled = true;
            SearchProgress.Visibility = Visibility.Collapsed;
            SearchProgress.IsIndeterminate = true;
        }
    }

    private void InstalledGamesList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (InstalledGamesList.SelectedItem is not DiscoveredGameGroup group)
            return;

        PopulateGameFields(group.PrimaryCandidate);
    }

    private void AlternativeExecutable_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count > 0 && e.AddedItems[0] is DiscoveredGame game)
            PopulateGameFields(game);
    }

    private void PopulateGameFields(DiscoveredGame game)
    {
        HideShortcutCreatedState();
        GameNameTextBox.Text = game.Name;
        ExecutablePathTextBox.Text = game.ExecutablePath ?? string.Empty;
        _selectedCoverPath = game.CoverPath;
        StatusText.Text = string.IsNullOrWhiteSpace(game.ExecutablePath)
            ? $"{game.Name} foi selecionado. Escolha o executavel do jogo para continuar."
            : $"{game.Name} foi selecionado e esta pronto para ser preparado.";
    }

    private void ShowShortcutCreatedState(string gameName, bool copiedToSdCard)
    {
        ShortcutCreatedTitle.Text = $"Atalho de {gameName} criado";
        ShortcutCreatedDetail.Text = copiedToSdCard
            ? "Já está no cartão SD e pronto para ser adicionado pelo Switch."
            : "Foi salvo no computador. Conecte o cartão SD e clique em Atualizar.";
        ShortcutCreatedBanner.Visibility = Visibility.Visible;
        PrepareGameButton.Content = "Criar novamente";
    }

    private void HideShortcutCreatedState()
    {
        ShortcutCreatedBanner.Visibility = Visibility.Collapsed;
        PrepareGameButton.Content = "Adicionar ao Switch";
    }

    private async Task RefreshLibraryHealthAsync(bool showResult)
    {
        try
        {
            var checks = await _libraryManager.RunHealthCheckAsync();
            if (!showResult)
                return;
            if (checks.Count == 0)
            {
                StatusText.Text = "Nenhum jogo foi adicionado a biblioteca ainda.";
                return;
            }

            StatusText.Text = string.Join(Environment.NewLine, checks.Select(check =>
                $"{check.Game.Name}: executavel {Describe(check.Executable)}, Sunshine {Describe(check.Sunshine)}, Moonlight {Describe(check.MoonlightFavorite)}, NSP {Describe(check.Nsp)}, SD {Describe(check.SdCard)}."));
        }
        catch (Exception exception)
        {
            AppLog.Error("Falha durante a verificacao da biblioteca.", exception);
            StatusText.Text = $"Nao foi possivel verificar os jogos: {exception.Message}";
        }
    }

    private static string Describe(MFG.Common.Library.HealthCheckItem item) => item.State switch
    {
        MFG.Common.Library.HealthCheckState.Ready => "pronto",
        MFG.Common.Library.HealthCheckState.Unavailable => "indisponivel",
        MFG.Common.Library.HealthCheckState.Invalid => "invalido",
        _ => "pendente"
    };

    private sealed class DiscoveredGameGroup
    {
        public DiscoveredGameGroup(string name, IReadOnlyList<DiscoveredGame> candidates)
        {
            Name = name;
            Candidates = candidates.OrderByDescending(candidate => candidate.Source == MFG.Common.Library.GameSource.Steam)
                .ThenBy(candidate => candidate.ExecutablePath).ToList();
            PrimaryCandidate = Candidates[0];
        }

        public string Name { get; }
        public IReadOnlyList<DiscoveredGame> Candidates { get; }
        public DiscoveredGame PrimaryCandidate { get; }
        public bool HasAlternatives => Candidates.Count > 1;
        public string Source => PrimaryCandidate.Source.ToString();
        public string? CoverPath => PrimaryCandidate.CoverPath;
    }
}
