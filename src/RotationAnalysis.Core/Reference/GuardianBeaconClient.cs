using System.Globalization;

namespace RotationAnalysis.Core.Reference;

/// <summary>Downloads and parses the published Guardian Beacon reference TSV. Same
/// header-name-lookup parsing approach as <c>CanonnClient.ParseTsv</c>, so column order/additions
/// on the sheet don't break this. The sheet rarely changes within a session, so callers should
/// fetch once and reuse the result rather than re-downloading per search.</summary>
public sealed class GuardianBeaconClient : IDisposable
{
    private const string BeaconTsvUrl =
        "https://docs.google.com/spreadsheets/d/e/2PACX-1vS7aHQ4uA9BdY7cPfrpswkt1tNi5m1zbqqVXPhsvRgMu3gGaRXGR5G3jlS4s7WhD0uXqnwlBaf9NEq_/pub?gid=1753875337&single=true&output=tsv";

    private readonly HttpClient _http;

    public GuardianBeaconClient(HttpClient? httpClient = null)
    {
        _http = httpClient ?? new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30),
        };
    }

    public async Task<List<GuardianBeaconEntry>> GetBeaconsAsync(CancellationToken ct = default)
    {
        using var response = await _http.GetAsync(BeaconTsvUrl, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var tsv = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        return ParseTsv(tsv);
    }

    private static List<GuardianBeaconEntry> ParseTsv(string tsv)
    {
        var result = new List<GuardianBeaconEntry>();
        using var reader = new StringReader(tsv);
        var headerLine = reader.ReadLine();
        if (headerLine is null)
        {
            return result;
        }

        var headers = headerLine.Split('\t');
        int siteIdIdx = IndexOf(headers, "SiteId");
        int systemIdx = IndexOf(headers, "System Name");
        int xIdx = IndexOf(headers, "x");
        int yIdx = IndexOf(headers, "y");
        int zIdx = IndexOf(headers, "z");
        int regionIdx = IndexOf(headers, "Region");
        int primaryStarIdx = IndexOf(headers, "Primary Star");
        int bodyNameIdx = IndexOf(headers, "Body Name");
        int bodySubTypeIdx = IndexOf(headers, "Body Sub Type");
        int distanceIdx = IndexOf(headers, "Distance To Arrival");
        int structureSystemIdx = IndexOf(headers, "Guardian Structure System");
        int structureBodyIdx = IndexOf(headers, "Guardian Structure Body");
        int reportedByIdx = IndexOf(headers, "Reported By");

        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (line.Length == 0)
            {
                continue;
            }

            var fields = line.Split('\t');
            result.Add(new GuardianBeaconEntry
            {
                SiteId = Field(fields, siteIdIdx),
                SystemName = Field(fields, systemIdx),
                X = ParseDouble(Field(fields, xIdx)),
                Y = ParseDouble(Field(fields, yIdx)),
                Z = ParseDouble(Field(fields, zIdx)),
                Region = Field(fields, regionIdx),
                PrimaryStar = Field(fields, primaryStarIdx),
                BodyName = Field(fields, bodyNameIdx),
                BodySubType = Field(fields, bodySubTypeIdx),
                DistanceToArrival = ParseDouble(Field(fields, distanceIdx)),
                GuardianStructureSystem = Field(fields, structureSystemIdx),
                GuardianStructureBody = Field(fields, structureBodyIdx),
                ReportedBy = Field(fields, reportedByIdx),
            });
        }

        return result;
    }

    private static int IndexOf(string[] headers, string name) =>
        Array.FindIndex(headers, h => string.Equals(h.Trim(), name, StringComparison.OrdinalIgnoreCase));

    private static string Field(string[] fields, int index) =>
        index >= 0 && index < fields.Length ? fields[index].Trim() : string.Empty;

    private static double ParseDouble(string s) =>
        double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : 0.0;

    public void Dispose() => _http.Dispose();
}
