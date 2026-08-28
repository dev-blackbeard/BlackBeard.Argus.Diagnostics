using System;
using System.Collections.Generic;
using Argus.Configuration;
using Argus.State;

namespace Argus.Contracts;

/// <summary>
/// The primary Argus API: feed it samples, get back findings.
/// </summary>
/// <remarks>
/// <para>
/// This is the interface new code should use. The colour-returning facade in
/// <c>Argus.Graphics</c> exists to keep one existing call site compiling and is a
/// compatibility shim over this.
/// </para>
/// <para>
/// The two-call shape — <see cref="CreateTickContext"/> once, then
/// <see cref="Observe"/> per entity — is the point of the interface rather than an
/// inconvenience. Group statistics are a property of the tick, not of the entity, and
/// computing them per entity is both O(n²) and wrong: an entity dragged toward its own
/// centroid understates its distance from the group. Building the context once makes the
/// cost linear and the self-exclusion exact.
/// </para>
/// <para>
/// Threading: see <c>docs/threading.md</c>. In short, one thread per entity at a time.
/// </para>
/// </remarks>
public interface IEntityStreamMonitor
{
    /// <summary>The options this monitor was configured with.</summary>
    MonitorOptions Options { get; }

    /// <summary>The per-entity state this monitor is holding.</summary>
    TrackStore Tracks { get; }

    /// <summary>
    /// Materialises one tick's group statistics, enumerating the sequence exactly once.
    /// </summary>
    /// <param name="samples">Every sample in the tick, including the ones about to be observed.</param>
    /// <param name="tickTimeUtc">The time to stamp the tick with.</param>
    /// <returns>The context to pass to <see cref="Observe"/> for every entity in this tick.</returns>
    GroupTickContext CreateTickContext(IEnumerable<EntitySample> samples, DateTime tickTimeUtc);

    /// <summary>
    /// Observes one sample and reports what is wrong with it.
    /// </summary>
    /// <param name="sample">The sample.</param>
    /// <param name="group">
    /// The tick's group statistics, or <c>null</c> when the caller has no group — in which
    /// case the group detectors report <c>NotEvaluable</c> rather than being skipped.
    /// </param>
    /// <param name="thresholds">
    /// Thresholds for this observation only, or <c>null</c> to use
    /// <see cref="MonitorOptions.Thresholds"/>. Supplying these does not mutate the
    /// monitor's own thresholds.
    /// </param>
    /// <returns>The report for this sample.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="sample"/> is <c>null</c>.</exception>
    EntityHealthReport Observe(EntitySample sample, GroupTickContext? group = null, DetectorThresholds? thresholds = null);

    /// <summary>Returns the state held for an entity, if any.</summary>
    /// <param name="entityId">The entity.</param>
    /// <param name="track">The entity's state, if present.</param>
    /// <returns><c>true</c> if state was found.</returns>
    bool TryGetTrack(string entityId, out EntityTrack? track);

    /// <summary>Discards the state held for one entity.</summary>
    /// <param name="entityId">The entity.</param>
    /// <returns><c>true</c> if state was present and has been discarded.</returns>
    bool Forget(string entityId);

    /// <summary>Discards all state, returning the monitor to its initial condition.</summary>
    void Reset();
}
