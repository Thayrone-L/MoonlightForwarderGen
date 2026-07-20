using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MFG.App.Converters;

public sealed class CoverPathToImageSourceConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var fallbackPath = Path.Combine(AppContext.BaseDirectory, "assets", "mfg-default-nsp.jpg");
        if (value is string coverPath && File.Exists(coverPath))
            return LoadImage(coverPath) ?? LoadImage(fallbackPath);

        return LoadImage(fallbackPath);
    }

    private static BitmapImage? LoadImage(string path)
    {
        if (!File.Exists(path))
            return null;
        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.UriSource = new Uri(path, UriKind.Absolute);
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch
        {
            return null;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
