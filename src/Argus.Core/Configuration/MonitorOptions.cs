using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Argus.Configuration;

/// <summary>
/// How a monitor behaves: what it remembers, which detectors it runs, and how it resolves
/// position out of an application's own entity types.
/// </summary>
/// <remarks>
/// Thresholds are deliberately a separate object (<see cref="Thresholds"/>) because they
/// have a different lifecycle: options are set up once at composition time, whereas
/// thresholds are tuned per environment and loaded from configuration.
/// </remarks>
public sealed class MonitorOptions
{
    /// <summary>The default property names the convention accessor tries for latitude.</summary>
    public static IReadOnlyList<string> DefaultLatitudeCandidates { get; } = new[]
    {
        "Latitude", "LatitudeWgs84", "LatitudeDegrees", "LatitudeDeg", "Lat", "LatDeg", "Y",
    };

    /// <summary>The default property names the convention accessor tries for longitude.</summary>
    public static IReadOnlyList<string> DefaultLongitudeCandidates { get; } = new[]
    {
        "Longitude", "LongitudeWgs84", "LongitudeDegrees", "LongitudeDeg", "Lon", "Lng", "LonDeg", "X",
    };

    /// <summary>The default property names the convention accessor tries for altitude.</summary>
    public static IReadOnlyList<string> DefaultAltitudeCandidates { get; } = new[]
    {
        "Altitude", "AltitudeMeters", "AltitudeMetres", "Alt", "Elevation", "Height", "Z",
    };

    /// <summary>The default property names the convention accessor tries for identity.</summary>
    public static IReadOnlyList<string> DefaultIdentityCandidates { get; } = new[]
    {
        "EntityId", "Id", "Identifier", "Key", "Name",
    };

    /// <summary>The numbers the detectors compare against.</summary>
    public DetectorThresholds Thresholds { get; set; } = new DetectorThresholds();

    /// <summary>Accessors the application has registered for its own entity types.</summary>
    public EntityAccessorRegistry Accessors { get; } = new EntityAccessorRegistry();

    /// <summary>Property names the convention accessor tries for latitude, in order.</summary>
    public IList<string> LatitudeCandidates { get; set; } = new List<string>(DefaultLatitudeCandidates);

    /// <summary>Property names the convention accessor tries for longitude, in order.</summary>
    public IList<string> LongitudeCandidates { get; set; } = new List<string>(DefaultLongitudeCandidates);

    /// <summary>Property names the convention accessor tries for altitude, in order.</summary>
    public IList<string> AltitudeCandidates { get; set; } = new List<string>(DefaultAltitudeCandidates);

    /// <summary>Property names the convention accessor tries for identity, in order.</summary>
    public IList<string> IdentityCandidates { get; set; } = new List<string>(DefaultIdentityCandidates);

    /// <summary>The hard cap on how many entities retain state at once.</summary>
    /// <remarks>
    /// Ten thousand. Each track is a few hundred bytes plus its retained history, so this is
    /// a small number of megabytes — chosen to be far above any plausible working set while
    /// still being a bound, because the failure this replaces was unbounded growth driven by
    /// however many distinct identifiers the stream ever mentions.
    /// </remarks>
    public int MaxTrackedEntities { get; set; } = 10000;

    /// <summary>How long an entity may go unmentioned before its state is discarded.</summary>
    /// <remarks>
    /// Five minutes. Long enough that an entity dropping out and returning keeps its history,
    /// short enough that a churning population does not accumulate.
    /// </remarks>
    public TimeSpan TrackIdleTimeout { get; set; } = TimeSpan.FromMinutes(5.0);

    /// <summary>How many recent valid positions each track retains.</summary>
    /// <remarks>Sixteen: above the largest window any detector asks for, and still O(1) memory per entity.</remarks>
    public int TrackHistoryCapacity { get; set; } = 16;

    /// <summary>
    /// How long a group tick context may be reused by the compatibility facade before it is
    /// rebuilt.
    /// </summary>
    /// <remarks>
    /// The facade's call site is per entity, but the group statistics are per tick, so the
    /// facade caches the context it builds and reuses it for the rest of the tick. This is
    /// the staleness bound on that reuse. Callers using <c>IEntityStreamMonitor</c> directly
    /// build one context per tick explicitly and are unaffected.
    /// </remarks>
    public TimeSpan GroupContextCacheDuration { get; set; } = TimeSpan.FromMilliseconds(200.0);

    /// <summary>
    /// Detector identifiers to skip.
    /// </summary>
    /// <remarks>
    /// Disabling a detector removes its findings entirely — including its
    /// <c>NotEvaluable</c> findings — so a report from a monitor with disabled detectors is
    /// silent about them. Prefer leaving them enabled and reading the outcome.
    /// </remarks>
    public ISet<string> DisabledDetectors { get; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>
    /// Whether unimplemented detectors appear in reports as <c>NotEvaluable</c> findings.
    /// </summary>
    /// <remarks>
    /// Off by default, so an ordinary report is not padded with the catalogue's backlog. Turn
    /// it on to see exactly which conditions are not being checked — which is the honest
    /// thing to look at before claiming a stream is clean.
    /// </remarks>
    public bool IncludeUnimplementedDetectors { get; set; }

    /// <summary>
    /// Whether healthy findings are included in reports alongside flagged ones.
    /// </summary>
    /// <remarks>
    /// Off by default: a report is normally read for what is wrong. Turn it on when the
    /// question is "what was actually checked", such as when producing evidence for the team
    /// producing the stream.
    /// </remarks>
    public bool IncludeHealthyFindings { get; set; }

    /// <summary>
    /// The per-type accessor cache backing convention resolution.
    /// </summary>
    /// <remarks>
    /// Held on the options rather than statically because the candidate name lists are part
    /// of the options: two monitors configured with different candidates must not share a
    /// resolved accessor.
    /// </remarks>
    internal ConcurrentDictionary<Type, object> AccessorCache { get; } = new ConcurrentDictionary<Type, object>();
}
