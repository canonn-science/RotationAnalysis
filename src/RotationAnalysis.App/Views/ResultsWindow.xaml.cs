using System.Windows;
using RotationAnalysis.App.Infrastructure;
using RotationAnalysis.Core.Diagnostics;
using RotationAnalysis.Core.VideoAnalysis;

namespace RotationAnalysis.App.Views;

/// <summary>
/// Shows the analysis result for review. Nothing is saved to the CSV until the user explicitly
/// clicks "Save to History" - DialogResult reflects that choice so the caller knows whether to
/// persist the measurement. Sending to Canonn is independent of that and can happen either way.
/// </summary>
public partial class ResultsWindow : Window
{
    private readonly Func<CancellationToken, Task>? _submitToCanonn;

    /// <summary>Whether the user successfully sent this measurement to Canonn from this dialog.</summary>
    public bool SubmittedToCanonn { get; private set; }

    /// <summary>
    /// <paramref name="submitToCanonn"/> is null to hide the "Send to Canonn" button entirely -
    /// used for modes (e.g. Station Rotation, for now) that don't have a submission endpoint yet.
    /// <paramref name="objectLabel"/>/<paramref name="objectName"/> are the third identity row's
    /// label/value ("Ring:"/ring name for Ring Rotation, "Station:"/station name for Station
    /// Rotation).
    /// </summary>
    public ResultsWindow(
        string systemName, string bodyName, string objectLabel, string objectName,
        double? estimatedPeriodSeconds, HorizontalVideoAnalysisResult result, string videoPath,
        Func<CancellationToken, Task>? submitToCanonn)
    {
        InitializeComponent();

        _submitToCanonn = submitToCanonn;

        SystemNameText.Text = systemName;
        BodyNameText.Text = bodyName;
        RingLabelText.Text = objectLabel;
        RingNameText.Text = objectName;

        EstimatedText.Text = DurationFormat.SecondsWithRaw(estimatedPeriodSeconds);
        ObservedText.Text = DurationFormat.SecondsWithRaw(result.ObservedPeriodSeconds);

        if (estimatedPeriodSeconds is double estimated && estimated > 0)
        {
            double diffPercent = (result.ObservedPeriodSeconds - estimated) / estimated * 100.0;
            DifferenceText.Text = $"{diffPercent:+0.0;-0.0}%";
        }
        else
        {
            DifferenceText.Text = "N/A";
        }

        ConfidenceText.Text = $"{result.ConfidencePercent:F0}%";
        RollText.Text = $"{result.MedianRollDegrees:+0.0;-0.0}° from level";
        TrackingText.Text = $"{result.ChunksUsed} of {result.ChunksAvailable} recording segments";

        if (_submitToCanonn is null)
        {
            SendToCanonnButton.Visibility = Visibility.Collapsed;
        }
    }

    private async void SendToCanonnButton_Click(object sender, RoutedEventArgs e)
    {
        if (_submitToCanonn is null)
        {
            return;
        }

        SendToCanonnButton.IsEnabled = false;
        SubmitErrorText.Text = null;
        try
        {
            await _submitToCanonn(CancellationToken.None);
            SubmittedToCanonn = true;
            SendToCanonnButton.Content = "Sent to Canonn ✓";
        }
        catch (Exception ex)
        {
            AppLog.LogError("SubmitToCanonn", ex);
            SubmitErrorText.Text = $"Send failed: {ex.Message}";
            SendToCanonnButton.IsEnabled = true;
        }
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
