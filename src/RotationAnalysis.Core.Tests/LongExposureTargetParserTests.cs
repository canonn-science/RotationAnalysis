using RotationAnalysis.Core.Domain;
using RotationAnalysis.Core.Spansh.Models;
using Xunit;

namespace RotationAnalysis.Core.Tests;

public class LongExposureTargetParserTests
{
    private static SpanshDumpResponse MakeDump(List<SpanshBody>? bodies = null, List<SpanshStation>? systemStations = null)
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

    [Fact]
    public void ExtractTargets_IncludesEveryBody_RegardlessOfRingsOrType()
    {
        var dump = MakeDump(bodies: new List<SpanshBody>
        {
            new() { Name = "Test System A", Type = "Star" },
            new() { Name = "Test System A 1", Type = "Planet", SubType = "Icy body" },
        });

        var targets = LongExposureTargetParser.ExtractTargets(dump);

        Assert.Equal(2, targets.Count);
        Assert.All(targets, t => Assert.Equal(LongExposureTargetKind.Body, t.Kind));
    }

    [Fact]
    public void ExtractTargets_IncludesOrbitalStations_ButNotSurfaceOrExcludedTypes()
    {
        var dump = MakeDump(systemStations: new List<SpanshStation>
        {
            new() { Name = "Daedalus", Type = "Orbis Starport" },
            new() { Name = "Some Settlement", Type = "Settlement" },
        });

        var targets = LongExposureTargetParser.ExtractTargets(dump);

        var station = Assert.Single(targets);
        Assert.Equal(LongExposureTargetKind.Station, station.Kind);
        Assert.Equal("Daedalus", station.ObjectName);
    }

    [Fact]
    public void ExtractTargets_CombinesBodiesAndStations()
    {
        var dump = MakeDump(
            bodies: new List<SpanshBody> { new() { Name = "Test System A", Type = "Star" } },
            systemStations: new List<SpanshStation> { new() { Name = "Daedalus", Type = "Orbis Starport" } });

        var targets = LongExposureTargetParser.ExtractTargets(dump);

        Assert.Equal(2, targets.Count);
        Assert.Contains(targets, t => t.Kind == LongExposureTargetKind.Body);
        Assert.Contains(targets, t => t.Kind == LongExposureTargetKind.Station);
    }
}
