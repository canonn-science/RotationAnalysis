using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using VideoAnalysis.App.Infrastructure;
using VideoAnalysis.Core.Storage;

namespace VideoAnalysis.App.ViewModels;

/// <summary>Station Rotation's counterpart to <see cref="MeasurementsViewModel"/> - same
/// Refresh/Show on Disk behavior, no Canonn column since that submission path isn't wired up yet.</summary>
public sealed class StationMeasurementsViewModel : ObservableObject
{
    private readonly StationMeasurementCsvStore _store;

    public StationMeasurementsViewModel(StationMeasurementCsvStore store)
    {
        _store = store;
        RefreshCommand = new RelayCommand(Refresh);
        ShowOnDiskCommand = new RelayCommand(ShowOnDisk);
        Refresh();
    }

    public ObservableCollection<StationMeasurementRowViewModel> Records { get; } = new();

    public RelayCommand RefreshCommand { get; }
    public RelayCommand ShowOnDiskCommand { get; }

    public void Refresh()
    {
        Records.Clear();
        foreach (var record in _store.ReadAll().OrderByDescending(r => r.Timestamp))
        {
            Records.Add(new StationMeasurementRowViewModel(record));
        }
    }

    private void ShowOnDisk()
    {
        var directory = Path.GetDirectoryName(_store.CsvPath)!;
        Directory.CreateDirectory(directory);
        if (File.Exists(_store.CsvPath))
        {
            Process.Start("explorer.exe", $"/select,\"{_store.CsvPath}\"");
        }
        else
        {
            Process.Start("explorer.exe", $"\"{directory}\"");
        }
    }
}
