using VideoAnalysis.App.Infrastructure;
using VideoAnalysis.Core.Domain;

namespace VideoAnalysis.App.ViewModels;

public sealed class StationRowViewModel
{
    public StationInfo Station { get; }

    public StationRowViewModel(StationInfo station, Action<StationRowViewModel> onSelectVideo)
    {
        Station = station;
        SelectVideoCommand = new RelayCommand(() => onSelectVideo(this));
    }

    public string SystemName => Station.SystemName;
    public string StationName => Station.StationName;
    public string Kind => Station.DisplayKind;
    public string BodyNameDisplay => Station.BodyName ?? "N/A";
    public string BodyRadiusDisplay => Station.BodyRadiusKm is double km ? $"{km:N0} km" : "N/A";
    public string BodyRotationalPeriodDisplay => Station.BodyRotationalPeriodDays is double d ? $"{d:N2} days" : "N/A";
    public string BodyInclinationDisplay => Station.BodyInclinationDegrees is double deg ? $"{deg:N1}°" : "N/A";
    public string EstimatedRotationDisplay => DurationFormat.Seconds(Station.EstimatedRotationSeconds);
    public string SuggestedDurationDisplay => DurationFormat.Minutes(Station.SuggestedVideoDurationMinutes);

    public RelayCommand SelectVideoCommand { get; }
}
