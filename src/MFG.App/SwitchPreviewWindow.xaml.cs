using System.Collections.ObjectModel;
using System.Windows;
using MFG.App.Data;
using MFG.App.Services;

namespace MFG.App;

public partial class SwitchPreviewWindow : Window
{
    private readonly LibraryManager _library = new();
    public ObservableCollection<LibraryGame> Games { get; } = [];

    public SwitchPreviewWindow()
    {
        InitializeComponent();
        DataContext = this;
        Loaded += LoadGamesAsync;
    }

    private async void LoadGamesAsync(object sender, RoutedEventArgs e)
    {
        try
        {
            foreach (var game in await _library.GetSwitchPreviewGamesAsync())
                Games.Add(game);
            if (Games.Count > 0)
                PreviewGamesList.SelectedIndex = 0;
            else
                EmptyLibraryText.Visibility = Visibility.Visible;
        }
        catch (Exception exception)
        {
            EmptyLibraryText.Text = $"Não foi possível carregar a prévia: {exception.Message}";
            EmptyLibraryText.Visibility = Visibility.Visible;
        }
    }
}
