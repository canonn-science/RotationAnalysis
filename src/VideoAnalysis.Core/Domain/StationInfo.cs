namespace VideoAnalysis.Core.Domain;

public enum StationKind
{
    Station,
    Installation,
    GuardianBeacon,
}

/// <summary>A single station, installation, or Guardian Beacon found in a system, with its
/// estimated rotation already computed - mirrors <see cref="RingInfo"/>'s shape.</summary>
public sealed class StationInfo
{
    public required string SystemName { get; init; }
    public required long SystemId64 { get; init; }
    public required double SystemX { get; init; }
    public required double SystemY { get; init; }
    public required double SystemZ { get; init; }

    public required string StationName { get; init; }
    public required StationKind Kind { get; init; }

    /// <summary>Raw station type as reported by EDSM (e.g. "Orbis Starport", "Outpost"). Null for
    /// synthesized Guardian Beacon entries, which have no EDSM station type.</summary>
    public string? StationType { get; init; }

    /// <summary>The body this station orbits or sits on. Null if EDSM has no body link for it
    /// (uncommon, but does happen for a handful of stations even beyond the well-known gap for
    /// Guardian Beacons, which always resolve a body via the reference sheet instead).</summary>
    public string? BodyName { get; init; }

    public string? BodyType { get; init; }
    public double? BodyRadiusKm { get; init; }
    public double? BodyMassEarthMasses { get; init; }

    /// <summary>Days, as reported by EDSM/Spansh.</summary>
    public double? BodyRotationalPeriodDays { get; init; }

    /// <summary>Degrees - the body's orbital inclination.</summary>
    public double? BodyInclinationDegrees { get; init; }

    /// <summary>Per spec, a station's estimated rotation is its parent body's rotational period
    /// (not a Kepler estimate the way ring rotation is) - simply that period expressed in seconds.
    /// Null if the parent body couldn't be resolved.</summary>
    public double? EstimatedRotationSeconds =>
        BodyRotationalPeriodDays is double days ? Math.Abs(days) * 86_400.0 : null;

    public int? SuggestedVideoDurationMinutes =>
        EstimatedRotationSeconds is double s ? RingMath.SuggestedVideoDurationMinutes(s) : null;

    public string DisplayKind => Kind switch
    {
        StationKind.GuardianBeacon => "Guardian Beacon",
        StationKind.Installation => "Installation",
        _ => StationType ?? "Station",
    };
}
