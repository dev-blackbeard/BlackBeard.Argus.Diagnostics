using System;
using System.Collections.Generic;
using Argus.Contracts;
using Microsoft.Maui.Graphics;

namespace Argus.Graphics;

/// <summary>
/// The colour-returning compatibility facade.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is a compatibility shim, not the primary API.</b> It exists so that an existing
/// application call site keeps compiling character-for-character while the diagnostics
/// underneath it are replaced. New code should use <see cref="IEntityStreamMonitor"/>
/// directly: it returns findings rather than a colour, it lets the caller build one
/// <c>GroupTickContext</c> per tick instead of once per entity, and it does not force a set
/// of conditions down to a single pixel before the caller has seen them.
/// </para>
/// <para>
/// The shape of <see cref="SetStatusColor"/> is fixed by the call site it has to keep
/// compiling and is not otherwise defensible. In particular the <c>out</c> parameter sits in
/// the middle of the signature because the original call passes it positionally after eight
/// named arguments, which pins it to position nine.
/// </para>
/// </remarks>
public interface IEntityHealthMonitor
{
    /// <summary>The diagnostics engine underneath the facade.</summary>
    /// <remarks>Reach for this to migrate a call site off the facade.</remarks>
    IEntityStreamMonitor Monitor { get; }

    /// <summary>How reports are turned into colours.</summary>
    ColorPolicy Colors { get; }

    /// <summary>How reports are turned into debug subtitles.</summary>
    SubtitleFormatter Subtitles { get; }

    /// <summary>
    /// Observes one entity's position and returns the colour to draw it in.
    /// </summary>
    /// <typeparam name="TId">The application's identifier type. Inferred; never specify it.</typeparam>
    /// <typeparam name="TEntity">The application's entity type. Inferred; never specify it.</typeparam>
    /// <param name="entityId">The entity's identity, rendered with the invariant culture.</param>
    /// <param name="latitude">Latitude in degrees, positive north.</param>
    /// <param name="longitude">Longitude in degrees, positive east.</param>
    /// <param name="altitude">Altitude in metres.</param>
    /// <param name="timestamp">When the sample arrived.</param>
    /// <param name="allEntities">
    /// Every entity in the same group this tick, used for the group checks. Pass the same
    /// collection instance for every entity in a tick and it is enumerated once; pass a fresh
    /// sequence per entity and it is enumerated per entity.
    /// </param>
    /// <param name="teleportDistanceMeters">
    /// The absolute distance gate, in metres: the furthest the entity may move between
    /// samples regardless of elapsed time.
    /// </param>
    /// <param name="entityRadiusMeters">
    /// The group radius, in metres: how far the entity may lie from the centroid of the other
    /// valid entities before it is an outlier.
    /// </param>
    /// <param name="debugSubTitle">A one-line, self-describing summary of what was found.</param>
    /// <param name="maxSpeedMetersPerSecond">
    /// The rate gate, in metres per second, or <c>null</c> to leave the speed check
    /// unevaluated.
    /// <para>
    /// Supply it. It is optional only because the call site this facade preserves did not
    /// pass one, and it detects a different fault from
    /// <paramref name="teleportDistanceMeters"/>: an absolute distance gate flags a slow
    /// entity that jumps across a tick boundary and misses a fast entity drifting steadily,
    /// and a rate gate does exactly the inverse. Neither subsumes the other.
    /// </para>
    /// </param>
    /// <returns>The colour for the entity, chosen by severity precedence.</returns>
    /// <exception cref="EntityAccessorException">
    /// Position could not be resolved from <typeparamref name="TEntity"/>. The message lists
    /// the property names tried and the three ways to fix it.
    /// </exception>
    Color SetStatusColor<TId, TEntity>(
        TId entityId,
        double latitude,
        double longitude,
        double altitude,
        DateTime timestamp,
        IEnumerable<TEntity> allEntities,
        double teleportDistanceMeters,
        double entityRadiusMeters,
        out string debugSubTitle,
        double? maxSpeedMetersPerSecond = null);

    /// <summary>Returns the colour for a report the caller already has.</summary>
    /// <param name="report">The report.</param>
    /// <returns>The colour.</returns>
    Color ColorFor(EntityHealthReport report);

    /// <summary>Returns the debug subtitle for a report the caller already has.</summary>
    /// <param name="report">The report.</param>
    /// <returns>The subtitle.</returns>
    string SubtitleFor(EntityHealthReport report);
}
