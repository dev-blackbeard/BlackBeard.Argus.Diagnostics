using System;
using System.Collections.Generic;
using System.Globalization;
using Argus.Configuration;
using Argus.Contracts;
using Argus.Internal;
using Argus.State;
using Microsoft.Maui.Graphics;

namespace Argus.Graphics;

/// <summary>
/// The colour-returning compatibility facade over <see cref="IEntityStreamMonitor"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>A compatibility shim.</b> See <see cref="IEntityHealthMonitor"/> for why it looks the
/// way it does, and prefer <see cref="Argus.Pipeline.EntityHealthMonitor"/> —
/// the diagnostics engine this wraps — in new code.
/// </para>
/// <para>
/// Note that this type and <c>Argus.Pipeline.EntityHealthMonitor</c> share a name across two
/// namespaces and two assemblies. That is deliberate and it is a migration aid: an
/// application swapping its old monitor for this one changes a <c>using</c>, not every call
/// site. Once the call sites have moved to <see cref="IEntityStreamMonitor"/>, the ambiguity
/// goes away with them.
/// </para>
/// </remarks>
public sealed class EntityHealthMonitor : IEntityHealthMonitor
{
    private readonly GroupTickContextCache _groupCache = new GroupTickContextCache();
    private readonly object _thresholdGate = new object();

    private DetectorThresholds? _perCallThresholds;
    private double _cachedTeleportDistanceMeters = double.NaN;
    private double _cachedEntityRadiusMeters = double.NaN;
    private double? _cachedMaxSpeedMetersPerSecond;

    /// <summary>Creates a facade over a new monitor with default options.</summary>
    public EntityHealthMonitor()
        : this(new MonitorOptions())
    {
    }

    /// <summary>Creates a facade over a new monitor.</summary>
    /// <param name="options">
    /// How the monitor behaves. Register accessors for the application's entity types on
    /// <c>MonitorOptions.Accessors</c> here — that is the second of the three position
    /// resolution routes, and the one to use when the model types cannot reference Argus.
    /// </param>
    /// <param name="colors">How reports become colours. A default policy is used when <c>null</c>.</param>
    /// <param name="subtitles">How reports become subtitles. A default formatter is used when <c>null</c>.</param>
    public EntityHealthMonitor(MonitorOptions options, ColorPolicy? colors = null, SubtitleFormatter? subtitles = null)
        : this(new global::Argus.Pipeline.EntityHealthMonitor(options), colors, subtitles)
    {
    }

    /// <summary>Creates a facade over an existing monitor.</summary>
    /// <param name="monitor">The diagnostics engine to wrap.</param>
    /// <param name="colors">How reports become colours. A default policy is used when <c>null</c>.</param>
    /// <param name="subtitles">How reports become subtitles. A default formatter is used when <c>null</c>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="monitor"/> is <c>null</c>.</exception>
    public EntityHealthMonitor(IEntityStreamMonitor monitor, ColorPolicy? colors = null, SubtitleFormatter? subtitles = null)
    {
        if (monitor == null)
        {
            throw new ArgumentNullException(nameof(monitor));
        }

        this.Monitor = monitor;
        this.Colors = colors ?? new ColorPolicy();
        this.Subtitles = subtitles ?? new SubtitleFormatter();
    }

    /// <inheritdoc />
    public IEntityStreamMonitor Monitor { get; }

    /// <inheritdoc />
    public ColorPolicy Colors { get; }

    /// <inheritdoc />
    public SubtitleFormatter Subtitles { get; }

    /// <summary>The report produced by the most recent <see cref="SetStatusColor"/> call, if any.</summary>
    /// <remarks>
    /// A convenience for a caller that wants the findings behind a colour without moving off
    /// the facade yet. It is per-instance rather than per-entity, so read it immediately.
    /// </remarks>
    public EntityHealthReport? LastReport { get; private set; }

