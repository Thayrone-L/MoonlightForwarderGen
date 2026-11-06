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
                            ? LocalizedText.Get("Main.Status.CopiedOneAndUpdater")
                            : LocalizedText.Get("Main.Status.CopiedOne")
                        : copied > 1
                            ? LocalizedText.Format("Main.Status.CopiedMany", copied)
                            : DescribeCompanion(companion);
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
                StatusText.Text = LocalizedText.Format("Main.Status.InitialFailure", exception.Message);
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
        ReadinessText.Text = health.IsReady && switchSdCard.IsDetected
            ? LocalizedText.Get("Main.Readiness.Ready")
            : !health.IsReady
                ? LocalizedText.Get("Main.Readiness.ComputerPending")
                : LocalizedText.Get("Main.Readiness.SdPending");
    }

    private async void PrepareSunshine_Click(object sender, RoutedEventArgs e)
    {
        var health = _sunshineManager.Check();
        if (!health.IsInstalled)
        {
            var confirmation = MessageBox.Show(
                LocalizedText.Get("Main.Sunshine.Confirm"),
                LocalizedText.Get("Main.Sunshine.Title"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (confirmation != MessageBoxResult.Yes)
                return;
        }

        try
        {
            StatusText.Text = LocalizedText.Get("Main.Sunshine.Preparing");
            var result = await _sunshineManager.PrepareAsync();
            StatusText.Text = result.IsReady
                ? LocalizedText.Get("Main.Sunshine.Ready")
                : result.IsRunning ? LocalizedText.Get("Service.Sunshine.Starting") : LocalizedText.Get("Service.Sunshine.Stopped");
        }
        catch (Exception exception)
        {
            AppLog.Error("Falha ao preparar o Sunshine.", exception);
            StatusText.Text = LocalizedText.Format("Service.GenericFailure", AppLog.LogDirectory);
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
            Filter = LocalizedText.Get("Main.Executable.Filter")
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
            StatusText.Text = LocalizedText.Get("Main.Game.Preparing");
            var game = await _libraryManager.RegisterAsync(GameNameTextBox.Text, ExecutablePathTextBox.Text, _selectedCoverPath);
            if (game.Status != MFG.Common.Library.GameStatus.SunshineApplicationReady)
            {
                StatusText.Text = LocalizedText.Format("Main.Game.NeedsComputer", game.Name);
                return;
            }

            StatusText.Text = LocalizedText.Format("Main.Game.Creating", game.Name);
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
                ? LocalizedText.Format("Main.Game.ReadyOnSd", game.Name, result.Delivery.SdCardPath ?? string.Empty)
                : LocalizedText.Format("Main.Game.ReadyLocal", game.Name, result.Delivery.LocalPath);
        }
        catch (Exception exception)
        {
            if (exception is SunshineConfigurationAccessException)
            {
                var confirmation = MessageBox.Show(
                    LocalizedText.Get("Main.Sunshine.PermissionConfirm"),
                    LocalizedText.Get("Main.Sunshine.PermissionTitle"),
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                if (confirmation == MessageBoxResult.Yes)
                {
                    try
                    {
                        StatusText.Text = LocalizedText.Get("Main.Sunshine.RequestingPermission");
                        await _libraryManager.GrantSunshineConfigurationAccessAsync();
                        var game = await _libraryManager.RegisterAsync(GameNameTextBox.Text, ExecutablePathTextBox.Text, _selectedCoverPath);
                        StatusText.Text = LocalizedText.Format("Main.Sunshine.GamePrepared", game.Name);
                        return;
                    }
                    catch (Exception repairException)
                    {
                        AppLog.Error("Falha ao reparar permissao da configuracao do Sunshine.", repairException);
                        StatusText.Text = LocalizedText.Format("Service.GenericFailure", AppLog.LogDirectory);
                        return;
                    }
                }
            }
            AppLog.Error("Falha ao preparar jogo.", exception);
            StatusText.Text = exception is IOException
                ? LocalizedText.Format("Main.Game.CopyFailure", AppLog.LogDirectory)
                : LocalizedText.Format("Service.GenericFailure", AppLog.LogDirectory);
        }
    }

    private async void RemoveGame_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ExecutablePathTextBox.Text))
        {
            StatusText.Text = LocalizedText.Get("Main.Game.SelectToRemove");
            return;
        }
        if (MessageBox.Show(LocalizedText.Format("Main.Game.RemoveConfirm", GameNameTextBox.Text),
                LocalizedText.Get("Main.Game.RemoveTitle"), MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;
        try
        {
            await _libraryManager.RemoveAsync(ExecutablePathTextBox.Text.Trim());
            StatusText.Text = LocalizedText.Format("Main.Game.Removed", GameNameTextBox.Text);
            GameNameTextBox.Clear();
            ExecutablePathTextBox.Clear();
            await RefreshLibraryHealthAsync(false);
        }
        catch (Exception exception)
        {
            AppLog.Error("Falha ao remover jogo do MFG.", exception);
            StatusText.Text = LocalizedText.Format("Service.GenericFailure", AppLog.LogDirectory);
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
            StatusText.Text = LocalizedText.Get("Main.Switch.Updating");
            var companion = await _switchCompanionDelivery.EnsureInstalledAsync();
            if (!companion.SdDetected)
            {
                StatusText.Text = LocalizedText.Get("Service.Sd.NotConnected");
                RefreshReadiness();
                return;
            }

            var library = await _libraryManager.SyncSdLibraryAsync();
            StatusText.Text = companion.Copied
                ? LocalizedText.Format("Main.Switch.Updated", companion.DestinationPath ?? string.Empty, DescribeLibrarySync(library))
                : $"{DescribeCompanion(companion)} {DescribeLibrarySync(library)}";
            RefreshReadiness();
        }
        catch (Exception exception)
        {
            AppLog.Error("Falha ao atualizar o complemento do Switch.", exception);
            StatusText.Text = LocalizedText.Format("Main.Switch.UpdateFailure", exception.Message);
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
                StatusText.Text = LocalizedText.Get("Main.Search.Searching");
                SearchProgress.Visibility = Visibility.Visible;
                progress = new Progress<GameDiscoveryProgress>(update =>
                {
                    StatusText.Text = LocalizedText.Format("Main.Search.Progress", update.DirectoriesVisited, update.ExecutablesChecked, update.CandidatesFound);
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
                ? LocalizedText.Get("Main.Search.None")
                : LocalizedText.Format("Main.Search.Result", _installedGames.Count, result.Added, result.MetadataUpdated, names);
        }
        catch (Exception exception)
        {
            AppLog.Error("Falha durante a busca de jogos.", exception);
            if (showResult)
                StatusText.Text = LocalizedText.Format("Main.Search.Failure", exception.Message);
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
            ? LocalizedText.Format("Main.Selection.NeedsExecutable", game.Name)
            : LocalizedText.Format("Main.Selection.Ready", game.Name);
    }

    private void ShowShortcutCreatedState(string gameName, bool copiedToSdCard)
    {
        ShortcutCreatedTitle.Text = LocalizedText.Format("Main.Shortcut.Title", gameName);
        ShortcutCreatedDetail.Text = copiedToSdCard
            ? LocalizedText.Get("Main.Shortcut.OnSd")
            : LocalizedText.Get("Main.Shortcut.Local");
        ShortcutCreatedBanner.Visibility = Visibility.Visible;
        PrepareGameButton.Content = LocalizedText.Get("Main.CreateAgain");
    }

    private void HideShortcutCreatedState()
    {
        ShortcutCreatedBanner.Visibility = Visibility.Collapsed;
        PrepareGameButton.Content = LocalizedText.Get("Main.AddToSwitch");
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
                StatusText.Text = LocalizedText.Get("Main.Health.Empty");
                return;
            }

            StatusText.Text = string.Join(Environment.NewLine, checks.Select(check =>
                LocalizedText.Format("Main.Health.Line", check.Game.Name, Describe(check.Executable), Describe(check.Sunshine), Describe(check.MoonlightFavorite), Describe(check.Nsp), Describe(check.SdCard))));
        }
        catch (Exception exception)
        {
            AppLog.Error("Falha durante a verificacao da biblioteca.", exception);
            StatusText.Text = LocalizedText.Format("Main.Health.Failure", exception.Message);
        }
    }

    private static string Describe(MFG.Common.Library.HealthCheckItem item) => item.State switch
    {
        MFG.Common.Library.HealthCheckState.Ready => LocalizedText.Get("Health.Ready"),
        MFG.Common.Library.HealthCheckState.Unavailable => LocalizedText.Get("Health.Unavailable"),
        MFG.Common.Library.HealthCheckState.Invalid => LocalizedText.Get("Health.Invalid"),
        _ => LocalizedText.Get("Health.Pending")
    };

    private static string DescribeCompanion(SwitchCompanionDeliveryResult result) =>
        LocalizedText.Get(result.Copied ? "Service.Updater.Copied" : "Service.Updater.Current");

    private static string DescribeLibrarySync(SdLibrarySyncResult result) =>
        LocalizedText.Get(!result.SdDetected ? "Service.Sd.NotConnected" : result.Copied == 0 ? "Service.Sd.UpToDate" : "Service.Sd.Synchronized");

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
        public string Source => LocalizedText.Get($"Source.{PrimaryCandidate.Source}");
        public string? CoverPath => PrimaryCandidate.CoverPath;
    }
}
