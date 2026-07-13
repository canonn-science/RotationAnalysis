using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using Microsoft.Win32;
using ModernWpf.Controls;
using VideoAnalysis.App.Infrastructure;
using VideoAnalysis.App.ViewModels;
using VideoAnalysis.Core.Diagnostics;
using VideoAnalysis.Core.Updates;

namespace VideoAnalysis.App.Views;

public partial class MainWindow : Window
{
    private const string UpdateRepoOwner = "canonn-science";
    private const string UpdateRepoName = "VideoAnalysis";

    private readonly MainViewModel _viewModel = new();
    private readonly UpdateChecker _updateChecker = new();
    private CancellationTokenSource? _searchDebounceCts;
    private CancellationTokenSource? _stationSearchDebounceCts;
    private CancellationTokenSource? _jetConeSearchDebounceCts;
    private CancellationTokenSource? _longExposureSearchDebounceCts;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
        _viewModel.VideoSelectionRequested += OnVideoSelectionRequested;
        _viewModel.VideoLibrary.UploadRequested += OnLibraryUploadRequested;
        _viewModel.Measurements.SubmissionFailed += OnCanonnSubmissionFailed;
        _viewModel.Stations.VideoSelectionRequested += OnStationVideoSelectionRequested;
        _viewModel.JetCone.VideoSelectionRequested += OnJetConeVideoSelectionRequested;
        _viewModel.LongExposure.VideoSelectionRequested += OnLongExposureVideoSelectionRequested;
        Closed += (_, _) =>
        {
            _viewModel.Dispose();
            _updateChecker.Dispose();
        };
        Loaded += async (_, _) => await CheckForUpdatesAsync();
        VersionText.Text = $"Version v{GetCurrentVersion().ToString(3)}";
        UpdateClaudeApiKeyStatusText();
    }

    private void UpdateClaudeApiKeyStatusText()
    {
        ClaudeApiKeyStatusText.Text = _viewModel.HasClaudeApiKey ? "A key is configured." : "No key configured.";
    }

    private static Version GetCurrentVersion() => Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0);

    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }

    private async Task CheckForUpdatesAsync()
    {
        UpdateInfo? update;
        try
        {
            update = await _updateChecker.CheckForUpdateAsync(UpdateRepoOwner, UpdateRepoName, GetCurrentVersion());
        }
        catch (Exception ex)
        {
            AppLog.LogError("CheckForUpdates", ex);
            return;
        }

        if (update is null)
        {
            return;
        }

        var promptResult = await new ContentDialog
        {
            Title = "Update available",
            Content = $"Video Analysis Lab {update.Version} is available. Download and install it now?",
            PrimaryButtonText = "Update Now",
            CloseButtonText = "Later",
        }.ShowAsync();

        if (promptResult != ContentDialogResult.Primary)
        {
            return;
        }

        var downloadWindow = new UpdateDownloadWindow(_updateChecker, update) { Owner = this };
        if (downloadWindow.ShowDialog() == true && downloadWindow.InstallerPath is string installerPath)
        {
            Process.Start(new ProcessStartInfo(installerPath) { UseShellExecute = true });
            Application.Current.Shutdown();
        }
        else if (downloadWindow.FailureMessage is not null)
        {
            await new ContentDialog
            {
                Title = "Update failed",
                Content = downloadWindow.FailureMessage,
                CloseButtonText = "OK",
            }.ShowAsync();
        }
    }

    private async void SystemSearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput)
        {
            return;
        }

        _searchDebounceCts?.Cancel();
        var cts = new CancellationTokenSource();
        _searchDebounceCts = cts;

        try
        {
            await Task.Delay(300, cts.Token);
            await _viewModel.RefreshSuggestionsAsync(sender.Text, cts.Token);
        }
        catch (OperationCanceledException)
        {
            // superseded by a newer keystroke
        }
    }

    private async void SystemSearchBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        if (args.SelectedItem is Core.Spansh.Models.SpanshSearchSystem system)
        {
            await _viewModel.SubmitAsync(system);
        }
    }

    private async void SystemSearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        var chosen = args.ChosenSuggestion as Core.Spansh.Models.SpanshSearchSystem;
        await _viewModel.SubmitAsync(chosen);
    }

    private async void SubmitButton_Click(object sender, RoutedEventArgs e)
    {
        await _viewModel.SubmitAsync(null);
    }

    private async void OnCanonnSubmissionFailed(string message)
    {
        await new ContentDialog
        {
            Title = "Send to Canonn failed",
            Content = message,
            CloseButtonText = "OK",
        }.ShowAsync();
    }

    private async void OnVideoSelectionRequested(RingRowViewModel row)
    {
        string videoPath;
        var libraryEntry = _viewModel.ActiveLibraryVideo;

        if (libraryEntry is not null && !libraryEntry.IsFileMissing)
        {
            // The library becomes the primary source for a working video once it has one active -
            // no need to prompt for a fresh file pick.
            videoPath = libraryEntry.FilePath;
        }
        else
        {
            var promptWindow = new VideoUploadPromptWindow { Owner = this };
            if (promptWindow.ShowDialog() != true || promptWindow.SelectedFilePath is not string pickedPath)
            {
                return;
            }

            videoPath = pickedPath;
            // Funnel every picked video through the library going forward, pre-filled from what
            // this ring row already knows (system/body/ring) so the user only confirms rather
            // than re-searching Spansh.
            libraryEntry = PromptAddVideoToLibrary(videoPath, row);
        }

        // Kick this off now rather than waiting for VideoProcessingWindow's Loaded event, so the
        // shell round-trip overlaps window construction/layout instead of starting after it.
        var quickMetadataTask = Task.Run(() => QuickVideoMetadataReader.Read(videoPath));

        var processingWindow = new VideoProcessingWindow(_viewModel.AnalyzeVideoAsync, videoPath, row.Ring.EstimatedPeriodSeconds, quickMetadataTask, row.Ring.RingName) { Owner = this };
        var completed = processingWindow.ShowDialog();

        if (completed == true && processingWindow.Result is not null)
        {
            var result = processingWindow.Result;
            var finalVideoPath = processingWindow.FinalVideoPath;

            if (libraryEntry is not null && !string.Equals(finalVideoPath, videoPath, StringComparison.OrdinalIgnoreCase))
            {
                // The in-place ring-rename flow (VideoRenamePromptWindow/VideoFileNamer) may have
                // renamed the source file - keep the library entry pointing at the real file
                // instead of letting it go "missing".
                _viewModel.VideoLibrary.UpdatePath(libraryEntry, finalVideoPath);
            }

            var resultsWindow = new ResultsWindow(
                row.Ring.SystemName, row.Ring.BodyName, "Ring:", row.Ring.RingName,
                row.Ring.EstimatedPeriodSeconds, result, finalVideoPath,
                ct => _viewModel.SubmitMeasurementToCanonnAsync(row, result, ct))
            { Owner = this };
            if (resultsWindow.ShowDialog() == true)
            {
                _viewModel.SaveMeasurement(row, result, finalVideoPath, resultsWindow.SubmittedToCanonn);
                if (libraryEntry is not null)
                {
                    _viewModel.VideoLibrary.MarkAnalyzed(libraryEntry, "RingRotation");
                }
            }
        }
        else if (processingWindow.FailureMessage is not null)
        {
            await new ContentDialog
            {
                Title = "Video analysis failed",
                Content = processingWindow.FailureMessage,
                CloseButtonText = "OK",
            }.ShowAsync();
        }
    }

    private void OnLibraryUploadRequested()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select a video",
            Filter = "Video files (*.mp4;*.mkv;*.avi;*.mov;*.wmv)|*.mp4;*.mkv;*.avi;*.mov;*.wmv|All files (*.*)|*.*",
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        PromptAddVideoToLibrary(dialog.FileName);
    }

    /// <summary>Shows the metadata modal for a freshly picked video and, if the user confirms,
    /// adds it to the library. <paramref name="prefillRow"/> supplies known system/body/ring
    /// values (from a Ring Rotation row) so the user doesn't have to re-search Spansh; absent
    /// that, the modal auto-detects the system from the filename (falling back to journal
    /// history) once it's shown - see <see cref="VideoUploadMetadataWindow"/>'s constructor doc.</summary>
    private VideoLibraryEntryViewModel? PromptAddVideoToLibrary(string videoPath, RingRowViewModel? prefillRow = null)
    {
        VideoUploadMetadataViewModel metadataViewModel;
        if (prefillRow is null)
        {
            metadataViewModel = new VideoUploadMetadataViewModel(_viewModel.SpanshClient, _viewModel.JournalMonitor);
        }
        else
        {
            metadataViewModel = new VideoUploadMetadataViewModel(
                _viewModel.SpanshClient,
                _viewModel.JournalMonitor,
                prefillSystemName: prefillRow.Ring.SystemName,
                prefillSystemId64: prefillRow.Ring.SystemId64,
                prefillSystemX: prefillRow.Ring.SystemX,
                prefillSystemY: prefillRow.Ring.SystemY,
                prefillSystemZ: prefillRow.Ring.SystemZ,
                prefillBodyName: prefillRow.Ring.BodyName,
                prefillRingName: prefillRow.Ring.RingName);
        }

        var metadataWindow = new VideoUploadMetadataWindow(
            metadataViewModel, videoPath,
            autoDetectFromFilename: prefillRow is null,
            organizeBySystemFolder: _viewModel.OrganizeRenamedVideosBySystem)
        { Owner = this };
        if (metadataWindow.ShowDialog() != true || metadataWindow.ResultEntry is not { } entry)
        {
            return null;
        }

        return _viewModel.VideoLibrary.AddFromUpload(entry);
    }

    private async void StationSystemSearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput)
        {
            return;
        }

        _stationSearchDebounceCts?.Cancel();
        var cts = new CancellationTokenSource();
        _stationSearchDebounceCts = cts;

        try
        {
            await Task.Delay(300, cts.Token);
            await _viewModel.Stations.RefreshSuggestionsAsync(sender.Text, cts.Token);
        }
        catch (OperationCanceledException)
        {
            // superseded by a newer keystroke
        }
    }

    private async void StationSystemSearchBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        if (args.SelectedItem is Core.Spansh.Models.SpanshSearchSystem system)
        {
            await _viewModel.Stations.SubmitAsync(system);
        }
    }

    private async void StationSystemSearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        var chosen = args.ChosenSuggestion as Core.Spansh.Models.SpanshSearchSystem;
        await _viewModel.Stations.SubmitAsync(chosen);
    }

    private async void StationSubmitButton_Click(object sender, RoutedEventArgs e)
    {
        await _viewModel.Stations.SubmitAsync(null);
    }

    private async void OnStationVideoSelectionRequested(StationRowViewModel row)
    {
        var promptWindow = new VideoUploadPromptWindow { Owner = this };
        if (promptWindow.ShowDialog() != true || promptWindow.SelectedFilePath is not string videoPath)
        {
            return;
        }

        var quickMetadataTask = Task.Run(() => QuickVideoMetadataReader.Read(videoPath));

        var processingWindow = new VideoProcessingWindow(_viewModel.Stations.AnalyzeVideoAsync, videoPath, row.Station.EstimatedRotationSeconds, quickMetadataTask, row.Station.StationName) { Owner = this };
        var completed = processingWindow.ShowDialog();

        if (completed == true && processingWindow.Result is not null)
        {
            var result = processingWindow.Result;
            var finalVideoPath = processingWindow.FinalVideoPath;
            var resultsWindow = new ResultsWindow(
                row.Station.SystemName, row.Station.BodyName ?? "N/A", "Station:", row.Station.StationName,
                row.Station.EstimatedRotationSeconds, result, finalVideoPath,
                submitToCanonn: null)
            { Owner = this };
            if (resultsWindow.ShowDialog() == true)
            {
                _viewModel.Stations.SaveMeasurement(row, result, finalVideoPath);
            }
        }
        else if (processingWindow.FailureMessage is not null)
        {
            await new ContentDialog
            {
                Title = "Video analysis failed",
                Content = processingWindow.FailureMessage,
                CloseButtonText = "OK",
            }.ShowAsync();
        }
    }

    private async void AddReplaceClaudeApiKeyButton_Click(object sender, RoutedEventArgs e)
    {
        var input = new PasswordBox { MinWidth = 320 };
        var dialog = new ContentDialog
        {
            Title = "Claude API key",
            Content = input,
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
        };
        input.Loaded += (_, _) => input.Focus();

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        var key = input.Password.Trim();
        if (key.Length > 0)
        {
            _viewModel.SetClaudeApiKey(key);
            UpdateClaudeApiKeyStatusText();
        }
    }

    private async void DeleteClaudeApiKeyButton_Click(object sender, RoutedEventArgs e)
    {
        var confirmResult = await new ContentDialog
        {
            Title = "Delete Claude API key?",
            Content = "You'll need to enter a new key to use Claude's vision model again.",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
        }.ShowAsync();

        if (confirmResult != ContentDialogResult.Primary)
        {
            return;
        }

        _viewModel.DeleteClaudeApiKey();
        UpdateClaudeApiKeyStatusText();
    }

    private async void JetConeSystemSearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput)
        {
            return;
        }

        _jetConeSearchDebounceCts?.Cancel();
        var cts = new CancellationTokenSource();
        _jetConeSearchDebounceCts = cts;

        try
        {
            await Task.Delay(300, cts.Token);
            await _viewModel.JetCone.RefreshSuggestionsAsync(sender.Text, cts.Token);
        }
        catch (OperationCanceledException)
        {
            // superseded by a newer keystroke
        }
    }

    private async void JetConeSystemSearchBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        if (args.SelectedItem is Core.Spansh.Models.SpanshSearchSystem system)
        {
            await _viewModel.JetCone.SubmitAsync(system);
        }
    }

    private async void JetConeSystemSearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        var chosen = args.ChosenSuggestion as Core.Spansh.Models.SpanshSearchSystem;
        await _viewModel.JetCone.SubmitAsync(chosen);
    }

    private async void JetConeSubmitButton_Click(object sender, RoutedEventArgs e)
    {
        await _viewModel.JetCone.SubmitAsync(null);
    }

    /// <summary>Local-OCR confidence (0-1) below which a configured Claude key is used
    /// automatically instead of the local guess. HudDistanceReader's heuristic classifier
    /// reliably scores well below this on real footage - see its class doc comment - so in
    /// practice this means Claude is tried whenever a key is present, and the local guess is
    /// mostly a fallback for when no key is configured yet.</summary>
    private const double TrustedLocalConfidenceThreshold = 0.55;

    private async void OnJetConeVideoSelectionRequested(JetConeRowViewModel row)
    {
        var promptWindow = new VideoUploadPromptWindow { Owner = this };
        if (promptWindow.ShowDialog() != true || promptWindow.SelectedFilePath is not string videoPath)
        {
            return;
        }

        var processingWindow = new JetConeProcessingWindow(_viewModel.JetCone.AnalyzeVideoAsync, videoPath) { Owner = this };
        if (processingWindow.ShowDialog() != true || processingWindow.Result is not { } result)
        {
            if (processingWindow.FailureMessage is not null)
            {
                await new ContentDialog
                {
                    Title = "Jet cone analysis failed",
                    Content = processingWindow.FailureMessage,
                    CloseButtonText = "OK",
                }.ShowAsync();
            }
            return;
        }

        if (!result.OnsetDetected)
        {
            await new ContentDialog
            {
                Title = "Warning overlay not found",
                Content = "Couldn't find the \"FSD OPERATING / BEYOND SAFETY LIMITS\" warning in this recording. Make sure it shows the approach all the way through the warning appearing.",
                CloseButtonText = "OK",
            }.ShowAsync();
            return;
        }

        double? prefill = result.LocalDistanceLs;
        string sourceLabel = result.LocalConfidence >= TrustedLocalConfidenceThreshold
            ? $"Local reading (confidence {result.LocalConfidence:P0})"
            : $"Local reading, low confidence ({result.LocalConfidence:P0}) - please verify";

        if (result.LocalConfidence < TrustedLocalConfidenceThreshold && _viewModel.JetCone.HasClaudeApiKey)
        {
            try
            {
                var claudeReading = await _viewModel.JetCone.ReadDistanceWithClaudeAsync(result.BottomLeftCropPng);
                prefill = claudeReading.DistanceLs;
                sourceLabel = $"Claude vision (confidence {claudeReading.Confidence}%)";
            }
            catch (Exception ex)
            {
                AppLog.LogError("ClaudeVisionFallback", ex);
                // Fall through with the local guess already assigned above.
            }
        }

        var reviewWindow = new JetConeReviewWindow(result.ReticleCropPng, result.BottomLeftCropPng, prefill, sourceLabel) { Owner = this };
        if (reviewWindow.ShowDialog() != true)
        {
            return;
        }

        if (reviewWindow.UserCorrectedValue && !_viewModel.JetCone.HasClaudeApiKey)
        {
            await OfferClaudeApiKeySetupAsync();
        }

        _viewModel.JetCone.SaveMeasurement(row, reviewWindow.DistanceLs);
    }

    private async Task OfferClaudeApiKeySetupAsync()
    {
        var offer = await new ContentDialog
        {
            Title = "Improve future readings?",
            Content = "Local text recognition struggled with this frame. Want to provide a Claude API key so future readings like this can use Claude's vision model instead? You can remove it any time from the About tab.",
            PrimaryButtonText = "Add API Key",
            CloseButtonText = "Not now",
        }.ShowAsync();

        if (offer != ContentDialogResult.Primary)
        {
            return;
        }

        var input = new PasswordBox { MinWidth = 320 };
        var keyDialog = new ContentDialog
        {
            Title = "Claude API key",
            Content = input,
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
        };
        input.Loaded += (_, _) => input.Focus();

        if (await keyDialog.ShowAsync() == ContentDialogResult.Primary)
        {
            var key = input.Password.Trim();
            if (key.Length > 0)
            {
                _viewModel.SetClaudeApiKey(key);
                UpdateClaudeApiKeyStatusText();
            }
        }
    }

    private async void LongExposureSystemSearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput)
        {
            return;
        }

        _longExposureSearchDebounceCts?.Cancel();
        var cts = new CancellationTokenSource();
        _longExposureSearchDebounceCts = cts;

        try
        {
            await Task.Delay(300, cts.Token);
            await _viewModel.LongExposure.RefreshSuggestionsAsync(sender.Text, cts.Token);
        }
        catch (OperationCanceledException)
        {
            // superseded by a newer keystroke
        }
    }

    private async void LongExposureSystemSearchBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        if (args.SelectedItem is Core.Spansh.Models.SpanshSearchSystem system)
        {
            await _viewModel.LongExposure.SubmitAsync(system);
        }
    }

    private async void LongExposureSystemSearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        var chosen = args.ChosenSuggestion as Core.Spansh.Models.SpanshSearchSystem;
        await _viewModel.LongExposure.SubmitAsync(chosen);
    }

    private async void LongExposureSubmitButton_Click(object sender, RoutedEventArgs e)
    {
        await _viewModel.LongExposure.SubmitAsync(null);
    }

    private async void OnLongExposureVideoSelectionRequested(LongExposureRowViewModel row)
    {
        var promptWindow = new VideoUploadPromptWindow { Owner = this };
        if (promptWindow.ShowDialog() != true || promptWindow.SelectedFilePath is not string videoPath)
        {
            return;
        }

        var processingWindow = new LongExposureProcessingWindow(_viewModel.LongExposure.GenerateAsync, videoPath) { Owner = this };
        if (processingWindow.ShowDialog() != true || processingWindow.Result is not { } result)
        {
            if (processingWindow.FailureMessage is not null)
            {
                await new ContentDialog
                {
                    Title = "Long exposure generation failed",
                    Content = processingWindow.FailureMessage,
                    CloseButtonText = "OK",
                }.ShowAsync();
            }
            return;
        }

        var resultsWindow = LongExposureResultsWindow.ForLongExposureResult(result, row.Target.SystemName, row.Target.ObjectName);
        resultsWindow.Owner = this;
        resultsWindow.ShowDialog();
    }

    private async void SlitScanUploadButton_Click(object sender, RoutedEventArgs e)
    {
        var promptWindow = new VideoUploadPromptWindow { Owner = this };
        if (promptWindow.ShowDialog() != true || promptWindow.SelectedFilePath is not string videoPath)
        {
            return;
        }

        _viewModel.SlitScan.ErrorMessage = null;
        _viewModel.SlitScan.VideoFilePath = videoPath;
        SlitScanControls.SetPreviewFrame(null);

        byte[]? previewFrame;
        try
        {
            previewFrame = await _viewModel.SlitScan.LoadPreviewFrameAsync(CancellationToken.None);
        }
        catch
        {
            previewFrame = null;
        }

        // The user may have uploaded a different video (or left the tab) while this was loading.
        if (_viewModel.SlitScan.VideoFilePath == videoPath)
        {
            SlitScanControls.SetPreviewFrame(previewFrame);
        }
    }

    private async void SlitScanGenerateButton_Click(object sender, RoutedEventArgs e)
    {
        var videoPath = _viewModel.SlitScan.VideoFilePath;
        if (videoPath is null)
        {
            _viewModel.SlitScan.ErrorMessage = "Upload a video first.";
            return;
        }

        var parameters = SlitScanControls.BuildParameters();
        if (parameters is null)
        {
            _viewModel.SlitScan.ErrorMessage = SlitScanControls.LastValidationError;
            return;
        }

        _viewModel.SlitScan.ErrorMessage = null;

        var processingWindow = new SlitScanProcessingWindow(
            (path, progress, ct) => _viewModel.SlitScan.GenerateAsync(path, parameters, progress, ct),
            videoPath)
        { Owner = this };
        if (processingWindow.ShowDialog() != true || processingWindow.Result is not { } result)
        {
            if (processingWindow.FailureMessage is not null)
            {
                await new ContentDialog
                {
                    Title = "Slit scan generation failed",
                    Content = processingWindow.FailureMessage,
                    CloseButtonText = "OK",
                }.ShowAsync();
            }
            return;
        }

        var resultsWindow = new LongExposureResultsWindow(
            new[] { ("Slit Scan", result.ImagePng) },
            Path.GetFileNameWithoutExtension(videoPath),
            null)
        { Owner = this };
        resultsWindow.ShowDialog();
    }

    private async void SlitScanResetButton_Click(object sender, RoutedEventArgs e)
    {
        var confirmResult = await new ContentDialog
        {
            Title = "Reset Slit Scan controls?",
            Content = "All Geometry, Motion, Sampling, Compositing, and Output settings will be restored to their defaults. This can't be undone.",
            PrimaryButtonText = "Reset",
            CloseButtonText = "Cancel",
        }.ShowAsync();

        if (confirmResult != ContentDialogResult.Primary)
        {
            return;
        }

        SlitScanControls.ResetToDefaults();
    }
}
