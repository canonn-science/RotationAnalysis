namespace VideoAnalysis.Core.Reference;

/// <summary>One row of the community-maintained Guardian Beacon reference sheet. Spansh/EDSM don't
/// carry Guardian Beacons as stations at all (they're a signal source, not a dockable station), so
/// this is the only source of "which systems have one" for Station Rotation's object selection.</summary>
public sealed class GuardianBeaconEntry
{
    public string SiteId { get; init; } = string.Empty;
    public string SystemName { get; init; } = string.Empty;
    public double X { get; init; }
    public double Y { get; init; }
    public double Z { get; init; }
    public string Region { get; init; } = string.Empty;
    public string PrimaryStar { get; init; } = string.Empty;

    /// <summary>The body the beacon itself sits on/near - used to resolve rotation-relevant body
    /// data (radius/rotational period/inclination) from the same system data Station Rotation
    /// already has.</summary>
    public string BodyName { get; init; } = string.Empty;
    public string BodySubType { get; init; } = string.Empty;
    public double DistanceToArrival { get; init; }
    public string GuardianStructureSystem { get; init; } = string.Empty;
    public string GuardianStructureBody { get; init; } = string.Empty;
    public string ReportedBy { get; init; } = string.Empty;
}
