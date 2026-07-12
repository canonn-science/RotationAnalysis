using VideoAnalysis.Core.Domain;
using VideoAnalysis.Core.Reference;
using VideoAnalysis.Core.Spansh.Models;
using Xunit;

namespace VideoAnalysis.Core.Tests;

public class StationParserTests
{
    private static SpanshDumpResponse MakeDump(
        List<SpanshStation>? systemStations = null, List<SpanshBody>? bodies = null)
        => new()
        {
            System = new SpanshSystem
            {
                Id64 = 12345,
                Name = "Test System",
                Coords = new SpanshCoords { X = 1.0, Y = 2.0, Z = 3.0 },
                Bodies = bodies ?? new List<SpanshBody>(),
                Stations = systemStations,
            },
        };

    private static List<StationInfo> Extract(SpanshDumpResponse dump, List<GuardianBeaconEntry>? beacons = null)
        => StationParser.ExtractStations(dump, beacons ?? new List<GuardianBeaconEntry>());

    [Fact]
    public void ExtractStations_IncludesOrbitalStarport_FromSystemLevelList()
    {
        var dump = MakeDump(systemStations: new List<SpanshStation>
        {
            new() { Name = "Daedalus", Type = "Orbis Starport" },
        });

        var result = Assert.Single(Extract(dump));

        Assert.Equal(StationKind.Station, result.Kind);
        Assert.Equal("Daedalus", result.StationName);
        // Spansh has no body link for orbital stations - body data is unresolvable by design.
        Assert.Null(result.BodyName);
        Assert.Null(result.EstimatedRotationSeconds);
    }

    [Fact]
    public void ExtractStations_IgnoresBodyNestedSurfaceStations_EvenIfPresent()
    {
        // Body-nested stations are surface ports (Planetary Outpost/Port, Settlement); per
        // "orbitals only" feedback these must never appear in the results, even though the data
        // is available.
        var body = new SpanshBody
        {
            Name = "Mercury",
            Type = "Planet",
            Radius = 2439.7,
            RotationalPeriod = 58.6,
            Stations = new List<SpanshStation>
            {
                new() { Name = "Walz Depot", Type = "Planetary Outpost" },
            },
        };
        var dump = MakeDump(bodies: new List<SpanshBody> { body });

        Assert.Empty(Extract(dump));
    }

    [Theory]
    [InlineData("Settlement")]
    [InlineData("Odyssey Settlement")]
    [InlineData("Planetary Outpost")]
    [InlineData("Planetary Port")]
    [InlineData("Fleet Carrier")]
    [InlineData("Mega ship")]
    public void ExtractStations_ExcludesNonOrbitalTypes(string excludedType)
    {
        var dump = MakeDump(systemStations: new List<SpanshStation>
        {
            new() { Name = "Excluded", Type = excludedType },
        });

        Assert.Empty(Extract(dump));
    }

    [Fact]
    public void ExtractStations_ClassifiesInstallationType()
    {
        var dump = MakeDump(systemStations: new List<SpanshStation>
        {
            new() { Name = "Comm Relay", Type = "Installation" },
        });

        var result = Assert.Single(Extract(dump));

        Assert.Equal(StationKind.Installation, result.Kind);
    }

    [Fact]
    public void ExtractStations_SkipsStationsWithNullType()
    {
        var dump = MakeDump(systemStations: new List<SpanshStation>
        {
            new() { Name = "Unclassified", Type = null },
        });

        Assert.Empty(Extract(dump));
    }

    [Fact]
    public void ExtractStations_SynthesizesGuardianBeaconEntry_ResolvingBodyFromSameSystemDump()
    {
        var body = new SpanshBody { Name = "Test System 2 A", Type = "Planet", Radius = 1000.0, RotationalPeriod = 3.0 };
        var dump = MakeDump(bodies: new List<SpanshBody> { body });
        var beacons = new List<GuardianBeaconEntry>
        {
            new() { SystemName = "Test System", BodyName = "Test System 2 A" },
        };

        var result = Assert.Single(Extract(dump, beacons));

        Assert.Equal(StationKind.GuardianBeacon, result.Kind);
        Assert.Equal("Test System 2 A", result.BodyName);
        Assert.Equal(1000.0, result.BodyRadiusKm);
        Assert.Equal(3.0 * 86_400.0, result.EstimatedRotationSeconds);
        Assert.Contains("Test System 2 A", result.StationName);
    }

    [Fact]
    public void ExtractStations_ResolvesGuardianBeaconStarRadius_FromSolarRadiusMultiplier()
    {
        var body = new SpanshBody { Name = "Test Star", Type = "Star", SolarRadius = 1.0, SolarMasses = 1.0 };
        var dump = MakeDump(bodies: new List<SpanshBody> { body });
        var beacons = new List<GuardianBeaconEntry>
        {
            new() { SystemName = "Test System", BodyName = "Test Star" },
        };

        var result = Assert.Single(Extract(dump, beacons));

        Assert.Equal(RingMath.SolarRadiusKm, result.BodyRadiusKm!.Value, precision: 3);
        Assert.Equal(1.0 * (RingMath.SolarMassKg / RingMath.EarthMassKg), result.BodyMassEarthMasses!.Value, precision: 3);
    }

    [Fact]
    public void ExtractStations_LeavesGuardianBeaconBodyDataNull_WhenBodyNotFoundInDump()
    {
        var dump = MakeDump();
        var beacons = new List<GuardianBeaconEntry>
        {
            new() { SystemName = "Test System", BodyName = "Unknown Body" },
        };

        var result = Assert.Single(Extract(dump, beacons));

        Assert.Null(result.BodyName);
        Assert.Null(result.EstimatedRotationSeconds);
    }
}
