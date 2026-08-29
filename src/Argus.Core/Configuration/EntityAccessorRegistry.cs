using System;
using System.Collections.Concurrent;
using Argus.Contracts;

namespace Argus.Configuration;

/// <summary>
/// Where an application registers how to read position out of its own entity types.
/// </summary>
/// <remarks>
/// <para>
/// This is the second of the three ways Argus resolves a position from an arbitrary
/// <c>TEntity</c>, and the one to reach for when the application's model types cannot take
/// a dependency on Argus (so <see cref="IArgusEntity"/> is out) and the property names do
/// not match any convention candidate, or the process runs somewhere without a JIT, where
/// the convention route's compiled expression trees do not work.
/// </para>
/// <para>
/// Registration is per entity type and idempotent; the last registration for a type wins.
/// </para>
/// </remarks>
public sealed class EntityAccessorRegistry
{
    private readonly ConcurrentDictionary<Type, Delegate> _accessors = new ConcurrentDictionary<Type, Delegate>();

    /// <summary>How many entity types have a registered accessor.</summary>
    public int Count
    {
        get { return _accessors.Count; }
    }

    /// <summary>Registers an accessor that yields a whole snapshot.</summary>
    /// <typeparam name="TEntity">The application's entity type.</typeparam>
    /// <param name="accessor">Reads identity and position out of an entity.</param>
    /// <returns>This registry, so registrations can be chained.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="accessor"/> is <c>null</c>.</exception>
    public EntityAccessorRegistry Register<TEntity>(Func<TEntity, EntitySnapshot> accessor)
    {
        if (accessor == null)
        {
            throw new ArgumentNullException(nameof(accessor));
        }

        _accessors[typeof(TEntity)] = accessor;
        return this;
    }

    /// <summary>Registers an accessor from four individual readers.</summary>
    /// <typeparam name="TEntity">The application's entity type.</typeparam>
    /// <param name="entityId">Reads the entity's identity.</param>
    /// <param name="latitude">Reads latitude in degrees.</param>
    /// <param name="longitude">Reads longitude in degrees.</param>
    /// <param name="altitude">Reads altitude in metres, or <c>null</c> if the type has none.</param>
    /// <returns>This registry, so registrations can be chained.</returns>
    /// <exception cref="ArgumentNullException">Any of <paramref name="entityId"/>, <paramref name="latitude"/> or <paramref name="longitude"/> is <c>null</c>.</exception>
    public EntityAccessorRegistry Register<TEntity>(
        Func<TEntity, string?> entityId,
        Func<TEntity, double?> latitude,
        Func<TEntity, double?> longitude,
        Func<TEntity, double?>? altitude = null)
    {
        if (entityId == null)
        {
            throw new ArgumentNullException(nameof(entityId));
        }

        if (latitude == null)
        {
            throw new ArgumentNullException(nameof(latitude));
        }

        if (longitude == null)
        {
            throw new ArgumentNullException(nameof(longitude));
        }

        return Register<TEntity>(entity => new EntitySnapshot(
            entityId(entity),
            latitude(entity),
            longitude(entity),
            altitude == null ? (double?)null : altitude(entity)));
    }

    /// <summary>Whether an accessor has been registered for a type.</summary>
    /// <typeparam name="TEntity">The application's entity type.</typeparam>
    /// <returns><c>true</c> if one is registered.</returns>
    public bool IsRegistered<TEntity>()
    {
        return _accessors.ContainsKey(typeof(TEntity));
    }

    /// <summary>Removes the accessor registered for a type.</summary>
    /// <typeparam name="TEntity">The application's entity type.</typeparam>
    /// <returns><c>true</c> if one was registered and has been removed.</returns>
    public bool Unregister<TEntity>()
    {
        Delegate? removed;
        return _accessors.TryRemove(typeof(TEntity), out removed);
    }

    internal bool TryGet<TEntity>(out Func<TEntity, EntitySnapshot>? accessor)
    {
        Delegate? registered;
        if (_accessors.TryGetValue(typeof(TEntity), out registered))
        {
            accessor = registered as Func<TEntity, EntitySnapshot>;
            return accessor != null;
        }

        accessor = null;
        return false;
    }
}
