using System.Collections.ObjectModel;
using VideoAnalysis.App.Infrastructure;
using VideoAnalysis.Core.Diagnostics;
using VideoAnalysis.Core.Journal;
using VideoAnalysis.Core.Spansh;
using VideoAnalysis.Core.Spansh.Models;
using VideoAnalysis.Core.Storage;

namespace VideoAnalysis.App.ViewModels;

/// <summary>Backs the video library's upload metadata modal: a Spansh system typeahead (same
/// pattern as the main tabs), followed by body/ring/station pickers populated from that system's
/// full dump once resolved. Body/station default from the current journal-derived location when
/// available (captured once, at construction) but stay freely editable - the user's choice always
/// wins, this is just a convenience prefill, not a live-updating binding.</summary>
public sealed class VideoUploadMetadataViewModel : ObservableObject
{
    private readonly SpanshClient _spanshClient;
    private readonly List<(string Name, string? Type)> _stationOptions = new();

    private string _systemQuery = string.Empty;
    private SpanshSearchSystem? _selectedSystem;
    private SpanshDumpResponse? _dump;
    private string? _selectedBodyName;
    private string? _selectedRingName;
    private string? _selectedStationName;
    private bool _isBusy;
    private string? _errorMessage;

    public VideoUploadMetadataViewModel(
        SpanshClient spanshClient,
        JournalMonitor? journalMonitor = null,
        string? prefillSystemName = null,
        long? prefillSystemId64 = null,
        double? prefillSystemX = null,
        double? prefillSystemY = null,
        double? prefillSystemZ = null,
        string? prefillBodyName = null,
        string? prefillRingName = null)
    {
        _spanshClient = spanshClient;

        if (prefillSystemName is not null && prefillSystemId64 is long id64)
        {
            _selectedSystem = new SpanshSearchSystem
            {
                Id64 = id64,
                Name = prefillSystemName,
                X = prefillSystemX ?? 0,
                Y = prefillSystemY ?? 0,
                Z = prefillSystemZ ?? 0,
            };
            _systemQuery = prefillSystemName;
            _selectedBodyName = prefillBodyName;
            _selectedRingName = prefillRingName;
            _ = LoadDumpAsync(_selectedSystem, resetSelections: false);
        }
        else if (journalMonitor?.LastKnownSystemName is not null)
        {
            // Text-only prefill - an id64 still needs resolving via typeahead/submit, same as
            // the main tabs' system search.
            _systemQuery = journalMonitor.LastKnownSystemName;
        }

        if (_selectedBodyName is null)
        {
            _selectedBodyName = journalMonitor?.LastKnownBodyName;
        }

        _selectedStationName = journalMonitor?.LastKnownStationName;
    }

    public string SystemQuery
    {
        get => _systemQuery;
        set => SetField(ref _systemQuery, value);
    }

    public string? SelectedBodyName
    {
        get => _selectedBodyName;
        set
        {
            if (SetField(ref _selectedBodyName, value))
            {
                UpdateRingOptions(resetSelection: true);
            }
        }
    }

    public string? SelectedRingName
    {
        get => _selectedRingName;
        set => SetField(ref _selectedRingName, value);
    }

    public string? SelectedStationName
    {
        get => _selectedStationName;
        set => SetField(ref _selectedStationName, value);
    }

    public bool HasRingOptions => RingNames.Count > 0;

