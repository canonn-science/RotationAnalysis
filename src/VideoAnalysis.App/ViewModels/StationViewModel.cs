using System.Collections.ObjectModel;
using System.IO;
using VideoAnalysis.App.Infrastructure;
using VideoAnalysis.Core.Diagnostics;
using VideoAnalysis.Core.Domain;
using VideoAnalysis.Core.Reference;
using VideoAnalysis.Core.Spansh;
using VideoAnalysis.Core.Spansh.Models;
using VideoAnalysis.Core.Storage;
using VideoAnalysis.Core.VideoAnalysis;

namespace VideoAnalysis.App.ViewModels;

/// <summary>Station Rotation's counterpart to <see cref="MainViewModel"/>. Uses only
/// <see cref="SpanshClient"/> (same system search and same dump endpoint Ring Rotation already
/// uses) - no other data source. Owns its own video-analysis/save flow rather than extending
/// <see cref="MainViewModel"/>, mirroring how <see cref="MeasurementsViewModel"/> is already a
/// separate view model rather than bloating the main one.</summary>
public sealed class StationViewModel : ObservableObject, IDisposable
{
    private readonly SpanshClient _spanshClient = new();
    private readonly GuardianBeaconClient _guardianBeaconClient = new();
    private readonly StationMeasurementCsvStore _measurementStore = new();

    private List<GuardianBeaconEntry>? _allBeacons;

    private string _systemQuery = string.Empty;
    private string? _errorMessage;
    private bool _isBusy;
    private string? _resolvedSystemName;

    public StationViewModel()
    {
        Measurements = new StationMeasurementsViewModel(_measurementStore);
    }

    public string SystemQuery
    {
        get => _systemQuery;
        set => SetField(ref _systemQuery, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        set => SetField(ref _errorMessage, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set => SetField(ref _isBusy, value);
    }

    public string? ResolvedSystemName
    {
        get => _resolvedSystemName;
        set => SetField(ref _resolvedSystemName, value);
    }

    public ObservableCollection<SpanshSearchSystem> Suggestions { get; } = new();

    public ObservableCollection<StationRowViewModel> Stations { get; } = new();

    public StationMeasurementsViewModel Measurements { get; }

    /// <summary>Raised when the user clicks "Select Video…" on a station row; the view handles the file picker.</summary>
    public event Action<StationRowViewModel>? VideoSelectionRequested;

    public async Task RefreshSuggestionsAsync(string query, CancellationToken ct)
    {
        if (query.Length < 3)
        {
            Suggestions.Clear();
            return;
        }

        try
        {
            var response = await _spanshClient.SearchSystemsAsync(query, ct).ConfigureAwait(true);
            Suggestions.Clear();
            foreach (var system in response.MinMax)
            {
                Suggestions.Add(system);
            }
        }
        catch (OperationCanceledException)
        {
            // superseded by a newer keystroke; ignore
        }
        catch (Exception ex)
        {
            AppLog.LogError("Station.RefreshSuggestions", ex);
            ErrorMessage = $"Search failed: {ex.Message}";
        }
    }

    public async Task SubmitAsync(SpanshSearchSystem? chosenSystem)
    {
        if (IsBusy)
        {
            return;
        }

        ErrorMessage = null;
        Stations.Clear();
        IsBusy = true;
        try
        {
            var resolved = chosenSystem;
            if (resolved is null)
            {
                var query = SystemQuery.Trim();
                if (query.Length == 0)
                {
                    ErrorMessage = "Enter a system name.";
                    return;
                }

                var response = await _spanshClient.SearchSystemsAsync(query).ConfigureAwait(true);
                resolved = response.MinMax.FirstOrDefault(s => string.Equals(s.Name, query, StringComparison.OrdinalIgnoreCase));
                if (resolved is null)
                {
                    ErrorMessage = $"System \"{query}\" not found.";
                    return;
                }
            }

            _allBeacons ??= await _guardianBeaconClient.GetBeaconsAsync().ConfigureAwait(true);

            var dump = await _spanshClient.GetDumpAsync(resolved.Id64).ConfigureAwait(true);
            if (dump is null)
            {
                ErrorMessage = $"System \"{resolved.Name}\" not found.";
                return;
            }

            var beaconsInSystem = _allBeacons
                .Where(b => string.Equals(b.SystemName, resolved.Name, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var stations = StationParser.ExtractStations(dump, beaconsInSystem);

            ResolvedSystemName = resolved.Name;
            foreach (var station in stations)
            {
                Stations.Add(new StationRowViewModel(station, row => VideoSelectionRequested?.Invoke(row)));
            }

            if (stations.Count == 0)
            {
                ErrorMessage = $"\"{resolved.Name}\" has no stations, installations, or Guardian Beacons.";
            }
        }
        catch (Exception ex)
        {
            AppLog.LogError("Station.SystemLookup", ex);
            ErrorMessage = $"Lookup failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public Task<HorizontalVideoAnalysisResult> AnalyzeVideoAsync(string videoPath, double? seedPeriodSeconds, IProgress<VideoAnalysisProgress> progress, CancellationToken ct)
        => HorizontalVideoAnalyzer.AnalyzeAsync(videoPath, seedPeriodSeconds, progress, ct);

    public void SaveMeasurement(StationRowViewModel row, HorizontalVideoAnalysisResult result, string videoPath)
    {
        var station = row.Station;
        _measurementStore.Append(new StationMeasurementRecord
        {
            Timestamp = DateTime.UtcNow,
            SystemName = station.SystemName,
            StationName = station.StationName,
            Id64 = station.SystemId64,
            X = station.SystemX,
            Y = station.SystemY,
            Z = station.SystemZ,
            BodyName = station.BodyName ?? string.Empty,
            BodyType = station.BodyType ?? string.Empty,
            BodyMassEarthMasses = station.BodyMassEarthMasses,
            BodyRadiusKm = station.BodyRadiusKm,
            BodyInclinationDegrees = station.BodyInclinationDegrees,
            EstimatedRotationSeconds = station.EstimatedRotationSeconds ?? double.NaN,
            ObservedRotationSeconds = result.ObservedPeriodSeconds,
            MeasuredPeriodSeconds = result.ObservedPeriodSeconds,
            Submitted = false,
            VideoFilename = Path.GetFileName(videoPath),
        });
        Measurements.Refresh();
    }

    public void Dispose()
    {
        _spanshClient.Dispose();
        _guardianBeaconClient.Dispose();
    }
}
