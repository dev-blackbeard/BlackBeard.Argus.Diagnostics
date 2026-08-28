using System;

namespace Argus.Testing;

/// <summary>
/// The inputs a synthetic stream is generated from.
/// </summary>
/// <remarks>
/// <para>
/// Every value here is an <b>input</b> with a neutral default, and that is the whole point of
/// the type. Origin, spacing, entity count and update rate are exactly the things that
/// describe a real deployment, so this repository holds the shape of them and never a value:
/// the defaults are an origin of <c>(0, 0)</c>, a round number of entities and round metric
/// spacings, none of which describe anything.
/// </para>
/// <para>
/// Real values belong to whoever is consuming this library, in their own private
/// configuration.
/// </para>
/// </remarks>
public sealed class ScenarioDefinition
{
    /// <summary>A name for the scenario, used in test output.</summary>
    public string Name { get; set; } = "synthetic";

    /// <summary>Latitude the group is generated around, in degrees.</summary>
    /// <remarks>The origin. Deliberately not anywhere.</remarks>
    public double OriginLatitude { get; set; }

    /// <summary>Longitude the group is generated around, in degrees.</summary>
    /// <remarks>The origin. Deliberately not anywhere.</remarks>
    public double OriginLongitude { get; set; }

    /// <summary>Altitude the group is generated at, in metres.</summary>
    public double OriginAltitude { get; set; } = 1000.0;

    /// <summary>How many entities the group contains.</summary>
    public int EntityCount { get; set; } = 8;

    /// <summary>How far apart consecutive entities are placed, in metres.</summary>
    public double SpacingMeters { get; set; } = 500.0;

    /// <summary>
    /// How far the first entity is offset from the origin, in metres.
    /// </summary>
    /// <remarks>
    /// Non-zero so that no generated entity sits at exactly <c>(0, 0)</c>, which
    /// <c>PositionValidity</c> treats as an uninitialised value by default. A synthetic
    /// stream should exercise the detectors, not the sentinel rule.
    /// </remarks>
    public double OriginOffsetMeters { get; set; } = 250.0;

    /// <summary>The interval between ticks, in seconds.</summary>
    public double UpdateIntervalSeconds { get; set; } = 1.0;

    /// <summary>How many ticks the scenario runs for.</summary>
    public int TickCount { get; set; } = 60;

    /// <summary>How fast the group travels, in metres per second.</summary>
    public double SpeedMetersPerSecond { get; set; } = 50.0;

    /// <summary>The bearing the group travels along, in degrees clockwise from north.</summary>
    public double HeadingDegrees { get; set; } = 90.0;

    /// <summary>The time the first tick is stamped with.</summary>
    public DateTime StartTimeUtc { get; set; } = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>The prefix generated entity identifiers carry.</summary>
    public string EntityIdPrefix { get; set; } = "entity-";

    /// <summary>The seed for any randomised injector, so scenarios are reproducible.</summary>
    public int Seed { get; set; } = 1;

    /// <summary>Whether generated samples carry sequence numbers.</summary>
    public bool IncludeSequenceNumbers { get; set; } = true;

    /// <summary>Whether generated samples carry attitude as a normalised quaternion.</summary>
    public bool IncludeAttitude { get; set; } = true;

    /// <summary>Whether generated samples carry reported velocity.</summary>
    public bool IncludeVelocity { get; set; } = true;

    /// <summary>Creates a copy of this definition.</summary>
    /// <returns>An independent copy.</returns>
    public ScenarioDefinition Clone()
    {
        return (ScenarioDefinition)MemberwiseClone();
    }
}
