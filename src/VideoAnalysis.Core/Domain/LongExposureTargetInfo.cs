namespace VideoAnalysis.Core.Domain;

public enum LongExposureTargetKind
{
    Body,
    Station,
}

/// <summary>A selectable body or station within a system, for Long Exposure/Slit Scan's "select a
/// system, then a body or station" workflow. Deliberately minimal compared to
/// <see cref="RingInfo"/>/<see cref="StationInfo"/> - this mode only needs an identity to build a
/// suggested output filename from, not any physical/rotation data.</summary>
public sealed class LongExposureTargetInfo
{
    public required string SystemName { get; init; }
    public required string ObjectName { get; init; }
    public required LongExposureTargetKind Kind { get; init; }
    public string? ObjectType { get; init; }

    public string DisplayKind => Kind == LongExposureTargetKind.Station ? "Station" : "Body";
}
