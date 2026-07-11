using System.Collections.ObjectModel;
using RotationAnalysis.App.Infrastructure;
using RotationAnalysis.Core.Diagnostics;
using RotationAnalysis.Core.Domain;
using RotationAnalysis.Core.Spansh;
using RotationAnalysis.Core.Spansh.Models;
using RotationAnalysis.Core.VideoAnalysis;
using RotationAnalysis.Core.VideoAnalysis.LongExposure;

namespace RotationAnalysis.App.ViewModels;

/// <summary>Long Exposure's counterpart to <see cref="MainViewModel"/>/<see cref="StationViewModel"/>.
/// System search is the same Spansh-backed flow as the other modes; the object list combines
/// every body and orbital station in the system (<see cref="LongExposureTargetParser"/>) since
/// this mode just needs an identity for the output filename, not rotation data.</summary>
public sealed class LongExposureViewModel : ObservableObject, IDisposable
{
    private readonly SpanshClient _spanshClient = new();

    private string _systemQuery = string.Empty;
    private string? _errorMessage;
    private bool _isBusy;
    private string? _resolvedSystemName;

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

    public ObservableCollection<LongExposureRowViewModel> Targets { get; } = new();

    /// <summary>Raised when the user clicks "Select Video…" on a target row; the view handles the file picker.</summary>
    public event Action<LongExposureRowViewModel>? VideoSelectionRequested;

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
            AppLog.LogError("LongExposure.RefreshSuggestions", ex);
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
        Targets.Clear();
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

            var dump = await _spanshClient.GetDumpAsync(resolved.Id64).ConfigureAwait(true);
            if (dump is null)
            {
                ErrorMessage = $"System \"{resolved.Name}\" not found.";
                return;
            }

            var targets = LongExposureTargetParser.ExtractTargets(dump);
            ResolvedSystemName = resolved.Name;
            foreach (var target in targets)
            {
                Targets.Add(new LongExposureRowViewModel(target, row => VideoSelectionRequested?.Invoke(row)));
            }

            if (targets.Count == 0)
            {
                ErrorMessage = $"\"{resolved.Name}\" has no bodies or stations.";
            }
        }
        catch (Exception ex)
        {
            AppLog.LogError("LongExposure.SystemLookup", ex);
            ErrorMessage = $"Lookup failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public Task<LongExposureResult> GenerateAsync(string videoPath, IProgress<VideoAnalysisProgress> progress, CancellationToken ct)
        => LongExposureProcessor.GenerateAsync(videoPath, progress, ct);

    public void Dispose()
    {
        _spanshClient.Dispose();
    }
}
