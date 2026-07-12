using VideoAnalysis.App.Infrastructure;
using VideoAnalysis.Core.Domain;

namespace VideoAnalysis.App.ViewModels;

public sealed class LongExposureRowViewModel
{
    public LongExposureTargetInfo Target { get; }

    public LongExposureRowViewModel(LongExposureTargetInfo target, Action<LongExposureRowViewModel> onSelectVideo)
    {
        Target = target;
        SelectVideoCommand = new RelayCommand(() => onSelectVideo(this));
    }

    public string SystemName => Target.SystemName;
    public string ObjectName => Target.ObjectName;
    public string Kind => Target.DisplayKind;
    public string ObjectType => Target.ObjectType ?? "N/A";

    public RelayCommand SelectVideoCommand { get; }
}
