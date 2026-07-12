using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;

namespace VideoAnalysis.Core.Storage;

/// <summary>Parallel to <see cref="MeasurementCsvStore"/> for Station Rotation measurements,
/// writing <c>stations.csv</c>. Not shared/generic with the ring store - same non-generic pattern
/// the codebase already uses, since the two record shapes only partially overlap and a shared
/// base would need as much indirection as it saves.</summary>
public sealed class StationMeasurementCsvStore
{
    public string CsvPath { get; }

    public StationMeasurementCsvStore(string? csvPath = null)
    {
        CsvPath = csvPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RotationAnalysisLab",
            "stations.csv");
    }

    public void Append(StationMeasurementRecord record)
    {
        var records = ReadAll();
        records.Add(record);
        SaveAll(records);
    }

    public void SaveAll(IEnumerable<StationMeasurementRecord> records)
    {
        var directory = Path.GetDirectoryName(CsvPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var stream = new FileStream(CsvPath, FileMode.Create, FileAccess.Write, FileShare.Read);
        using var writer = new StreamWriter(stream);
        using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);

        csv.WriteHeader<StationMeasurementRecord>();
        csv.NextRecord();
        foreach (var record in records)
        {
            csv.WriteRecord(record);
            csv.NextRecord();
        }
    }

    public List<StationMeasurementRecord> ReadAll()
    {
        if (!File.Exists(CsvPath))
        {
            return new List<StationMeasurementRecord>();
        }

        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HeaderValidated = null,
            MissingFieldFound = null,
        };
        using var reader = new StreamReader(CsvPath);
        using var csv = new CsvReader(reader, config);
        return csv.GetRecords<StationMeasurementRecord>().ToList();
    }
}
