using CsvHelper.Configuration.Attributes;

namespace VideoAnalysis.Core.Storage;

/// <summary>One row of the stations CSV. Column set matches the spec's Measurement Output list
/// (<c>Timestamp,System Name,id64,x,y,z,Body Name,Body Type,Body Mass,estimated rotation,
/// observed rotation,submitted,measured_period_s</c>) plus "Station Name", which the spec's own
/// Measurement History table requires displaying but the column list omits - added so history
/// rows are self-identifying rather than only distinguishable by body.</summary>
public sealed class StationMeasurementRecord
{
    [Name("Timestamp")]
    public DateTime Timestamp { get; set; }

    [Name("System Name")]
    public string SystemName { get; set; } = string.Empty;

    [Name("Station Name")]
    public string StationName { get; set; } = string.Empty;

    [Name("id64")]
    public long Id64 { get; set; }

    [Name("x")]
    public double X { get; set; }

    [Name("y")]
    public double Y { get; set; }

    [Name("z")]
    public double Z { get; set; }

    [Name("Body Name")]
    public string BodyName { get; set; } = string.Empty;

    [Name("Body Type")]
    public string BodyType { get; set; } = string.Empty;

    [Name("Body Mass")]
    public double? BodyMassEarthMasses { get; set; }

    /// <summary>Body radius, in kilometers. Not part of the spec's literal CSV field list but kept
    /// alongside the other body columns so the Measurement History view (which the spec requires
    /// to show Body Radius) doesn't need a second lookup back to Spansh/EDSM for historical rows.</summary>
    [Name("Body Radius")]
    public double? BodyRadiusKm { get; set; }

    /// <summary>Degrees.</summary>
    [Name("Body Inclination")]
    public double? BodyInclinationDegrees { get; set; }

    /// <summary>Seconds. Populated from the parent body's rotational period, per spec.</summary>
    [Name("estimated rotation")]
    public double EstimatedRotationSeconds { get; set; }

    /// <summary>Seconds. Video-measured rotation period.</summary>
    [Name("observed rotation")]
    public double ObservedRotationSeconds { get; set; }

    [Name("submitted")]
    public bool Submitted { get; set; }

    /// <summary>Seconds. Same value as <see cref="ObservedRotationSeconds"/> - kept as a distinct
    /// column because the spec lists both names explicitly.</summary>
    [Name("measured_period_s")]
    public double MeasuredPeriodSeconds { get; set; }

    [Name("video filename")]
    public string VideoFilename { get; set; } = string.Empty;
}
