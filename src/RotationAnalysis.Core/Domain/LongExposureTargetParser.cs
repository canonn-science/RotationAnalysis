using RotationAnalysis.Core.Reference;
using RotationAnalysis.Core.Spansh.Models;

namespace RotationAnalysis.Core.Domain;

public static class LongExposureTargetParser
{
    /// <summary>Every body in the system plus every orbital station (reusing
    /// <see cref="StationParser"/>'s existing orbital-only filter, with no Guardian Beacon
    /// lookup - beacons aren't a meaningful "body or station" subject for this mode).</summary>
    public static List<LongExposureTargetInfo> ExtractTargets(SpanshDumpResponse dump)
    {
        var results = new List<LongExposureTargetInfo>();

        foreach (var body in dump.System.Bodies)
        {
            results.Add(new LongExposureTargetInfo
            {
                SystemName = dump.System.Name,
                ObjectName = body.Name,
                Kind = LongExposureTargetKind.Body,
                ObjectType = body.SubType ?? body.Type,
            });
        }

        var stations = StationParser.ExtractStations(dump, Array.Empty<GuardianBeaconEntry>());
        foreach (var station in stations)
        {
            results.Add(new LongExposureTargetInfo
            {
                SystemName = dump.System.Name,
                ObjectName = station.StationName,
                Kind = LongExposureTargetKind.Station,
                ObjectType = station.DisplayKind,
            });
        }

        return results;
    }
}
