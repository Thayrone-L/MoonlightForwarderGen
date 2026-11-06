using System.Globalization;
using System.Windows;
using System.Windows.Media;
using MFG.App.Services;
using MFG.Common.Localization;

namespace MFG.App;

public partial class LanguageSelectionWindow : Window
{
    private string _selectedLanguage;

    public LanguageSelectionWindow()
    {
        InitializeComponent();
        _selectedLanguage = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "en"
            ? AppLanguage.English
            : AppLanguage.Portuguese;
        RefreshSelection();
    }

    private void Portuguese_Click(object sender, RoutedEventArgs e)
    {
        _selectedLanguage = AppLanguage.Portuguese;
        RefreshSelection();
    }

    private void English_Click(object sender, RoutedEventArgs e)
    {
        _selectedLanguage = AppLanguage.English;
        RefreshSelection();
    }

    private async void Continue_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await new LanguageSettingsStore().SaveAsync(_selectedLanguage);
            LanguageSettingsStore.Apply(_selectedLanguage);
            DialogResult = true;
        }
        catch (Exception exception)
        {
            AppLog.Error("Nao foi possivel salvar o idioma.", exception);
            MessageBox.Show(
                "The language could not be saved. / Não foi possível salvar o idioma.",
                "MFG", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void RefreshSelection()
    {
        var accent = (Brush)FindResource("AccentBrush");
        var normal = (Brush)FindResource("BorderBrush");
        PortugueseCard.BorderBrush = _selectedLanguage == AppLanguage.Portuguese ? accent : normal;
        EnglishCard.BorderBrush = _selectedLanguage == AppLanguage.English ? accent : normal;
    }
}
