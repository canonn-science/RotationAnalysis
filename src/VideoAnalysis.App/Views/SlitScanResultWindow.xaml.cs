using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using VideoAnalysis.Core.Storage;

namespace VideoAnalysis.App.Views;

/// <summary>Preview-and-save dialog for a single generated image. Slit Scan only ever produces
/// one output, so unlike <see cref="LongExposureResultsWindow"/>'s variant-picker grid, this just
/// shows it full-size with a single Save action instead of "Save Selected"/"Save All".</summary>
public partial class SlitScanResultWindow : Window
{
    private static readonly string DefaultOutputRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "RotationAnalysisLab", "SlitScan");

    private readonly byte[] _png;
    private readonly string _systemName;

    public SlitScanResultWindow(byte[] png, string systemName)
    {
        InitializeComponent();
        _png = png;
        _systemName = systemName;

        var bitmap = new BitmapImage();
        using (var stream = new MemoryStream(png))
        {
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
        }
        bitmap.Freeze();
        PreviewImage.Source = bitmap;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        var directory = LongExposureFileNamer.SuggestDirectory(DefaultOutputRoot, _systemName);
        Directory.CreateDirectory(directory);
        var fileName = LongExposureFileNamer.SuggestFileName(_systemName, null, null, "Slit Scan", ".png");

        var dialog = new SaveFileDialog
        {
            Title = "Save Slit Scan Image",
            InitialDirectory = directory,
            FileName = fileName,
            Filter = "PNG Image (*.png)|*.png",
            OverwritePrompt = true,
        };

        if (dialog.ShowDialog(this) == true)
        {
            File.WriteAllBytes(dialog.FileName, _png);
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}
