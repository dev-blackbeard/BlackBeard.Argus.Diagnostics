using System;

namespace Argus.Controls;

/// <summary>
/// Uniquely identifies one row in an <see cref="EntityHealthCollection"/>: an entity id plus an
/// optional caller-supplied disambiguator.
/// </summary>
/// <remarks>
/// An entity id alone is not trusted to be unique. Two different producers, or two different
/// streams combined into one view, can reuse the same id — deliberately or by accident — and a
/// collection keyed on id alone would silently merge them into one row. <see cref="GroupTag"/> is
/// supplied entirely by whoever is feeding the collection; nothing here or elsewhere in Argus
/// parses it out of the stream, so it means whatever the caller decides it means (a stream name, a
/// source, a partition), and callers that don't need disambiguation can leave it <c>null</c>.
/// </remarks>
public readonly struct EntityKey : IEquatable<EntityKey>
{
    /// <summary>Creates a key.</summary>
    /// <param name="entityId">The entity's stable identity, as reported by the stream.</param>
    /// <param name="groupTag">A caller-supplied disambiguator, or <c>null</c> if none is needed.</param>
    /// <exception cref="ArgumentNullException"><paramref name="entityId"/> is <c>null</c>.</exception>
    public EntityKey(string entityId, string? groupTag)
    {
        if (entityId == null)
        {
            throw new ArgumentNullException(nameof(entityId));
        }

        EntityId = entityId;
        GroupTag = groupTag;
    }

    /// <summary>The entity's stable identity, as reported by the stream.</summary>
    public string EntityId { get; }

    /// <summary>The caller-supplied disambiguator, if any.</summary>
    public string? GroupTag { get; }

    /// <inheritdoc />
    public bool Equals(EntityKey other)
    {
        return string.Equals(EntityId, other.EntityId, StringComparison.Ordinal)
            && string.Equals(GroupTag, other.GroupTag, StringComparison.Ordinal);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return obj is EntityKey other && Equals(other);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 17;
            hash = (hash * 31) + StringComparer.Ordinal.GetHashCode(EntityId);
            hash = (hash * 31) + (GroupTag == null ? 0 : StringComparer.Ordinal.GetHashCode(GroupTag));
            return hash;
        }
    }

    /// <summary>Compares two keys for equality.</summary>
    public static bool operator ==(EntityKey left, EntityKey right)
    {
        return left.Equals(right);
    }

    /// <summary>Compares two keys for inequality.</summary>
    public static bool operator !=(EntityKey left, EntityKey right)
    {
        return !left.Equals(right);
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return GroupTag == null ? EntityId : GroupTag + "/" + EntityId;
    }
}
