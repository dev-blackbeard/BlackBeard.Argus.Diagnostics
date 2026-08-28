namespace Argus.Contracts;

/// <summary>
/// The family a <see cref="HealthFlags"/> value belongs to.
/// </summary>
/// <remarks>
/// The category drives severity precedence in presentation. Encoding and framing faults
/// rank highest because they are the ones that render as entirely plausible values: a
/// misaligned field produces a position a map will happily draw.
/// </remarks>
public enum HealthFlagCategory
{
    /// <summary>Not a categorised condition (<see cref="HealthFlags.None"/>).</summary>
    None = 0,

    /// <summary>Arrival ordering, cadence and timestamps.</summary>
    Temporal = 1,

    /// <summary>Wire representation: byte order, scale, units, offsets, sentinels, precision.</summary>
    Encoding = 2,

    /// <summary>Motion plausibility derived from consecutive positions.</summary>
    Kinematic = 3,

    /// <summary>Orientation ranges, wrapping and quaternion validity.</summary>
    Attitude = 4,

    /// <summary>Relationships between an entity and the other entities in its group.</summary>
    Group = 5,
}
