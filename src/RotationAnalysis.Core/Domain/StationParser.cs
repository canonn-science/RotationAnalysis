using RotationAnalysis.Core.Reference;
using RotationAnalysis.Core.Spansh.Models;

namespace RotationAnalysis.Core.Domain;

public static class StationParser
{
    /// <summary>Spansh station types excluded from Station Rotation's object selection - per spec,
    /// only Stations, Guardian Beacons, and Installations are shown, and per follow-up feedback
    /// this is orbitals-only (no surface ports). Settlements/Planetary Outposts/Planetary Ports
    /// are all surface and don't rotate; player-owned Fleet Carriers and NPC Mega ships/rescue
    /// ships aren't stable long-term rotation subjects. In practice the system-level station list
    /// this method reads from never contains the "Planetary "/"Settlement" types anyway (see
    /// <see cref="ExtractStations"/>'s doc comment) - kept here too as a defensive, explicit list
    /// rather than relying solely on that data-shape coincidence.</summary>
    private static readonly HashSet<string> ExcludedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Settlement",
        "Odyssey Settlement",
        "Planetary Outpost",
        "Planetary Port",
        "Fleet Carrier",
        "Mega ship",
    };

    /// <summary>Only the system-level station list (orbital starports, mega/rescue ships) is used -
    /// deliberately excludes each body's own nested station list (surface Planetary Outposts/
    /// Ports/Settlements), per instruction that Station Rotation is orbitals-only, not surface
    /// ports. Convenient side effect of Spansh's own data split (see <see cref="Spansh.Models.SpanshSystem.Stations"/>'s
    /// doc comment): the system-level list is already exactly the orbital ones. Guardian Beacons
    /// from the reference sheet are added on top, resolving a body (for radius/rotational
    /// period/inclination) from the dump's body list by name.
    ///
    /// Because Spansh has no data linking an orbital starport to the body it orbits, ordinary
    /// station rows never get Body Radius/Rotational Period/Inclination/Estimated Rotation - only
    /// Guardian Beacon rows do, since the reference sheet supplies the body name directly.</summary>
    public static List<StationInfo> ExtractStations(SpanshDumpResponse dump, IReadOnlyList<GuardianBeaconEntry> beaconsInSystem)
    {
        var system = dump.System;
        var results = new List<StationInfo>();

        foreach (var station in system.Stations ?? Enumerable.Empty<SpanshStation>())
        {
            if (string.IsNullOrEmpty(station.Type) || ExcludedTypes.Contains(station.Type))
            {
                continue;
            }

            results.Add(BuildStationInfo(system, station.Name, ClassifyKind(station.Type), station.Type, body: null));
        }

        var bodiesByName = system.Bodies
            .GroupBy(b => b.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var beacon in beaconsInSystem)
        {
            bodiesByName.TryGetValue(beacon.BodyName, out var body);
            results.Add(BuildStationInfo(
                system, $"Guardian Beacon ({beacon.BodyName})", StationKind.GuardianBeacon, stationType: null, body));
        }

        return results;
    }

    private static StationKind ClassifyKind(string stationType) =>
        string.Equals(stationType, "Installation", StringComparison.OrdinalIgnoreCase)
            ? StationKind.Installation
            : StationKind.Station;

    private static StationInfo BuildStationInfo(
        SpanshSystem system, string stationName, StationKind kind, string? stationType, SpanshBody? body)
    {
        return new StationInfo
        {
            SystemName = system.Name,
            SystemId64 = system.Id64,
            SystemX = system.Coords.X,
            SystemY = system.Coords.Y,
            SystemZ = system.Coords.Z,
            StationName = stationName,
            Kind = kind,
            StationType = stationType,
            BodyName = body?.Name,
            BodyType = body is null ? null : body.SubType ?? body.Type,
            BodyRadiusKm = ResolveBodyRadiusKm(body),
            BodyMassEarthMasses = ResolveBodyMassEarthMasses(body),
            BodyRotationalPeriodDays = body?.RotationalPeriod,
            BodyInclinationDegrees = body?.OrbitalInclination,
        };
    }

    private static double? ResolveBodyRadiusKm(SpanshBody? body)
    {
        if (body is null)
        {
            return null;
        }
        if (body.Radius is double radiusKm)
        {
            return radiusKm;
        }
        if (body.SolarRadius is double solarRadius)
        {
            return solarRadius * RingMath.SolarRadiusKm;
        }
        return null;
    }

    private static double? ResolveBodyMassEarthMasses(SpanshBody? body)
    {
        if (body is null)
        {
            return null;
        }
        if (body.SolarMasses is double solar)
        {
            return solar * (RingMath.SolarMassKg / RingMath.EarthMassKg);
        }
        if (body.EarthMasses is double earth)
        {
            return earth;
        }
        return null;
    }
}
