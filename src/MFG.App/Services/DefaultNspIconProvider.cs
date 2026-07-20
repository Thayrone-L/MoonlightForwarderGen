using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MFG.App.Services;

public sealed class DefaultNspIconProvider
{
    private static readonly string BundledIconPath = Path.Combine(
        AppContext.BaseDirectory, "assets", "mfg-default-nsp.jpg");

    private static readonly string IconPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MoonlightForwarderGenerator", "assets", "mfg-default-icon.jpg");

    public string GetPath()
    {
        if (File.Exists(BundledIconPath) && IsJpeg(BundledIconPath))
            return BundledIconPath;

        if (File.Exists(IconPath) && IsJpeg(IconPath))
            return IconPath;

        Directory.CreateDirectory(Path.GetDirectoryName(IconPath)!);
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            context.DrawRectangle(new LinearGradientBrush(Color.FromRgb(31, 22, 72), Color.FromRgb(87, 45, 150), 35), null,
                new Rect(0, 0, 256, 256));
            context.DrawEllipse(new SolidColorBrush(Color.FromArgb(170, 255, 255, 255)), null, new Point(128, 128), 68, 68);
        }

        var bitmap = new RenderTargetBitmap(256, 256, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var encoder = new JpegBitmapEncoder { QualityLevel = 92 };
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new FileStream(IconPath, FileMode.Create, FileAccess.Write, FileShare.Read);
        encoder.Save(stream);
        return IconPath;
    }

    private static bool IsJpeg(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        return stream.Length >= 3 && stream.ReadByte() == 0xFF && stream.ReadByte() == 0xD8 && stream.ReadByte() == 0xFF;
    }
}
