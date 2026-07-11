using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using RotationAnalysis.App.Infrastructure;
using RotationAnalysis.Core.Diagnostics;
using RotationAnalysis.Core.Storage;
using RotationAnalysis.Core.VideoAnalysis;

namespace RotationAnalysis.App.Views;

public partial class VideoProcessingWindow : Window
{
    private readonly Func<string, double?, IProgress<VideoAnalysisProgress>, CancellationToken, Task<HorizontalVideoAnalysisResult>> _analyzeVideo;
    private readonly string _objectName;
    private readonly double? _seedPeriodSeconds;
    private readonly Task<QuickVideoMetadata> _quickMetadataTask;
    private readonly CancellationTokenSource _cts = new();
    private bool _realProgressReceived;
    private string _videoPath;

    public HorizontalVideoAnalysisResult? Result { get; private set; }
    public string? FailureMessage { get; private set; }

    /// <summary>The video's path after this window closes - the renamed path if the user accepted
    /// the rename prompt and the rename succeeded, otherwise the original upload path.</summary>
    public string FinalVideoPath => _videoPath;

    private Task<HorizontalVideoAnalysisResult>? _analysisTask;
    private string? _pendingRenamePath;

    /// <summary>
    /// <paramref name="analyzeVideo"/> decouples this window from any particular mode's view
    /// model - Ring Rotation passes <c>MainViewModel.AnalyzeVideoAsync</c>, Station Rotation
    /// passes the equivalent method on <c>StationViewModel</c>, but the analysis itself
    /// (<see cref="HorizontalVideoAnalyzer"/>) is identical either way. <paramref name="objectName"/>
    /// is the ring/station name used for the rename-to-match-object-name prompt.
    /// </summary>
    public VideoProcessingWindow(
        Func<string, double?, IProgress<VideoAnalysisProgress>, CancellationToken, Task<HorizontalVideoAnalysisResult>> analyzeVideo,
        string videoPath, double? seedPeriodSeconds, Task<QuickVideoMetadata> quickMetadataTask, string objectName)
    {
        InitializeComponent();
        _analyzeVideo = analyzeVideo;
        _videoPath = videoPath;
        _seedPeriodSeconds = seedPeriodSeconds;
        _quickMetadataTask = quickMetadataTask;
        _objectName = objectName;
        Loaded += VideoProcessingWindow_Loaded;
    }

    private void VideoProcessingWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // OpenCV's own file open can be slow (large/poorly-indexed capture files), and reports no
        // progress at all until it finishes. While that's in flight, read the same metadata and
        // thumbnail Windows Explorer would show (via the Shell Property System / thumbnail cache)
        // so the window shows something immediately instead of sitting blank.
        _ = ShowQuickPreviewAsync();

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

            if (p.Stage == VideoAnalysisStage.Opening && !string.IsNullOrEmpty(p.Message))
            {
                VideoMetaText.Text = p.Message;
            }

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

        _analysisTask = _analyzeVideo(_videoPath, _seedPeriodSeconds, progress, _cts.Token);

        if (VideoFileNamer.MatchesRingName(_videoPath, _objectName))
        {
            _ = FinishAnalysisAsync();
        }
        else
        {
            // Wait for this window's own first paint before stealing the message pump with a
            // nested modal - otherwise it can sit unpainted behind the rename dialog for the
            // whole analysis run.
            ContentRendered += VideoProcessingWindow_ContentRendered;
        }
    }

    private async void VideoProcessingWindow_ContentRendered(object? sender, EventArgs e)
    {
        ContentRendered -= VideoProcessingWindow_ContentRendered;

        var suggestedPath = VideoFileNamer.GetNextAvailableFileName(_videoPath, _objectName);
        var renamePrompt = new VideoRenamePromptWindow(Path.GetFileName(suggestedPath)) { Owner = this };
        if (renamePrompt.ShowDialog() == true)
        {
            _pendingRenamePath = suggestedPath;
        }

        await FinishAnalysisAsync();
    }

    private async Task FinishAnalysisAsync()
    {
        try
        {
            Result = await _analysisTask!;

            if (_pendingRenamePath is not null)
            {
                try
                {
                    File.Move(_videoPath, _pendingRenamePath);
                    _videoPath = _pendingRenamePath;
                }
                catch (Exception ex)
                {
                    AppLog.LogError("RenameVideo", ex);
                }
            }

            DialogResult = true;
        }
        catch (OperationCanceledException)
        {
            DialogResult = false;
        }
        catch (Exception ex)
        {
            AppLog.LogError("AnalyzeVideo", ex);
            FailureMessage = ex.Message;
            DialogResult = false;
        }
    }

    private async Task ShowQuickPreviewAsync()
    {
        var meta = await _quickMetadataTask;

        // The real analysis may already have reported actual (OpenCV-derived) numbers by the
        // time this finishes - never regress the display back to the approximate shell values.
        if (_realProgressReceived)
        {
            return;
        }

        if (meta.Thumbnail is not null)
        {
            CurrentFrameImage.Source = meta.Thumbnail;
            HideLoadingRing();
        }

        if (meta.Width is int width && meta.Height is int height)
        {
            var parts = new List<string> { $"{width}×{height}" };
            if (meta.FrameRate is double fps)
            {
                parts.Add($"{fps:0.##} fps");
            }
            if (meta.Duration is TimeSpan duration)
            {
                parts.Add(DurationFormat.Seconds(duration.TotalSeconds));
            }
            VideoMetaText.Text = string.Join(" · ", parts) + " (from file properties)";
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
