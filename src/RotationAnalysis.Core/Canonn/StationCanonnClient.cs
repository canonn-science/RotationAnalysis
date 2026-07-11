namespace RotationAnalysis.Core.Canonn;

/// <summary>
/// Station Rotation's counterpart to <see cref="CanonnClient"/>. Not wired into the UI yet - the
/// spec calls out that the submission URL for station measurements is "to be supplied" separately
/// from the ring form. Left as a stub with the same shape so wiring it up later is a small,
/// isolated change once the URL/entry IDs are known, rather than a new client to design from
/// scratch.
/// </summary>
public sealed class StationCanonnClient : IDisposable
{
    // TODO: Canonn has not yet supplied a form URL/entry-ID mapping for station measurements.
    // Once supplied, this should mirror CanonnClient.SubmitAsync's query-string construction.
    private const string FormResponseUrl = "";

    private readonly HttpClient _http;

    public StationCanonnClient(HttpClient? httpClient = null)
    {
        _http = httpClient ?? new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30),
        };
    }

    public Task SubmitAsync(CanonnSubmission submission, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(FormResponseUrl))
        {
            throw new NotSupportedException(
                "Station measurement submission to Canonn isn't available yet - no form URL has been supplied for this mode.");
        }

        throw new NotImplementedException();
    }

    public void Dispose() => _http.Dispose();
}