    public bool IsBusy
    {
        get => _isBusy;
        set => SetField(ref _isBusy, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        set => SetField(ref _errorMessage, value);
    }

    public ObservableCollection<SpanshSearchSystem> Suggestions { get; } = new();

    public ObservableCollection<string> BodyNames { get; } = new();

    public ObservableCollection<string> RingNames { get; } = new();

    public ObservableCollection<string> StationNames { get; } = new();

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
            AppLog.LogError("VideoUploadMetadataSuggestions", ex);
            ErrorMessage = $"Search failed: {ex.Message}";
        }
    }

    public async Task SelectSystemAsync(SpanshSearchSystem? chosenSystem)
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

            IsBusy = true;
            try
            {
                var response = await _spanshClient.SearchSystemsAsync(query).ConfigureAwait(true);
                resolved = response.MinMax.FirstOrDefault(s => string.Equals(s.Name, query, StringComparison.OrdinalIgnoreCase));
                if (resolved is null)
                {
                    ErrorMessage = $"System \"{query}\" not found.";
                    return;
                }
            }
            catch (Exception ex)
            {
                AppLog.LogError("VideoUploadMetadataSystemLookup", ex);
                ErrorMessage = $"Lookup failed: {ex.Message}";
                return;
            }
            finally
            {
                IsBusy = false;
            }
        }

        _selectedSystem = resolved;
        SystemQuery = resolved.Name;
        await LoadDumpAsync(resolved, resetSelections: true).ConfigureAwait(true);
    }

    /// <summary>Builds the library entry to persist, using whatever system/body/ring/station the
    /// user ended up with (resolved via Spansh, or just free-typed text, or the initial prefill).</summary>
    public VideoLibraryEntry BuildEntry(string filePath)
    {
        return new VideoLibraryEntry
        {
            FilePath = filePath,
            SystemName = _selectedSystem?.Name ?? (SystemQuery.Trim().Length > 0 ? SystemQuery.Trim() : null),
            SystemId64 = _selectedSystem?.Id64,
            SystemX = _selectedSystem?.X,
            SystemY = _selectedSystem?.Y,
            SystemZ = _selectedSystem?.Z,
            BodyName = string.IsNullOrWhiteSpace(SelectedBodyName) ? null : SelectedBodyName,
            RingName = string.IsNullOrWhiteSpace(SelectedRingName) ? null : SelectedRingName,
            StationName = string.IsNullOrWhiteSpace(SelectedStationName) ? null : SelectedStationName,
            StationType = _stationOptions.FirstOrDefault(o => o.Name == SelectedStationName).Type,
        };
    }

    private async Task LoadDumpAsync(SpanshSearchSystem system, bool resetSelections)
    {
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            _dump = await _spanshClient.GetDumpAsync(system.Id64).ConfigureAwait(true);
            BodyNames.Clear();
            if (_dump is not null)
            {
                foreach (var body in _dump.System.Bodies)
                {
                    BodyNames.Add(body.Name);
                }
            }

            UpdateStationOptions(resetSelections);
            UpdateRingOptions(resetSelections);
        }
        catch (Exception ex)
        {
            AppLog.LogError("VideoUploadMetadataDumpLookup", ex);
            ErrorMessage = $"Lookup failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void UpdateStationOptions(bool resetSelection)
    {
        _stationOptions.Clear();
        StationNames.Clear();
        if (resetSelection)
        {
            SelectedStationName = null;
        }

        if (_dump is null)
        {
            return;
        }

        foreach (var station in _dump.System.Stations ?? new List<SpanshStation>())
        {
            AddStationOption(station);
        }

        foreach (var body in _dump.System.Bodies)
        {
            foreach (var station in body.Stations ?? new List<SpanshStation>())
            {
                AddStationOption(station);
            }
        }
    }

    private void AddStationOption(SpanshStation station)
    {
        _stationOptions.Add((station.Name, station.Type));
        StationNames.Add(station.Name);
    }

    private void UpdateRingOptions(bool resetSelection)
    {
        RingNames.Clear();
        if (resetSelection)
        {
            SelectedRingName = null;
        }

        var body = _dump?.System.Bodies.FirstOrDefault(b => b.Name == SelectedBodyName);
        if (body is not null)
        {
            foreach (var ring in body.Rings ?? new List<SpanshRingOrBelt>())
            {
                RingNames.Add(ring.Name);
            }

            foreach (var belt in body.Belts ?? new List<SpanshRingOrBelt>())
            {
                RingNames.Add(belt.Name);
            }
        }

        OnPropertyChanged(nameof(HasRingOptions));
    }
}
