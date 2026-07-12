using VideoAnalysis.App.Infrastructure;
using VideoAnalysis.Core.Domain;

namespace VideoAnalysis.App.ViewModels;

public sealed class JetConeRowViewModel
{
    public JetTargetInfo Target { get; }

    public JetConeRowViewModel(JetTargetInfo target, Action<JetConeRowViewModel> onSelectVideo)
    {
        Target = target;
        SelectVideoCommand = new RelayCommand(() => onSelectVideo(this));
    }

    public string SystemName => Target.SystemName;
    public string BodyName => Target.BodyName;
    public string BodyType => Target.BodyType;

    public RelayCommand SelectVideoCommand { get; }
}