    /// <inheritdoc />
    public Color SetStatusColor<TId, TEntity>(
        TId entityId,
        double latitude,
        double longitude,
        double altitude,
        DateTime timestamp,
        IEnumerable<TEntity> allEntities,
        double teleportDistanceMeters,
        double entityRadiusMeters,
        out string debugSubTitle,
        double? maxSpeedMetersPerSecond = null)
    {
        // One box per call when TId is a value type. The allocation-free routes are
        // IEntityStreamMonitor.Observe, which takes an EntitySample carrying a string id, and
        // IArgusEntity. Both are documented as the direction of travel.
        string id = Convert.ToString(entityId, CultureInfo.InvariantCulture) ?? string.Empty;

        // Qualified with `this` throughout: Monitor and Colors are also the names of types in
        // System.Threading and Microsoft.Maui.Graphics respectively. The members win by the
        // language's own rules, but a reader should not have to know that.
        MonitorOptions options = this.Monitor.Options;
        DetectorThresholds thresholds = GetPerCallThresholds(
            options,
            teleportDistanceMeters,
            entityRadiusMeters,
            maxSpeedMetersPerSecond);

        var sample = new EntitySample(id, timestamp)
        {
            Latitude = latitude,
            Longitude = longitude,
            Altitude = altitude,
        };

        Func<TEntity, EntitySnapshot> accessor = EntityAccessorFactory.Resolve<TEntity>(options);

        GroupTickContext group = _groupCache.GetOrBuild(
            allEntities,
            timestamp,
            options.GroupContextCacheDuration,
            accessor,
            thresholds.TreatZeroIslandAsSentinel);

        EntityHealthReport report = this.Monitor.Observe(sample, group, thresholds);
        LastReport = report;

        // Assigned exactly once, from exactly one source. The prototype assigned its subtitle
        // twice and the first assignment was dead code that had drifted out of agreement with
        // the second.
        debugSubTitle = this.Subtitles.Format(report);

        return this.Colors.Resolve(report);
    }

    /// <inheritdoc />
    public Color ColorFor(EntityHealthReport report)
    {
        return this.Colors.Resolve(report);
    }

    /// <inheritdoc />
    public string SubtitleFor(EntityHealthReport report)
    {
        return this.Subtitles.Format(report);
    }

    /// <summary>Discards the cached group context, forcing the next call to rebuild it.</summary>
    /// <remarks>Call this if the contents of a collection change without the instance changing.</remarks>
    public void InvalidateGroupCache()
    {
        _groupCache.Invalidate();
    }

    private DetectorThresholds GetPerCallThresholds(
        MonitorOptions options,
        double teleportDistanceMeters,
        double entityRadiusMeters,
        double? maxSpeedMetersPerSecond)
    {
        lock (_thresholdGate)
        {
            bool unchanged = _perCallThresholds != null
                && _cachedTeleportDistanceMeters.Equals(teleportDistanceMeters)
                && _cachedEntityRadiusMeters.Equals(entityRadiusMeters)
                && Nullable.Equals(_cachedMaxSpeedMetersPerSecond, maxSpeedMetersPerSecond);

            if (unchanged)
            {
                return _perCallThresholds!;
            }

            // Cloned rather than mutated: the facade receives its gates per call, and the
            // monitor's own thresholds are shared with every other caller of the same monitor.
            DetectorThresholds thresholds = options.Thresholds.Clone();
            thresholds.MaxTeleportDistanceMeters = teleportDistanceMeters;
            thresholds.GroupOutlierRadiusMeters = entityRadiusMeters;

            // Both gates, always. Only overridden when the caller supplied one, so a monitor
            // configured with a rate gate keeps it even from a call site that cannot pass one.
            if (maxSpeedMetersPerSecond.HasValue)
            {
                thresholds.MaxSpeedMetersPerSecond = maxSpeedMetersPerSecond;
            }

            _perCallThresholds = thresholds;
            _cachedTeleportDistanceMeters = teleportDistanceMeters;
            _cachedEntityRadiusMeters = entityRadiusMeters;
            _cachedMaxSpeedMetersPerSecond = maxSpeedMetersPerSecond;

            return thresholds;
        }
    }
}
