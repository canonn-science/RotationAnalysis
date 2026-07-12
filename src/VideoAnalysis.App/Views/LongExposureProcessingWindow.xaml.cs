using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using VideoAnalysis.Core.Diagnostics;
using VideoAnalysis.Core.VideoAnalysis;
using VideoAnalysis.Core.VideoAnalysis.LongExposure;

namespace VideoAnalysis.App.Views;

public partial class LongExposureProcessingWindow : Window
{
    private readonly Func<string, IProgress<VideoAnalysisProgress>, CancellationToken, Task<LongExposureResult>> _generate;
    private readonly string _videoPath;
    private readonly CancellationTokenSource _cts = new();
    private bool _realProgressReceived;

    public LongExposureResult? Result { get; private set; }
    public string? FailureMessage { get; private set; }

    public LongExposureProcessingWindow(
        Func<string, IProgress<VideoAnalysisProgress>, CancellationToken, Task<LongExposureResult>> generate,
        string videoPath)
    {
        InitializeComponent();
        _generate = generate;
        _videoPath = videoPath;
        Loaded += async (_, _) => await RunAsync();
    }

    private async Task RunAsync()
    {
        var progress = new Progress<VideoAnalysisProgress>(p =>
        {
            if (!_realProgressReceived)
            {
                _realProgressReceived = true;
                ProgressBarControl.IsIndeterminate = false;
            }
            ProgressBarControl.Value = p.PercentComplete;
            StatusText.Text = p.Message;
            FrameCounterText.Text = p.TotalFrames > 0 ? $"Frame {p.FramesProcessed} of {p.TotalFrames}" : string.Empty;

            if (p.PreviewImageBytes is { Length: > 0 } bytes)
            {
                var bitmap = new BitmapImage();
                using var stream = new MemoryStream(bytes);
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = stream;
                bitmap.EndInit();
                bitmap.Freeze();
                CurrentFrameImage.Source = bitmap;
                HideLoadingRing();
            }
        });

        try
        {
            Result = await _generate(_videoPath, progress, _cts.Token);
            DialogResult = true;
        }
        catch (OperationCanceledException)
        {
            DialogResult = false;
        }
        catch (Exception ex)
        {
            AppLog.LogError("GenerateLongExposure", ex);
            FailureMessage = ex.Message;
            DialogResult = false;
        }
    }

    private void HideLoadingRing()
    {
        LoadingRing.IsActive = false;
        LoadingRing.Visibility = Visibility.Collapsed;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        _cts.Cancel();
        StatusText.Text = "Cancelling…";
    }
}
