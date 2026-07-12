using VideoAnalysis.Core.Storage;

namespace VideoAnalysis.App.ViewModels;

/// <summary>Formats one logged <see cref="StationMeasurementRecord"/> for display. No Canonn
/// submission state here (unlike <c>MeasurementRowViewModel</c>) - Station Rotation's Canonn
/// endpoint isn't available yet, see <c>StationCanonnClient</c>.</summary>
public sealed class StationMeasurementRowViewModel
{
    public StationMeasurementRowViewModel(StationMeasurementRecord record)
    {
        Record = record;
    }

    public StationMeasurementRecord Record { get; }

    public string TimestampDisplay => Record.Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
    public string SystemName => Record.SystemName;
    public string StationName => Record.StationName;
    public string BodyName => Record.BodyName;
    public string BodyRadiusDisplay => Record.BodyRadiusKm is double km ? $"{km:N0} km" : "N/A";
    public string BodyRotationalPeriodDisplay => Record.EstimatedRotationSeconds > 0
        ? $"{Record.EstimatedRotationSeconds / 86_400.0:N2} days"
        : "N/A";
    public string BodyInclinationDisplay => Record.BodyInclinationDegrees is double deg ? $"{deg:N1}°" : "N/A";
    public string EstimatedRotationDisplay => FormatSeconds(Record.EstimatedRotationSeconds);
    public string ObservedRotationDisplay => FormatSeconds(Record.ObservedRotationSeconds);
    public string VideoFilename => Record.VideoFilename;

    private static string FormatSeconds(double seconds) =>
        double.IsNaN(seconds) || double.IsInfinity(seconds) ? "N/A" : seconds.ToString();
}
